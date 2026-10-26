using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

/// <summary>
/// A slice of one squadron in a commanded battle (down to a single ship), with the orders it is under. Depth is its
/// distance behind the line in gaps (0 in the line, 1 out of range), Lateral its offset across it (+-1 on a flank).
/// With no orders every field stays at its default and the unit fights exactly as the squadron did in auto-resolve.
/// </summary>
public class CmdUnit
{
    public int Id;
    public Fleet Fleet;
    public Squadron Sq;
    public int Hull;
    public double Ships;
    /// <summary>Ships before the round now playing out (the view draws Prev falling to Ships over the round).</summary>
    public double Prev;
    public float Xp;
    public double Depth, Lateral;
    /// <summary>Orders: where to go (HasMove), the enemy unit to focus (-1 none), the own unit to screen (-1 none), the formation, and retreat.</summary>
    public bool HasMove;
    public double MoveDepth, MoveLateral;
    public int FocusId = -1, ScreenId = -1;
    public string Formation = "line";
    public bool Retreat;
    /// <summary>Rounds left before a retreating unit is clear of the field.</summary>
    public int LeaveLeft;
    /// <summary>Clear of the field: its ships are safe and it takes no further part.</summary>
    public bool Gone;
    // worked out each round (Tactics.Prepare)
    public bool Live;
    public double Mult = 1, Exp = 1;

    public bool Commanded => HasMove || FocusId >= 0 || ScreenId >= 0 || Formation != "line" || Retreat;
    public void ClearOrders() { HasMove = false; FocusId = ScreenId = -1; Formation = "line"; Retreat = false; LeaveLeft = 0; }
}

/// <summary>Setting up units from a side's squadrons, and splitting and merging them.</summary>
public static class Units
{
    static double C(string key) => WarData.D("battle", "command", key);
    public static int MaxPerSide => (int)C("maxUnitsPerSide");

    /// <summary>One unit per squadron, in fleet then squadron order (what auto-resolve sums over).</summary>
    public static void Build(BattleSide side, ref int nextId)
    {
        side.Units = new List<CmdUnit>();
        foreach (var f in side.Fleets)
            foreach (var q in f.Squadrons)
                side.Units.Add(new CmdUnit { Id = nextId++, Fleet = f, Sq = q, Hull = q.Hull, Ships = q.Ships, Prev = q.Ships, Xp = q.Xp });
    }

    /// <summary>Split `ships` off a unit into a new one right after it, carrying its orders. Null if it cannot (too few ships, the cap, or the unit is gone).</summary>
    public static CmdUnit Split(BattleSide side, CmdUnit u, double ships, ref int nextId)
    {
        if (u.Gone || ships <= 0 || ships >= u.Ships || side.Units.Count >= MaxPerSide) return null;
        var n = new CmdUnit
        {
            Id = nextId++, Fleet = u.Fleet, Sq = u.Sq, Hull = u.Hull, Ships = ships, Xp = u.Xp, Depth = u.Depth, Lateral = u.Lateral,
            HasMove = u.HasMove, MoveDepth = u.MoveDepth, MoveLateral = u.MoveLateral, FocusId = u.FocusId, ScreenId = u.ScreenId,
            Formation = u.Formation, Retreat = u.Retreat, LeaveLeft = u.LeaveLeft,
        };
        u.Ships -= ships;
        side.Units.Insert(side.Units.IndexOf(u) + 1, n);
        return n;
    }

    /// <summary>Fold b into a (same squadron, neither gone). Orders aimed at b now aim at a.</summary>
    public static bool Merge(BattleSide side, CmdUnit a, CmdUnit b)
    {
        if (a == b || a.Sq != b.Sq || a.Gone || b.Gone) return false;
        a.Ships += b.Ships;
        side.Units.Remove(b);
        foreach (var u in side.Units) { if (u.ScreenId == b.Id) u.ScreenId = a.Id; }
        return true;
    }

    /// <summary>Write the units' ships back to their squadrons (what the rest of the game sees).</summary>
    public static void WriteBack(BattleSide side)
    {
        foreach (var f in side.Fleets)
            foreach (var q in f.Squadrons)
            {
                double n = 0; bool any = false;
                foreach (var u in side.Units) if (u.Sq == q) { n += u.Ships; any = true; }
                if (any) q.Ships = n;
            }
    }
}

/// <summary>
/// What orders do to the numbers. Each round, before the exchange, every unit gets an output multiplier and an exposure
/// (its weight when damage is spread). Defaults are exactly 1, so a battle with no orders is the auto-resolve battle.
/// Allocation orders (screen, focus, formation exposure) move the hit points a round removes between units; they never
/// create or remove any. Bonuses are capped by data.
/// </summary>
public static class Tactics
{
    static double C(string key) => WarData.D("battle", "command", key);

    public static double HullPower(CmdUnit u) => u.Ships * Battles.HullHp(u.Hull);

    internal static void Prepare(BattleSide mine, BattleSide theirs)
    {
        mine.Concentrate.Clear();
        if (mine.Units == null) return;
        double band = C("rangeBand"), flankAt = C("flankAt");
        foreach (var u in mine.Units)
        {
            u.Mult = 1; u.Exp = 1;
            if (u.Gone) { u.Live = false; u.Mult = 0; u.Exp = 0; continue; }
            u.Live = Math.Abs(u.Depth) <= band;
            if (!u.Live) { u.Mult = 0; u.Exp = 0; continue; }
            if (u.Formation != "line")
            {
                var f = WarData.P("battle", "command", "formations", u.Formation);
                u.Mult *= f.GetProperty("dmg").GetDouble(); u.Exp *= f.GetProperty("exp").GetDouble();
            }
            if (Math.Abs(u.Lateral) >= flankAt) { u.Mult *= 1 + C("flankBonus"); u.Exp *= 1 + C("flankExposure"); }
            if (u.Retreat) { if (u.LeaveLeft <= 0) u.LeaveLeft = (int)C("retreatRounds"); u.Mult = 0; }
        }
        // screens: a screener takes a share of what would fall on the unit it covers
        double k = C("screenK"), cmax = C("screenCoverMax"), pen = C("screenPenalty");
        foreach (var s in mine.Units)
        {
            if (!s.Live || s.ScreenId < 0 || s.Retreat) continue;
            var p = mine.Units.FirstOrDefault(x => x.Id == s.ScreenId);
            if (p == null || p == s || !p.Live) continue;
            double hs = HullPower(s), hp = HullPower(p);
            if (hs <= 0 || hp <= 0) continue;
            double c = Math.Min(cmax, k * hs / hp);
            double moved = c * hp * p.Exp;
            p.Exp *= 1 - c; s.Exp += moved / hs; s.Mult *= 1 - pen;
        }
        // focus: part of this side's fire is aimed at one enemy unit
        if (theirs.Units == null) return;
        double total = 0;
        foreach (var u in mine.Units) if (u.Live) total += HullPower(u) * (1 + u.Xp) * u.Mult;
        if (total <= 0) return;
        double share = C("focusShare"), sum = 0;
        var list = new List<(CmdUnit T, double Phi)>();
        foreach (var u in mine.Units)
        {
            if (!u.Live || u.FocusId < 0 || u.Retreat) continue;
            var t = theirs.Units.FirstOrDefault(x => x.Id == u.FocusId);
            if (t == null || t.Gone || t.Ships <= 0) continue;
            double phi = share * HullPower(u) * (1 + u.Xp) * u.Mult / total;
            list.Add((t, phi)); sum += phi;
        }
        double scale = sum > C("focusMax") ? C("focusMax") / sum : 1;
        foreach (var (t, phi) in list) mine.Concentrate.Add((t, phi * scale));
    }

    static double Step(double from, double to, double speed) => Math.Abs(to - from) <= speed ? to : from + Math.Sign(to - from) * speed;

    /// <summary>After the exchange: retreating units that have had their round are clear.</summary>
    internal static void After(BattleSide s)
    {
        if (s.Units == null) return;
        double speed = C("moveSpeed");
        foreach (var u in s.Units)
        {
            if (u.Retreat && !u.Gone && u.Live && --u.LeaveLeft <= 0) u.Gone = true;
            // units move after the exchange: pulling out of range does not dodge the round it is ordered in
            if (u.HasMove && !u.Gone) { u.Depth = Step(u.Depth, u.MoveDepth, speed); u.Lateral = Step(u.Lateral, u.MoveLateral, speed); }
        }
    }

    /// <summary>True when no order bends this side's numbers, so the exchange can use auto-resolve's own arithmetic.</summary>
    internal static bool Neutral(BattleSide s, BattleSide att)
    {
        if (s.Units == null) return true;
        if (att.Concentrate.Count > 0) return false;
        foreach (var u in s.Units) if (u.Gone || !u.Live || u.Exp != 1) return false;
        return true;
    }
}
