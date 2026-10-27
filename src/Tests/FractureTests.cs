using System;
using System.Collections.Generic;
using System.Linq;
using Starforge.Sim;

namespace Starforge.Tests;

/// <summary>Fracture: Hive Consensus is voted on, refusers hold out, and risings can grow into civil wars (docs/AUDIT.md findings 3 and 4).</summary>
public static class FractureTests
{
    /// <summary>Humanity at 2461, not yet a hive, with every system calm.</summary>
    static (Simulation sim, Civ h) Calm()
    {
        var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
        h.M.Hive = false; h.Techs.Remove("S8");
        foreach (var c in h.Colonies) { c.Grievance = 0; c.Unrest = 0; c.Movement = null; c.Holdout = false; }
        return (sim, h);
    }

    public static readonly List<(string Name, Action Body)> All = new()
    {
        ("hive_vote_clean", () => {
            var (sim, h) = Calm(); int n = h.Colonies.Count;
            var r = sim.HiveVote(h, 0);
            SelfTest.Check(r.Refused == 0 && h.Colonies.Count == n && h.Colonies.All(c => !c.Holdout), "with no will to refuse, every system joins");
        }),
        ("hive_vote_civil_war", () => {
            var (sim, h) = Calm(); int humans0 = sim.Civs.Count(c => c.Human && !c.Dead);
            var r = sim.HiveVote(h, 100);
            var s = sim.Civs.Last();
            SelfTest.Check(r.Refused > 0 && r.Split == "civil war" && s.Human && sim.WarBetween(h, s) != null, "most of humanity refuses: civil war");
            SelfTest.Check(sim.Civs.Count(c => c.Human && !c.Dead) == humans0 + 1, "humanity is two");
        }),
        ("hive_vote_peaceful", () => {
            var (sim, h) = Calm(); h.SetDrive(h.Drive, "Reverent");
            // only a handful refuse: a Reverent council lets them go
            var refusers = h.Colonies.Where(c => !c.Home).OrderByDescending(c => c.Star.Pos.DistanceTo(sim.HomeColony.Star.Pos)).Take(Math.Max(2, h.Colonies.Count / 8)).ToHashSet();
            var r = sim.HiveVote(h, refusers);
            SelfTest.Check(r.Split == "peaceful" && !h.AtWar.Contains(sim.Civs.Last().Id), "a small refusal leaves in peace");
        }),
        ("hive_vote_simmers", () => {
            var (sim, h) = Calm();
            var one = h.Colonies.First(c => !c.Home);
            var r = sim.HiveVote(h, new HashSet<Colony> { one });
            SelfTest.Check(r.Split == null && one.Civ == h && one.Holdout, "one refuser stays, unlinked");
        }),
        ("splinter_from_hive_is_flesh", () => {
            var (sim, h) = Calm(); h.Techs.Add("S8"); h.ComputeMods();
            var s = sim.Separate(h, h.Colonies.Where(c => !c.Home).Take(2).ToList(), h.Drive, peaceful: true);
            SelfTest.Check(h.M.Hive && !s.M.Hive && !sim.Available(s, Defs.TechById("S8")), "those who leave a hive are not a hive, and will not build one soon");
        }),
        ("holdouts_feel", () => {
            var (sim, h) = Calm(); h.Techs.Add("S8"); h.ComputeMods();
            var hold = h.Colonies.First(c => !c.Home); hold.Holdout = true;
            var linked = h.Colonies.First(c => !c.Home && c != hold); linked.Grievance = 80;
            sim.Warp(2);
            SelfTest.Check(hold.Grievance > 10 && hold.CauseKey == "hive", "a holdout resents the hive");
            SelfTest.Check(linked.Grievance < 1, "a linked system feels nothing");
        }),
        ("rising_pulls_insurgents", () => {
            var (sim, h) = Calm(); h.SetDrive(h.Drive, "Ruthless"); int civil0 = sim.StrifeStats.GetValueOrDefault("civilwars");
            var cap = h.Colonies.First(c => c.Home);
            var rest = h.Colonies.Where(c => !c.Home).ToList();
            // a third of the empire in insurgency over the levy, one of them in open revolt
            var insurgent = rest.Take(Math.Max(3, rest.Count / 3)).ToList();
            foreach (var c in insurgent) { c.Unrest = 3; c.Grievance = 70; c.CauseKey = "levy"; }
            insurgent[0].Unrest = 4; insurgent[0].Grievance = 95;
            var s = sim.RevoltNow(h);
            SelfTest.Check(s != null && s.Colonies.Count >= insurgent.Count / 2, "the rising takes insurgent systems that share its cause");
            SelfTest.Check(sim.StrifeStats.GetValueOrDefault("civilwars") == civil0 + 1, "and it is a civil war");
        }),
        ("holdout_saved", () => {
            var (sim, h) = Calm();
            var c = h.Colonies.First(x => !x.Home); c.Holdout = true; h.HiveSwing = 0.75;
            SaveGame.Save(sim, 20610, "user://selftest_holdout.json");
            var (s2, _) = SaveGame.Load("user://selftest_holdout.json");
            SelfTest.Check(s2.Humanity.Colonies.First(x => x.Star.Id == c.Star.Id).Holdout && s2.Humanity.HiveSwing == 0.75, "a holdout and the swing round-trip");
        }),
        ("hive_link_frays", () => {
            var (sim, h) = Calm(); h.Techs.Add("S8"); h.ComputeMods(); h.Used.Add("hiveVote");
            h.HiveSwing = 50;
            sim.Warp(3);
            SelfTest.Check(h.Colonies.Any(c => c.Holdout), "a hive with a strong swing loses systems from the link");
            var (sim2, h2) = Calm(); h2.Techs.Add("S8"); h2.ComputeMods(); h2.Used.Add("hiveVote");
            sim2.Warp(3);
            SelfTest.Check(!h2.Colonies.Any(c => c.Holdout), "with no swing the link holds");
        }),
        ("crackdowns_fade", () => {
            var (sim, h) = Calm();
            var c = h.Colonies.First(x => !x.Home); c.Movement = "m";
            var drops = new List<double>();
            for (int i = 0; i < 3; i++) { c.Grievance = 90; sim.Respond(c, Simulation.Answer.Crackdown); drops.Add(90 - c.Grievance); }
            SelfTest.Check(drops[0] > drops[1] && drops[1] > drops[2] && c.Crackdowns == 3, "each crackdown in the same system relieves less");
        }),
        ("young_polity_grace", () => {
            var (sim, h) = Calm();
            var s = sim.Separate(h, h.Colonies.Where(c => !c.Home).Take(4).ToList(), h.Drive, peaceful: true);
            foreach (var c in s.Colonies.Skip(1)) { c.Unrest = 4; c.Grievance = 95; }
            SelfTest.Check(sim.RevoltNow(s) == null, "a splinter does not split again in its first years");
        }),
        ("link_hive_splinter_is_flesh", () => {
            // Link-Hive (ASC-C3b) makes a hive too: those who leave it are flesh as well
            var (sim, h) = Calm(); h.Techs.Remove("ASC-C3b"); h.Techs.Add("ASC-C3b"); h.ComputeMods();
            var s = sim.Separate(h, h.Colonies.Where(c => !c.Home).Take(2).ToList(), h.Drive, peaceful: true);
            SelfTest.Check(h.M.Hive && !s.M.Hive && !s.Knows("ASC-C3b") && s.Banned.ContainsKey("ASC-C3b"), "a Link-Hive splinter forgets the link and bans it");
            SelfTest.Check(s.Colonies.All(c => !c.Holdout), "and its systems are nobody's holdouts");
        }),
        ("vote_never_takes_all", () => {
            var (sim, h) = Calm(); int n = h.Colonies.Count;
            var r = sim.HiveVote(h, 100);
            SelfTest.Check(r.Refused > 0 && r.Refused <= WarData.D("strife", "hive", "maxShare") * n, "even a fierce refusal leaves the hive most of what it had");
        }),
        ("any_split_sets_cooldown", () => {
            var (sim, h) = Calm(); h.NextRising = 0;
            sim.Separate(h, h.Colonies.Where(c => !c.Home).Take(2).ToList(), h.Drive, peaceful: true);
            SelfTest.Check(h.NextRising > sim.Year, "a civ that has just lost systems, however, has years before the next rising");
        }),
        ("holdout_cause_is_hive", () => {
            var (sim, h) = Calm(); h.Techs.Add("S8"); h.ComputeMods();
            var home = h.Colonies.First(c => c.Home);
            var far = h.Colonies.Where(c => !c.Home).OrderByDescending(c => sim.DistanceFactor(c, home)).First(); far.Holdout = true;
            sim.GrievanceTarget(far, out string cause);
            SelfTest.Check(cause == Simulation.CausePhrase["hive"], "a far holdout is in arms against the hive, not the distance");
        }),
        ("foreign_wonders_share_news", () => {
            // a galaxy of many powers: their wonders share one slot in humanity's chronicle (interface.foreignNewsYears)
            var (sim, h) = Calm();
            var aliens = sim.Civs.Where(c => c.Alien && !c.Dead && c.Colonies.Count > 0).Take(2).ToList();
            foreach (var a in aliens) { a.Met.Add(h.Id); h.Met.Add(a.Id); }
            int n = sim.Chronicle.Count;
            foreach (var a in aliens) sim.MoveStar(a.Colonies[0].Star, a.Colonies[0].Star.Pos + new Godot.Vector3(0.1f, 0, 0), a);
            SelfTest.Check(sim.Chronicle.Count == n + 1, "two foreign wonders in a year make one line");
            sim.MoveStar(h.Colonies.First(c => !c.Home).Star, h.Colonies.First(c => !c.Home).Star.Pos + new Godot.Vector3(0.1f, 0, 0), h);
            SelfTest.Check(sim.Chronicle.Count == n + 2, "humanity's own are always told");
            sim.Log(aliens[0].Colonies[0], "battle one", "era", null, party: true); sim.Log(aliens[0].Colonies[0], "battle two", "era", null, party: true);
            SelfTest.Check(sim.Chronicle.Count == n + 4, "and so are humanity's battles in foreign systems");
        }),
        ("hive_tech_calls_vote", () => {
            var (sim, h) = Calm();
            sim.CompleteTech(h, Defs.TechById("S8"));
            SelfTest.Check(h.Used.Contains("hiveVote"), "researching Hive Consensus puts it to the systems");
        }),
        ("movement_counted_once", () => {
            // a movement broken up by troops that climbs back to insurgency is the same movement, not a new one
            var (sim, h) = Calm(); h.SetDrive(h.Drive, "Ruthless");
            var c = h.Colonies.First(x => !x.Home);
            int m0 = sim.StrifeStats.GetValueOrDefault("movements");
            c.Grievance = 70; c.Unrest = 3; sim.Rise(c);
            SelfTest.Check(c.Movement != null && c.Unrest < 3, "the movement is cracked down and drops off insurgency");
            c.Grievance = 70; c.Unrest = 3; sim.Rise(c);
            SelfTest.Check(sim.StrifeStats.GetValueOrDefault("movements") == m0 + 1, $"one movement, counted {sim.StrifeStats.GetValueOrDefault("movements") - m0} times");
            // one that laid down its arms and takes them up again within strife.movementMemoryYears is the same movement too
            var d = h.Colonies.First(x => !x.Home && x != c);
            d.Movement = null; d.QuietSince = sim.Year - 5; d.Grievance = 70; d.Unrest = 3; sim.Rise(d);
            SelfTest.Check(sim.StrifeStats.GetValueOrDefault("movements") == m0 + 1, "a movement back in arms within years is not new");
            d.Movement = null; d.QuietSince = sim.Year - 50; sim.Rise(d);
            SelfTest.Check(sim.StrifeStats.GetValueOrDefault("movements") == m0 + 2, "one a generation later is");
        }),
        ("risings_news_throttled", () => {
            // humanity's risings make one chronicle line per interface.movementNewsYears, and the next one counts the rest
            var (sim, h) = Calm(); h.SetDrive(h.Drive, "Pragmatic");
            var cs = h.Colonies.Where(x => !x.Home).Take(3).ToList();
            if (cs.Count < 3) return;
            int Lines() => sim.Chronicle.Count(e => e.Text.Contains("take up arms"));
            int n0 = Lines();
            foreach (var c in cs.Take(2)) { c.Grievance = 70; c.Unrest = 3; sim.Rise(c); }
            SelfTest.Check(Lines() == n0 + 1, "two risings in a year make one line");
            sim.Warp(WarData.D("interface", "movementNewsYears") + 1);
            cs[2].Movement = null; cs[2].Crackdowns = 0; cs[2].Grievance = 70; cs[2].Unrest = 3; sim.Rise(cs[2]);
            SelfTest.Check(sim.Chronicle.Last(e => e.Text.Contains("take up arms")).Text.Contains("other movement"), "the next line counts the untold ones");
        }),
        ("secession_told", () => {
            // humanity's splits reach the chronicle, although the line is written where the splinter now rules
            foreach (bool peaceful in new[] { true, false })
            {
                var (sim, h) = Calm();
                var bloc = h.Colonies.Where(c => !c.Home).Take(2).ToList();
                var s = sim.Separate(h, bloc, h.Drive, peaceful);
                SelfTest.Check(sim.Chronicle.Any(e => e.Text.Contains(s.Name) && e.Civ == h), $"the {(peaceful ? "peaceful" : "war")} split is in the chronicle");
            }
        }),
        ("sprawl_breeds_grievance", () => {
            // an empire grown past strife.sprawl.from resents its own size, the same for every civ
            var (sim, h) = Calm();
            var c = h.Colonies.First(x => !x.Home);
            SelfTest.Check(sim.Sprawl(h) == 0 && sim.GrievanceCauses(c).All(x => x.Key != "sprawl"), "a small empire does not");
            foreach (var st in sim.Gal.Stars.Where(st => st.Owner < 0 && st.Special == null && !st.Dead).Take((int)WarData.D("strife", "sprawl", "full")).ToList())
                sim.Found(h, st, c, false);
            SelfTest.Check(sim.Sprawl(h) == 1 && sim.GrievanceCauses(c).Any(x => x.Key == "sprawl"), "a vast one does");
        }),
        ("schism_needs_restless_colonies", () => {
            // a close election splits the far colonies off only when they are already restless (strikes or worse), and
            // not within strife.revolt.cooldownYears of the last split; otherwise the losing faction leaves as emigrants
            foreach (var (restless, cooling) in new[] { (true, false), (true, true), (false, false) })
            {
                var (sim, h) = Calm(); h.Founded = sim.Year - 100;
                if (h.Colonies.Count < 8) return;                                  // too few colonies on this seed for a schism
                h.NextRising = cooling ? sim.Year + 40 : 0;
                if (restless) foreach (var c in h.Colonies.Where(c => !c.Home)) { c.Grievance = 50; c.Unrest = 2; }
                var ks = h.Inf.Keys.ToList(); string other = ks.First(k => k != h.Drive);
                foreach (var k in ks) h.Inf[k] = k == h.Drive ? 0.45f : k == other ? 0.44f : 0.05f;
                int civs = sim.Civs.Count; h.NextElection = sim.Year;
                sim.Warp(1);
                bool split = restless && !cooling;
                SelfTest.Check((sim.Civs.Count > civs) == split, split ? "a close vote splits the restless far colonies off"
                    : cooling ? "no schism split while the last one is recent" : "content colonies do not leave over a vote");
            }
        }),
        ("hive_is_one_choice_of_three", () => {
            // Hive Consensus is a spine tech, but its exclusive rivals are as open as it is: none waits on known tier-7 techs
            var (sim, h) = Calm();
            foreach (var id in new[] { "ASC-S3b", "ASC-S3c" })
                SelfTest.Check(Simulation.SpineLike(Defs.TechById(id)) && Simulation.TierOk(h, Defs.TechById(id)), $"{id} opens with the spine");
        }),
        ("overstretched_hive_frays", () => {
            // a hive formed with little dissent holds while it is small, but a vast one frays all the same
            var (sim, h) = Calm(); h.M.Hive = true; h.HiveSwing = 0;
            sim.Warp(30);
            SelfTest.Check(h.Colonies.All(c => !c.Holdout), "a small hive with no swing holds");
            var from = h.Colonies.First(x => !x.Home);
            foreach (var st in sim.Gal.Stars.Where(st => st.Owner < 0 && st.Special == null && !st.Dead).Take((int)WarData.D("strife", "sprawl", "full")).ToList())
                sim.Found(h, st, from, false);
            for (int i = 0; i < 100 && h.Colonies.All(c => !c.Holdout); i++) { h.M.Hive = true; sim.Warp(1); }
            SelfTest.Check(h.Colonies.Any(c => c.Holdout), "a vast hive frays");
        }),
    };
}
