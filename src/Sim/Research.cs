using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Starforge.Sim;

/// <summary>
/// Research, Stellaris-style: three slots (Physics, Engineering, Society) each work one tech at a time, drawn from a
/// weighted hand of three. The ruling faction's drive, the civ's ethic, its neighbours and the voice all lean on the draw.
/// See docs/TECH_INTEGRATION.md §6.
/// </summary>
public partial class Simulation
{
    public static readonly string[] Areas = { "Physics", "Engineering", "Society" };
    public static string AreaOf(char b) => b is 'E' or 'M' or 'Q' ? "Physics" : b is 'I' or 'X' or 'W' ? "Engineering" : "Society";

    static Dictionary<(char, int), int> _tierCount;
    static int TierCount(char b, int t)
    {
        _tierCount ??= Defs.Techs.GroupBy(x => (x.B, x.Tier)).ToDictionary(g => g.Key, g => g.Count());
        return _tierCount.GetValueOrDefault((b, t));
    }

    /// <summary>Research points arrive from colonies and are split across the slots by the drive's taste for each area.</summary>
    public void AddResearch(Civ civ, double rp)
    {
        if (rp <= 0) return;
        if (Laws.Contains("research2")) rp *= 2;
        if (civ.OvermindUntil > Year) rp *= 1.3;
        rp += civ.RpBank; civ.RpBank = 0;
        double total = 0;
        Span<double> share = stackalloc double[3];
        for (int i = 0; i < 3; i++)
        {
            var s = civ.Slots[Areas[i]];
            if (s.Id == null) continue;
            foreach (var b in TechData.P("research", "areas", Areas[i]).EnumerateArray()) share[i] += civ.W.GetValueOrDefault(b.GetString()[0], 1f);
            share[i] *= civ.Policy.W(Areas[i]);                                   // the player's research priorities
            total += share[i];
        }
        if (total <= 0) { civ.RpBank = Math.Min(rp, 1e6); return; }
        for (int i = 0; i < 3; i++) if (share[i] > 0) civ.Slots[Areas[i]].P += rp * share[i] / total;
    }

    /// <summary>Some discoveries need more than knowledge: a foothold near the core, a galaxy behind you.</summary>
    public bool Gate(Civ civ, Tech t)
    {
        if (t.Cond == null) return true;
        foreach (var part in t.Cond.Split('&'))
        {
            bool ok = part switch
            {
                "core_colony" => civ.Colonies.Any(c => new Vector2(c.Star.Pos.X, c.Star.Pos.Z).Length() < 220f),
                _ when part.StartsWith("colonies>=") => civ.Colonies.Count >= int.Parse(part[10..]),
                _ when part.StartsWith("kardashev>=") => Defs.Kardashev(civ.GalacticPower) >= double.Parse(part[11..], System.Globalization.CultureInfo.InvariantCulture),
                _ when part.StartsWith("digital>=") => civ.Digital >= double.Parse(part[9..], System.Globalization.CultureInfo.InvariantCulture),
                _ => true,
            };
            if (!ok) return false;
        }
        return true;
    }

    /// <summary>An exclusive choice already made shuts the other doors.</summary>
    public static bool ExclusiveOk(Civ civ, Tech t) =>
        t.Exclusive == null || !Defs.ExclusiveGroups[t.Exclusive].Any(o => o != t.Id && civ.Techs.Contains(o));

    /// <summary>Beyond the spine, a tier opens once the tier below it is reasonably well known in that branch.</summary>
    public static bool TierOk(Civ civ, Tech t)
    {
        if (t.Spine || t.Tier == 0) return true;
        // count only the tier below that this civ could still learn: an exclusive choice closes some doors for good
        int open = Defs.Techs.Count(x => x.B == t.B && x.Tier == t.Tier - 1 && !Closed(civ, x));
        int need = Math.Min(3, open);
        if (need == 0) return true;
        int have = 0;
        foreach (var id in civ.Techs) { var k = Defs.TryTech(id); if (k != null && k.B == t.B && k.Tier == t.Tier - 1 && ++have >= need) return true; }
        return false;
    }

    /// <summary>Shut off for good by an exclusive choice already made (directly, or through every route to it).</summary>
    public static bool Closed(Civ civ, Tech t, int depth = 0)
    {
        if (civ.Techs.Contains(t.Id)) return false;
        if (!ExclusiveOk(civ, t)) return true;
        if (depth > 12) return false;
        foreach (var g in t.Req) if (g.All(r => Defs.TryTech(r) is { } rt && Closed(civ, rt, depth + 1))) return true;
        return false;
    }

    /// <summary>Could this civ research t right now (ignoring which slot is free)?</summary>
    public bool Available(Civ civ, Tech t) =>
        !civ.Techs.Contains(t.Id) && civ.PrereqDone(t) && ExclusiveOk(civ, t) && TierOk(civ, t) && Gate(civ, t)
        && (t.Gate == null || civ.Gates.GetValueOrDefault(t.Gate) >= 2)
        && !(civ.Banned.TryGetValue(t.Id, out var until) && Year < until);

    public IEnumerable<Tech> AvailableIn(Civ civ, string area) =>
        Defs.Techs.Where(t => AreaOf(t.B) == area && Available(civ, t) && civ.Slots.Values.All(s => s.Id != t.Id));

    /// <summary>Which member of an exclusive group this council leans toward (decided once, by drive and ethic, with a little fate).</summary>
    string Lean(Civ civ, string group)
    {
        if (civ.ExclusivePick.TryGetValue(group, out var pick)) return pick;
        var members = Defs.ExclusiveGroups[group];
        pick = members[_r.Next(members.Length)];
        var prefs = TechData.P("research", "aiExclusivePrefs");
        if (prefs.TryGetProperty(group, out var gp))
        {
            double chance = gp.TryGetProperty("randomChance", out var rc) ? rc.GetDouble() : 0;
            if (_r.NextDouble() >= chance)
            {
                if (gp.TryGetProperty(civ.Drive, out var byDrive)) pick = byDrive.GetString();
                else if (gp.TryGetProperty(civ.Ethic, out var byEthic)) pick = byEthic.GetString();
            }
            else if (gp.TryGetProperty("alternates", out var alt) && _r.NextDouble() < 0.5)
            {
                var a = alt.EnumerateArray().Select(x => x.GetString()).ToArray();
                pick = a[_r.Next(a.Length)];
            }
        }
        civ.ExclusivePick[group] = pick;
        return pick;
    }

    public double Weight(Civ civ, Tech t, Slot slot, bool restless)
    {
        var w = TechData.P("research", "weights");
        double x = civ.W.GetValueOrDefault(t.B, 1f) * (restless && t.B == 'X' ? 4 : 1);
        if (t.Rare) x *= w.GetProperty("rare").GetDouble();
        if (t.Keystone) x *= w.GetProperty("keystone").GetDouble();
        if (t.Danger) x *= w.GetProperty("dangerByEthic").GetProperty(civ.Ethic).GetDouble();
        if (t.Atrocity) x *= w.GetProperty("atrocityByEthic").GetProperty(civ.Ethic).GetDouble();
        if (slot.LastHand.Contains(t.Id) && !t.Spine) x *= w.GetProperty("repeatPenalty").GetDouble();
        if (civ.Met.Any(id => id < Civs.Count && !Civs[id].Dead && Civs[id].Techs.Contains(t.Id))) x *= w.GetProperty("neighbourKnows").GetDouble();
        if (t.Exclusive != null && Lean(civ, t.Exclusive) != t.Id) x *= 0.02;
        if (t.Spine) x *= SpineBias;
        return x / Math.Pow(t.Tier + 1, w.GetProperty("tierExponent").GetDouble());
    }
    /// <summary>Pacing knob: how strongly councils favour the old spine over the wider tree.</summary>
    public static double SpineBias = 1.0;
    public static bool DebugHands = OS.GetCmdlineUserArgs().Contains("--hands");

    /// <summary>Deal a slot a fresh hand of three and let the council choose.</summary>
    public void PickResearch(Civ civ, string area)
    {
        var slot = civ.Slots[area];
        bool manual = civ.Main && civ.Policy.IsManual(Area.Research);
        if (manual && Waiting("research:" + area)) return;                       // the hand dealt is still on the table
        slot.Id = null;
        var avail = AvailableIn(civ, area).ToList();
        if (avail.Count == 0) { slot.Hand.Clear(); return; }
        if (slot.Pinned != null && avail.Find(t => t.Id == slot.Pinned) is { } pinned)
        {
            slot.Pinned = null; slot.Hand = new() { pinned.Id }; slot.Id = pinned.Id; return;
        }
        bool restless = civ.Colonies.Any(c => c.Capture > 0.05 || c.Power > 1e20) && !civ.Techs.Contains("X4");
        var ws = avail.Select(t => Weight(civ, t, slot, restless)).ToList();
        int handSize = (int)TechData.D("research", "handSize"), pityAt = (int)TechData.D("research", "weights", "pityDraws");
        var hand = new List<Tech>();
        // a keystone left waiting too long is forced into the hand
        foreach (var k in avail.Where(t => t.Keystone && civ.Pity.GetValueOrDefault(t.Id) >= pityAt).Take(handSize)) hand.Add(k);
        // the spine always gets a card: the old milestones keep their pace however wide the tree grows
        // (the lowest tier first, so a slot shared by two spine branches advances both rather than racing one)
        if (hand.Count < handSize && avail.Where(t => t.Spine && !hand.Contains(t)).OrderBy(t => t.Tier).ThenByDescending(t => Weight(civ, t, slot, restless) * civ.Rnd(0.8f, 1.2f)).FirstOrDefault() is { } sp) hand.Add(sp);
        var pool = avail.Select((t, i) => (t, w: ws[i])).Where(p => !hand.Contains(p.t) && p.w > 0).ToList();
        while (hand.Count < handSize && pool.Count > 0)
        {
            double sum = pool.Sum(p => p.w), r = _r.NextDouble() * sum;
            int j = 0;
            for (; j < pool.Count - 1; j++) { r -= pool[j].w; if (r <= 0) break; }
            hand.Add(pool[j].t); pool.RemoveAt(j);
        }
        if (hand.Count == 0) { slot.Hand.Clear(); return; }
        var best = hand.OrderByDescending(t => Weight(civ, t, slot, restless) * civ.Rnd(0.6f, 1.4f) * (civ.Pity.GetValueOrDefault(t.Id) >= pityAt ? 100 : 1)).First();
        if (manual)
        {
            // the hand goes to the player, the council's favourite first
            slot.LastHand = slot.Hand;
            slot.Hand = hand.Select(t => t.Id).ToList();
            var opts = hand.OrderBy(t => t == best ? 0 : 1).Select(t => ($"{t.Name}  ·  {Defs.Branch(t.B).Name}", (Action)(() => { if (slot.Id == null && Available(civ, t)) slot.Id = t.Id; }))).ToList();
            Raise("Decision", $"{area} research", $"The {area.ToLower()} slot is free. The council favours {best.Name}: {best.Fx}.", HomeColony, "research:" + area, opts);
            return;
        }
        foreach (var k in avail.Where(t => t.Keystone && t != best)) civ.Pity[k.Id] = civ.Pity.GetValueOrDefault(k.Id) + 1;
        civ.Pity.Remove(best.Id);
        slot.LastHand = slot.Hand;
        slot.Hand = hand.Select(t => t.Id).ToList();
        slot.Id = best.Id;
        if (DebugHands && civ.Main && Year < 2320) GD.Print($"HAND {Year:0000.0} {area} {civ.Drive}/{civ.Ethic} W={string.Join("", civ.W.Select(kv => kv.Key + kv.Value.ToString("0.0")))} [{string.Join(", ", hand.Select(t => t.Id + (t.Spine ? "*" : "") + ":" + Weight(civ, t, slot, restless).ToString("0.00")))}] -> {best.Id}  spineAvail=[{string.Join(",", avail.Where(t => t.Spine).Select(t => t.Id))}]");
        if (civ.Main && (best.Spine || best.Keystone))
            Log(HomeColony, $"The {civ.Drive.ToLower()} majority directs research toward {best.Name} ({Defs.Branch(best.B).Name}).", "council", null);
    }

    public void PickResearch(Civ civ) { foreach (var a in Areas) if (civ.Slots[a].Id == null) PickResearch(civ, a); }

    /// <summary>Work through finished research and refill empty slots.</summary>
    void ResearchStep(Civ civ)
    {
        foreach (var a in Areas)
        {
            var slot = civ.Slots[a];
            for (int guard = 0; guard < 6; guard++)
            {
                if (slot.Id == null) { PickResearch(civ, a); if (slot.Id == null) break; }
                var t = Defs.TechById(slot.Id);
                if (civ.Techs.Contains(t.Id) || !ExclusiveOk(civ, t)) { slot.Id = null; continue; }
                double cost = t.CostFor(civ);
                if (slot.P < cost) break;
                slot.P -= cost;
                CompleteTech(civ, t);
            }
        }
    }

    public event Action<Civ, string> TechLost;

    /// <summary>A tech becomes known: its standing effects reapply and its one-off consequences happen once.</summary>
    public void CompleteTech(Civ civ, Tech t, bool quiet = false)
    {
        if (civ.Techs.Contains(t.Id)) return;
        civ.Techs.Add(t.Id);
        foreach (var s in civ.Slots.Values) if (s.Id == t.Id) s.Id = null;
        if (t.Uses.Length > 0) RefreshFed(civ);
        civ.ComputeMods();
        civ.ShiftInf(Defs.BranchFaction[t.B], t.Spine ? 0.06f : 0.01f);
        foreach (var e in t.Effects)
        {
            switch (e.Kind)
            {
                case "meter": civ.AddMeter(e.A[0], (e.A[1] == "-" ? -1 : 1) * double.Parse(e.A[2], System.Globalization.CultureInfo.InvariantCulture)); break;
                case "digital": civ.Digital = Math.Max(civ.Digital, double.Parse(e.A[0], System.Globalization.CultureInfo.InvariantCulture)); break;
                case "conscience": NudgeConscience(civ, float.Parse(e.A[0], System.Globalization.CultureInfo.InvariantCulture)); break;
                case "path": civ.Path = e.A[0]; break;
                case "event": TechEvent(civ, e.A[0]); break;
            }
        }
        if (civ.Main && !quiet)
        {
            if (t.Spine || t.Keystone) { Log(HomeColony, $"Breakthrough: {t.Name}. {t.LogText}", "break", null); Fx(HomeColony, "breakthrough", null); }
            else Log(HomeColony, $"Research: {t.Name}. {t.Fx}.", "research", null);
        }
        GateConsequence(civ, t);
        if (civ.M.Hive && civ.Colonies.Count > 1) HiveAdopted(civ);
        TechCompleted?.Invoke(civ, t);
        if (civ.Main && t.Id == "E9") Milestone(HomeColony, "The Core Ignites", $"Year {Year:0}. {t.LogText}", "galaxy");
        if (civ.Main && t.Id == "X10") ArkYear = Year;
        if (civ.Main && t.Id == "X10") Milestone(HomeColony, "Andromeda", $"Year {Year:0}. {t.LogText} Somewhere ahead, another galaxy is waiting to be asked the same questions.", "galaxy");
        if (civ.Main && t.Id == "S10") Milestone(HomeColony, "Transcendence", $"Year {Year:0}. {t.LogText}", "galaxy");
        if (civ.Main && t.Path != null) Milestone(HomeColony, PathName(t.Path), $"Year {Year:0}. {t.Name}: humanity sets foot on the {PathName(t.Path).ToLower()}. There is no turning back from it.", "seat");
        foreach (var s in civ.Slots.Values) if (s.Id != null && !ExclusiveOk(civ, Defs.TechById(s.Id))) s.Id = null;
        TechLog?.Invoke(civ, t);
    }
    public static event Action<Civ, Tech> TechLog;

    public static string PathName(string p) => p switch
    {
        "S" => "Synthetic Path", "G" => "Genetic Path", "C" => "Cybernetic Path", "P" => "Psionic Path", "D" => "Path of Departure", _ => "Luminary Path",
    };

    /// <summary>Forget a tech (the voice can do this). Techs built on it stay known; only this one goes.</summary>
    public void LoseTech(Civ civ, Tech t)
    {
        if (!civ.Techs.Remove(t.Id)) return;
        civ.ComputeMods();
        TechLost?.Invoke(civ, t.Id);
    }

    /// <summary>Grant a newly spawned civ everything up to a tier, resolving exclusive choices the way its council would.</summary>
    public void GrantUpTo(Civ civ, int maxTier)
    {
        foreach (var t in Defs.Techs) if (t.Start) civ.Techs.Add(t.Id);
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var t in Defs.Techs)
            {
                if (t.Tier > maxTier || civ.Techs.Contains(t.Id) || !civ.PrereqDone(t) || !ExclusiveOk(civ, t)) continue;
                if (t.Exclusive != null && Lean(civ, t.Exclusive) != t.Id && Defs.ExclusiveGroups[t.Exclusive].Any(o => Defs.TechById(o).Tier <= maxTier && o == Lean(civ, t.Exclusive))) continue;
                if (t.Atrocity && civ.Ethic == "Reverent") continue;
                civ.Techs.Add(t.Id); changed = true;
                foreach (var e in t.Effects)
                {
                    if (e.Kind == "path") civ.Path = e.A[0];
                    if (e.Kind == "digital") civ.Digital = Math.Max(civ.Digital, double.Parse(e.A[0], System.Globalization.CultureInfo.InvariantCulture));
                }
                if (t.Gate != null) civ.Gates[t.Gate] = 2;
            }
        }
        // an old empire's stockpiles are full
        foreach (var t in civ.Techs.Select(Defs.TechById)) foreach (var e in t.Effects) if (e.Kind == "res") civ.Pools[e.A[0]] = Math.Max(civ.Pools.GetValueOrDefault(e.A[0]), 3);
        RefreshFed(civ);
        civ.ComputeMods();
    }
}
