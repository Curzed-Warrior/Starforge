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
}
