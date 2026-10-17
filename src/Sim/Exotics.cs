using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Starforge.Sim;

/// <summary>
/// The exotic layer (docs/TECH_INTEGRATION.md §7): five pools of strange stuff that keep the deep techs running, and the
/// gate events that open each exotic tier. Gates: 1 = triggered and waiting for a key, 2 = open (its tech can be drawn).
/// The Vault, the dormant Shard and the four rim Gates are placed when the galaxy is made and stay hidden until perceived.
/// </summary>
public partial class Simulation
{
    public static readonly string[] PoolNames = { "Umbral", "Abyssal", "Anima", "Stellarine", "Lumen" };
    /// <summary>Special places some civ has perceived, so the voice can see them too.</summary>
    public readonly HashSet<string> Revealed = new();
    public bool ShowHiddenToVoice;
    public event Action<StarInfo, string> SpecialRevealed;

    public static double PoolCap(Civ civ, string pool) => (10 + 2 * civ.Colonies.Count) * (pool == "Abyssal" && civ.M.Has("Reservoir") ? 4 : 1);

    /// <summary>Which use: techs are running: everything while the pool has stock, otherwise the cheapest tiers production can carry.</summary>
    public void RefreshFed(Civ civ)
    {
        var fed = new HashSet<string>();
        int n = Math.Max(1, civ.Colonies.Count);
        foreach (var p in PoolNames)
        {
            var users = civ.Techs.Select(Defs.TechById).Where(t => t.Uses.Contains(p)).OrderBy(t => t.Tier).ToList();
            if (users.Count == 0) continue;
            if (civ.Pools.GetValueOrDefault(p) > 1e-6) { foreach (var t in users) fed.Add(t.Id + "@" + p); continue; }
            double budget = ProduceRate(civ, p), cost = 0;
            foreach (var t in users) { cost += 0.002 * n; if (cost <= budget + 1e-12) fed.Add(t.Id + "@" + p); else break; }
        }
        var now = civ.Techs.Select(Defs.TechById).Where(t => t.Uses.Length > 0 && t.Uses.All(p => fed.Contains(t.Id + "@" + p))).Select(t => t.Id).ToHashSet();
        if (now.SetEquals(civ.Fed)) return;
        civ.Fed.Clear(); civ.Fed.UnionWith(now);
        civ.ComputeMods();
    }

    double ProduceRate(Civ civ, string pool)
    {
        double k = civ.M.Produce.GetValueOrDefault(pool);
        if (k <= 0) return 0;
        double cap = 0;
        foreach (var c in civ.Colonies) cap += Math.Max(0.1, c.Capture);
        return 0.01 * k * cap;
    }

    void PoolStep(Civ civ, double dy)
    {
        int n = civ.Colonies.Count;
        if (n == 0) return;
        bool any = false;
        foreach (var p in PoolNames)
        {
            double make = ProduceRate(civ, p), use = civ.M.Upkeep.GetValueOrDefault(p) * 0.002 * n;
            if (make <= 0 && use <= 0 && !civ.Pools.ContainsKey(p)) continue;
            any = true;
            double v = civ.Pools.GetValueOrDefault(p) + (make - use) * dy, cap = PoolCap(civ, p);
            if (v > cap)
            {
                // too much of the strange stuff in one place is its own danger
                if (p == "Abyssal") civ.AddMeter("Breach", (v - cap) * 2);
                if (p == "Stellarine") civ.AddMeter("Flux", (v - cap) * 2);
                v = cap;
            }
            civ.Pools[p] = Math.Max(0, v);
        }
        if (any) RefreshFed(civ);
    }

    // ---------- one-off tech events ----------
    void TechEvent(Civ civ, string id)
    {
        var home = civ.Colonies.FirstOrDefault();
        switch (id)
        {
            case "ai_rights":
                if (civ.Main) Log(home, "An AI files for citizenship. The case takes nine years. It wins.", "event", null);
                break;
            case "GE6_start":
                if (civ.Main) Log(home, "The halo maps come back wrong. There is more mass around the galaxy than there are stars to account for, and some of it is close enough to touch.", "omen", null);
                break;
            case "integration_crisis":
                if (home != null && home.Crisis == null) Narrative.StartCrisis(home, "integration");
                break;
        }
    }

    // ---------- gate events ----------
    static string GateName(string g) => TechData.P("gateEvents", g, "name").GetString();
    /// <summary>The chronicle's line when a gate first stirs for a civ.</summary>
    static string GateIntro(string g) => g switch
    {
        "GE6" => "dark matter is everywhere in the halo, and nobody knows how to hold it.",
        "GE7" => "the Umbral work points at the energy of the vacuum itself.",
        "GE7b" => "somewhere there is an archive that remembers the galaxy before us.",
        "GE8" => "the old writing on the Umbral relics leads to a place: the Vault.",
        "GE9" => "a dead star could be woken, if anyone knew its pulse.",
        "GE10" => "the four sealed gates on the rim are weakening.",
        _ => "the first trial is passed; two more remain.",
    };

    void OpenGate(Civ civ, string g, string how)
    {
        if (civ.Gates.GetValueOrDefault(g) >= 2) return;
        civ.Gates[g] = 2;
        var home = civ.Colonies.FirstOrDefault();
        string opens = TechData.P("gateEvents", g, "opens").GetString();
        var t = Defs.TryTech(opens);
        if (civ.Main || civ == Focus?.Civ)
            Log(home, $"{GateName(g)}: {how} {(t != null ? t.Name + " can be studied now." : "")}", "omen", null);
        if (civ.Main) Milestone(home, GateName(g), $"Year {Year:0}. {how}", "seat");
        switch (g)
        {
            case "GE6":   // the ancients feel the dark matter move
                foreach (var a in Civs.Where(a => a.Ancient && a.Met.Contains(civ.Id))) a.AddMeter("Elders", 5);
                break;
            case "GE9":
                var shard = Gal.Stars.FirstOrDefault(s => s.Special == "shard");
                if (shard != null && shard.Dead)
                {
                    shard.Dead = false; shard.Lum = 1.6f; shard.Temp = 7200f; shard.Name = "the Rekindled Shard";
                    if (Systems.TryGetValue(shard, out var ssys)) { ssys.StarColor = Galaxy.Blackbody(7200f); ssys.Name = shard.Name; }
                    Edited.Add(shard);
                    Log(home, "The Shard relights. For a moment it outshines everything in its quarter of the sky. Its light is the wrong colour, and it is beautiful.", "omen", null);
                    Milestone(home, "The Rekindled Star", $"Year {Year:0}. The dead star called the Shard burns again, and something in its light can be gathered.", "none");
                }
                break;
        }
        // the new tech goes straight into its slot's next hand
        if (t != null) civ.Slots[AreaOf(t.B)].Pinned ??= t.Id;
    }

    /// <summary>What this civ's council would rather do to open a gate.</summary>
    string PreferredKey(Civ civ, string g)
    {
        foreach (var k in TechData.P("gateEvents", g, "keys").EnumerateArray())
            if (k.TryGetProperty("ai", out var ai) && ai.GetString() == civ.Drive) return k.GetProperty("id").GetString();
        return null;
    }

    bool Perceives(Civ civ, string special) => special switch
    {
        "gate" => civ.Knows("SEN-9") || civ.Knows("ARC-7"),
        "vault" => civ.Knows("ARC-5") || civ.Knows("SEN-7"),
        "shard" => civ.Knows("GLY-2"),
        _ => false,
    };

    /// <summary>Within reach of one of the special places?</summary>
    StarInfo Reach(Civ civ, string special, float extra = 0)
    {
        float range = MaxRangeGu(civ) + extra;
        foreach (var s in Gal.Stars)
        {
            if (s.Special != special) continue;
            foreach (var c in civ.Colonies) if (c.Star.Pos.DistanceTo(s.Pos) <= range) return s;
        }
        return null;
    }

    void Perception(Civ civ)
    {
        if (civ.Knows("SEN-7") && !civ.Used.Contains("rumour-gate"))
        {
            civ.Used.Add("rumour-gate");
            if (civ.Main) Log(HomeColony, "Something at the rim bends the halo maps, the way a hand bends a sheet. Nobody can say where.", "omen", null);
        }
        foreach (var sp in new[] { "vault", "shard", "gate" })
        {
            if (!Perceives(civ, sp) || civ.Used.Contains("see-" + sp)) continue;
            civ.Used.Add("see-" + sp);
            bool first = Revealed.Add(sp);
            var stars = Gal.Stars.Where(s => s.Special == sp).ToList();
            if (stars.Count == 0) continue;
            string what = sp switch
            {
                "vault" => $"a system at {stars[0].Name} that every sensor slides off. Something is kept there, and guarded.",
                "shard" => $"a dead star, {stars[0].Name}, still warm in a way dead stars are not.",
                _ => $"four doors on the rim of the galaxy, sealed. {string.Join(", ", stars.Select(s => s.Name))}.",
            };
            if (civ.Main || first) Log(civ.Main ? HomeColony : civ.Colonies.First(), $"{(civ.Main ? "Humanity" : Narrative2.Called(civ, true))} finds {what}", "omen", null);
            if (first) { foreach (var s in stars) SpecialRevealed?.Invoke(s, sp); if (civ.Main || sp == "gate") Milestone(civ.Main ? HomeColony : HomeColony, sp == "gate" ? "The Sealed Gates" : sp == "vault" ? "The Vault" : "The Shard", $"Year {Year:0}. Someone has found {what}", "none"); }
        }
    }

    /// <summary>Yearly: perception, gate triggers, and attempts at their keys.</summary>
    void GateStep(Civ civ, double dy)
    {
        Perception(civ);
        var home = civ.Colonies.FirstOrDefault();
        if (home == null) return;
        bool Trig(string g, bool cond)
        {
            if (!cond || civ.Gates.ContainsKey(g)) return civ.Gates.GetValueOrDefault(g) == 1;
            civ.Gates[g] = 1; civ.GateP["at:" + g] = Year;
            if (civ.Main) Log(home, $"{GateName(g)}: {GateIntro(g)} The council looks for a way in.", "council", null);
            return true;
        }
        double Roll() => _r.NextDouble();
        int tier5 = civ.Techs.Count(id => Defs.TryTech(id)?.Tier == 5);

        // GE6 Umbral Breakthrough
        if (Trig("GE6", civ.Knows("UMB-1") && tier5 >= 4))
        {
            var teacher = Civs.Where(o => !o.Dead && o != civ && civ.Met.Contains(o.Id) && o.Knows("UMB-3") && civ.Relations.GetValueOrDefault(o.Id) >= 50).OrderByDescending(o => civ.Relations.GetValueOrDefault(o.Id)).FirstOrDefault();
            if (teacher != null && Roll() < 0.1 * dy)
            {
                string choice = teacher.Ethic switch { "Reverent" => Roll() < 0.5 ? "Charity" : "Teach", "Pragmatic" => Roll() < 0.6 ? "Trade" : "Refuse", _ => Roll() < 0.5 ? "Refuse" : "Insult" };
                if (choice is "Charity" or "Teach" or "Gift") OpenGate(civ, "GE6", $"{Narrative2.Called(teacher, true)} teach {Narrative2.Called(civ)} how to draw dark matter out of the halo.");
                else if (choice == "Trade" && home.Stock > 0.5 * home.PFrac) { home.Stock -= 0.5 * home.PFrac; OpenGate(civ, "GE6", $"{Narrative2.Called(teacher, true)} sell {Narrative2.Called(civ)} the secret of Umbral extraction, at a price."); }
                else if (choice == "Insult") { civ.Relations[teacher.Id] -= 10; teacher.Relations[civ.Id] = civ.Relations[teacher.Id]; if (civ.Main) Log(home, $"{Narrative2.Called(teacher, true)} answer our request with a lecture on our immaturity.", "event", null); }
            }
            var seller = Civs.FirstOrDefault(o => o.Ancient && civ.Met.Contains(o.Id) && civ.Relations.GetValueOrDefault(o.Id) > -20 && o.Knows("UMB-2"));
            if (seller != null && !civ.GateP.ContainsKey("GE6") && home.Stock > 2 * home.PFrac && Roll() < 0.05 * dy)
            {
                home.Stock -= 2 * home.PFrac;
                civ.GateP["GE6"] = 0.75;
                if (civ.Main) Log(home, $"{Narrative2.Called(seller, true)} sell us three-quarters of the answer. The rest, they say, we must earn.", "council", null);
            }
            // the independent project: a share of physics research
            if (PreferredKey(civ, "GE6") == "independent" || civ.GateP.ContainsKey("GE6") || Year - civ.GateT("GE6") > 40)
            {
                double need = 40 * Defs.TierCost[6] * Defs.CostScaleNonSpine;
                civ.GateP["GE6"] = civ.GateP.GetValueOrDefault("GE6") + civ.Colonies.Sum(c => c.RpRate) * 0.3 * dy / need;
                if (civ.GateP["GE6"] >= 1) OpenGate(civ, "GE6", "Years of work pay off: a way to pull dark matter out of the halo and hold it.");
            }
            if (civ.GateP.GetValueOrDefault("GE6:salvage") >= 3) OpenGate(civ, "GE6", "Salvage from three Umbral-armed wrecks finally gives up its secret.");
        }

        // GE7 Abyssal Drawing: opens on research alone; the ancients' demand comes when it is taken
        if (Trig("GE7", civ.Knows("UMB-4"))) OpenGate(civ, "GE7", "The Umbral work points at something deeper: the energy of the vacuum itself.");

        // GE7b Archive Uplink
        if (Trig("GE7b", civ.Knows("UMB-4") || civ.Knows("CHR-3")))
        {
            if (Civs.Any(o => o.Ancient && civ.Met.Contains(o.Id) && civ.Relations.GetValueOrDefault(o.Id) >= 30))
                OpenGate(civ, "GE7b", "The ancients give us an address, and a protocol, and a warning.");
            else if (home.VisitorOutcome is "gift" or "study")
                OpenGate(civ, "GE7b", "The visitor's notation, studied for a century, turns out to be an address.");
            else if (civ.Main && AnsweredAt >= 0)
                OpenGate(civ, "GE7b", "The signal we answered long ago answers back, with a way in.");
            else if (home.Stock > 1.5 * home.PFrac && Roll() < 0.05 * dy)
            {
                home.Stock -= 1.5 * home.PFrac;
                double r = Roll();
                if (r < 0.45) OpenGate(civ, "GE7b", "The council builds its own uplink from first principles. It connects.");
                else if (r < 0.9) { if (civ.Main) Log(home, "The home-built uplink connects to nothing. The council will try again.", "event", null); }
                else
                {
                    home.Pop *= 0.8; home.Capture *= 0.85;
                    Log(home, "The home-built uplink connects to something that is not the archive. It speaks for nine seconds. The city around the dish is gone.", "loss", null);
                }
            }
        }

        // GE8 The Vault
        if (Trig("GE8", civ.Knows("UMB-5") || civ.Knows("CHR-3")))
        {
            if (civ.GateP.GetValueOrDefault("GE8:wrecks") >= 1) OpenGate(civ, "GE8", "The wreck of a glyph-armed ship is full of writing. Some of it moves.");
            var vault = Reach(civ, "vault");
            if (vault != null && Perceives(civ, "vault") && Roll() < 0.025 * dy * civ.M.Fleet)
                OpenGate(civ, "GE8", $"An expedition reaches the Vault at {vault.Name} and comes back with a single tablet, and fewer ships.");
        }

        // GE9 Rekindled Star
        if (Trig("GE9", civ.Knows("GLY-2") && (civ.Knows("ARC-7") || civ.Knows("CHR-5"))))
        {
            if (civ.Knows("ARC-7")) OpenGate(civ, "GE9", "The archive tells us where the dead star is, and how to wake it.");
            else if (Reach(civ, "shard", 60 - MaxRangeGu(civ)) != null) OpenGate(civ, "GE9", "Our colonies near the Shard learn its pulse. It can be woken.");
        }

        // GE10 / GE10b Sealed Gates: trials at the rim
        bool sealsWeak = Year >= GateSealYear;
        if (Trig("GE10", civ.Knows("STL-3") && sealsWeak) && Perceives(civ, "gate") && Reach(civ, "gate") is { } g1 && Roll() < 0.08 * dy)
            Trial(civ, "GE10", g1);
        if (Trig("GE10b", civ.Knows("LUX-2") && civ.Gates.GetValueOrDefault("GE10") >= 2) && Reach(civ, "gate") is { } g2 && Roll() < 0.06 * dy)
        {
            Trial(civ, "GE10b:t", g2);
            if (civ.GateP.GetValueOrDefault("GE10b") >= 2) OpenGate(civ, "GE10b", $"The second and third trials at {g2.Name} are passed. The gate does not open; it lets them in.");
        }
    }
    public double GateSealYear = 2600;

    /// <summary>A trial at a rim gate: a fight with its guardian, or a riddle answered by the council's character.</summary>
    void Trial(Civ civ, string g, StarInfo gate)
    {
        var home = civ.Colonies.First();
        bool fight = civ.Drive == "Expansionist" || civ.Ethic == "Ruthless";
        double odds = fight ? Math.Clamp(WarStrength(civ) / (WarStrength(civ) + 4e4), 0.1, 0.9)
                            : (civ.Ethic == "Reverent" ? 0.55 : 0.35) * (civ.M.Acts.Contains("gate_riddle") ? 1.6 : 1);
        bool ok = _r.NextDouble() < odds;
        if (g == "GE10")
        {
            if (ok) { OpenGate(civ, "GE10", fight ? $"The guardian of {gate.Name} falls. Behind it the seal is thinner." : $"At {gate.Name} the council answers the gate's riddle. The seal thins."); return; }
        }
        else if (ok) { civ.GateP["GE10b"] = civ.GateP.GetValueOrDefault("GE10b") + 1; if (civ.Main) Log(home, $"A trial at {gate.Name} is passed.", "omen", null); return; }
        // a wrong answer still teaches something
        if (!fight)
        {
            var minor = Defs.Techs.Where(t => Available(civ, t) && t.Tier <= 6).OrderBy(_ => _r.Next()).FirstOrDefault();
            if (minor != null) CompleteTech(civ, minor, true);
            if (civ.Main) Log(home, $"The gate at {gate.Name} hears our answer and does not open. Something in the attempt teaches us {(minor != null ? minor.Name : "patience")}.", "event", null);
        }
        else
        {
            civ.Colonies.First().FrigateNames.Clear();
            if (civ.Main) Log(home, $"The guardian of {gate.Name} turns the fleet back. Few ships come home.", "loss", null);
        }
    }

    /// <summary>When a tech that crosses a gate is learned, the world reacts.</summary>
    void GateConsequence(Civ civ, Tech t)
    {
        if (t.Id == "UMB-5")
        {
            // the ancients want a sample of what was drawn
            foreach (var a in Civs.Where(a => a.Ancient && a.Met.Contains(civ.Id) && a != civ).ToList())
            {
                bool refuse = civ.Ethic == "Ruthless" || civ.Drive == "Expansionist" && _r.NextDouble() < 0.5;
                a.AddMeter("Elders", 10);
                if (refuse)
                {
                    Blame(a, civ, 40, civ.Colonies.First(), "demanded a sample of the abyss and were refused");
                }
                else
                {
                    civ.Pools["Abyssal"] = civ.Pools.GetValueOrDefault("Abyssal") * 0.5;
                    a.Relations[civ.Id] = a.Relations.GetValueOrDefault(civ.Id) + 10; civ.Relations[a.Id] = a.Relations[civ.Id];
                    if (civ.Main) Log(HomeColony, $"{Narrative2.Called(a, true)} demand a sample of what we drew from the abyss. The council sends half of it.", "council", null);
                }
            }
        }
    }
}
