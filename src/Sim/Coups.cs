using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

/// <summary>
/// Coups, revolutions and juntas (stage 3): a victorious, ambitious admiral in an unpopular war takes the council and
/// rules by decree for a generation; a people pushed too far overturns its council and puts its own faction in.
/// </summary>
public partial class Simulation
{
    static double CD(string a, string b) => SD(a, b);

    /// <summary>Yearly: a junta whose time is up hands back to an elected council.</summary>
    void JuntaStep(Civ civ)
    {
        if (civ.JuntaUntil <= 0 || civ.Junta(Year)) return;
        civ.JuntaUntil = 0;
        civ.Leader = civ.NewLeader(civ.Drive, Year);
        Log(CapitalOf(civ), $"The junta in {Narrative2.Called(civ)} stands down. Speaker {civ.Leader.Name} is elected.", "era", null);
    }

    /// <summary>
    /// Yearly: in an open war that has worn the civ down, a victorious admiral who is disloyal or ambitious may take the
    /// council. Never in a junta, a hive or under an overmind.
    /// </summary>
    void Coup(Civ civ, double dy)
    {
        if (civ.Junta(Year) || civ.M.Hive || civ.OvermindUntil > Year) return;
        if (WarExhaustionOf(civ) < CD("coup", "exhaustion")) return;
        var f = FleetsOf(civ).Where(f => f.Admiral != null && f.Admiral.Wins >= CD("coup", "wins")
                                         && (f.Admiral.Loyalty < CD("coup", "loyalty") || f.Admiral.Trait == "Ambitious"))
            .OrderByDescending(f => f.Admiral.Wins).FirstOrDefault();
        if (f == null || _r.NextDouble() >= CD("coup", "chancePerYear") * dy) return;
        var adm = f.Admiral;
        civ.JuntaUntil = Year + CD("coup", "juntaYears");
        civ.Leader = new Leader { Name = adm.Name, Drive = civ.Drive, Since = Year, Born = adm.Born };
        f.Admiral = NewAdmiral(civ);
        StrifeCount("coups");
        string text = $"Coup. Admiral {adm.Name}, victor of {adm.Wins} battles, brings the {f.Name} home and takes the council. A junta rules {Narrative2.Called(civ)} now.";
        Log(CapitalOf(civ), text, "loss", null);
        if (civ.Main) Raise("Unrest", "Coup", text, CapitalOf(civ));
    }

    /// <summary>
    /// Yearly: when a third of the systems are in arms, or a war has exhausted the people completely, the people may
    /// overturn the council: the faction most of the risings lean to takes power, any junta falls, and grievances halve.
    /// </summary>
    void Revolution(Civ civ, double dy)
    {
        if (civ.M.Hive || civ.OvermindUntil > Year || civ.Colonies.Count == 0) return;
        var risen = civ.Colonies.Where(c => c.Unrest >= 3).ToList();
        bool pushed = risen.Count >= CD("revolution", "insurgentShare") * civ.Colonies.Count || WarExhaustionOf(civ) >= 100;
        if (!pushed || _r.NextDouble() >= CD("revolution", "chancePerYear") * dy) return;
        var pool = risen.Count > 0 ? risen : civ.Colonies;
        string drive = pool.GroupBy(Leaning).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key;
        string old = civ.Drive;
        civ.SetDrive(drive, civ.Ethic);
        civ.Leader = civ.NewLeader(drive, Year);
        civ.JuntaUntil = 0;
        foreach (var c in civ.Colonies) c.Grievance /= 2;
        StrifeCount("revolutions");
        string text = drive == old
            ? $"Revolution in {Narrative2.Called(civ)}. The council falls; Speaker {civ.Leader.Name} of the {civ.Faction(drive)} promises to listen."
            : $"Revolution in {Narrative2.Called(civ)}. The {civ.Faction(old)} are thrown out, and the {civ.Faction(drive)} take the council under Speaker {civ.Leader.Name}.";
        Log(CapitalOf(civ), text, "era", null);
        if (civ.Main) Raise("Unrest", "Revolution", text, CapitalOf(civ));
    }
}
