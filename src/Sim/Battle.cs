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
    internal double Hp(Func<int, double> hp) => Fleets.Sum(f => f.Squadrons.Sum(s => s.Ships * hp(s.Hull))) + StationHulls * hp(StationHull);
}

public record BattleResult(bool AWins, double LostA, double LostB, double KgLostA, double KgLostB, int Rounds, bool ARetreats, bool BRetreats);

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
        double hpA0 = a.Hp(HullHp), hpB0 = b.Hp(HullHp);
        double kgA0 = Kg(a), kgB0 = Kg(b), shipsA0 = Ships(a), shipsB0 = Ships(b);
        double da = HullDamage(a.Civ, b.Civ), db = HullDamage(b.Civ, a.Civ), mean = Math.Max((da + db) / 2, 1e-300);
        double relA = da / mean, relB = db / mean;
        double kill = B("roundKill"), cap = B("roundCap"), moraleLoss = B("moraleLossPerLossShare");
        int rounds = 0; bool aBreaks = false, bBreaks = false;
        for (int r = 0; r < (int)B("rounds"); r++)
        {
            double outA = Output(a, relA) * kill, outB = Output(b, relB) * kill;
            if (outA <= 0 && outB <= 0) break;
            rounds++;
            double shareB = Hit(b, outA, cap), shareA = Hit(a, outB, cap);
            Shake(a, shareA, moraleLoss); Shake(b, shareB, moraleLoss);
            aBreaks = Broken(a); bBreaks = Broken(b);
            if (aBreaks || bBreaks || a.Hp(HullHp) <= 0 || b.Hp(HullHp) <= 0) break;
        }
        double hpA = a.Hp(HullHp), hpB = b.Hp(HullHp);
        bool aWins = bBreaks && !aBreaks || hpB <= 0 && hpA > 0
                     || !(aBreaks && !bBreaks) && !(hpA <= 0 && hpB > 0) && (1 - hpA / Math.Max(hpA0, 1e-300)) < (1 - hpB / Math.Max(hpB0, 1e-300));
        double xp = B("xpPerBattle");
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
        if (home != null) foreach (var f in loser.Fleets) sim.Order(f, home.Star.Id, f.Mission == "blockade" ? "raid" : f.Mission);
        return new BattleResult(aWins, shipsA0 - Ships(a), shipsB0 - Ships(b), kgA0 - Kg(a), kgB0 - Kg(b), rounds, aBreaks, bBreaks);
    }

    static double Ships(BattleSide s) => s.Fleets.Sum(f => f.Ships);
    static double Kg(BattleSide s) => s.Fleets.Sum(f => f.Squadrons.Sum(q => q.Ships * Simulation.HullKg(q.Hull)));

    static double Output(BattleSide s, double rel) =>
        s.Fleets.Sum(f => f.Squadrons.Sum(q => q.Ships * HullHp(q.Hull) * (1 + q.Xp)) * rel * Trait(f, "dmg") * (f.Admiral?.Skill ?? 1) * f.Supply)
        + s.StationHulls * HullHp(s.StationHull) * rel;

    /// <summary>Spread incoming damage over a side by toughness; returns the share of its hit points lost.</summary>
    static double Hit(BattleSide s, double dmg, double cap)
    {
        double hp = s.Hp(HullHp);
        if (hp <= 0 || dmg <= 0) return 0;
        double lost = Math.Min(dmg, cap * hp), share = lost / hp;
        foreach (var f in s.Fleets)
        {
            double fs = Math.Min(1, share * Trait(f, "loss"));
            foreach (var q in f.Squadrons) q.Ships = Math.Max(0, q.Ships * (1 - fs));
        }
        s.StationHulls = Math.Max(0, s.StationHulls * (1 - share));
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
            double hp = f.Squadrons.Sum(q => q.Ships * HullHp(q.Hull));
            total += hp;
            if (f.Morale < (f.Admiral?.Trait == "Cautious" ? Trait(f, "break") : at)) broken += hp;
        }
        return total > 0 && broken > total / 2;
    }
}
