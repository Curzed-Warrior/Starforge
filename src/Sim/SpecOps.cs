using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

/// <summary>
/// Special forces (war-and-strife spec §4.6): a few expensive elite teams, trained in an academy and sent into systems
/// their civ does not control. Quality comes from training time, by the armies' rule: a course begun in peacetime is the
/// full course and turns out the best teams; one begun at war is rushed, quicker and worse.
/// </summary>
public partial class Simulation
{
    static double S(params string[] path) => WarData.D(new[] { "specops" }.Concat(path).ToArray());

    /// <summary>How many teams (trained or in training) a civ may keep: few, more with a bigger empire. None without an intelligence agency.</summary>
    public int TeamCap(Civ c)
    {
        if (!c.M.Acts.Contains("spy")) return 0;
        return (int)Math.Min(S("teamsMax"), S("teamsBase") + Math.Floor(Math.Log2(1 + c.Colonies.Count)));
    }

    /// <summary>Yearly: finished courses become teams at home, teams are paid for (the newest disbanded when they cannot be), and new courses begin up to the cap.</summary>
    void AcademyStep(Civ c, double dy)
    {
        var home = c.Colonies.FirstOrDefault(x => x.Home) ?? c.Colonies.FirstOrDefault();
        if (home == null) return;
        int cap = TeamCap(c);
        if (cap == 0 && c.Academy.Count == 0 && !TeamsOf(c).Any()) return;

        for (int i = c.Academy.Count - 1; i >= 0; i--)
        {
            var t = c.Academy[i];
            t.Left -= dy;
            if (t.Left > 1e-9) continue;
            c.Academy.RemoveAt(i);
            int n = TeamsOf(c).Count();
            Teams.Add(new Team
            {
                Id = NextTeamId++, Civ = c.Id, At = home.Star.Id, Rushed = t.Rushed,
                Name = Forces.Ordinals[n % Forces.Ordinals.Length] + " Special Group",
                Quality = (float)S(t.Rushed ? "rushedQuality" : "trainedQuality"),
            });
        }

        double kg = S("kgPerTeam"), upk = S("upkeepPerYear") * kg * dy;
        var mine = TeamsOf(c).OrderByDescending(t => t.Id).ToList();
        double bill = upk * mine.Count;
        foreach (var t in mine)
        {
            if (c.Materiel >= bill) break;
            Teams.Remove(t); bill -= upk;
        }
        c.Materiel = Math.Max(0, c.Materiel - bill);

        bool war = c.AtWar.Count > 0;
        double men = S("manpowerPerTeam");
        while (TeamsOf(c).Count() + c.Academy.Count < cap && c.Materiel >= kg && c.Manpower >= men)
        {
            c.Materiel -= kg; c.Manpower -= men;
            c.Academy.Add(new Training { Troops = 1, Left = S(war ? "rushedYears" : "trainYears"), Rushed = war, At = home.Star.Id });
        }
    }
}
