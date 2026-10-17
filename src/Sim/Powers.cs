using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Starforge.Sim;

/// <summary>
/// What can be done to the universe, and by whom. Every power has one core that takes an actor: null is the unseen voice,
/// otherwise a civilisation that has learned to do it. The voice is unlimited (no Will, no cooldowns); its acts are witnessed
/// and move Faith and Dread. A civ's acts move its own meters and its neighbours' opinions instead.
/// </summary>
public partial class Simulation
{
    public StarInfo Guided;
    public double ArkYear = -1;
    public readonly List<(double Year, double K, double Pop, int Systems)> Timeline = new();
    double _nextSample = 0;

    static string Who(Civ actor, bool capital = true) => actor == null ? "" : Narrative2.Called(actor, capital);

    // ---------- witnessing ----------
    /// <summary>
    /// Every act of the voice is witnessed. Wonders raise Faith; horrors raise Faith and Dread together. Faith feeds the
    /// civ's chance at apotheosis, Dread hardens hearts. Aliens who know humanity and suffer a horror blame humanity for it.
    /// </summary>
    public void Witness(Colony c, bool terrible, float k = 1f)
    {
        var civ = c?.Civ ?? Humanity;
        civ.Faith = Math.Min(1f, civ.Faith + (terrible ? 0.12f : 0.07f) * k);
        if (terrible)
        {
            civ.Dread = Math.Min(1f, civ.Dread + 0.15f * k);
            NudgeConscience(civ, -0.04f * k);
            civ.ShiftInf("Industrious", 0.03f);
            if (!civ.Main && civ.Met.Contains(Humanity.Id) && !civ.AtWar.Contains(Humanity.Id)) Blame(civ, Humanity, 35 * k, c, "have decided we did this");
            if (civ.Exalted) civ.VoiceRelation -= 25 * k;
        }
        else { NudgeConscience(civ, 0.02f * k); if (civ.Exalted) civ.VoiceRelation += 5 * k; }
        FaithBeats(civ, c);
    }

    /// <summary>A civ holds another responsible for something; bad enough, and it is war.</summary>
    void Blame(Civ victim, Civ culprit, float amount, Colony where, string phrase)
    {
        if (victim == culprit || victim.Dead) return;
        victim.Relations[culprit.Id] = victim.Relations.GetValueOrDefault(culprit.Id) - amount;
        culprit.Relations[victim.Id] = victim.Relations[culprit.Id];
        if (victim.Relations[culprit.Id] < -60 && !victim.AtWar.Contains(culprit.Id) && !Laws.Contains("nowar"))
        {
            victim.AtWar.Add(culprit.Id); culprit.AtWar.Add(victim.Id);
            Log(where, $"{Narrative2.Called(victim, true)} will not believe it was anyone but {Narrative2.Called(culprit)}. They declare war.", "loss", null);
        }
        else if (victim.Main || culprit.Main) Log(where, $"{Narrative2.Called(victim, true)} {phrase}. Their envoys stop answering.", "event", null);
    }

    void FaithBeats(Civ civ, Colony c)
    {
        int stage = civ.Faith > 0.85f ? 3 : civ.Faith > 0.55f ? 2 : civ.Faith > 0.25f ? 1 : 0;
        if (stage <= civ.FaithStage) return;
        civ.FaithStage = stage;
        bool fear = civ.Dread > civ.Faith * 0.6f;
        string who = civ.Main ? "" : Narrative2.Called(civ, true) + ": ";
        string text = (stage, fear) switch
        {
            (1, false) => "A small movement grows up around the idea that someone is listening, and answering. They call it the Attentive.",
            (1, true) => "Whispers that something out there is watching us, and is not kind. People leave offerings at the launch sites.",
            (2, false) => "The Attentive are a church now, with a hundred million members and a seat on the council. They pray for signs, and get them.",
            (2, true) => "The Propitiants hold the council's ear: build what it wants, and perhaps it will spare us.",
            (3, false) => "Most of them believe. Children are taught that the voice has a name, and that one day it will speak it.",
            _ => "Fear of the voice is now the oldest institution they have. Nothing is decided without asking what it would allow.",
        };
        Log(c ?? civ.Colonies.FirstOrDefault(), who + text, "omen", null);
        if (civ.Main) Milestone(HomeColony, stage == 3 ? "The Believers" : stage == 2 ? "A Church of the Voice" : "The Attentive", $"Year {Year:0}. {text}", "none");
    }

    /// <summary>A civ (not the voice) did something terrible: its own Atrocity meter climbs and everyone who knows it sours.</summary>
    void CivAtrocity(Civ actor, Colony where, float amount, Civ victim = null)
    {
        actor.AddMeter("Atrocity", amount);
        NudgeConscience(actor, -0.03f);
        foreach (var id in actor.Met)
        {
            if (id >= Civs.Count) continue;
            var o = Civs[id];
            if (o.Dead) continue;
            float k = o == victim ? 3f : o.Ethic == "Reverent" ? 1.2f : 0.5f;
            o.Relations[actor.Id] = o.Relations.GetValueOrDefault(actor.Id) - amount * k * 0.4f;
            actor.Relations[o.Id] = o.Relations[actor.Id];
        }
        if (victim != null) Blame(victim, actor, amount, where, $"swear that {Narrative2.Called(actor)} will answer for it");
    }

    // ---------- the old nudges, now free ----------
    public void Gift(Colony c)
    {
        c.Stock += Math.Max(0.6 * c.PFrac, 2e-15);
        c.Civ.W['X'] += 0.3f;
        c.Civ.ShiftInf("Expansionist", 0.05f);
        Fx(c, "comet", null);
        Log(c, "A comet falls inward on a perfect intercept. They call it luck. Some of them don't, and start looking outward.", "god", null);
    }

    public void Smite(Colony c)
    {
        double loss = 0.08 * c.Civ.M.Shield;
        c.Capture *= 1 - loss;
        c.Civ.W['E'] += 0.6f;
        c.Civ.ShiftInf("Contemplative", 0.06f); NudgeConscience(c.Civ, 0.06f);
        string react = c.Civ.Ethic switch { "Reverent" => "The Reverent call it a judgment and fast for a week.", "Ruthless" => "They log it as weather and rebuild.", _ => "Shielding research gets more funding." };
        Fx(c, "flare", null);
        Log(c, c.Capture > 1e-6 ? $"An unseasonal flare tears through the swarm. {(loss * 100):0.#}% lost. {react}" : $"An unseasonal flare scorches every satellite in the system. {react}", "god", null);
    }

    public void Inspire(Civ civ, char b)
    {
        civ.W[b] = Math.Min(6, civ.W.GetValueOrDefault(b, 1f) * 1.6f + 0.4f);
        civ.ShiftInf(Defs.BranchFaction[b], 0.07f);
        // the dream redraws the hand of the area it touches, unless the work there is already well along
        var slot = civ.Slots[AreaOf(b)];
        if (slot.Id == null || Defs.TechById(slot.Id).B != b && slot.P < 0.3 * Defs.TechById(slot.Id).CostFor(civ)) { slot.Id = null; PickResearch(civ, slot.Area); }
        Log(civ.Colonies[0], $"A shared dream spreads through the {(civ.Human ? "species" : "fleet")}: {Defs.Branch(b).Dream}", "god", null);
    }

    public void DirectHub(Colony c, Body b)
    {
        if (!c.CanMine(b, true)) return;
        c.Stock += c.HubCost(b);
        c.StartHub(b, true);
    }

    public void Protect(Colony c, Body b)
    {
        if (!c.Natives.TryGetValue(b, out var n) || n.Protected || n.Contact == "cleared") return;
        n.Protected = true;
        if (n.Contact == "clearance") n.Contact = "quarantine";
        NudgeConscience(c.Civ, 0.08f);
        Log(c, $"A voice no one can locate speaks in the council chamber: not these ones. The {n.Name} are left alone.", "god", b);
    }

    /// <summary>Draw the next human seed ship toward a chosen star.</summary>
    public bool CanGuide(StarInfo s) => s != null && s.Owner < 0 && Humanity.M.Seed && !s.Targeted && !s.Dead;
    public void Guide(StarInfo s)
    {
        if (!CanGuide(s)) return;
        Guided = s;
        Humanity.ShiftInf("Expansionist", 0.04f);
        Log(HomeColony, $"Navigators everywhere dream the same star: {s.Name}. The next seed ship will not be argued out of it.", "god", null);
    }

    public bool CanPeace => Humanity.AtWar.Count > 0;
    public void ImposePeace()
    {
        if (!CanPeace) return;
        foreach (var id in Humanity.AtWar.ToList())
        {
            var o = Civs[id];
            o.AtWar.Remove(Humanity.Id); Humanity.AtWar.Remove(id);
            o.Relations[Humanity.Id] = 0; Humanity.Relations[id] = 0;
            Log(HomeColony, $"Every weapon facing {Narrative2.Called(o)} falls silent at once, on both sides. Both sides hear the same voice. The war is over.", "god", null);
        }
        NudgeConscience(Humanity, 0.06f);
        Humanity.ShiftInf("Contemplative", 0.05f);
    }

    // ---------- great works ----------
    /// <summary>Raise a megastructure in a colony's system, whether or not its people could build it.</summary>
    public bool Raise(Colony c, string id, string name, Civ actor = null)
    {
        if (c == null || c.Raised.Contains(id)) return false;
        c.Raised.Add(id);
        if (id == "cracker" && !c.Sys.All().Any(b => b.Mined > 0.02f && b.Mined < 0.999f && b.Kind != BodyKind.Belt))
        {
            // the arc furnace needs a world to eat: it takes the largest one nobody lives on
            var w = c.Sys.All().Where(b => b.Kind is not (BodyKind.Belt or BodyKind.Gas or BodyKind.IceGiant) && b != c.Seat && !c.Settled.ContainsKey(b) && b.Mined < 0.5f)
                .OrderByDescending(b => b.Radius).FirstOrDefault();
            if (w != null) { w.Mined = Math.Max(w.Mined, 0.03f); c.Hubs[w] = Math.Max(c.Hubs.GetValueOrDefault(w), 1); }
        }
        c.Civ.ShiftInf("Industrious", 0.04f);
        if (actor == null)
        {
            Witness(c, false, 1.2f);
            Log(c, $"Overnight, a {name} hangs over {c.Sys.Name} where there was nothing. Nobody built it. Everybody will use it.", "god", null);
        }
        else Log(c, $"{Who(actor)} raise a {name} over {c.Sys.Name}, as their gods once did for them.", "era", null);
        return true;
    }

    // ---------- worlds ----------
    public static bool Seedable(Body b) => b.Kind is not (BodyKind.Gas or BodyKind.IceGiant or BodyKind.Belt or BodyKind.Lava) && !b.Life && b.Greening < 1 && b.Mined < 0.3f;
    /// <summary>Plant a living biosphere on a dead world: seas fill, green spreads, and it becomes good to live on.</summary>
    public bool SeedLife(Colony c, StarSystem sys, Body b, Civ actor = null)
    {
        if (!Seedable(b)) return false;
        b.Life = true; b.Greening = 1f;
        if (c != null && c.Settled.ContainsKey(b)) c.Settled[b] = Math.Min(1f, c.Settled[b] + 0.3f);
        if (actor == null)
        {
            if (c != null) Witness(c, false);
            Log(c ?? HomeColony, $"Rain falls on {b.Name} in {sys.Name}, and keeps falling. Within a season there are seas, and something green at their edges.", "god", b);
        }
        else Log(c ?? actor.Colonies.FirstOrDefault(), $"{Who(actor)} seed {b.Name} in {sys.Name} with life. In a few decades it has seas, and in a century, forests.", "era", b);
        return true;
    }

    public bool Shatterable(Colony c, Body b) => b.Kind != BodyKind.Belt && b.Mined < 0.999f && (c == null || b != c.Seat);
    /// <summary>Break a world apart. Anyone living there dies; what's left becomes a ring of rubble along its orbit.</summary>
    public bool Shatter(Colony c, StarSystem sys, Body b, Civ actor = null)
    {
        if (!Shatterable(c, b)) return false;
        b.Mined = 1f;
        bool lived = false;
        if (c != null)
        {
            lived = c.Settled.Remove(b);
            c.Hubs.Remove(b); c.Stations.Remove(b);
            if (c.Natives.Remove(b)) lived = true;
            c.Stock += 0.2 * c.PFrac;      // the rubble is easy mining
            if (lived) NudgeConscience(c.Civ, -0.1f);
            if (actor == null) Witness(c, lived, lived ? 1.2f : 0.5f);
            else if (lived) CivAtrocity(actor, c, 25, c.Civ == actor ? null : c.Civ);
        }
        if (actor == null)
            Log(c ?? HomeColony, lived ? $"{b.Name} in {sys.Name} cracks open like an egg. Everyone who lived there is gone. The council does not know whom to blame."
                                       : $"{b.Name} in {sys.Name} breaks apart in a single afternoon. Its pieces spread along its orbit, glowing.", "god", b);
        else
            Log(c ?? actor.Colonies.FirstOrDefault(), lived ? $"{Who(actor)} crack {b.Name} in {sys.Name} open, with people still on it."
                                                         : $"{Who(actor)} crack {b.Name} in {sys.Name} open for its core. Its pieces spread along its orbit, glowing.", lived ? "loss" : "era", b);
        return true;
    }

    public static bool Ignitable(Body b) => b.Kind is BodyKind.Gas or BodyKind.IceGiant && !b.Ignited && b.Mined < 0.5f;
    /// <summary>Set a gas giant burning: a second sun, too small to last forever, bright enough to farm by.</summary>
    public bool Ignite(Colony c, StarSystem sys, Body b, Civ actor = null)
    {
        if (!Ignitable(b)) return false;
        b.Ignited = true;
        if (c != null) { c.Stock += 0.5 * c.PFrac; c.Boost = Math.Max(c.Boost, 1.5f); c.Civ.W['E'] += 0.5f; if (actor == null) Witness(c, false, 1.5f); }
        if (actor == null) Log(c ?? HomeColony, $"{b.Name} in {sys.Name} ignites. For the first time in its history it casts shadows of its own.", "god", b);
        else Log(c ?? actor.Colonies.FirstOrDefault(), $"{Who(actor)} light {b.Name} in {sys.Name}. It burns as a small second sun.", "era", b);
        Fx(c, "ignite", b);
        return true;
    }

    /// <summary>Make a star erupt: the swarm is torn, the inner worlds are scorched, and nobody forgets it.</summary>
    public bool Nova(Colony c, StarSystem sys, Civ actor = null)
    {
        if (c != null)
        {
            c.Capture *= 0.25; c.Pop *= 0.6;
            foreach (var b in c.Sys.Bodies.Where(b => b.Orbit < sys.Bodies.Average(x => x.Orbit) * 0.5f)) c.Settled.Remove(b);
            c.Civ.ShiftInf("Contemplative", 0.12f);
            if (actor == null) Witness(c, true, 2f);
            else CivAtrocity(actor, c, 40, c.Civ == actor ? null : c.Civ);
        }
        if (actor == null)
            Log(c ?? HomeColony, c != null ? $"{sys.Name} erupts. The swarm burns like paper; the inner worlds are scoured to bare rock. The survivors will speak of it for a thousand years."
                                           : $"{sys.Name} erupts, briefly brighter than a galaxy. Astronomers everywhere stop what they are doing.", "god", null);
        else Log(c ?? actor.Colonies.FirstOrDefault(), $"{Who(actor)} make {sys.Name} erupt. Its swarm burns like paper.", "loss", null);
        return true;
    }

    /// <summary>Worlds slowly coming alive after being moved into the warmth.</summary>
    public readonly List<Body> Thawing = new();
    public static float HabitableOrbit(StarSystem sys) => StarSystem.AU * Mathf.Sqrt(Mathf.Max(sys.Star.Lum, 0.01f));
    /// <summary>A world already moved sits in the nearest free lane to the warm band (which may still be outside it, when
    /// the band is taken): moving it again changes nothing, so it can be moved once.</summary>
    public static bool Movable(StarSystem sys, Body b) => b.Orbit0 < 0 && b.Parent == null && b.Kind != BodyKind.Belt && b.Mined < 0.5f && Mathf.Abs(b.Orbit - HabitableOrbit(sys)) > HabitableOrbit(sys) * 0.12f;
    /// <summary>Walk a world into the band where water stays liquid. Over the next years it warms (or cools) toward life.</summary>
    public bool ShiftOrbit(Colony c, StarSystem sys, Body b, Civ actor = null)
    {
        if (!Movable(sys, b)) return false;
        if (b.Orbit0 < 0) b.Orbit0 = b.Orbit;
        float hz = HabitableOrbit(sys);
        float best = FreeLane(sys, hz, b.Radius, b);
        float before = b.Orbit;
        b.Orbit = best;
        b.Period = (float)(b.Period * Math.Pow(best / before, 1.5));
        if (b.Kind is BodyKind.Ice or BodyKind.Rocky or BodyKind.Desert or BodyKind.Ocean) { b.Greening = Math.Max(b.Greening, 0.3f); if (!Thawing.Contains(b)) Thawing.Add(b); }   // thawing seas, a first thin air
        if (actor == null)
        {
            if (c != null) Witness(c, false, 1.3f);
            Log(c ?? HomeColony, $"Over one long night {b.Name} in {sys.Name} slides {(best < before ? "inward" : "outward")} toward the warmth, its moons in tow. By morning its seasons have changed.", "god", b);
        }
        else Log(c ?? actor.Colonies.FirstOrDefault(), $"{Who(actor)} walk {b.Name} in {sys.Name} into the warmth on a century of thrust.", "era", b);
        return true;
    }

    /// <summary>A lane near orbit o clear of the other worlds.</summary>
    static float FreeLane(StarSystem sys, float o0, float radius, Body except = null)
    {
        for (int k = 0; k < 24; k++)
        {
            float o = o0 * (0.88f + 0.02f * k);
            if (sys.Bodies.All(x => x == except || Mathf.Abs(x.Orbit - o) > Mathf.Max(x.Radius, radius) * 60f + 0.04f * StarSystem.AU)) return o;
        }
        return o0;
    }

    public bool CanSummon(Colony c) => c != null && c.Home && !c.VisitorDone && c.VisitorT < 0;
    /// <summary>Call the visitor down on the homeworld now, rather than whenever it chooses.</summary>
    public bool Summon(Colony c)
    {
        if (!CanSummon(c)) return false;
        c.VisitorT = Year; c.VisitorStage = 0;
        Witness(c, false, 1f);
        Log(c, "A fold signature at the edge of the system, though nobody was calling. Something has arrived. It is not ours.", "omen", "visitor");
        return true;
    }

    public bool CanHerald(Colony c) => c != null && c.Seat != null && PickTargetFor(c) != null;
    StarInfo PickTargetFor(Colony c)
    {
        foreach (int id in c.Star.Near) { var s = Gal.Stars[id]; if (s.Owner < 0 && !s.Targeted && !s.Dead && !Blocked(c.Star, s)) return s; }
        return null;
    }
    /// <summary>A seed ship that nobody remembers building, crewed and provisioned, leaves for the nearest free star.</summary>
    public bool Herald(Colony c)
    {
        if (!CanHerald(c)) return false;
        var t = Guided != null && Guided.Owner < 0 && !Guided.Targeted ? Guided : PickTargetFor(c);
        if (t == Guided) Guided = null;
        var saved = c.Civ.M.ShipSpeed;
        c.Civ.M.ShipSpeed = Math.Max(saved, 0.12f);           // the herald is faster than anything they could build yet
        LaunchSeed(c, t);
        c.Civ.M.ShipSpeed = saved;
        Witness(c, false, 1f);
        Log(c, $"A ship nobody remembers building is found in the yards, fuelled, crewed by volunteers who all dreamed of {t.Name}. It is gone before the council meets.", "god", null);
        return true;
    }

    public bool CanReveal(Civ civ) => civ.Slots.Values.Any(s => s.Id != null);
    /// <summary>Hand a civilisation the answers it is working on, in every field at once.</summary>
    public bool Reveal(Civ civ)
    {
        var now = civ.Researching.ToList();
        if (now.Count == 0) return false;
        Log(civ.Colonies[0], $"Researchers wake with the whole of {string.Join(", ", now.Select(t => t.Name))} in their heads, as if they had always known it.", "god", null);
        Witness(civ.Colonies[0], false, 0.6f);
        foreach (var t in now) CompleteTech(civ, t);
        return true;
    }

    // =====================================================================================
    // The voice's new acts (docs/TECH_INTEGRATION.md §11, progression.json godActs.new)
    // =====================================================================================

    public static bool Exotic(Tech t) => t.B == 'Q' || t.Uses.Length > 0 || t.Gate != null;

    /// <summary>Give any tech to any civ, gate-locked and exotic ones included.</summary>
    public bool GiftTech(Civ civ, Tech t)
    {
        if (civ == null || civ.Dead || civ.Techs.Contains(t.Id)) return false;
        if (t.Gate != null) civ.Gates[t.Gate] = 2;
        foreach (var e in t.Effects) if (e.Kind == "res") civ.Pools[e.A[0]] = Math.Max(civ.Pools.GetValueOrDefault(e.A[0]), 1);
        var home = civ.Colonies.FirstOrDefault();
        Log(home, $"{(civ.Main ? "A" : Narrative2.Called(civ, true) + ": a")} researcher wakes with all of {t.Name} in her head. Nobody taught it to her.", "god", null);
        CompleteTech(civ, t, true);
        Witness(home, false, 0.6f);
        foreach (var id in civ.Met)
        {
            if (id >= Civs.Count) continue;
            var o = Civs[id];
            if (o.Dead) continue;
            if (Exotic(t) && o.Ancient) o.AddMeter("Elders", 5);
            o.Relations[civ.Id] = o.Relations.GetValueOrDefault(civ.Id) - 3;      // somebody is being favoured
            civ.Relations[o.Id] = o.Relations[civ.Id];
        }
        return true;
    }

    /// <summary>Take a tech away from one civ, or from everyone (erase the concept).</summary>
    public int EraseTech(Civ civ, Tech t)
    {
        var who = civ == null ? Civs.Where(c => !c.Dead && c.Techs.Contains(t.Id)).ToList() : new List<Civ> { civ };
        int n = 0;
        foreach (var c in who)
        {
            if (!c.Techs.Contains(t.Id)) continue;
            LoseTech(c, t); n++;
            c.Dread = Math.Min(1, c.Dread + 0.05f);
            if (c.Main || c == Focus?.Civ) Log(c.Colonies.FirstOrDefault(), $"Overnight, nobody remembers how {t.Name} worked. The machines that need it still run. Nobody can say why, or build another.", "god", null);
        }
        if (civ == null && n > 0) Log(HomeColony, $"The idea of {t.Name} is gone from the galaxy. {n} civilisations wake up without it.", "god", null);
        return n;
    }

    /// <summary>Light a new star in empty space near a point.</summary>
    public StarInfo KindleStar(Vector3 near, float temp = 5800f)
    {
        var r = new Random((int)(Year * 1000) ^ Gal.Stars.Count);
        Vector3 p = near;
        for (int i = 0; i < 40; i++)
        {
            var q = near + new Vector3((float)(r.NextDouble() - 0.5), (float)(r.NextDouble() - 0.5) * 0.3f, (float)(r.NextDouble() - 0.5)) * 6f;
            if (Gal.Stars.All(s => s.Pos.DistanceTo(q) > 0.8f)) { p = q; break; }
        }
        float lum = Mathf.Pow(temp / 5772f, 4) * Mathf.Pow(temp / 5772f, 1.2f);
        string cls = temp < 3700 ? "M" : temp < 5200 ? "K" : temp < 6000 ? "G" : temp < 7500 ? "F" : temp < 10000 ? "A" : "B";
        var st = new StarInfo { Id = Gal.Stars.Count, Pos = p, Temp = temp, Lum = lum, Cls = cls + "5V", Seed = r.Next(), Name = "Kindled " + Galaxy.MakeName(r), Kindled = true };
        Gal.Stars.Add(st);
        Gal.Relink(st);
        Edited.Add(st);
        Log(HomeColony, $"A new star lights between {Gal.Stars[st.Near[0]].Name} and its neighbours, where there was only dark. Astronomers name it {st.Name}.", "god", null);
        foreach (var c in Civs.Where(c => !c.Dead)) if (c.Colonies.Any(x => x.Star.Pos.DistanceTo(p) < 12)) { var col = c.Colonies.OrderBy(x => x.Star.Pos.DistanceTo(p)).First(); Witness(col, false, 1.5f); }
        return st;
    }

    /// <summary>Put a star out. Everyone there is gone.</summary>
    public bool SnuffStar(StarInfo s, Civ actor = null)
    {
        if (s == null || s.Dead) return false;
        var col = Colonies.FirstOrDefault(c => c.Star == s);
        s.Dead = true; s.Lum = 0.0005f; s.Temp = 1800f;
        if (Systems.TryGetValue(s, out var dsys)) dsys.StarColor = Galaxy.Blackbody(1800f);
        Edited.Add(s);
        if (col != null)
        {
            var owner = col.Civ;
            if (actor == null) Witness(col, true, 3f);
            else CivAtrocity(actor, col, 50, owner == actor ? null : owner);
            DestroyColony(col, $"{s.Name} goes out. Not a nova: it simply stops. The light already on its way will keep arriving for years. Nobody there will.");
            foreach (var c in owner.Colonies.Take(3)) Witness(c, true, 1f);
        }
        else Log(HomeColony, $"{s.Name} goes out like a lamp. The sky there is one star poorer.", "god", null);
        return true;
    }

    /// <summary>Move a star, and everything around it, somewhere else in the galaxy.</summary>
    public bool MoveStar(StarInfo s, Vector3 to, Civ actor = null)
    {
        if (s == null || s == Gal.Sol && actor != null) return false;
        var from = s.Pos;
        s.Pos = to;
        Gal.Relink(s);
        foreach (int id in s.Near.ToList()) Gal.Relink(Gal.Stars[id]);
        Edited.Add(s);
        var col = Colonies.FirstOrDefault(c => c.Star == s);
        if (actor == null)
        {
            if (col != null) Witness(col, false, 2f);
            Log(col ?? HomeColony, $"{s.Name} moves. Over a single year it crosses {from.DistanceTo(to) * Galaxy.LyPerUnit:0} light-years, its worlds still in their orbits. Nobody aboard felt a thing.", "god", null);
        }
        else Log(col ?? actor.Colonies.FirstOrDefault(), $"{Who(actor)} push {s.Name} across the dark on its own light, worlds and all.", "era", null);
        return true;
    }

    /// <summary>Add a world to a system: kind and orbit chosen.</summary>
    public Body ForgeWorld(StarSystem sys, BodyKind kind, float orbit = -1, Civ actor = null)
    {
        orbit = FreeLane(sys, orbit > 0 ? orbit : HabitableOrbit(sys), kind is BodyKind.Gas or BodyKind.IceGiant ? 11 : 1.2f);
        var r = new Random(sys.Bodies.Count * 7919 + (int)Year);
        var b = StarSystem.Forge(sys, kind, orbit, r);
        if (!Forged.TryGetValue(sys.Star.Id, out var list)) Forged[sys.Star.Id] = list = new();
        list.Add((b.Name, (int)kind, orbit, b.Radius, b.ProcSeed));
        var col = Colonies.FirstOrDefault(c => c.Star == sys.Star);
        if (actor == null)
        {
            if (col != null) Witness(col, false, 1.5f);
            Log(col ?? HomeColony, $"A world condenses out of nothing in {sys.Name}'s {(orbit < HabitableOrbit(sys) * 1.3f ? "warm" : "cold")} lanes. It is {KindName(kind)}, and it is called {b.Name}.", "god", b);
        }
        else Log(col ?? actor.Colonies.FirstOrDefault(), $"{Who(actor)} build a planet in {sys.Name} out of the scraps of others. They call it {b.Name}.", "era", b);
        WorldsChanged?.Invoke(sys);
        return b;
    }
    public event Action<StarSystem> WorldsChanged;
    public readonly Dictionary<int, List<(string Name, int Kind, float Orbit, float Radius, int Seed)>> Forged = new();
    public readonly HashSet<StarInfo> Edited = new();
    static string KindName(BodyKind k) => k switch
    {
        BodyKind.Terran => "green and blue", BodyKind.Ocean => "all ocean", BodyKind.Desert => "desert", BodyKind.Ice => "ice", BodyKind.Lava => "molten",
        BodyKind.Gas => "a gas giant", BodyKind.IceGiant => "an ice giant", _ => "bare rock",
    };

    /// <summary>Lift a native people straight to the stars: they become a young civilisation of their own.</summary>
    public Civ UpliftNow(Colony c, Body b)
    {
        if (c == null || !c.Natives.TryGetValue(b, out var n) || n.Contact == "cleared") return null;
        var star = PickTargetFor(c);
        if (star == null) return null;
        c.Natives.Remove(b);
        var civ = NewCiv(n.Name, n.Name, age: 0, near: star);
        civ.Relations[c.Civ.Id] = n.Contact == "quarantine" ? 20 : n.Contact == "uplift" ? 40 : -50;
        c.Civ.Relations[civ.Id] = civ.Relations[c.Civ.Id];
        civ.Met.Add(c.Civ.Id); c.Civ.Met.Add(civ.Id);
        civ.Faith = 0.6f;
        Witness(c, false, 1.5f);
        Log(c, $"The {n.Name} of {b.Name} wake knowing how to leave. In a generation they cross to {star.Name} and call it home. They remember who woke them.", "god", b);
        return civ;
    }

    /// <summary>Place a brand-new people on a world nobody owns.</summary>
    public Civ SeedSpecies(StarInfo star)
    {
        if (star == null || star.Owner >= 0 || star.Dead) return null;
        var r = new Random(star.Seed ^ (int)Year);
        string sp = SpeciesName(r);
        var civ = NewCiv("the " + sp, sp, age: 0, near: star);
        civ.Faith = 0.4f;
        Log(civ.Colonies[0], $"On a world of {star.Name}, something wakes up already able to think. They call themselves the {sp}. They have never wondered where they came from; they assume someone was listening.", "god", null);
        return civ;
    }
    static string SpeciesName(Random r)
    {
        string[] a = { "Ae", "Ky", "Sol", "Ith", "Mor", "Va", "Ul", "Zer", "Ny", "Qua", "Bel", "Tor" }, b = { "ari", "eth", "oon", "is", "ann", "ix", "ume", "ol", "eid", "ar" };
        return a[r.Next(a.Length)] + b[r.Next(b.Length)];
    }

    public Civ NewCiv(string name, string species, int age, StarInfo near)
    {
        var civ = new Civ(Civs.Count, _r.Next()) { Name = name, Species = species, Alien = true, Founded = Year, Age = age, Color = Color.FromHsv((float)_r.NextDouble(), 0.55f, 1f) };
        var drives = Defs.Drives.Keys.ToList();
        civ.SetDrive(drives[_r.Next(drives.Count)], new[] { "Reverent", "Pragmatic", "Ruthless" }[_r.Next(3)]);
        GrantUpTo(civ, age switch { 0 => 1, 1 => 4, 2 => 5, _ => 6 });
        civ.Leader = civ.NewLeader(civ.Drive, Year);
        civ.NextElection = Year + 15;
        civ.Vigour = 1.2f;
        Civs.Add(civ);
        var col = Found(civ, near, null, true);
        col.Capture = 0; col.PlanetPower = 5e13; col.Pop = 3;
        PickResearch(civ);
        return civ;
    }

    /// <summary>Choose how the current crisis (or the visitor) turns out.</summary>
    public bool BendFate(Colony c, string outcome)
    {
        if (c == null) return false;
        if (c.Crisis != null)
        {
            if (outcome == "mercy") { Narrative.FixCrisis(c); Witness(c, false, 0.8f); }
            else { Narrative.RuinCrisis(c); Witness(c, true, 1f); }
            return true;
        }
        if (c.VisitorT >= 0 && c.VisitorStage < 2) { c.VisitorForced = outcome; Witness(c, false, 0.5f); return true; }
        return false;
    }

    /// <summary>Speak through the Speaker: the council takes a course it did not choose.</summary>
    public void Possess(Civ civ, string drive)
    {
        if (civ.Drive == drive) return;
        string old = civ.Drive;
        civ.Drive = drive;
        civ.Inf[drive] = Math.Max(civ.Inf[drive], civ.Inf.Values.Max() + 0.05f); civ.ShiftInf(drive, 0);
        foreach (var b in civ.W.Keys.ToList()) civ.W[b] = Math.Min(6, 0.5f * civ.W[b] + 0.5f * Defs.Drives[drive].W.GetValueOrDefault(b, 1f));
        civ.Dread = Math.Min(1, civ.Dread + 0.1f);
        var home = civ.Colonies.FirstOrDefault();
        Log(home, $"Speaker {civ.Leader?.Name} stands mid-session and speaks in a voice that is not theirs. By the end of the day the {civ.Faction(drive)} hold the council, and nobody can say how the {civ.Faction(old)} lost it.", "god", null);
        foreach (var a in Areas) civ.Slots[a].Id = null;
        PickResearch(civ);
    }

    /// <summary>A permanent door between two stars: ships cross in a year, and the two neighbourhoods meet.</summary>
    public readonly List<(StarInfo A, StarInfo B)> Lanes = new();
    public bool OpenLane(StarInfo a, StarInfo b)
    {
        if (a == null || b == null || a == b || Lanes.Any(l => l.A == a && l.B == b || l.A == b && l.B == a)) return false;
        Lanes.Add((a, b));
        if (!a.Near.Contains(b.Id)) a.Near.Insert(0, b.Id);
        if (!b.Near.Contains(a.Id)) b.Near.Insert(0, a.Id);
        Log(HomeColony, $"A door opens between {a.Name} and {b.Name}, {a.Pos.DistanceTo(b.Pos) * Galaxy.LyPerUnit:0} light-years apart. It does not close.", "god", null);
        foreach (var c in Colonies.Where(c => c.Star == a || c.Star == b).ToList()) Witness(c, false, 1.2f);
        var ca = Colonies.FirstOrDefault(c => c.Star == a); var cb = Colonies.FirstOrDefault(c => c.Star == b);
        if (ca != null && cb != null && ca.Civ != cb.Civ) Meet(ca.Civ, cb.Civ, b);
        return true;
    }
    public StarInfo LaneFrom(StarInfo s, StarInfo to) => Lanes.Any(l => l.A == s && l.B == to || l.B == s && l.A == to) ? to : null;

    /// <summary>A sealed region: no seed ship crosses its edge, in or out.</summary>
    public readonly List<(Vector3 Center, float Radius)> Quarantines = new();
    public void Quarantine(StarInfo center, float radius = 40f)
    {
        Quarantines.Add((center.Pos, radius));
        Log(HomeColony, $"Every navigator in the {center.Name} reach dreams the same wall. Ships turn back at its edge; nobody can say what they saw.", "god", null);
        foreach (var s in Ships.ToList())
            if (Blocked(s.Origin, s.Target)) { Ships.Remove(s); s.Target.Targeted = false; Log(s.From ?? HomeColony, $"The {s.Name} turns back at the edge of the {center.Name} reach. Its crew will not say why.", "omen", null); }
    }
    public bool Blocked(StarInfo a, StarInfo b) => Quarantines.Any(q => a.Pos.DistanceTo(q.Center) < q.Radius != b.Pos.DistanceTo(q.Center) < q.Radius);

    /// <summary>Rules of the universe the voice can rewrite: nowar, research2, nofold, growth2, nocrisis.</summary>
    public readonly HashSet<string> Laws = new();
    public static readonly (string Id, string Name, string About)[] LawList =
    {
        ("nowar", "No war", "No civilisation can declare war, and every war ends."),
        ("research2", "Quick minds", "Research everywhere runs twice as fast."),
        ("nofold", "Fold forbidden", "Space will not fold. Ships fly at the speed of light or slower."),
        ("growth2", "Fecund", "Populations grow twice as fast."),
        ("nocrisis", "Calm", "No new crises begin."),
    };
    public void RewriteLaw(string law, bool on)
    {
        if (on ? !Laws.Add(law) : !Laws.Remove(law)) return;
        var l = LawList.First(x => x.Id == law);
        if (law == "nowar" && on) foreach (var c in Civs) { foreach (var o in c.AtWar) if (o < Civs.Count) Civs[o].AtWar.Remove(c.Id); c.AtWar.Clear(); }
        foreach (var c in Civs) c.ComputeMods();
        Log(HomeColony, on ? $"A law of the universe changes. {l.About}" : $"The law \"{l.Name}\" lifts. The universe goes back to the way it was.", "god", null);
        foreach (var c in Civs.Where(c => !c.Dead && c.Colonies.Count > 0)) { c.Dread = Math.Min(1, c.Dread + 0.03f); c.Faith = Math.Min(1, c.Faith + 0.03f); }
    }

    // ---------- Steer: a lighter touch on research ----------
    public void SteerSwap(Civ civ, string area, string techId)
    {
        var s = civ.Slots[area];
        if (!s.Hand.Contains(techId) || s.Id == techId) return;
        s.Id = techId;
        civ.Faith = Math.Min(1, civ.Faith + 0.01f);
    }
    public void SteerRedraw(Civ civ, string area) { PickResearch(civ, area); civ.Faith = Math.Min(1, civ.Faith + 0.01f); }
    public void SteerPin(Civ civ, Tech t)
    {
        var s = civ.Slots[AreaOf(t.B)];
        if (Available(civ, t)) { s.Id = t.Id; s.Hand = new() { t.Id }; }
        else s.Pinned = t.Id;
        civ.Faith = Math.Min(1, civ.Faith + 0.01f); civ.Dread = Math.Min(1, civ.Dread + 0.01f);
    }
    public void SteerBan(Civ civ, Tech t, double years)
    {
        civ.Banned[t.Id] = Year + years;
        foreach (var s in civ.Slots.Values) if (s.Id == t.Id) { s.Id = null; PickResearch(civ, s.Area); }
        civ.Dread = Math.Min(1, civ.Dread + 0.03f);
        if (civ.Main) Log(HomeColony, $"Every attempt to study {t.Name} fails. Equipment breaks; notes go missing; researchers dream of walls.", "god", null);
    }
    public void SteerDecide(Civ civ, string group, string member)
    {
        civ.ExclusivePick[group] = member;
        civ.Dread = Math.Min(1, civ.Dread + 0.02f);
        foreach (var s in civ.Slots.Values) if (s.Id != null && Defs.TechById(s.Id).Exclusive == group && s.Id != member) { s.Id = null; PickResearch(civ, s.Area); }
    }
}
