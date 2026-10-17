using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

/// <summary>The chronicle's voice: decisions explained, stories, crises, natives, rivals and visitors.</summary>
public static class Narrative
{
    static string LossPct(double f) => (f * 100 < 1 ? (f * 100).ToString("0.0") : (f * 100).ToString("0")) + "%";

    public static void SpeakerSays(Colony c)
    {
        var civ = c.Civ;
        if (civ.Leader == null) return;
        string q = civ.Choose(Defs.Quotes[civ.Drive]) + civ.Choose(Defs.EthicColor[civ.Ethic]);
        c.Sim.Log(c, $"Speaker {civ.Leader.Name}: “{q}”", "council", null);
    }

    public static string SettleDecision(Colony c, Body b)
    {
        var e = c.Civ.Ethic;
        if (c.Home)
        {
            return b.Name switch
            {
                "Moon" => "Council decision: expand the lunar base into a permanent city at Shackleton crater. Ice from the dark craters, sunlight from the rim.",
                "Mars" => c.Settled.Count < 3 ? "Council decision: the first crewed landing on Mars. Six people, one ship, a nine-month road." : "Council decision: a second Martian settlement, in the shadow of Olympus Mons.",
                "Venus" => "Council decision: float cities fifty kilometres above Venus, where the air is Earth-thick and the temperature is mild. Below them is hell.",
                "Main Belt" => "Council decision: settle Ceres. It is the belt's harbour, and it is full of ice.",
                "Europa" => "Council decision: an outpost on Europa's ice. Somewhere beneath it is an ocean, and something might be in it.",
                "Titan" => "Council decision: settle Titan. Methane rain, orange skies, lakes you can sail on.",
                "Ganymede" => "Council decision: settle Ganymede, the largest moon in the system, under the shelter of its own magnetic field.",
                "Callisto" => "Council decision: settle Callisto, far enough from Jupiter's radiation to raise children.",
                "Jupiter" or "Saturn" => $"Council decision: float cities in the upper atmosphere of {b.Name}. The habitats are crowded; the clouds are not.",
                _ => $"Council decision: settle {b.Name}. The habitats are crowded, and some people want a sky again, even a borrowed one."
            };
        }
        if (b.Life && e == "Reverent") return $"Council decision: settle {b.Name} beneath domes. Its biosphere will be left exactly as it is.";
        if (b.Kind is BodyKind.Gas or BodyKind.IceGiant) return $"Council decision: float cities in the upper atmosphere of {b.Name}.";
        return $"Council decision: settle {b.Name}. The habitats are crowded, and some of them want a sky again, even a borrowed one.";
    }

    public static string Landed(Colony c, Body b) => b.Name switch
    {
        "Mars" when c.Settled.Count(kv => kv.Key.Name == "Mars") <= 1 => "Humans walk on Mars. The first words spoken there are lost under the static; the second are a child's name, read out from home.",
        "Moon" => "Shackleton City opens. Its lights are visible from Earth with a good pair of binoculars, and people go outside to look.",
        "Venus" => "The first aerostat city drifts into the Venusian dawn. From its deck, the clouds go on forever.",
        "Main Belt" => "Ceres station is crewed. The belt now has a harbour, a hospital and a bar.",
        "Europa" => "A drill crew lands on Europa. Twenty kilometres down, the ocean waits.",
        "Titan" => "The first settlers reach Titan and step out under an orange sky. The gravity is gentle; the cold is not.",
        _ => $"Settlers reach {b.Name}. The first lights come on there."
    };

    public static string HubDecision(Colony c, Body b, int lvl)
    {
        if (lvl > 0) return $"Council decision: deepen the works on {b.Name} to level {lvl + 1}.";
        if (c.Home)
        {
            switch (b.Name)
            {
                case "Moon": return "Council decision: open lunar regolith mines. The Moon will build what Earth cannot lift.";
                case "Main Belt": return "Council decision: open the belt. Ten thousand prospector drones fan out from Ceres.";
                case "Mercury": return "Council decision: begin the disassembly of Mercury. It is close to the Sun, rich in metal, and nobody lives there. It will become the swarm.";
                case "Jupiter": case "Saturn": return $"Council decision: skimmer fleets for {b.Name}. Its upper atmosphere will be worked for centuries.";
                case "Venus": return "Council decision: Venus will be mined. The aerostat cities are evacuated first. Mostly.";
                case "Mars": return "Council decision: Mars will be mined. The vote was close. Some of the people who voted yes were born there.";
            }
        }
        string why = "Best yield within reach.";
        if (b.Life && c.Civ.Ethic == "Ruthless") why = "The native microbes were not discussed.";
        else if (b.Life && c.Civ.Ethic == "Pragmatic") why = "Its biosphere is regrettable. The vote was 6-3.";
        else if (b.Life) why = "Nothing else is left. The vote took eleven days.";
        else if (b.Kind is BodyKind.Gas or BodyKind.IceGiant) why = "Skimmers will work its upper atmosphere for centuries.";
        return $"Council decision: open {b.Name}. {why}";
    }

    public static string HubOpened(Colony c, Body b) => b.Name switch
    {
        "Moon" => "The lunar mass driver fires for the first time. A bucket of grey dust leaves the Moon at two kilometres a second and does not come back.",
        "Mercury" => "The first disassembly swarm lands on Mercury. By nightfall there is a scar on it visible from Earth.",
        "Main Belt" => "Ore freighters begin the long fall inward from the belt.",
        _ => $"The works on {b.Name} are running."
    };

    public static string ElevatorDone(Colony c) => "The space elevator is finished. A climber leaves the ground station off Ecuador and reaches orbit five days later, carrying tourists, seeds, and a piano.";
    public static string RingDone(Colony c) => "The orbital ring closes. Earth now wears a thin bright line around its waist, visible from every equatorial city by day and by night.";

    // ---------- Random events ----------
    static readonly (string Id, Func<Colony, bool> Ok, Func<Colony, string> Text)[] Stories =
    {
        ("kessler", c => c.Home && c.Year() < 2120, c => "A satellite collision cascade shreds low Earth orbit for a month. Launches stop. When they resume, every new satellite carries a sweeper."),
        ("mars_child", c => c.Home && c.Settled.Keys.Any(b => b.Name == "Mars"), c => "The first child is born on Mars. Her bones will never be strong enough for Earth. She will never need them to be."),
        ("lunar_tourism", c => c.Home && c.Settled.Keys.Any(b => b.Name == "Moon"), c => "A wedding is held at Shackleton City. The couple float down the aisle, slowly, in one-sixth gravity."),
        ("independence", c => c.Home && c.Settled.Keys.Any(b => b.Name == "Mars") && c.Year() > 2110, c => "Mars votes on independence. It loses by a single seat. The losing side throws a party anyway."),
        ("ai_rights", c => c.Home && c.Civ.Techs.Contains("S3"), c => "An AI files for citizenship. The case takes nine years. It wins."),
        ("climate", c => c.Home && c.Year() < 2150, c => "The last summer the Arctic freezes over. Orbital mirrors are proposed, debated, and quietly built."),
        ("hearth", c => c.Capture > 1e-3 && c.Civ.Ethic != "Ruthless", c => "A religion forms around the Sun. Its followers call it the Hearth, and ask its forgiveness each time a collector launches."),
        ("accident", c => c.Hubs.Any(h => h.Value > 0), c => $"A slope failure in the open pits on {c.Civ.Choose(c.Hubs.Where(h => h.Value > 0).Select(h => h.Key.Name).ToList())} kills four thousand workers. The works do not stop."),
        ("nostalgia", c => !c.Home && c.Sim.Year - c.Founded > 60, c => "The first generation that never saw the homeworld comes of age. They do not miss it. Their parents find this unbearable."),
        ("painters", c => c.Capture > 0.05, c => "Painters begin depicting the star as it looked before the swarm. It becomes the most popular subject in the system."),
        ("mad", c => c.Civ.Digital > 0.4, c => "An uploaded mind degrades in the archive and has to be quarantined. It keeps asking what year it is."),
        ("merger", c => c.Civ.Drive == "Contemplative", c => "Gravitational-wave arrays catch two black holes merging a billion years ago. The Choristers hold a vigil that lasts a month."),
        ("shard", c => c.Habitats > 0, c => "A collector shard punctures a habitat. Nine hundred dead. The shielding is doubled, then doubled again."),
        ("child", c => c.Capture > 0.02, c => "A child born in the swarm asks what the star looked like before. Nobody has a good answer."),
        ("lullaby", c => c.Sim.Year - c.Founded > 50, c => "A lullaby about the old sky spreads through the system. Nobody knows who wrote it."),
        ("census", c => c.Pop > 12, c => $"The census passes {c.Pop:0.0} billion. Most of them have never been more than a light-minute from where they were born."),
        ("monument", c => c.Sys.All().Any(b => b.Mined >= 1), c => "A monument is raised to the worlds that were consumed. It is made from what is left of them."),
        ("voyager", c => c.Home && c.Civ.Techs.Contains("X2"), c => "A fusion yacht overtakes Voyager 1, still drifting outward. They do not touch it. They leave a light on beside it."),
        ("earthrise", c => c.Home && c.Year() < 2100, c => "A new photograph of Earth from the far side of the Moon becomes the most shared image in history. It looks very small."),
        ("pale_dot", c => c.Home && c.Civ.Techs.Contains("X3"), c => "A lightsail probe looks back from the edge of the system. Earth is a pale blue pixel. The Sun, now, is not quite as bright as it was."),
    };

    static double Year(this Colony c) => c.Sim.Year;

    static readonly Func<Colony, string>[] Events =
    {
        c => c.Capture > 1e-4 ? Flare(c, 0.04) : null,
        c => c.Civ.R.NextDouble() < 0.5 * c.Civ.M.Comets ? Comet(c) : null,
        c => c.Boost < 2.5f ? Iterate(c) : null,
        c => c.Capture > 1e-3 && c.Civ.M.Shield == 1 ? Collision(c) : null,
        c => c.Home && c.Civ.Used.Contains("signal") == false && c.Sim.Year > 2100 ? Signal(c) : null,
    };

    static string Flare(Colony c, double f) { c.Capture *= 1 - f * c.Civ.M.Shield; c.Sim.Fx(c, "flare", null); return $"A stellar flare scoured the inner swarm. {LossPct(f * c.Civ.M.Shield)} of collectors lost."; }
    static string Comet(Colony c) { c.Stock += 0.3 * c.PFrac; c.Sim.Fx(c, "comet", null); return "Captured a long-period comet. Ice and carbon added to the stockpile."; }
    static string Iterate(Colony c) { c.Boost *= 1.06f; return $"Replicator design iteration {2 + (int)((c.Sim.Year - c.Founded) / 9)} adopted. All industry +6%."; }
    static string Collision(Colony c) { c.Capture *= 0.98; return "Collision cascade in a crowded orbital shell. 2% of collectors lost."; }
    static string Signal(Colony c)
    {
        c.Civ.Used.Add("signal");
        bool answer = c.Civ.Drive == "Expansionist" || c.Civ.R.NextDouble() < 0.3;
        if (answer) { c.Sim.AnsweredAt = c.Sim.Year; c.Civ.ShiftInf("Expansionist", 0.04f); }
        return "Long-range scan: a repeating narrowband signal, 41 light-years out. " + (answer ? "The council votes to answer it." : "The council votes to stay silent and dark.");
    }

    public static void RandomEvent(Colony c)
    {
        var civ = c.Civ;
        if (c.Star.Cls.StartsWith("M") && c.Capture > 1e-4 && civ.R.NextDouble() < 0.25)
        {
            c.Capture *= 1 - 0.05 * civ.M.Shield; civ.ShiftInf("Industrious", 0.02f);
            c.Sim.Fx(c, "flare", null);
            c.Sim.Log(c, $"{c.Sys.Name} is a flare star. Another eruption takes {LossPct(0.05 * civ.M.Shield)} of the inner swarm.", "event", null);
            return;
        }
        if (c.Crisis == null && civ.R.NextDouble() < 0.16 && StartCrisis(c)) return;
        if (civ.R.NextDouble() < 0.55)
        {
            var open = Stories.Where(s => !civ.Used.Contains(s.Id) && s.Ok(c)).ToList();
            if (open.Count > 0)
            {
                var s = civ.Choose(open);
                civ.Used.Add(s.Id);
                c.Sim.Log(c, s.Text(c), "event", null);
                return;
            }
        }
        for (int t = 0; t < 8; t++)
        {
            var msg = civ.Choose(Events)(c);
            if (msg != null) { c.Sim.Log(c, msg, "event", null); return; }
        }
    }

    // ---------- Crises ----------
    record CrisisDef(Func<Colony, bool> Ok, double Len, Func<Colony, Crisis, string> Start, Action<Colony, Crisis, double> Tick, Func<Colony, string> End, string Fix, Action<Colony> OnFix = null, Action<Colony> OnEnd = null);

    static readonly Dictionary<string, CrisisDef> Crises = new()
    {
        ["runaway"] = new(c => c.Civ.Techs.Contains("I3") && c.Sim.Year - c.Founded > 40, 12,
            (c, k) =>
            {
                var t = c.Sys.All().Where(b => b.Kind != BodyKind.Gas && b.Kind != BodyKind.IceGiant && b.Kind != BodyKind.Belt && b.Mined < 0.9f && !c.Settled.ContainsKey(b) && b != c.Seat).ToList();
                if (t.Count == 0) return null;
                k.Target = c.Civ.Choose(t);
                return $"A replicator lineage mutates and stops taking orders. It is eating {k.Target.Name}, and keeping what it eats.";
            },
            (c, k, dy) => { k.Target.Mined = Math.Min(1, k.Target.Mined + (float)(dy * 0.02)); },
            c => c.Civ.Drive == "Industrious" || c.Civ.M.Shield < 1 ? "The Forgewrights seed a counter-swarm. The runaway lineage is hunted down to the last molecule." : "The runaway lineage burns itself out. What it ate is gone for good.",
            "The runaway replicators all stop at once, mid-bite. Nobody can find out why."),
        ["secession"] = new(c => c.Civ.Digital >= 0.5, 20,
            (c, k) => { c.Capture *= 0.9; c.Pop *= 0.88; return "Uploaded minds declare a polity of their own and take a tenth of the swarm's compute with them. They call themselves the Quiet."; },
            (c, k, dy) => { },
            c => c.Civ.Drive == "Contemplative" ? "The Choristers talk the Quiet back. The swarm is whole again; the Quiet keep their name." : "The Quiet build a seed of their own and leave without permission. Nobody is sure what they took.",
            "Every one of the Quiet wakes up at once with the same memory of a voice. They rejoin before lunch.",
            c => { c.Capture = Math.Min(1, c.Capture / 0.9); c.Pop /= 0.88; },
            c => { if (c.Civ.Drive == "Contemplative") c.Capture = Math.Min(1, c.Capture / 0.92); }),
        ["storms"] = new(c => c.Civ.M.StarLift && c.Capture > 0.01, 10,
            (c, k) => "Stellar lifting has destabilised the star. Flare storms begin, and will not stop for a decade.",
            (c, k, dy) => { k.Acc += dy; if (k.Acc > 2) { k.Acc = 0; c.Capture *= 1 - 0.02 * c.Civ.M.Shield; c.Sim.Fx(c, "flare", null); } },
            c => "The star settles. The flare storms are over, and the lifting funnels are turned down by a third.",
            "The storms end between one flare and the next, as if the star had been told to stop."),
        ["integration"] = new(c => false, 8,
            (c, k) => { c.Pop *= 0.96; return "Half the population has chrome in its skull and half refuses it. The augmented stop being invited to dinner; then they stop being invited to vote."; },
            (c, k, dy) => { c.Pop *= 1 - 0.002 * dy; },
            c => "The integration riots end with a compromise nobody likes: every body is its owner's business, and nobody else's.",
            "Everyone wakes up having dreamed the other side's argument, word for word. The riots stop by noon."),
        ["plague"] = new(c => c.Home && c.Settled.Count > 3, 6,
            (c, k) => { c.Pop *= 0.97; return "An engineered pathogen escapes a lab on Ceres. Every port in the system closes."; },
            (c, k, dy) => { c.Pop *= 1 - 0.004 * dy; },
            c => "The plague burns out. Ports reopen. Every habitat now keeps its own airlock vote.",
            "Every sample of the pathogen dies in the same hour. The doctors call it a miracle and are not joking."),
        ["war"] = new(c => false, 10,
            (c, k) => $"The {c.Rival.Name} refuse to stand down. Fighting breaks out across the system.",
            (c, k, dy) =>
            {
                k.Acc += dy;
                if (k.Acc > 2)
                {
                    k.Acc = 0;
                    if (c.Civ.R.NextDouble() < 0.5 && c.FrigateNames.Count > 0) { var n = c.FrigateNames[^1]; c.FrigateNames.RemoveAt(c.FrigateNames.Count - 1); c.Sim.Log(c, $"The frigate {n} is lost with all hands.", "loss", null); }
                    else if (c.Rival.Ships > 0) c.Rival.Ships--;
                }
            },
            c =>
            {
                bool won = c.Rival.Ships <= 3;
                string msg = won ? $"The {c.Rival.Name} are destroyed. Their world is ours again. Nobody celebrates." : $"Neither side can win. The {c.Rival.Name} withdraw beyond the outer planets with what is left of their fleet.";
                c.Rival = null;
                return msg;
            },
            "Every gun in the system goes quiet at the same instant. Both sides hear the same voice. The war is over.",
            c => { if (c.Rival != null) { c.Pop += c.Rival.Pop * 0.5; c.Rival = null; } }),
    };

    public static bool StartCrisis(Colony c, string forced = null)
    {
        var open = Crises.Where(kv => (forced == null ? !c.Civ.Used.Contains("crisis-" + kv.Key) && kv.Value.Ok(c) : kv.Key == forced)).Select(kv => kv.Key).ToList();
        if (open.Count == 0) return false;
        string id = c.Civ.Choose(open);
        var k = new Crisis { Id = id, T0 = c.Sim.Year };
        var msg = Crises[id].Start(c, k);
        if (msg == null) return false;
        c.Civ.Used.Add("crisis-" + id);
        c.Crisis = k;
        c.Sim.Log(c, (id == "war" ? "War. " : "Crisis. ") + msg, "loss", k.Target);
        if (c.Civ.Main) c.Sim.Raise("Crisis", $"Crisis in {c.Sys.Name}", msg, c);
        return true;
    }

    public static void CrisisStep(Colony c, double dy)
    {
        var k = c.Crisis;
        if (k == null) return;
        var d = Crises[k.Id];
        d.Tick(c, k, dy);
        if (c.Sim.Year - k.T0 >= d.Len)
        {
            c.Crisis = null;
            d.OnEnd?.Invoke(c);
            c.Sim.Log(c, d.End(c), "era", k.Target);
        }
    }

    /// <summary>A crisis simply stops (frozen in stasis): no fix, no consequences.</summary>
    public static void EndCrisisQuietly(Colony c) { c.Crisis = null; }

    /// <summary>The voice decides the crisis ends as badly as it can.</summary>
    public static void RuinCrisis(Colony c)
    {
        var k = c.Crisis;
        if (k == null) return;
        var d = Crises[k.Id];
        d.Tick(c, k, d.Len * 0.5);          // the rest of it happens at once
        c.Crisis = null;
        d.OnEnd?.Invoke(c);
        c.Pop *= 0.9; c.Capture *= 0.95;
        c.Sim.NudgeConscience(c.Civ, -0.04f);
        c.Sim.Log(c, "It ends the worst way it could have. Afterwards everyone agrees it was always going to.", "god", k.Target);
    }

    public static void FixCrisis(Colony c)
    {
        var k = c.Crisis;
        if (k == null) return;
        var d = Crises[k.Id];
        c.Crisis = null;
        d.OnFix?.Invoke(c);
        c.Sim.NudgeConscience(c.Civ, 0.04f);
        c.Sim.Log(c, d.Fix, "god", k.Target);
    }

    // ---------- Splinters return ----------
    public static void RivalStep(Colony c)
    {
        var sp = c.Splinter;
        if (sp is { Returned: false } s && c.Sim.Year > s.Back)
        {
            c.Splinter = s with { Returned = true };
            if (c.Civ.R.NextDouble() < 0.4) return;
            var options = c.Sys.All().Where(b => b != c.Seat && !c.Settled.ContainsKey(b) && b.Mined < 0.6f && b.Kind != BodyKind.Belt).ToList();
            if (options.Count == 0) return;
            var target = options.OrderBy(b => -b.WorldPos(0).Length()).First();
            c.Rival = new Rival { Name = s.Faction + " of the Long Night", Pop = s.Pop * 1.6, Ships = 6 + c.Civ.R.Next(5), Planet = target, T0 = c.Sim.Year };
            c.Sim.Log(c, $"A fleet folds in beside {target.Name}. It is the {s.Faction} who left {(int)(c.Sim.Year - s.At)} years ago. They call themselves the {c.Rival.Name} now.", "omen", target);
            return;
        }
        var rv = c.Rival;
        if (rv == null || rv.Stance != null || c.Sim.Year < rv.T0 + 3) return;
        if (c.Civ.Ethic == "Reverent")
        {
            rv.Stance = "reunion";
            c.Pop += rv.Pop;
            c.Sim.Log(c, $"The council meets the {rv.Name} without weapons. After three years of talks, they come home.", "era", null);
            c.Rival = null;
        }
        else if (c.Civ.Ethic == "Pragmatic")
        {
            rv.Stance = "partition";
            c.Sim.Log(c, $"The council and the {rv.Name} sign a partition. {rv.Planet.Name} is theirs now, and nobody else's.", "council", rv.Planet);
        }
        else
        {
            rv.Stance = "war";
            StartCrisis(c, "war");
        }
    }

    // ---------- Natives ----------
    static readonly string[] NativeNames = { "Tessic", "Ouma", "Vreth", "Ilari", "Kesh-of-the-Shallows", "Moru", "Saan", "Tidewalkers", "Hollin" };
    static readonly string[] NativeAges = { "Stone age", "Bronze age", "Industrial", "Atomic", "Spacefaring" };

    public static void SeedNatives(Colony c)
    {
        foreach (var b in c.Sys.All())
            if (b.Life && c.Civ.R.NextDouble() < 0.55)
                c.Natives[b] = new Native { Name = c.Civ.Choose(NativeNames), Level = c.Civ.R.NextDouble() < 0.5 ? 0 : 1 };
    }

    public static void NativesStep(Colony c, double dy)
    {
        var civ = c.Civ;
        foreach (var (b, n) in c.Natives)
        {
            if (n.Contact == "cleared") continue;
            if (n.Contact == null && c.Sim.Year - c.Founded > 6)
            {
                string k = n.Protected || civ.Ethic == "Reverent" ? "quarantine" : civ.Ethic == "Pragmatic" ? (civ.R.NextDouble() < 0.6 ? "uplift" : "quarantine") : (civ.R.NextDouble() < 0.7 ? "clearance" : "uplift");
                n.Contact = k;
                if (k == "quarantine") c.Sim.Log(c, $"{b.Name} is inhabited. The {n.Name} are {NativeAges[n.Level].ToLower()}, and do not know we are here. The council declares their world closed. Nobody lands; nobody speaks.", "omen", b);
                else if (k == "uplift") { n.Level = 3; c.Pop += 1.2; c.Boost *= 1.08f; civ.ShiftInf("Contemplative", 0.05f); c.Sim.Log(c, $"{b.Name} is inhabited. The council chooses to make contact and uplift the {n.Name}. Within a generation they hold a seat on the council.", "omen", b); }
                else { n.ClearAt = c.Sim.Year + 4; c.Sim.Log(c, $"{b.Name} is inhabited by the {n.Name}. The council votes for clearance. The world is wanted for its mass.", "omen", b); }
                continue;
            }
            if (n.Contact == "clearance" && c.Sim.Year >= n.ClearAt)
            {
                if (n.Protected) { n.Contact = "quarantine"; continue; }
                n.Contact = "cleared";
                c.Sim.NudgeConscience(civ, -0.3f);
                c.Sim.Log(c, $"Clearance of {b.Name} is complete. The council does not record the number. The {n.Name} are now only a word in the chronicle.", "loss", b);
                continue;
            }
            if (n.Contact == "quarantine" && n.Level < 4)
            {
                n.T += dy;
                if (n.T > 60 + civ.R.NextDouble() * 40)
                {
                    n.T = 0; n.Level++;
                    string msg = n.Level switch
                    {
                        1 => $"The {n.Name} have learned to work bronze. Their first cities ring the shallow seas.",
                        2 => $"The {n.Name} have an industrial revolution. Their night side begins to glow.",
                        3 => $"The {n.Name} have split the atom. The council watches, and waits.",
                        _ => $"The {n.Name} have reached orbit. Their first telescope is pointed at the dimming star. Their first broadcast asks it to stop going dark."
                    };
                    c.Sim.Log(c, msg, n.Level == 4 ? "omen" : "event", b);
                    if (n.Level == 4) civ.ShiftInf("Contemplative", 0.08f);
                }
            }
        }
    }

    // ---------- First contact (a visitor drawn by the dimming) ----------
    public static void VisitorStep(Colony c)
    {
        var sim = c.Sim;
        if (!c.Home) return;
        if (!c.VisitorDone && c.VisitorT < 0)
        {
            bool called = sim.AnsweredAt >= 0 && sim.Year > sim.AnsweredAt + 60;
            bool noticed = c.Capture > 0.3 && c.Civ.R.NextDouble() < 0.004;
            if (called || noticed)
            {
                c.VisitorT = sim.Year; c.VisitorStage = 0;
                sim.Log(c, called ? "A fold signature at the edge of the system, on the bearing of the signal they answered. Something has arrived. It is not ours."
                    : "A fold signature at the edge of the system. The dimming of the Sun has been noticed. Something has arrived. It is not ours.", "omen", "visitor");
            }
            return;
        }
        if (c.VisitorT < 0) return;
        double age = sim.Year - c.VisitorT;
        var civ = c.Civ;
        if (c.VisitorStage == 0 && age > 3)
        {
            c.VisitorStage = 1;
            string react = civ.Drive switch
            {
                "Expansionist" => "The Wayfarer council sends a delegation in an unarmed frigate. It is allowed to approach.",
                "Contemplative" => "The Choristers point every instrument they own at it and listen.",
                _ => civ.Ethic == "Ruthless" ? "The Forgewrights re-tool three foundries for weapons." : "The Forgewrights keep building, and keep watching."
            };
            sim.Log(c, react, "omen", "visitor");
            if (civ.Drive == "Industrious" && civ.Ethic == "Ruthless") c.Stock *= 0.8;
        }
        else if (c.VisitorStage == 1 && age > 15)
        {
            c.VisitorStage = 2;
            double r = civ.R.NextDouble();
            string o = civ.Drive == "Industrious" && civ.Ethic == "Ruthless" ? "fire"
                : civ.Drive == "Expansionist" ? (r < 0.45 ? "gift" : r < 0.8 ? "silent" : "take")
                : civ.Drive == "Contemplative" ? (r < 0.5 ? "study" : r < 0.8 ? "silent" : "take")
                : (r < 0.55 ? "silent" : r < 0.8 ? "take" : "gift");
            if (o == "take" && civ.M.Deterrent) o = "silent";
            if (c.VisitorForced != null) o = c.VisitorForced;
            c.VisitorOutcome = o;
            switch (o)
            {
                case "gift":
                    var t = Defs.Techs.Where(x => sim.Available(civ, x)).OrderByDescending(x => x.Tier).FirstOrDefault();
                    if (t != null) sim.CompleteTech(civ, t, true);
                    sim.Log(c, $"The delegation returns with a schematic, drawn in our own notation.{(t != null ? " It is " + t.Name + ". Nobody can say how it knew." : "")}", "omen", null);
                    break;
                case "study": c.Boost *= 1.15f; sim.Log(c, "Fifteen years of listening. The Choristers learn very little about the visitor, and a great deal about physics. Industry +15%.", "omen", "visitor"); break;
                case "take": c.Capture *= 0.85; sim.NudgeConscience(civ, 0.05f); sim.Log(c, "Fifteen percent of the swarm is simply gone one morning. No debris. No heat. The visitor has not moved.", "omen", null); break;
                case "fire": c.HomeDark = sim.Year + 20; sim.NudgeConscience(civ, -0.1f); sim.Log(c, "The fleet fires on the visitor. It does not fire back. It turns, slowly, toward the nearest star we have settled.", "omen", "visitor"); break;
                default: sim.Log(c, "It does nothing. It watches. Every attempt at contact is ignored. Children grow up under it.", "omen", "visitor"); break;
            }
        }
        else if (c.VisitorStage == 2 && age > (c.VisitorOutcome == "fire" ? 22 : 32))
        {
            c.VisitorStage = 3;
            sim.Log(c, "The visitor leaves the way it came. Nobody sleeps well for a decade.", "omen", "visitor");
        }
        else if (c.VisitorStage == 3 && age > 36) { c.VisitorT = -1; c.VisitorDone = true; }
        if (c.HomeDark > 0 && sim.Year > c.HomeDark)
        {
            c.HomeDark = -1;
            var lost = civ.Colonies.Where(x => !x.Home).OrderBy(x => x.Star.Pos.DistanceTo(c.Star.Pos)).FirstOrDefault();
            if (lost != null) sim.DestroyColony(lost, "The light from " + lost.Sys.Name + " stops arriving. Not dimmed. Stopped. The council orders every transmitter silent.");
            else sim.Log(c, "Something passes between the Sun and Alpha Centauri, and Alpha Centauri goes out. The council orders every transmitter silent.", "omen", null);
            sim.NudgeConscience(civ, 0.15f); civ.ShiftInf("Contemplative", 0.15f);
        }
    }
}
