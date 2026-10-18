using System;
using System.Linq;
using System.Text.Json;

namespace Starforge.Sim;

/// <summary>
/// The war economy: how much of a civ's industry is turned to war (mobilisation), how long the factories take to
/// retool, how well the new lines run, and what it costs the people (strain). Same rules for every civ.
/// </summary>
public partial class Simulation
{
    /// <summary>A mobilisation level from war.json (0 peace, 1 partial, 2 war economy, 3 total war).</summary>
    public static JsonElement Level(int i) => WarData.P("mobilisation", i.ToString());
    static double LevelD(int i, string key) => Level(i).GetProperty(key).GetDouble();

    /// <summary>The highest level a people bears without complaint: warrior peoples total war, pacifists partial.</summary>
    static int Comfort(Civ c) => c.Culture.Martial > 0.3f ? 3 : c.Culture.Martial > -0.3f ? 2 : 1;

    /// <summary>What the council would choose: peace unless a neighbour turns hostile, a war economy only against a real navy.</summary>
    public int CouncilMobilisation(Civ c)
    {
        int lvl;
        var foes = c.AtWar.Select(id => Civs[id]).Where(o => !o.Dead).ToList();
        if (foes.Count > 0)
        {
            double ours = NavyStrength(c);
            lvl = foes.Any(o => NavyStrength(o) >= 0.6 * ours) ? 2 : 1;
            if (c.Emergency) lvl = 3;                         // stage 2 adds: a home or capital-adjacent system occupied
        }
        else lvl = c.Relations.Any(r => r.Value < -40 && r.Key < Civs.Count && !Civs[r.Key].Dead) ? 1 : 0;
        return Math.Min(lvl, Comfort(c) + 1);
    }

    /// <summary>Yearly: move the military share towards the level in force, retool, and work out the strain.</summary>
    void MobiliseStep(Civ c, double dy)
    {
        bool manual = c.Policy.IsManual(Area.Production);
        int lvl = Math.Clamp(manual ? c.Policy.Mobilisation : CouncilMobilisation(c), 0, 3);
        if (lvl != c.Mobilisation) { c.Mobilisation = lvl; c.MilFrom = c.MilShare; c.MilSince = Year; }

        double target = LevelD(lvl, "military");
        double f = Math.Clamp((Year - c.MilSince) / WarData.D("economy", "retoolYears"), 0, 1);
        double old = c.MilShare, now = c.MilFrom + (target - c.MilFrom) * f;
        if (now > old + 1e-12) c.Efficiency = (c.Efficiency * old + WarData.D("economy", "efficiencyStart") * (now - old)) / now;
        else c.Efficiency = Math.Min(1, c.Efficiency + WarData.D("economy", "efficiencyPerYear") * dy);
        c.MilShare = now;

        if (!manual)
        {
            if (lvl >= 2 && c.Culture.Duty > 0) c.MandatoryService = true;
            else if (lvl == 0 && c.Culture.Duty < 0.5f) c.MandatoryService = false;
        }
        c.WarStrain = LevelD(lvl, "strain") * (1 - 0.5 * c.Culture.Martial)
                    + (c.MandatoryService && c.Culture.Duty < 0 ? WarData.D("economy", "mandatoryStrain") : 0)
                    + Math.Max(0, lvl - Comfort(c));
    }

    /// <summary>A level's key, interpolated by where the military share actually stands, softened by a martial culture.</summary>
    double WarFactor(Civ c, string key)
    {
        int n = WarData.P("mobilisation").GetArrayLength();
        double x = LevelD(n - 1, key);
        for (int i = 0; i < n - 1; i++)
        {
            double a = LevelD(i, "military"), b = LevelD(i + 1, "military");
            if (c.MilShare <= b) { x = LevelD(i, key) + (LevelD(i + 1, key) - LevelD(i, key)) * Math.Clamp((c.MilShare - a) / (b - a), 0, 1); break; }
        }
        return 1 - (1 - x) * (1 - 0.5 * c.Culture.Martial);
    }
    public double WarGrowth(Civ c) => WarFactor(c, "growth");
    public double WarResearch(Civ c) => WarFactor(c, "research");

    // ---------- Materiel and shipyards ----------
    static int _battleship = -2;
    static int Battleship => _battleship != -2 ? _battleship : _battleship = Array.IndexOf(Defs.Hulls, "Battleship");

    /// <summary>Building slips: one at home, more per finished yard, and big yards once the civ builds capital ships.</summary>
    public int Slips(Civ c)
    {
        int n = 0;
        bool capital = Battleship >= 0 && c.Hull >= Battleship;
        foreach (var col in c.Colonies)
        {
            if (col.Home) n += (int)WarData.D("economy", "slipsHome");
            if (col.Done("yard")) n += (int)WarData.D("economy", "slipsPerYard") + (capital ? (int)WarData.D("economy", "slipsMegaYard") : 0);
        }
        return n;
    }

    /// <summary>
    /// Yearly: war industry makes materiel; the navy's upkeep is paid from it (or ships are laid up); the slips build
    /// hulls from it, one per slip at a time, and each finished hull joins the fleet that most needs ships.
    /// </summary>
    void YardStep(Civ c, double dy)
    {
        // stage 2: skip colonies under occupation
        c.Materiel += c.Colonies.Sum(col => col.IndustryKg) * c.MilShare * c.Efficiency * dy;

        var fleets = FleetsOf(c).ToList();
        double upk = WarData.D("economy", "upkeepPerYear") * dy;
        double due = fleets.Sum(f => f.Squadrons.Sum(s => upk * HullKg(s.Hull) * s.Ships));
        if (due <= c.Materiel) c.Materiel -= due;
        else
        {
            double deficit = due - c.Materiel; c.Materiel = 0;
            // mothball from the heaviest squadrons until the bill is covered
            foreach (var (f, s) in fleets.SelectMany(f => f.Squadrons.Select(s => (f, s))).OrderByDescending(p => p.s.Ships * HullKg(p.s.Hull)).ToList())
            {
                if (deficit <= 0) break;
                double kg = HullKg(s.Hull), laid = Math.Min(s.Ships, Math.Ceiling(deficit / kg));
                s.Ships -= laid; deficit -= laid * kg;
                if (s.Ships <= 0) f.Squadrons.Remove(s);
            }
        }

        int slips = Slips(c);
        if (c.Yard.Count > slips) c.Yard.RemoveRange(slips, c.Yard.Count - slips);
        while (c.Yard.Count < slips) c.Yard.Add(new Slip { Hull = ChooseHull(c) });
        for (int i = 0; i < c.Yard.Count; i++)
        {
            var sl = c.Yard[i];
            double kg = HullKg(sl.Hull);
            double step = Math.Min(dy / BuildYears(sl.Hull), 1 - sl.Progress);
            step = Math.Min(step, c.Materiel / kg);                  // a slip waits when materiel is short
            if (step <= 0) continue;
            c.Materiel = Math.Max(0, c.Materiel - kg * step);
            sl.Paid += kg * step; sl.Progress += step;
            if (sl.Progress >= 1 - 1e-9 && fleets.Count > 0)
            {
                fleets.OrderBy(f => f.Ships).ThenBy(f => f.Id).First().Add(sl.Hull, 1, 0f);
                c.Yard[i] = new Slip { Hull = ChooseHull(c) };
            }
        }
    }

    /// <summary>The council builds its best hull most of the time and a cheaper class the rest; manual Production may fix one.</summary>
    int ChooseHull(Civ c)
    {
        if (c.Policy.IsManual(Area.Production) && c.Policy.HullMix >= 0) return Math.Min(c.Policy.HullMix, c.Hull);
        return c.Rnd(0f, 1f) < 0.6f ? c.Hull : Math.Max(0, c.Hull - 2);
    }
}

/// <summary>A hull on a building slip: how far along it is, and how much materiel has gone into it.</summary>
public class Slip
{
    public int Hull;
    public double Progress, Paid;
}
