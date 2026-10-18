using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Starforge.Sim;

/// <summary>
/// Battles are resolved in the sim. Weapon and defence techs set family tiers; the best of them, the fleet, the biggest
/// hull and the ground forces multiply raw power into war strength (docs/TECH_INTEGRATION.md §5).
/// </summary>
public partial class Simulation
{
    static double[] _tierSteps;
    public static double TierPow(int t)
    {
        _tierSteps ??= TechData.P("tierPower", "steps").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        double p = 1;
        for (int i = 1; i <= Math.Min(t, _tierSteps.Length - 1); i++) p *= _tierSteps[i];
        return p;
    }

    /// <summary>How hard this civ hits. Unfed exotic weapons have already fallen back to their best fed tier in Mods.</summary>
    public double WarStrength(Civ civ)
    {
        var m = civ.M;
        int arms = m.Arms.Where(kv => kv.Key != "ground").Select(kv => kv.Value).DefaultIfEmpty(0).Max();
        double guard = (m.Guard.GetValueOrDefault("armor") + m.Guard.GetValueOrDefault("shield") + m.Guard.GetValueOrDefault("pd")) / 3.0;
        double s = Math.Pow(Math.Max(civ.GalacticPower, 1), 0.25)
            * (civ.Ethic == "Ruthless" ? 1.2 : 1)
            * TierPow(arms)
            * Math.Sqrt(TierPow((int)Math.Round(guard)))
            * m.Fleet
            * (1 + TechData.D("hulls", "warBonusPerClass") * civ.Hull)
            * (1 + 0.05 * m.Arms.GetValueOrDefault("ground") * m.Ground);
        if (civ.Exalted) s *= 1.5;
        if (civ.Fallen) s *= 0.6;
        if (civ.Emergency) s *= 1.2;            // every yard on war footing
        return s;
    }

    public event Action<Colony, Colony, Civ> BattleFought;

    /// <summary>Atrocities are for the Ruthless; the Pragmatic stoop to them only at war and losing.</summary>
    public bool AtrocityAllowed(Civ c) => c.Ethic == "Ruthless" || c.Ethic == "Pragmatic" && c.AtWar.Any(id => id < Civs.Count && WarStrength(Civs[id]) > WarStrength(c));
}
