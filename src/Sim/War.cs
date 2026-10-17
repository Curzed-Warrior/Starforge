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

    void Battle(Civ a, Civ b, (Colony A, Colony B, float D) front)
    {
        // the stronger side takes a border system
        double pa = WarStrength(a), pb = WarStrength(b);
        var att = _r.NextDouble() < pa / (pa + pb) ? a : b;
        var def = att == a ? b : a;
        var target = front.A.Civ == def ? front.A : front.B;
        var source = target == front.A ? front.B : front.A;
        if (target.Home && def.Colonies.Count > 1)
        {
            target = def.Colonies.Where(c => !c.Home).OrderBy(c => c.Star.Pos.DistanceTo(source.Star.Pos)).FirstOrDefault();
            if (target == null) return;
        }
        // a stealthy defender is hard to find in the first place
        if (_r.NextDouble() < 0.6 || _r.NextDouble() > 1.0 / Math.Max(1, def.M.Stealth)) return;
        BattleFought?.Invoke(source, target, att);
        if (target == Focus || target.Home) { Fx(target, "battle", target.Seat); }
        // wrecks teach: salvage toward the Umbral and Vault gates
        if (def.Knows("UMB-3") && att.Gates.GetValueOrDefault("GE6") == 1) att.GateP["GE6:salvage"] = att.GateP.GetValueOrDefault("GE6:salvage") + 1;
        if (def.Knows("GLY-1") && att.Gates.GetValueOrDefault("GE8") == 1) att.GateP["GE8:wrecks"] = 1;
        if (att.M.Acts.Contains("salvage_tech") && _r.NextDouble() < 0.3)
        {
            var t = def.Techs.Select(Defs.TechById).Where(x => Available(att, x)).OrderBy(_ => _r.Next()).FirstOrDefault();
            if (t != null) { CompleteTech(att, t, true); if (att.Main) Log(target, $"Salvage teams pick over the wrecks at {target.Sys.Name}. What they find becomes {t.Name}.", "event", null); }
        }
        // losses on both sides, eased by doctrine
        target.Pop *= 1 - 0.05 * def.M.WarLoss;
        double burnOdds = att.Ethic == "Ruthless" ? 0.4 / (1 + 0.1 * att.M.Arms.GetValueOrDefault("ground") * att.M.Ground) : 0;
        if (_r.NextDouble() < burnOdds)
            DestroyColony(target, $"{Narrative2.Called(att, true)} burn {target.Sys.Name}. Its swarm is scattered; its worlds go dark.");
        else
        {
            def.LastLoss = Year;
            def.Colonies.Remove(target);
            att.Colonies.Add(target);
            target.Civ = att;
            target.Star.Owner = att.Id;
            if (att.M.Acts.Contains("purge") && AtrocityAllowed(att)) { target.Pop *= 0.5; CivAtrocity(att, target, 10, def); if (att.Main || def.Main) Log(target, $"{Narrative2.Called(att, true)} purge {target.Sys.Name} of its former owners.", "loss", null); }
            else if (att.Main || def.Main) Log(target, $"{target.Sys.Name} falls to {Narrative2.Called(att)}.", att.Main ? "era" : "loss", null);
            if (att.Main) Raise("Battle", $"{target.Sys.Name} taken", $"Our fleets take {target.Sys.Name} from {Narrative2.Called(def)}.", target);
            else if (def.Main) Raise("War", $"{target.Sys.Name} lost", $"{target.Sys.Name} falls to {Narrative2.Called(att)}.", target);
        }
        if (def.Colonies.Count == 0) Extinct(def);
    }

    /// <summary>Atrocities are for the Ruthless; the Pragmatic stoop to them only at war and losing.</summary>
    public bool AtrocityAllowed(Civ c) => c.Ethic == "Ruthless" || c.Ethic == "Pragmatic" && c.AtWar.Any(id => id < Civs.Count && WarStrength(Civs[id]) > WarStrength(c));
}
