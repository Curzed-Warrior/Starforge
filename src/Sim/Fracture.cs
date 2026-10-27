using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

public partial class Colony
{
    /// <summary>The system refused to join its civ's Hive Consensus: it is not linked, and keeps its grievances.</summary>
    public bool Holdout;
    /// <summary>Crackdowns this system has suffered under its owner: each one relieves less (strife.answers.crackdownFade).</summary>
    public int Crackdowns;
}

/// <summary>
/// Fracture: Hive Consensus is not a quiet tech pick. When a civ researches it every system votes; those that refuse
/// leave at once when there are enough of them (in peace, or by war), and otherwise stay as holdouts who resent the
/// hive and can rise later. The same rules hold for every civ.
/// </summary>
public partial class Simulation
{
    /// <summary>Each hive vote this game, for the trace summary: year, civ, refused/of, and how it ended. Not saved.</summary>
    public readonly List<string> HiveVotes = new();

    /// <summary>Called once a civ has become a hive (S8 learned): how strongly its systems will refuse is a draw of its own.</summary>
    void HiveAdopted(Civ civ)
    {
        if (civ.Used.Contains("hiveVote")) return;
        double swing = Math.Pow(_r.NextDouble(), SD("hive", "swingPower")) * SD("hive", "swingMax");
        HiveVote(civ, swing);
    }

    /// <summary>How much a system will not be joined, before the civ's swing: far systems, those whose people lean away
    /// from the Contemplatives who push the hive, and a people still mostly flesh refuse more.</summary>
    double Refusal(Colony c, Colony capital)
        => SD("hive", "base") + SD("hive", "distance") * DistanceFactor(c, capital)
           + (Leaning(c) != "Contemplative" ? SD("hive", "outOfPower") : 0) + SD("hive", "flesh") * (1 - c.Civ.Digital);

    /// <summary>
    /// The vote, with `swing` scaling every system's will to refuse. The capital always joins, and a civ of only a few
    /// systems (strife.hive.minSystems) joins whole. The swing stays with the civ: it is how fast the link frays later.
    /// No system is certain to refuse (strife.hive.refuseMax), and the hive keeps most of what it had: at most
    /// strife.hive.maxShare of its systems refuse, the most unwilling first.
    /// </summary>
    public (int Refused, string Split) HiveVote(Civ civ, double swing)
    {
        civ.HiveSwing = swing;
        var capital = CapitalOf(civ);
        var refusers = new List<(Colony C, double P)>();
        if (civ.Colonies.Count >= SD("hive", "minSystems"))
            foreach (var c in civ.Colonies)
            {
                if (c == capital || c.Home) continue;
                double p = Math.Min(SD("hive", "refuseMax"), swing * Refusal(c, capital));
                if (_r.NextDouble() < p) refusers.Add((c, p));
            }
        int most = (int)(SD("hive", "maxShare") * civ.Colonies.Count);
        return HiveVote(civ, refusers.OrderByDescending(x => x.P).Take(most).Select(x => x.C).ToHashSet());
    }

    /// <summary>
    /// Yearly in a hive: the link frays. Each linked system may slip out of the consensus and become a holdout, the more
    /// likely the further it is and the stronger the civ's swing was at the vote.
    /// </summary>
    void HiveFray(Civ civ, double dy)
    {
        if (civ.HiveSwing < SD("hive", "frayMinSwing")) return;     // a weak refusal: the link holds for good
        var capital = CapitalOf(civ);
        double k = SD("hive", "frayPerYear") * civ.HiveSwing * dy;
        bool first = civ.Main && !civ.Colonies.Any(c => c.Holdout);      // the chronicle tells of the first crack in the link
        foreach (var c in civ.Colonies)
            if (!c.Holdout && c != capital && !c.Home && c.OccupiedBy < 0 && _r.NextDouble() < k * Refusal(c, capital))
            {
                c.Holdout = true; c.Grievance = Math.Max(c.Grievance, (float)SD("hive", "holdoutGrievance"));
                if (first) Log(c, $"{c.Sys.Name} falls silent on the consensus. Its people cut the link and will not be joined again.", "loss", null);
                first = false;
            }
    }

    /// <summary>The vote's outcome for a given set of refusers: they split away when there are enough, else they hold out.</summary>
    public (int Refused, string Split) HiveVote(Civ civ, HashSet<Colony> refusers)
    {
        civ.Used.Add("hiveVote");
        StrifeCount("hiveVotes");
        var capital = CapitalOf(civ);
        var bloc = civ.Colonies.Where(c => refusers.Contains(c) && c != capital && !c.Home && c.OccupiedBy < 0).ToList();
        int systems = civ.Colonies.Count;
        string split = null;
        if (bloc.Count >= Math.Max(2, SD("hive", "splitShare") * systems))
        {
            bool civilWar = (bloc.Count >= SD("revolt", "civilWarShare") * systems || bloc.Sum(c => c.Pop) >= SD("revolt", "civilWarPopShare") * civ.Pop)
                            && bloc.Count >= SD("revolt", "civilWarMinSystems");
            bool peaceful = !civilWar && civ.Ethic != "Ruthless";
            string drive = bloc.GroupBy(Leaning).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key;
            Log(CapitalOf(civ), $"{Narrative2.Called(civ, true)} votes to join the Hive Consensus. {bloc.Count} of {systems} systems refuse.", "loss", null);
            StrifeCount("revolts");
            if (civilWar) StrifeCount("civilwars");
            var s = Separate(civ, bloc, drive, peaceful, civilWar: civilWar);
            split = s == null ? null : civilWar ? "civil war" : peaceful ? "peaceful" : "war";
        }
        if (split == null)
            foreach (var c in bloc) { c.Holdout = true; c.Grievance = Math.Max(c.Grievance, (float)SD("hive", "holdoutGrievance")); }
        if (split == null && bloc.Count > 0 && civ.Main)
            Log(CapitalOf(civ), $"Humanity joins the Hive Consensus, all but {(bloc.Count == 1 ? bloc[0].Sys.Name : bloc.Count + " systems")}. They will not be linked.", "loss", null);
        HiveVotes.Add($"{Year:0}:{(civ.Human ? "H" : "A")}{bloc.Count}/{systems}:{split ?? (bloc.Count > 0 ? "holdouts" : "clean")}");
        return (bloc.Count, split);
    }

    /// <summary>
    /// Those who leave a hive are not a hive: the splinter forgets every tech that links minds into one (Hive Consensus,
    /// Link-Hive: any with the Hive flag) and will not build one for a long while.
    /// </summary>
    void LeaveHive(Civ s)
    {
        foreach (var id in s.Techs.Where(IsHiveTech).ToList())
        {
            s.Techs.Remove(id);
            s.Banned[id] = Year + SD("hive", "banYears");
        }
        s.ComputeMods();
        foreach (var c in s.Colonies) c.Holdout = false;
    }

    static bool IsHiveTech(string id) => Defs.TechById(id) is { } t && t.Effects.Any(e => e.Kind == "flag" && e.A.Length > 0 && e.A[0] == "Hive");

    /// <summary>A rising now, in a civ with a system in open revolt (tests).</summary>
    public Civ RevoltNow(Civ civ) => RevoltStep(civ, 1e6);
}
