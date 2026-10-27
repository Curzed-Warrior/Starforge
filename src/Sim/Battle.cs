using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

/// <summary>One side of a battle: its civ, the fleets in the fight, and (for a defended system) its station, counted in hulls of one class.</summary>
public class BattleSide
{
    public Civ Civ;
    public List<Fleet> Fleets = new();
    public double StationHulls;
    public int StationHull = 2;
    /// <summary>Battle command: the side's squadrons as units (null in auto-resolve), and the enemy units this side's focus orders aim at.</summary>
    public List<CmdUnit> Units;
    internal readonly List<(CmdUnit T, double Phi)> Concentrate = new();
    internal double Hp(Func<int, double> hp) => (Units == null
        ? Fleets.Sum(f => f.Squadrons.Sum(s => s.Ships * hp(s.Hull)))
        : Fleets.Sum(f => Units.Sum(u => u.Fleet == f && !u.Gone ? u.Ships * hp(u.Hull) : 0))) + StationHulls * hp(StationHull);
}

public record BattleResult(bool AWins, double LostA, double LostB, double KgLostA, double KgLostB, int Rounds, bool ARetreats, bool BRetreats);

/// <summary>A battle as the screens remember it: where and when, which war and sides, who won, the hulls each lost and each side's main weapon family.</summary>
public record BattleRecord(int Star, double Year, int WarId, int CivA, int CivB, bool AWins, double LostA, double LostB, string FamA, string FamB);

public partial class Simulation
{
    /// <summary>Recent battles, newest last, at most interface.battleRecordsMax (transient: not saved, rebuilt as the sim runs).
    /// Inside Simulation this name hides the static class Battles: call that one as Starforge.Sim.Battles.</summary>
    public readonly List<BattleRecord> Battles = new();

    /// <summary>The battles fought at a star since a year, oldest first.</summary>
    public IEnumerable<BattleRecord> BattlesAt(int star, double since) => Battles.Where(b => b.Star == star && b.Year >= since && b.Year <= Year);

    void Record(BattleRecord r)
    {
        Battles.Add(r);
        int max = (int)WarData.D("interface", "battleRecordsMax");
        if (Battles.Count > max) Battles.RemoveRange(0, Battles.Count - max);
    }
}

/// <summary>
/// Battles by the numbers. Each hull's punch is its best weapon family plus a share of its second, each blunted by the
/// defence that counters it (armour stops kinetics, shields drink beams, point defence swats missiles and strike craft;
/// exotic weapons pass half of everything). Big hulls hit harder and last longer. Crews learn, admirals colour the
/// fight, and a side whose morale breaks pulls out.
/// </summary>
public static class Battles
{
    static double B(string key) => WarData.D("battle", key);
    static readonly string[] Excluded = { "ground", "doom" };

    // war.json is read once per process (WarData keeps the parsed document too), so this never goes stale
    static string[] _families;

    /// <summary>The weapon family a civ's fleets mostly fight with: its highest-tier space weapon (ties in war.json's
    /// family order: the countered families, then exotic), kinetics when it has none.</summary>
    public static string BestFamily(Civ c)
    {
        _families ??= WarData.P("battle", "counters").EnumerateObject().Select(p => p.Name).Append("exotic").ToArray();
        string best = "kinetic"; int bt = int.MinValue, bi = int.MaxValue;
        foreach (var (fam, t) in c.M.Arms)
        {
            if (Array.IndexOf(Excluded, fam) >= 0) continue;
            int i = Array.IndexOf(_families, fam); if (i < 0) i = _families.Length;
            if (t > bt || t == bt && i < bi) { best = fam; bt = t; bi = i; }
        }
        return best;
    }

    static double Block(int t, int td) { double p = Simulation.TierPow(t), q = Simulation.TierPow(td); return 2 * p / (p + q); }

    /// <summary>What one frigate-sized hull of att does to def per unit of time, before size, experience and command.</summary>
    public static double HullDamage(Civ att, Civ def)
    {
        var counters = WarData.P("battle", "counters");
        var g = def.M.Guard;
        var vals = new List<double>();
        foreach (var (fam, t) in att.M.Arms)
        {
            if (Array.IndexOf(Excluded, fam) >= 0) continue;
            double p = Simulation.TierPow(t);
            if (fam == "exotic")
            {
                int avg = (int)Math.Round((g.GetValueOrDefault("armor") + g.GetValueOrDefault("shield") + g.GetValueOrDefault("pd")) / 3.0);
                double pass = B("exoticPass");
                vals.Add(p * (pass + (1 - pass) * Block(t, avg)));
            }
            else if (counters.TryGetProperty(fam, out var c)) vals.Add(p * Block(t, g.GetValueOrDefault(c.GetString())));
            else vals.Add(p);
        }
        vals.Sort((x, y) => y.CompareTo(x));
        double d = vals.Count == 0 ? B("unarmed") : vals[0] + (vals.Count > 1 ? B("secondWeapon") * vals[1] : 0);
        return d * att.M.Fleet;
    }

    /// <summary>A hull's toughness and weight of fire, against a frigate's: the square of its length.</summary>
    public static double HullHp(int cls) { double r = Simulation.LengthKm(cls) / WarData.D("economy", "frigateLengthKm"); return r * r; }

    static double Trait(Fleet f, string key) =>
        f.Admiral?.Trait is string t && WarData.P("battle", "traits", t).TryGetProperty(key, out var v) ? v.GetDouble() : 1;

    /// <summary>Fight it out: up to `rounds` exchanges, stopping when a side breaks or is gone. Survivors learn; the loser's fleets head home.</summary>
    public static BattleResult Resolve(Simulation sim, BattleSide a, BattleSide b)
    {
        var st = new BattleState(a, b);
        while (st.Step()) { }
        return st.Finish(sim);
    }

    /// <summary>
    /// A battle in progress, one exchange at a time. Resolve runs it to the end; battle command (BattleCommand.cs) steps
    /// the same object round by round, so a commanded battle is worked out by exactly these numbers.
    /// </summary>
    public sealed class BattleState
    {
        public readonly BattleSide A, B;
        public int Rounds { get; private set; }
        public bool ABreaks { get; private set; }
        public bool BBreaks { get; private set; }
        public bool Over { get; private set; }
        readonly double _hpA0, _hpB0, _kgA0, _kgB0, _shipsA0, _shipsB0, _relA, _relB, _kill, _cap, _moraleLoss;
        readonly int _max;

        public BattleState(BattleSide a, BattleSide b)
        {
            A = a; B = b;
            _hpA0 = a.Hp(HullHp); _hpB0 = b.Hp(HullHp);
            _kgA0 = Kg(a); _kgB0 = Kg(b); _shipsA0 = Ships(a); _shipsB0 = Ships(b);
            double da = HullDamage(a.Civ, b.Civ), db = HullDamage(b.Civ, a.Civ), mean = Math.Max((da + db) / 2, 1e-300);
            _relA = da / mean; _relB = db / mean;
            _kill = Battles.B("roundKill"); _cap = Battles.B("roundCap"); _moraleLoss = Battles.B("moraleLossPerLossShare");
            _max = (int)Battles.B("rounds");
        }

        /// <summary>One exchange. Returns true while the battle goes on.</summary>
        public bool Step()
        {
            if (Over) return false;
            if (Rounds >= _max) { Over = true; return false; }
            Tactics.Prepare(A, B); Tactics.Prepare(B, A);
            double outA = Output(A, _relA) * _kill, outB = Output(B, _relB) * _kill;
            if (outA <= 0 && outB <= 0) { Over = true; return false; }
            Rounds++;
            double shareB = Hit(B, A, outA, _cap), shareA = Hit(A, B, outB, _cap);
            Shake(A, shareA, _moraleLoss); Shake(B, shareB, _moraleLoss);
            Tactics.After(A); Tactics.After(B);
            ABreaks = Broken(A); BBreaks = Broken(B);
            if (ABreaks || BBreaks || A.Hp(HullHp) <= 0 || B.Hp(HullHp) <= 0 || Rounds >= _max) Over = true;
            return !Over;
        }

        /// <summary>Settle it: who won, experience, admirals' records, and the loser's fleets going home.</summary>
        public BattleResult Finish(Simulation sim)
        {
            var a = A; var b = B;
            if (a.Units != null) Units.WriteBack(a);
            if (b.Units != null) Units.WriteBack(b);
            double hpA = a.Hp(HullHp), hpB = b.Hp(HullHp);
            bool aBreaks = ABreaks, bBreaks = BBreaks;
            bool aWins = bBreaks && !aBreaks || hpB <= 0 && hpA > 0
                         || !(aBreaks && !bBreaks) && !(hpA <= 0 && hpB > 0) && (1 - hpA / Math.Max(_hpA0, 1e-300)) < (1 - hpB / Math.Max(_hpB0, 1e-300));
            double xp = Battles.B("xpPerBattle");
            foreach (var side in new[] { a, b })
                foreach (var f in side.Fleets)
                {
                    f.Squadrons.RemoveAll(s => s.Ships <= 1e-9);
                    foreach (var s in f.Squadrons) s.Xp = Math.Min(1f, s.Xp + (float)xp);
                    if (f.Admiral != null) { if (side == a == aWins) f.Admiral.Wins++; else f.Admiral.Losses++; }
                }
            // the loser's fleets go home to refit
            var loser = aWins ? b : a;
            var home = loser.Civ.Colonies.FirstOrDefault(c => c.Home) ?? loser.Civ.Colonies.FirstOrDefault();
            // (when Fleets is in the player's hands, their fleets go home only if broken)
            bool manual = loser.Civ.Policy.IsManual(Area.Fleets);
            if (home != null)
                foreach (var f in loser.Fleets)
                    if (!manual || f.Morale < Battles.B("moraleBreak")) sim.Order(f, home.Star.Id, f.Mission == "blockade" ? "raid" : f.Mission);
            return new BattleResult(aWins, _shipsA0 - Ships(a), _shipsB0 - Ships(b), _kgA0 - Kg(a), _kgB0 - Kg(b), Rounds, aBreaks, bBreaks);
        }
    }

    static double Ships(BattleSide s) => s.Fleets.Sum(f => f.Ships);
    static double Kg(BattleSide s) => s.Fleets.Sum(f => f.Squadrons.Sum(q => q.Ships * Simulation.HullKg(q.Hull)));

    static double Output(BattleSide s, double rel) =>
        s.Fleets.Sum(f => (s.Units == null
            ? f.Squadrons.Sum(q => q.Ships * HullHp(q.Hull) * (1 + q.Xp))
            : s.Units.Sum(u => u.Fleet == f && u.Live ? u.Ships * HullHp(u.Hull) * (1 + u.Xp) * u.Mult : 0)) * rel * Trait(f, "dmg") * (f.Admiral?.Skill ?? 1) * f.Supply)
        + s.StationHulls * HullHp(s.StationHull) * rel;

    /// <summary>Spread incoming damage over a side by toughness; returns the share of its hit points lost. With orders in play
    /// (focus, screens, formations, units out of the line) the same hit points are shared out by exposure instead.</summary>
    static double Hit(BattleSide s, BattleSide att, double dmg, double cap)
    {
        double hp = s.Hp(HullHp);
        if (hp <= 0 || dmg <= 0) return 0;
        double lost = Math.Min(dmg, cap * hp), share = lost / hp;
        if (Tactics.Neutral(s, att))
        {
            foreach (var f in s.Fleets)
            {
                double fs = Math.Min(1, share * Trait(f, "loss"));
                if (s.Units == null) foreach (var q in f.Squadrons) q.Ships = Math.Max(0, q.Ships * (1 - fs));
                else foreach (var u in s.Units) if (u.Fleet == f) u.Ships = Math.Max(0, u.Ships * (1 - fs));
            }
            s.StationHulls = Math.Max(0, s.StationHulls * (1 - share));
            return share;
        }
        // exposure-weighted: the hit-point-weighted mean exposure is 1 after normalising, so the spread removes `lost` in all
        double w = s.StationHulls * HullHp(s.StationHull);
        foreach (var u in s.Units) w += u.Gone ? 0 : u.Ships * HullHp(u.Hull) * u.Exp;
        double mean = w / hp, phiSum = 0;
        foreach (var (_, phi) in att.Concentrate) phiSum += phi;
        var focus = new Dictionary<CmdUnit, double>();
        foreach (var (t, phi) in att.Concentrate) focus[t] = focus.GetValueOrDefault(t) + phi;
        foreach (var u in s.Units)
        {
            if (u.Gone || u.Ships <= 0) continue;
            double uhp = u.Ships * HullHp(u.Hull);
            double frac = mean > 0 ? (1 - phiSum) * share * u.Exp / mean : 0;
            if (focus.TryGetValue(u, out var ph)) frac += ph * lost / uhp;
            u.Ships = Math.Max(0, u.Ships * (1 - Math.Min(1, frac * Trait(u.Fleet, "loss"))));
        }
        s.StationHulls = Math.Max(0, s.StationHulls * (1 - (mean > 0 ? (1 - phiSum) * share / mean : 0)));
        return share;
    }

    static void Shake(BattleSide s, double share, double k)
    {
        foreach (var f in s.Fleets) f.Morale = (float)Math.Max(0, f.Morale - k * share / Trait(f, "morale"));
    }

    /// <summary>A side breaks when most of its fleets (by toughness) have; a station alone never runs.</summary>
    static bool Broken(BattleSide s)
    {
        double total = 0, broken = 0, at = B("moraleBreak");
        foreach (var f in s.Fleets)
        {
            double hp = s.Units == null ? f.Squadrons.Sum(q => q.Ships * HullHp(q.Hull)) : s.Units.Sum(u => u.Fleet == f && !u.Gone ? u.Ships * HullHp(u.Hull) : 0);
            total += hp;
            if (f.Morale < (f.Admiral?.Trait == "Cautious" ? Trait(f, "break") : at)) broken += hp;
        }
        if (s.Units != null && s.Units.Count > 0 && s.Units.All(u => u.Gone || u.Ships <= 0) && s.Units.Any(u => u.Gone && u.Ships > 0)) return true;     // the whole line has left the field
        return total > 0 && broken > total / 2;
    }
}
