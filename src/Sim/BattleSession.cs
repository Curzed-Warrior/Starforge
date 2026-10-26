using System;
using System.Collections.Generic;
using System.Linq;
using static Starforge.Sim.Battles;

namespace Starforge.Sim;

/// <summary>How the player takes part in the battles they fight (spec 4.4, 4.8).</summary>
public enum BattleMode { Auto, Ask, Command }

/// <summary>
/// A battle the player is commanding. The sim year is held while it exists. It runs on its own clock: a round every
/// roundSeconds at the chosen speed, paused until the player says go, with the same BattleState as auto-resolve doing
/// the sums. Orders go through the methods here; only the player's own units can be ordered.
/// </summary>
public class CommandSession
{
    public readonly Simulation Sim;
    public readonly War War;
    public readonly int Star;
    public readonly BattleSide A, B;
    public readonly BattleState State;
    public readonly bool PlayerIsA;
    public readonly double NavyA, NavyB;
    /// <summary>Seconds into the current round.</summary>
    public double Clock;
    public bool Paused = true;
    public double Speed = 1;
    public bool Over { get; private set; }
    public BattleResult Result { get; private set; }
    int _nextId = 1;

    public BattleSide Mine => PlayerIsA ? A : B;
    public BattleSide Theirs => PlayerIsA ? B : A;
    public static double RoundSeconds => WarData.D("battle", "command", "roundSeconds");
    /// <summary>How far through the current round, 0..1.</summary>
    public double Progress => Over ? 1 : Math.Clamp(Clock / RoundSeconds, 0, 1);
    public int Round => State.Rounds;

    internal CommandSession(Simulation sim, War w, int star, BattleSide a, BattleSide b, bool playerIsA, double navyA, double navyB)
    {
        Sim = sim; War = w; Star = star; A = a; B = b; PlayerIsA = playerIsA; NavyA = navyA; NavyB = navyB;
        Units.Build(A, ref _nextId); Units.Build(B, ref _nextId);
        State = new BattleState(A, B);
    }

    public CmdUnit Find(int id) => A.Units.Concat(B.Units).FirstOrDefault(u => u.Id == id);
    bool Own(CmdUnit u) => u != null && Mine.Units.Contains(u) && !u.Gone;

    bool _stepped;

    /// <summary>
    /// Run the battle clock. A round's exchange is worked out when it begins and its losses play out over the round (each
    /// unit's Prev is its ships before, Ships after); orders given now take effect from the next round. Returns true when
    /// the battle has ended.
    /// </summary>
    public bool Tick(double seconds)
    {
        if (Over) return true;
        if (Paused) return false;
        Clock += seconds * Speed;
        while (!Over)
        {
            if (!_stepped)
            {
                foreach (var u in A.Units.Concat(B.Units)) u.Prev = u.Ships;
                int before = State.Rounds;
                State.Step();
                if (State.Rounds == before) { Conclude(); break; }
                _stepped = true;
            }
            if (Clock < RoundSeconds) break;
            Clock -= RoundSeconds; _stepped = false;
            if (State.Over) Conclude();
        }
        return Over;
    }

    /// <summary>Leave it to the admirals: the rest of the battle runs at once, with orders as they stand.</summary>
    public BattleResult AutoFinish()
    {
        while (!Over && State.Step()) { }
        if (!Over) Conclude();
        return Result;
    }

    void Conclude() { Result = State.Finish(Sim); Over = true; Clock = 0; }

    // ---------- orders ----------
    public bool Move(IEnumerable<CmdUnit> us, double depth, double lateral)
    {
        bool any = false;
        foreach (var u in us.Where(Own)) { u.HasMove = true; u.MoveDepth = depth; u.MoveLateral = lateral; u.Retreat = false; u.LeaveLeft = 0; any = true; }
        return any;
    }

    public bool Focus(IEnumerable<CmdUnit> us, CmdUnit target)
    {
        if (target == null || !Theirs.Units.Contains(target) || target.Gone) return false;
        bool any = false;
        foreach (var u in us.Where(Own)) { u.FocusId = target.Id; any = true; }
        return any;
    }

    public bool SetFormation(IEnumerable<CmdUnit> us, string formation)
    {
        if (!WarData.P("battle", "command", "formations").TryGetProperty(formation, out _)) return false;
        bool any = false;
        foreach (var u in us.Where(Own)) { u.Formation = formation; any = true; }
        return any;
    }

    /// <summary>The units cover `protectedUnit` (one of the player's own, not among them).</summary>
    public bool Screen(IEnumerable<CmdUnit> us, CmdUnit protectedUnit)
    {
        if (!Own(protectedUnit)) return false;
        bool any = false;
        foreach (var u in us.Where(Own)) if (u != protectedUnit) { u.ScreenId = protectedUnit.Id; any = true; }
        return any;
    }

    public bool Retreat(IEnumerable<CmdUnit> us)
    {
        bool any = false;
        foreach (var u in us.Where(Own)) { u.Retreat = true; u.HasMove = false; u.FocusId = u.ScreenId = -1; any = true; }
        return any;
    }

    /// <summary>Back to the admiral's doctrine.</summary>
    public void Clear(IEnumerable<CmdUnit> us) { foreach (var u in us.Where(Own)) { u.ClearOrders(); } }

    public CmdUnit Split(CmdUnit u, double ships)
    {
        double all = u.Ships; double prev = u.Prev;
        var n = Own(u) ? Units.Split(Mine, u, ships, ref _nextId) : null;
        if (n != null) { n.Prev = prev * ships / all; u.Prev = prev - n.Prev; }
        return n;
    }
    public bool Merge(CmdUnit a, CmdUnit b) { double p = a.Prev + b.Prev; if (!(Own(a) && Own(b) && Units.Merge(Mine, a, b))) return false; a.Prev = p; return true; }
}

public partial class Simulation
{
    /// <summary>Set by the game once it is running for a player: until then (tests, warps, headless runs) every battle is auto-resolved.</summary>
    public bool CommandEnabled;
    /// <summary>The battle the player is commanding, or null.</summary>
    public CommandSession Command { get; private set; }
    public event Action<CommandSession> CommandOpened, CommandClosed;

    record Pending(War W, int Star, BattleSide A, BattleSide B, double NavyA, double NavyB, bool Asked);
    readonly Queue<Pending> _pending = new();
    Pending _asking;

    /// <summary>The game year is held while the player commands a battle, is deciding whether to, or has one waiting.</summary>
    public bool Holding => Command != null || _asking != null || _pending.Count > 0;

    /// <summary>A battle starts: auto-resolve it, or (the player's, in Ask or Command mode) hold it for the player.</summary>
    BattleResult Dispatch(War w, int star, BattleSide sa, BattleSide sb)
    {
        double navyA = NavyStrength(sa.Civ), navyB = NavyStrength(sb.Civ);
        bool mine = sa.Civ == Humanity || sb.Civ == Humanity;
        var mode = mine && CommandEnabled ? Humanity.Policy.Battle : BattleMode.Auto;
        if (mode == BattleMode.Auto) return Book(w, star, sa, sb, Starforge.Sim.Battles.Resolve(this, sa, sb), navyA, navyB);
        _pending.Enqueue(new Pending(w, star, sa, sb, navyA, navyB, mode == BattleMode.Ask));
        PumpBattles();
        return null;
    }

    /// <summary>Open the next waiting battle for the player, or ask about it.</summary>
    void PumpBattles()
    {
        while (Command == null && _asking == null && _pending.Count > 0)
        {
            var p = _pending.Dequeue();
            if (!StillOn(p)) continue;
            if (!p.Asked) { Open(p); continue; }
            _asking = p;
            var foe = p.A.Civ == Humanity ? p.B.Civ : p.A.Civ;
            var at = ColonyAt(p.Star) ?? HomeColony;
            Raise("Battle", $"Battle at {Gal.Stars[p.Star].Name}", $"Our fleets meet {Narrative2.Called(foe)}'s. Take command, or leave it to the admirals?", at,
                key: "battle:" + p.Star, choices: new()
                {
                    ("Take command", () => { var q = _asking; _asking = null; if (q != null) Open(q); }),
                    ("Leave it to the admirals", () => { var q = _asking; _asking = null; if (q != null) Book(q.W, q.Star, q.A, q.B, Starforge.Sim.Battles.Resolve(this, q.A, q.B), q.NavyA, q.NavyB); PumpBattles(); }),
                });
        }
    }

    /// <summary>The battle is still there to fight: the fleets are at the star, alive, and the war still stands.</summary>
    bool StillOn(Pending p) => p.W.Open && p.A.Civ.Colonies.Count > 0 && p.B.Civ.Colonies.Count > 0
        && p.A.Fleets.Concat(p.B.Fleets).All(f => Fleets.Contains(f) && f.At == p.Star && !f.Moving);

    void Open(Pending p)
    {
        Command = new CommandSession(this, p.W, p.Star, p.A, p.B, p.A.Civ == Humanity, p.NavyA, p.NavyB);
        CommandOpened?.Invoke(Command);
    }

    /// <summary>The commanded battle is over (or given up): book it like any other and carry on with the next.</summary>
    public void CloseCommand()
    {
        var s = Command; if (s == null) return;
        var r = s.Over ? s.Result : s.AutoFinish();
        Command = null;
        Book(s.War, s.Star, s.A, s.B, r, s.NavyA, s.NavyB);
        CommandClosed?.Invoke(s);
        PumpBattles();
    }

    internal void TestAnswer(int choice) { var a = Alerts.LastOrDefault(x => x.Key != null && x.Key.StartsWith("battle:") && !x.Done); if (a != null) Decide(a, choice); }
}
