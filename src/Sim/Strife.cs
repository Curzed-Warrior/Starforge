using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

/// <summary>Infighting (stage 3): what a system holds against the core, and how far up the ladder it has climbed.</summary>
public partial class Colony
{
    /// <summary>Grievance 0..100, pulled each year toward a target made of named causes (Simulation.GrievanceTarget).</summary>
    /// <summary>Special forces' work here (SpecOps.Succeed): until when its yards and deposits are wrecked, agitators stir it, and who prepared it for invasion.</summary>
    public double YardDownUntil, DepositsDownUntil, IncitedUntil, PreparedUntil;
    public int PreparedBy = -1;
    public float Grievance;
    /// <summary>The ladder: 0 calm, 1 discontent, 2 strikes, 3 insurgency, 4 open revolt.</summary>
    public int Unrest;
    /// <summary>The biggest cause of the grievance, as a phrase; the movement's name once one rises (stage 3+).</summary>
    public string Cause, Movement;
    /// <summary>Self-rule granted: its industry stays at home, and it resents the core less.</summary>
    public bool Autonomy;
    /// <summary>Until when word of a crackdown elsewhere has already reached this system (not saved).</summary>
    internal double HeardCrackdown;
    /// <summary>The cause key behind Cause (strife.causes), recomputed yearly.</summary>
    public string CauseKey;
    /// <summary>The council has invested here: relief until this year.</summary>
    public double InvestedUntil;

    /// <summary>What this system's works budget may send to other systems' great works: nothing while it strikes or rules itself.</summary>
    public double LocalWorksShipped() => Unrest >= 2 || Autonomy ? 0 : LocalWorks;
}

public partial class Simulation
{
    /// <summary>Counts of strife events this game (movements, revolts, coups...), for the trace summary. Not saved.</summary>
    public readonly Dictionary<string, int> StrifeStats = new();
    void StrifeCount(string what) => StrifeStats[what] = StrifeStats.GetValueOrDefault(what) + 1;

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
        return civ.Junta(Year) ? f * SD("gov", "junta") : f;
    }

    /// <summary>The worst exhaustion of any open war this civ is in (0..100).</summary>
    double WarExhaustionOf(Civ civ)
    {
        double e = 0;
        foreach (var w in Wars) if (w.Open && w.Has(civ.Id)) e = Math.Max(e, w.A.Contains(civ.Id) ? w.ExhA : w.ExhB);
        return e;
    }

    /// <summary>
    /// How far from the capital a system feels, 0..1. With fold drives it is plain distance (25 galaxy units and more
    /// is as far as it gets); without them it is the voyage: a lifetime's travel (strife.lifetimeYears) or more is as
    /// far away as anywhere.
    /// </summary>
    public double DistanceFactor(Colony c, Colony capital)
    {
        if (Folds(c.Civ)) return Math.Min(c.Star.Pos.DistanceTo(capital.Star.Pos) / 25.0, 3) / 3;
        return Math.Min(TravelYears(c.Civ, c.Star, capital.Star) / SD("lifetimeYears"), 1);
    }

    static double MeanIndustry(Civ civ) => civ.Colonies.Count == 0 ? 0 : civ.Colonies.Average(x => x.IndustryKg);

    /// <summary>Each cause of grievance (strife.causes) as the chronicle and the interface say it.</summary>
    public static readonly Dictionary<string, string> CausePhrase = new()
    {
        ["levy"] = "its industry is shipped off to the capital's great works", ["distance"] = "the capital is a lifetime away",
        ["distanceSlow"] = "the capital is a lifetime away", ["crowding"] = "there is no room left to live",
        ["emergency"] = "the emergency takes everything it makes", ["warStrain"] = "the war economy bleeds it dry",
        ["warExhaustion"] = "the war has gone on far too long", ["outOfPower"] = "its people have no voice on the council",
        ["deposits"] = "its rare deposits feed other worlds' projects", ["occupied"] = "it is under enemy occupation",
        ["poverty"] = "it is poor beside the core's wealth", ["dread"] = "it lives in fear of the voice",
        ["mandatory"] = "its young are called up against their will", ["incited"] = "agitators from abroad are stirring it up",
        ["hive"] = "it will not be joined to the hive",
    };

    /// <summary>Where this system's grievance is heading (0..100), and its biggest cause as a phrase.</summary>
    public double GrievanceTarget(Colony c, out string cause)
    {
        double t = GrievanceTarget(c, out string key, MeanIndustry(c.Civ), WarExhaustionOf(c.Civ), CapitalOf(c.Civ));
        cause = key == null ? null : CausePhrase[key];
        return t;
    }

    /// <summary>The target, with its biggest cause as a key of strife.causes.</summary>
    double GrievanceTarget(Colony c, out string causeKey, double meanInd, double exhaustion, Colony capital)
        => GrievanceWork(c, out causeKey, meanInd, exhaustion, capital, null);

    /// <summary>Every positive cause of a system's grievance, biggest first, then the relief as a negative entry.
    /// The causes sum, times GovFactor, plus the relief (negative), clamped to 0..100, is GrievanceTarget.</summary>
    public List<(string Key, string Phrase, double Points)> GrievanceCauses(Colony c)
    {
        var list = new List<(string Key, string Phrase, double Points)>();
        GrievanceWork(c, out _, MeanIndustry(c.Civ), WarExhaustionOf(c.Civ), CapitalOf(c.Civ), list);
        return list;
    }

    double GrievanceWork(Colony c, out string causeKey, double meanInd, double exhaustion, Colony capital, List<(string, string, double)> causes)
    {
        var civ = c.Civ;
        double best = 0, sum = 0; string top = null;
        void Add(string key, double k)
        {
            if (k <= 0) return;
            double v = SD("causes", key) * k;
            sum += v;
            causes?.Add((key, CausePhrase[key], v));
            if (v > best) { best = v; top = key; }
        }
        if (!c.Autonomy) Add("levy", Math.Clamp(civ.LevyOut.GetValueOrDefault(c), 0, 1));
        if (capital != null && capital != c) Add(Folds(civ) ? "distance" : "distanceSlow", DistanceFactor(c, capital));
        if (c.PopCap > 0 && c.Pop > 0.92 * c.PopCap) Add("crowding", 1);
        if (civ.Emergency) Add("emergency", 1);
        Add("warStrain", civ.WarStrain);
        Add("warExhaustion", exhaustion);
        if (Leaning(c) != civ.Drive) Add("outOfPower", 1);
        if (!c.Autonomy && c.Res.Strategic.Count > 0) Add("deposits", 1);
        if (c.OccupiedBy >= 0) Add("occupied", 1);
        if (meanInd > 0 && c.IndustryKg < 0.2 * meanInd) Add("poverty", 1);
        Add("dread", civ.Dread);
        if (civ.MandatoryService && civ.Culture != null && civ.Culture.Duty < 0) Add("mandatory", 1);
        if (c.IncitedUntil > Year) Add("incited", 1);
        bool holdout = c.Holdout && civ.M.Hive;
        if (holdout) Add("hive", 1);
        double relief = SD("relief", "base") + SD("relief", "megaOrder") * c.MegaOrder + SD("relief", "faith") * civ.Faith
                        + (c.Autonomy ? SD("relief", "autonomy") : 0) + (Year < c.InvestedUntil ? SD("relief", "invested") : 0);
        causeKey = top;
        if (causes != null)
        {
            causes.Sort((a, b) => b.Item3.CompareTo(a.Item3));
            causes.Add(("relief", "what eases it: its own order, faith, and investment", -relief));
        }
        return Math.Clamp(sum * (holdout ? SD("gov", "holdout") : GovFactor(civ)) - relief, 0, 100);
    }

    /// <summary>
    /// Yearly per civ: every system's grievance moves toward its target, the ladder is climbed or descended, and a
    /// system on strike stops shipping its industry (Colony.LocalWorksShipped). A hive feels its people's grievances
    /// at strife.gov.hive of the strength (at 0 it feels nothing, and nothing climbs the ladder), except in the systems
    /// that refused to join it (Colony.Holdout), which feel everything.
    /// </summary>
    void StrifeStep(Civ civ, double dy)
    {
        bool linkedOnly = civ.M.Hive && SD("gov", "hive") <= 0;
        if (linkedOnly)
        {
            bool any = false;
            foreach (var c in civ.Colonies)
                if (c.Holdout) any = true;
                else { c.Grievance = 0; c.Unrest = 0; c.Movement = null; }
            HiveFray(civ, dy);
            if (!any) return;
        }
        else
        {
            // the council's fate is judged on the year as it began: who was in arms, how tired the people were
            JuntaStep(civ);
            Coup(civ, dy);
            Revolution(civ, dy);
        }
        double mean = MeanIndustry(civ), exh = WarExhaustionOf(civ), pull = Math.Min(1, SD("pull") * dy);
        var capital = CapitalOf(civ);
        foreach (var c in civ.Colonies.ToList())
        {
            if (c.Home) { if (!linkedOnly) HomeWorlds(c, dy); continue; }
            if (linkedOnly && !c.Holdout) continue;
            double t = GrievanceTarget(c, out var key, mean, exh, capital);
            c.Grievance = (float)Math.Clamp(c.Grievance + (t - c.Grievance) * pull, 0, 100);
            c.CauseKey = key; c.Cause = key == null ? null : CausePhrase[key];
            int old = c.Unrest;
            c.Unrest = Stage(c.Grievance, old);
            if (old < 3 && c.Unrest >= 3) Rise(c);
            else if (c.Unrest >= 3) AnswerAgain(c);
            if (old >= 3 && c.Unrest < 3 && c.Movement != null)
            {
                if (civ.Main) Log(c, $"{Cap(c.Movement)} lay down their arms. {c.Sys.Name} is quiet again.", "council", null);
                c.Movement = null;
            }
        }
        RevoltStep(civ, dy);
    }
}
