using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

/// <summary>Infighting (stage 3): what a system holds against the core, and how far up the ladder it has climbed.</summary>
public partial class Colony
{
    /// <summary>Grievance 0..100, pulled each year toward a target made of named causes (Simulation.GrievanceTarget).</summary>
    public float Grievance;
    /// <summary>The ladder: 0 calm, 1 discontent, 2 strikes, 3 insurgency, 4 open revolt.</summary>
    public int Unrest;
    /// <summary>The biggest cause of the grievance, as a phrase; the movement's name once one rises (stage 3+).</summary>
    public string Cause, Movement;
    /// <summary>Self-rule granted: its industry stays at home, and it resents the core less.</summary>
    public bool Autonomy;
    /// <summary>The council has invested here: relief until this year.</summary>
    public double InvestedUntil;

    /// <summary>What this system's works budget may send to other systems' great works: nothing while it strikes or rules itself.</summary>
    public double LocalWorksShipped() => Unrest >= 2 || Autonomy ? 0 : LocalWorks;
}

public partial class Simulation
{
    static double SD(params string[] path) => WarData.D(new[] { "strife" }.Concat(path).ToArray());

    static string[] _unrestNames;
    public static string[] UnrestNames => _unrestNames ??= WarData.P("strife", "ladder", "names").EnumerateArray().Select(e => e.GetString()).ToArray();

    /// <summary>The ladder with hysteresis: a step is climbed at up[s] and only left once grievance falls below down[s-1].</summary>
    public static int Stage(float g, int cur)
    {
        int s = Math.Clamp(cur, 0, 4);
        while (s < 4 && g >= SD("ladder", "up", s.ToString())) s++;
        while (s > 0 && g < SD("ladder", "down", (s - 1).ToString())) s--;
        return s;
    }

    /// <summary>The drive a system's people lean to: the capital to the council's, mining worlds to the Forgewrights, the rest by temperament.</summary>
    public static string Leaning(Colony c)
    {
        if (c.Home) return c.Civ.Drive;
        if (c.Res.Strategic.Count > 0 || c.Res.Metals >= 2.5) return "Industrious";
        return (c.Star.Seed & 3) == 0 ? "Contemplative" : "Expansionist";
    }

    /// <summary>How much a government feels its people's grievances: a hive not at all, a mostly digital people little, a junta most.</summary>
    public double GovFactor(Civ civ)
    {
        if (civ.M.Hive) return SD("gov", "hive");
        var g = Government.Of(civ);
        double f = g == Government.Synthetic ? SD("gov", "Synthetic Commonwealth")
            : (g == Government.Directorate ? SD("gov", "Directorate") : 1) * (1 - civ.Digital * (1 - SD("gov", "digital")));
        return f;
    }

    /// <summary>The worst exhaustion of any open war this civ is in (0..100).</summary>
    double WarExhaustionOf(Civ civ)
    {
        double e = 0;
        foreach (var w in Wars) if (w.Open && w.Has(civ.Id)) e = Math.Max(e, w.A.Contains(civ.Id) ? w.ExhA : w.ExhB);
        return e;
    }

    static double MeanIndustry(Civ civ) => civ.Colonies.Count == 0 ? 0 : civ.Colonies.Average(x => x.IndustryKg);

    /// <summary>Where this system's grievance is heading (0..100), and its biggest cause as a phrase.</summary>
    public double GrievanceTarget(Colony c, out string cause) => GrievanceTarget(c, out cause, MeanIndustry(c.Civ), WarExhaustionOf(c.Civ));

    double GrievanceTarget(Colony c, out string cause, double meanInd, double exhaustion)
    {
        var civ = c.Civ;
        double best = 0, sum = 0; string top = null;
        void Add(string key, double k, string phrase)
        {
            if (k <= 0) return;
            double v = SD("causes", key) * k;
            sum += v;
            if (v > best) { best = v; top = phrase; }
        }
        if (!c.Autonomy) Add("levy", Math.Clamp(civ.LevyOut.GetValueOrDefault(c), 0, 1), "its industry is shipped off to the capital's great works");
        var capital = civ.Colonies.FirstOrDefault(x => x.Home) ?? civ.Colonies.FirstOrDefault();
        if (capital != null && capital != c)
            Add(Folds(civ) ? "distance" : "distanceSlow", Math.Min(c.Star.Pos.DistanceTo(capital.Star.Pos) / 25.0, 3) / 3, "the capital is a lifetime away");
        if (c.PopCap > 0 && c.Pop > 0.92 * c.PopCap) Add("crowding", 1, "there is no room left to live");
        if (civ.Emergency) Add("emergency", 1, "the emergency takes everything it makes");
        Add("warStrain", civ.WarStrain, "the war economy bleeds it dry");
        Add("warExhaustion", exhaustion, "the war has gone on far too long");
        if (Leaning(c) != civ.Drive) Add("outOfPower", 1, "its people have no voice on the council");
        if (!c.Autonomy && c.Res.Strategic.Count > 0) Add("deposits", 1, "its rare deposits feed other worlds' projects");
        if (c.OccupiedBy >= 0) Add("occupied", 1, "it is under enemy occupation");
        if (meanInd > 0 && c.IndustryKg < 0.2 * meanInd) Add("poverty", 1, "it is poor beside the core's wealth");
        Add("dread", civ.Dread, "it lives in fear of the voice");
        if (civ.MandatoryService && civ.Culture != null && civ.Culture.Duty < 0) Add("mandatory", 1, "its young are called up against their will");
        double relief = SD("relief", "base") + SD("relief", "megaOrder") * c.MegaOrder + SD("relief", "faith") * civ.Faith
                        + (c.Autonomy ? SD("relief", "autonomy") : 0) + (Year < c.InvestedUntil ? SD("relief", "invested") : 0);
        cause = top;
        return Math.Clamp(sum * GovFactor(civ) - relief, 0, 100);
    }

    /// <summary>
    /// Yearly per civ: every system's grievance moves toward its target, the ladder is climbed or descended, and a
    /// system on strike stops shipping its industry (Colony.LocalWorksShipped). A hive feels nothing.
    /// </summary>
    void StrifeStep(Civ civ, double dy)
    {
        if (civ.M.Hive)
        {
            foreach (var c in civ.Colonies) { c.Grievance = 0; c.Unrest = 0; c.Movement = null; }
            return;
        }
        double mean = MeanIndustry(civ), exh = WarExhaustionOf(civ), pull = Math.Min(1, SD("pull") * dy);
        foreach (var c in civ.Colonies.ToList())
        {
            if (c.Home) continue;
            double t = GrievanceTarget(c, out var cause, mean, exh);
            c.Grievance = (float)Math.Clamp(c.Grievance + (t - c.Grievance) * pull, 0, 100);
            c.Cause = cause;
            int old = c.Unrest;
            c.Unrest = Stage(c.Grievance, old);
            if (old >= 3 && c.Unrest < 3 && c.Movement != null)
            {
                if (civ.Main) Log(c, $"{Cap(c.Movement)} lay down their arms. {c.Sys.Name} is quiet again.", "council", null);
                c.Movement = null;
            }
        }
    }

}
