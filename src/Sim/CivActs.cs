using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Starforge.Sim;

/// <summary>
/// Mortals reaching for the voice's tools (docs/TECH_INTEGRATION.md §9). act: effects unlock council actions; every few years a
/// council considers what it has unlocked and does one. Acts that the voice can also do run through the same core, so the
/// chronicle and the ripples are the same, but Faith and Dread only move when the voice was the actor.
/// </summary>
public partial class Simulation
{
    record Act(Func<Civ, bool> Can, Func<Civ, bool> Do, double Weight = 1, bool Atrocity = false);

    Dictionary<string, Act> _acts;
    Dictionary<string, Act> Acts => _acts ??= BuildActs();

    void ActStep(Civ civ, double dy)
    {
        if (civ.M.Acts.Count == 0 || civ.Colonies.Count == 0) return;
        // a council acts every eight years or so, more often at war
        if (_r.NextDouble() > dy * (civ.AtWar.Count > 0 ? 0.25 : 0.12)) return;
        var options = new List<(string Id, Act A, double W)>();
        foreach (var id in civ.M.Acts)
        {
            if (!Acts.TryGetValue(id, out var a)) continue;
            if (Year < civ.ActAt.GetValueOrDefault(id)) continue;
            if (a.Atrocity && !AtrocityAllowed(civ)) continue;
            if (!a.Can(civ)) continue;
            options.Add((id, a, a.Weight));
        }
        if (options.Count == 0) return;
        double sum = options.Sum(o => o.W), r = _r.NextDouble() * sum;
        foreach (var o in options)
        {
            r -= o.W;
            if (r > 0) continue;
            if (o.A.Do(civ)) civ.ActAt[o.Id] = Year + 15 + _r.Next(25);
            else civ.ActAt[o.Id] = Year + 5;
            return;
        }
    }

    // ---------- helpers ----------
    Colony Home(Civ c) => c.Colonies.FirstOrDefault(x => x.Home) ?? c.Colonies.FirstOrDefault();
    Colony RandomColony(Civ c) => c.Colonies.Count == 0 ? null : c.Colonies[_r.Next(c.Colonies.Count)];
    IEnumerable<Civ> Enemies(Civ c) => c.AtWar.Where(id => id < Civs.Count).Select(id => Civs[id]).Where(o => !o.Dead && o.Colonies.Count > 0);
    IEnumerable<Civ> Known(Civ c) => c.Met.Where(id => id < Civs.Count).Select(id => Civs[id]).Where(o => !o.Dead && o.Colonies.Count > 0);
    Colony EnemyFront(Civ c)
    {
        Colony best = null; float bd = float.MaxValue;
        foreach (var e in Enemies(c))
            foreach (var ec in e.Colonies.Where(x => !x.Home).DefaultIfEmpty(e.Colonies[0]))
                foreach (var mc in c.Colonies.Take(40))
                {
                    float d = mc.Star.Pos.DistanceTo(ec.Star.Pos);
                    if (d < bd) { bd = d; best = ec; }
                }
        return best;
    }
    bool Chronicled(Civ c, Colony at = null) => c.Main || at != null && at == Focus || at?.Civ.Main == true;
    void Say(Civ c, Colony at, string text, string kind = "event") { if (Chronicled(c, at) || kind is "era" or "loss" or "omen") Log(at ?? Home(c), text, kind, null); }
    static string Cap(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];
    string N(Civ c) => Narrative2.Called(c);

    Dictionary<string, Act> BuildActs()
    {
        var d = new Dictionary<string, Act>();

        // ---- the voice's own tools, in mortal hands ----
        d["ignite"] = new(c => c.Colonies.Any(x => x.Sys.Bodies.Any(Ignitable)), c =>
        {
            var col = c.Colonies.Where(x => x.Sys.Bodies.Any(Ignitable)).OrderByDescending(x => x.Home).First();
            return Ignite(col, col.Sys, col.Sys.Bodies.First(Ignitable), c);
        }, 1.5);
        d["shift_orbit"] = new(c => Home(c) is { } h && h.Sys.Bodies.Any(b => b != h.Seat && Movable(h.Sys, b) && b.Kind is BodyKind.Ice or BodyKind.Rocky or BodyKind.Desert), c =>
        {
            var h = Home(c);
            var b = h.Sys.Bodies.First(b => b != h.Seat && Movable(h.Sys, b) && b.Kind is BodyKind.Ice or BodyKind.Rocky or BodyKind.Desert);
            return ShiftOrbit(h, h.Sys, b, c);
        });
        Func<Civ, bool> canSeed = c => c.Colonies.Any(x => x.Sys.All().Any(Seedable));
        Func<Civ, bool> doSeed = c =>
        {
            var col = c.Colonies.Where(x => x.Sys.All().Any(Seedable)).OrderByDescending(x => x.Home ? 2 : x.Settled.Count).First();
            return SeedLife(col, col.Sys, col.Sys.All().First(Seedable), c);
        };
        d["seed_life"] = new(canSeed, doSeed, 1.2);
        d["garden"] = new(canSeed, c => { bool ok = doSeed(c); if (ok) foreach (var x in c.Colonies.Take(5)) x.PopCap *= 1.05; return ok; });
        d["gaia"] = new(canSeed, c => { bool ok = doSeed(c); if (ok) { var h = Home(c); h.PopCap *= 1.15; h.Boost *= 1.05f; Say(c, h, $"{Cap(N(c))} weave every living world they hold into one balanced biosphere. They call it Gaia, and mean it.", "era"); } return ok; });
        d["living_world"] = new(canSeed, c => { bool ok = doSeed(c); if (ok) Home(c).PopCap *= 1.1; return ok; });
        d["awaken_world"] = new(c => c.Colonies.Any(x => x.Sys.All().Any(b => b.Life)), c =>
        {
            var col = c.Colonies.First(x => x.Sys.All().Any(b => b.Life));
            var b = col.Sys.All().First(b => b.Life);
            col.Boost *= 1.1f;
            Say(c, col, $"{Cap(N(c))} wake the living world {b.Name}. Its forests turn toward their ships the way flowers turn to the sun. It is an ally now.", "era");
            return true;
        }, 0.6);
        d["restore"] = new(c => c.Colonies.Any(x => x.Sys.All().Any(b => b.Mined > 0.5f && b.Mined < 1f && b.Kind != BodyKind.Belt)), c =>
        {
            var col = c.Colonies.First(x => x.Sys.All().Any(b => b.Mined > 0.5f && b.Mined < 1f && b.Kind != BodyKind.Belt));
            var b = col.Sys.All().First(b => b.Mined > 0.5f && b.Mined < 1f && b.Kind != BodyKind.Belt);
            b.Mined = 0.1f; col.Hubs.Remove(b);
            Say(c, col, $"{Cap(N(c))} put {b.Name} back together from its own rubble. It is smaller than it was, and it is whole.", "era");
            return true;
        }, 0.5);
        d["terraform_any"] = new(c => c.Colonies.Any(x => x.Settled.Keys.Any(b => !b.Life && Seedable(b))), c =>
        {
            var col = c.Colonies.First(x => x.Settled.Keys.Any(b => !b.Life && Seedable(b)));
            var b = col.Settled.Keys.First(b => !b.Life && Seedable(b));
            b.Greening = Math.Max(b.Greening, 0.3f); if (!Thawing.Contains(b)) Thawing.Add(b);
            Say(c, col, $"{Cap(N(c))} begin terraforming {b.Name}. It will take {90 / c.M.Terra:0} years.", "council");
            return true;
        });
        d["seed_life_far"] = new(c => Home(c) != null, c =>
        {
            var h = Home(c);
            var s = h.Star.Near.Select(id => Gal.Stars[id]).FirstOrDefault(x => x.Owner < 0 && !x.Dead && x.Special == null);
            if (s == null) return false;
            var sys = SystemFor(s);
            var b = sys.All().FirstOrDefault(Seedable);
            if (b == null) return false;
            b.Life = true; b.Greening = 1;
            Say(c, h, $"{Cap(N(c))} send panspermia engines to {s.Name}. Within a century {b.Name} is green, and nobody lives there yet.", "era");
            return true;
        }, 0.5);
        d["uplift"] = new(c => c.Colonies.Any(x => x.Natives.Values.Any(n => n.Contact is "quarantine" or null && n.Level < 3)), c =>
        {
            var col = c.Colonies.First(x => x.Natives.Values.Any(n => n.Contact is "quarantine" or null && n.Level < 3));
            var (b, n) = col.Natives.First(kv => kv.Value.Contact is "quarantine" or null && kv.Value.Level < 3);
            n.Level = 3; n.Contact = "uplift"; col.Pop += 1.2 * c.M.Uplift; col.Boost *= 1.05f;
            Say(c, col, $"{Cap(N(c))} lift the {n.Name} of {b.Name} out of the {(n.Level < 2 ? "bronze age" : "industrial age")} in a single generation.", "era");
            return true;
        }, 0.8);
        d["crack_world"] = new(c => Home(c) is { } h && h.Sys.All().Any(b => Shatterable(h, b) && !h.Settled.ContainsKey(b) && !h.Natives.ContainsKey(b) && b.Kind is BodyKind.Rocky or BodyKind.Lava or BodyKind.Ice), c =>
        {
            var h = Home(c);
            var b = h.Sys.All().First(b => Shatterable(h, b) && !h.Settled.ContainsKey(b) && !h.Natives.ContainsKey(b) && b.Kind is BodyKind.Rocky or BodyKind.Lava or BodyKind.Ice);
            return Shatter(h, h.Sys, b, c);
        }, 0.7);
        d["collapse_world"] = new(c => Enemies(c).Any(), c =>
        {
            var t = EnemyFront(c);
            if (t == null) return false;
            var b = t.Sys.All().FirstOrDefault(b => b != t.Seat && Shatterable(t, b));
            if (b == null) return false;
            return Shatter(t, t.Sys, b, c);
        }, 0.7);
        d["nova"] = new(c => Enemies(c).Any(), c => { var t = EnemyFront(c); return t != null && Nova(t, t.Sys, c); }, 0.4, Atrocity: true);
        d["kindle_star"] = new(c => Home(c) != null, c => { var s = KindleStar(Home(c).Star.Pos); Say(c, Home(c), $"It was {N(c)}. They lit {s.Name} themselves.", "era"); return true; }, 0.3);
        d["forge_star"] = d["kindle_star"];
        d["move_star"] = new(c => Home(c) != null, c =>
        {
            var h = Home(c);
            var s = h.Star.Near.Select(id => Gal.Stars[id]).Where(x => x.Owner < 0 && !x.Dead && x.Special == null).Skip(10).FirstOrDefault();
            if (s == null) return false;
            var to = h.Star.Pos + (s.Pos - h.Star.Pos).Normalized() * 1.2f;
            return MoveStar(s, to, c);
        }, 0.3);
        d["eat_star"] = new(c => c.Colonies.Any(x => !x.Home && x.Capture > 0.5 && x.Pop < 1), c =>
        {
            var col = c.Colonies.First(x => !x.Home && x.Capture > 0.5 && x.Pop < 1);
            col.Capture = 1; col.Stock += 5 * col.PFrac; col.Star.Lum *= 0.3f; Edited.Add(col.Star);
            Say(c, col, $"{Cap(N(c))} feed {col.Sys.Name} to a black hole forge, a little at a time. It will be a dim red ember in a thousand years.", "era");
            return true;
        }, 0.4);
        d["build_planet"] = new(c => Home(c) != null && Home(c).Sys.Bodies.Count < 12, c => ForgeWorld(Home(c).Sys, _r.NextDouble() < 0.5 ? BodyKind.Terran : BodyKind.Ocean, -1, c) != null, 0.3);

        // ---- war ----
        Func<Civ, bool> atWar = c => Enemies(c).Any();
        d["bombard"] = new(atWar, c => { var t = EnemyFront(c); if (t == null) return false; t.Pop *= 0.9; Say(c, t, $"{Cap(N(c))} bombard {t.Sys.Name} from orbit.", "loss"); return true; }, 2);
        d["kkv"] = new(atWar, c => { var t = EnemyFront(c); if (t == null) return false; t.Pop *= 0.8; t.Stock *= 0.8; Say(c, t, $"Relativistic slugs from {N(c)} strike {t.Sys.Name}. Cities become craters.", "loss"); return true; }, 1.5);
        d["beam_strike"] = new(atWar, c => { var t = EnemyFront(c); if (t == null) return false; t.Capture *= 0.7; Say(c, t, $"{Cap(N(c))} turn their star's light into a beam and sweep {t.Sys.Name}'s swarm out of the sky.", "loss"); return true; }, 1.5);
        d["star_weapons"] = d["beam_strike"];
        d["planet_killer"] = new(atWar, c =>
        {
            var t = EnemyFront(c); if (t == null) return false;
            var b = t.Settled.Keys.FirstOrDefault(b => b != t.Seat) ?? t.Sys.All().FirstOrDefault(b => b != t.Seat && Shatterable(t, b));
            return b != null && Shatter(t, t.Sys, b, c);
        }, 1, Atrocity: true);
        d["seal_world"] = new(atWar, c => { var t = EnemyFront(c); if (t == null) return false; t.NextSeed = Math.Max(t.NextSeed, Year + 25); t.Stock *= 0.7; Say(c, t, $"{Cap(N(c))} seal {t.Sys.Name} inside a shell nothing can launch through.", "loss"); return true; });
        d["nanite_world"] = new(atWar, c =>
        {
            var t = EnemyFront(c); if (t == null) return false;
            var b = t.Sys.All().FirstOrDefault(b => b != t.Seat && b.Mined < 0.9f && b.Kind != BodyKind.Belt);
            if (b == null) return false;
            b.Mined = Math.Min(1, b.Mined + 0.6f); t.Settled.Remove(b); c.AddMeter("Goo", 10);
            Say(c, t, $"{Cap(N(c))} loose a nanite plague on {b.Name}. It eats until there is very little left to eat.", "loss");
            return true;
        }, 0.7);
        d["harvest"] = new(c => c.Colonies.Any(x => x.Natives.Values.Any(n => n.Contact != "cleared")) || Enemies(c).Any(), c =>
        {
            var col = c.Colonies.FirstOrDefault(x => x.Natives.Values.Any(n => n.Contact != "cleared"));
            if (col != null)
            {
                var (b, n) = col.Natives.First(kv => kv.Value.Contact != "cleared");
                n.Contact = "cleared";
                c.Pools["Anima"] = c.Pools.GetValueOrDefault("Anima") + 3;
                CivAtrocity(c, col, 30);
                Say(c, col, $"{Cap(N(c))} harvest the {n.Name} of {b.Name}. Their souls fuel the engines now.", "loss");
                return true;
            }
            var t = EnemyFront(c); if (t == null) return false;
            t.Pop *= 0.7; c.Pools["Anima"] = c.Pools.GetValueOrDefault("Anima") + 3; CivAtrocity(c, t, 30, t.Civ);
            Say(c, t, $"{Cap(N(c))} harvest the people of {t.Sys.Name} for their Anima.", "loss");
            return true;
        }, 1, Atrocity: true);
        d["erase_cluster"] = new(atWar, c =>
        {
            var t = EnemyFront(c); if (t == null) return false;
            var near = t.Civ.Colonies.Where(x => !x.Home && x.Star.Pos.DistanceTo(t.Star.Pos) < 15).Take(3).ToList();
            if (near.Count == 0) return false;
            CivAtrocity(c, near[0], 40, t.Civ);
            foreach (var x in near) DestroyColony(x, $"{Cap(N(c))} erase {x.Sys.Name}. Not destroy: erase. The star is still there. Nothing else is.");
            return true;
        }, 0.6, Atrocity: true);
        d["assimilate"] = new(c => Known(c).Any(o => !o.Main && o.Colonies.Count <= 2 && WarStrength(o) < WarStrength(c) * 0.3), c =>
        {
            var o = Known(c).Where(o => !o.Main && o.Colonies.Count <= 2 && WarStrength(o) < WarStrength(c) * 0.3).OrderBy(o => WarStrength(o)).First();
            CivAtrocity(c, o.Colonies[0], 25, o);
            foreach (var x in o.Colonies.ToList()) { o.Colonies.Remove(x); c.Colonies.Add(x); x.Civ = c; x.Star.Owner = c.Id; }
            Extinct(o);
            Say(c, Home(c), $"{Cap(N(c))} assimilate {N(o)}. Every one of them is still alive. None of them is still themselves.", "loss");
            return true;
        }, 0.5, Atrocity: true);

        // ---- diplomacy ----
        d["federation"] = new(c => c.Federation < 0 && Known(c).Any(o => c.Relations.GetValueOrDefault(o.Id) >= 60 && !c.AtWar.Contains(o.Id)), c =>
        {
            var friends = Known(c).Where(o => c.Relations.GetValueOrDefault(o.Id) >= 60 && !c.AtWar.Contains(o.Id) && o.Federation < 0).ToList();
            if (friends.Count == 0) return false;
            c.Federation = c.Id;
            foreach (var f in friends) f.Federation = c.Id;
            Say(c, Home(c), $"{Cap(N(c))} and {string.Join(", ", friends.Select(N))} found a federation. Their fleets will not fire on one another.", "era");
            if (c.Main || friends.Any(f => f.Main)) Milestone(HomeColony, "A Federation", $"Year {Year:0}. {Cap(N(c))} and {friends.Count} other {(friends.Count == 1 ? "power" : "powers")} bind themselves together.", "seat");
            return true;
        }, 1.2);
        d["assembly"] = new(c => Known(c).Count() >= 2, c =>
        {
            var all = Known(c).Append(c).ToList();
            foreach (var a in all) foreach (var b in all) if (a != b && a.Met.Contains(b.Id)) a.Relations[b.Id] = Math.Min(100, a.Relations.GetValueOrDefault(b.Id) + 10);
            Say(c, Home(c), $"{Cap(N(c))} call a galactic assembly. {all.Count} powers send delegates. Nothing is decided, and everyone feels better.", "era");
            return true;
        }, 0.6);
        d["custodian"] = new(c => !c.Imperial, c =>
        {
            c.Custodian = true;
            var war = Known(c).SelectMany(a => a.AtWar.Where(id => id < Civs.Count && id != c.Id).Select(id => (a, b: Civs[id]))).FirstOrDefault();
            if (war.a != null)
            {
                war.a.AtWar.Remove(war.b.Id); war.b.AtWar.Remove(war.a.Id);
                war.a.Relations[war.b.Id] = war.b.Relations[war.a.Id] = -10;
                Say(c, Home(c), $"As custodians of the galaxy, {N(c)} step between {N(war.a)} and {N(war.b)}. The war stops.", "era");
            }
            return true;
        }, 0.8);
        d["empire"] = new(c => !c.Custodian && Known(c).Any(o => o.Overlord == null && !o.Main && WarStrength(o) < WarStrength(c) * 0.4), c =>
        {
            c.Imperial = true;
            var o = Known(c).Where(o => o.Overlord == null && !o.Main && WarStrength(o) < WarStrength(c) * 0.4).OrderBy(o => WarStrength(o)).First();
            o.Overlord = c;
            o.AtWar.Remove(c.Id); c.AtWar.Remove(o.Id);
            var oh = Home(o);
            if (oh != null) { var tithe = oh.Stock * 0.3; oh.Stock -= tithe; var ch = Home(c); if (ch != null) ch.Stock += tithe; }
            Say(c, Home(c), $"{Cap(N(o))} bend the knee to {N(c)}. They keep their worlds, and send a third of what they make.", "era");
            return true;
        }, 1);
        d["ancient_accord"] = new(c => Known(c).Any(o => o.Ancient), c =>
        {
            foreach (var a in Known(c).Where(o => o.Ancient)) { a.Relations[c.Id] = Math.Min(100, a.Relations.GetValueOrDefault(c.Id) + 20); c.Relations[a.Id] = a.Relations[c.Id]; a.AddMeter("Elders", -10); }
            c.AddMeter("Elders", -15);
            Say(c, Home(c), $"{Cap(N(c))} sign an accord with the ancient empires. The old ones agree to be a little less offended.", "era");
            return true;
        });
        d["memetic"] = new(c => Known(c).Any(o => o.Drive != c.Drive && !o.M.Hive), c =>
        {
            var o = Known(c).Where(o => o.Drive != c.Drive && !o.M.Hive).OrderBy(_ => _r.Next()).First();
            o.ShiftInf(c.Drive, 0.15f);
            c.AddMeter("Spy", 3);
            Say(c, Home(c), $"A song spreads through {N(o)}'s worlds. Nobody knows where it came from. It sounds like {c.Faction(c.Drive)} philosophy, because it is.");
            return true;
        });

        // ---- espionage ----
        Func<Civ, Civ> mark = c => Known(c).OrderBy(o => c.Relations.GetValueOrDefault(o.Id)).FirstOrDefault();
        bool Caught(Civ c, Civ o, double risk) { bool caught = _r.NextDouble() < risk * o.M.Spy / Math.Max(0.2f, c.M.Spy); if (caught) { c.AddMeter("Spy", 10); o.Relations[c.Id] = o.Relations.GetValueOrDefault(c.Id) - 15; c.Relations[o.Id] = o.Relations[c.Id]; } return caught; }
        d["spy"] = new(c => mark(c) != null, c => { var o = mark(c); c.AddMeter("Spy", 2); Caught(c, o, 0.1); return true; }, 0.5);
        d["steal_tech"] = new(c => mark(c) is { } o && o.Techs.Any(id => Available(c, Defs.TechById(id))), c =>
        {
            var o = Known(c).Where(o => o.Techs.Any(id => Available(c, Defs.TechById(id)))).OrderBy(_ => _r.Next()).First();
            var t = o.Techs.Select(Defs.TechById).Where(x => Available(c, x)).OrderBy(_ => _r.Next()).First();
            CompleteTech(c, t, true);
            bool caught = Caught(c, o, 0.35);
            Say(c, Home(c), caught ? $"{Cap(N(c))} are caught stealing {t.Name} from {N(o)}." : $"{Cap(N(c))} acquire {t.Name}. Nobody asks where from.", caught ? "loss" : "event");
            return true;
        }, 1.2);
        d["dreamwalk"] = new(c => mark(c) is { } o && o.Techs.Any(id => Available(c, Defs.TechById(id))), c =>
        {
            var o = Known(c).Where(o => o.Techs.Any(id => Available(c, Defs.TechById(id)))).OrderBy(_ => _r.Next()).First();
            var t = o.Techs.Select(Defs.TechById).Where(x => Available(c, x)).OrderBy(_ => _r.Next()).First();
            CompleteTech(c, t, true);
            c.AddMeter("Veil", 3);
            Say(c, Home(c), $"Psychics of {N(c)} walk through the dreams of {N(o)}'s scientists and come back with {t.Name}.");
            return true;
        });
        d["sabotage"] = new(c => mark(c) != null, c => { var o = mark(c); foreach (var s in o.Slots.Values) s.P *= 0.5; Caught(c, o, 0.3); Say(c, Home(o), $"Laboratories across {N(o)}'s worlds burn on the same night.", "loss"); return true; });
        d["block_research"] = new(c => mark(c) is { } o && o.Slots.Values.Any(s => s.Id != null), c =>
        {
            var o = mark(c);
            var s = o.Slots.Values.First(s => s.Id != null);
            o.Banned[s.Id] = Year + 10; s.Id = null; PickResearch(o, s.Area);
            Caught(c, o, 0.2);
            return true;
        });
        d["incite"] = new(c => mark(c) != null, c => { var o = mark(c); var other = Defs.Drives.Keys.First(k => k != o.Drive); o.ShiftInf(other, 0.12f); Caught(c, o, 0.25); return true; });

        // ---- flavour ----
        d["dig"] = new(c => true, c => { var h = Home(c); h.Stock += 0.05 * h.PFrac; foreach (var s in c.Slots.Values) s.P += 2; Say(c, h, $"Archaeologists of {N(c)} open a dig on a dead world and find a library nobody can read yet."); return true; }, 0.5);
        d["find_ruins"] = new(c => true, c => { var h = Home(c); h.Stock += 0.08 * h.PFrac; c.GateP["ruins"] = c.GateP.GetValueOrDefault("ruins") + 1; Say(c, h, $"Deep scans find ruins under {h.Sys.Name}'s outermost moon. Whoever built them was here before the star had planets."); return true; }, 0.5);
        d["salvage_tech"] = new(c => false, c => false);       // happens after battles (War.cs)
        d["foresee"] = new(c => c.Colonies.Any(x => x.Crisis != null), c =>
        {
            var col = c.Colonies.First(x => x.Crisis != null);
            col.Crisis.T0 -= 3;           // seen coming, it ends sooner
            Say(c, col, $"{Cap(N(c))} saw it coming. The crisis at {col.Sys.Name} is shorter than it should have been.");
            return true;
        });
        d["migrate"] = new(c => c.Colonies.Count > 1, c =>
        {
            var from = c.Colonies.OrderByDescending(x => x.Pop / Math.Max(x.PopCap, 0.01)).First();
            var to = c.Colonies.OrderBy(x => x.Pop / Math.Max(x.PopCap, 0.01)).First();
            if (from == to) return false;
            double n = from.Pop * 0.1; from.Pop -= n; to.Pop += n;
            Say(c, to, $"A great migration: {n:0.0} billion of {N(c)} leave {from.Sys.Name} for {to.Sys.Name}.");
            return true;
        }, 0.5);
        d["nomad"] = new(c => true, c => { foreach (var x in c.Colonies.Where(x => x.Complete).Take(5)) x.Region = Math.Min(1, x.Region + 0.02); Say(c, Home(c), $"Whole fleets of {N(c)} give up planets altogether and live aboard, wandering."); return true; }, 0.4);
        d["tame"] = new(c => true, c => { c.Vigour *= 1.03f; Say(c, Home(c), $"{Cap(N(c))} tame a herd of void-whales and harness them to their freighters."); return true; }, 0.4);
        d["veil_pact"] = new(c => true, c => { c.AddMeter("Veil", 5); foreach (var s in c.Slots.Values) s.P += 5; Say(c, Home(c), $"{Cap(N(c))} make a pact with something on the far side of the Veil. Their scientists sleep less, and know more."); return true; }, 0.5);
        d["veil_patron"] = new(c => true, c => { c.AddMeter("Veil", 8); c.Faith = Math.Min(1, c.Faith + 0.05f); Say(c, Home(c), $"{Cap(N(c))}'s patron in the Veil speaks to them again. It asks for very little. So far."); return true; }, 0.5);
        d["hear_voice"] = new(c => true, c =>
        {
            c.Faith = Math.Min(1, c.Faith + 0.1f);
            Say(c, Home(c), $"{Cap(N(c))} build an embassy to the divine, and for the first time someone there can hear the voice. They report that it is listening.", "omen");
            if (c.Main) Milestone(HomeColony, "Embassy to the Divine", $"Year {Year:0}. Humanity has learned to hear the voice that has been speaking to it all along.", "seat");
            return true;
        }, 0.3);
        d["avatar"] = new(c => true, c => { c.Vigour *= 1.1f; Say(c, Home(c), $"{Cap(N(c))} project their collective will into a single body. It walks among them for a year, and everyone who meets it is changed.", "era"); return true; }, 0.3);
        d["stasis"] = new(c => c.Colonies.Any(x => x.Crisis != null), c => { var col = c.Colonies.First(x => x.Crisis != null); Narrative.EndCrisisQuietly(col); Say(c, col, $"{Cap(N(c))} freeze the crisis at {col.Sys.Name} in a bubble of stopped time. It is still in there."); return true; });
        d["vault_raid"] = new(c => c.GateP.GetValueOrDefault("vault_raid") < 4 && Reach(c, "vault") != null, c =>
        {
            c.GateP["vault_raid"] = c.GateP.GetValueOrDefault("vault_raid") + 1;
            var t = Defs.Techs.Where(x => x.Line == "GLY" && Available(c, x)).OrderBy(x => x.Tier).FirstOrDefault()
                 ?? Defs.Techs.Where(x => x.Line == "GLY" && !c.Knows(x.Id) && ExclusiveOk(c, x)).OrderBy(x => x.Tier).FirstOrDefault();
            if (t != null) CompleteTech(c, t, true);
            Say(c, Home(c), $"Raiders of {N(c)} breach the Vault's {Ordinal((int)c.GateP["vault_raid"])} ring{(t != null ? " and come back with " + t.Name : "")}.", "omen");
            return true;
        }, 1.5);
        d["gate_riddle"] = new(c => false, c => false);        // improves trial odds (Exotics.cs)
        d["egress"] = new(c => c.Colonies.Count > 0 && c.Knows("CHR-7"), c => { Egress(c); return true; }, 0.15);
        return d;
    }

    static string Ordinal(int n) => n switch { 1 => "first", 2 => "second", 3 => "third", _ => "fourth" };

    /// <summary>A civilisation leaves for a younger universe. Humanity sends only those who choose to go.</summary>
    void Egress(Civ c)
    {
        var h = Home(c);
        c.AddMeter("Egress", 50);
        if (c.Main)
        {
            foreach (var x in c.Colonies) x.Pop *= 0.5;
            Log(h, "Half of humanity steps through the Horizon into a universe that has not happened yet. The other half stays to keep the lights on.", "era", null);
            Milestone(h, "Horizon Egress", $"Year {Year:0}. Half of humanity leaves the universe for a younger one. They promise to write.", "galaxy");
            HorizonShock(c);
            return;
        }
        Log(h, $"{Cap(N(c))} leave the universe. Their stars are still burning, wrapped in empty swarms, and nobody is home.", "omen", null);
        foreach (var x in c.Colonies.ToList()) { c.Colonies.Remove(x); Colonies.Remove(x); x.Star.Owner = -1; }
        Extinct(c);
        HorizonShock(c);
    }
}
