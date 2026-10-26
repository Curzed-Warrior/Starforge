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
            var (sim, h) = Calm(); h.M.Hive = true;
            var hold = h.Colonies.First(c => !c.Home); hold.Holdout = true;
            var linked = h.Colonies.First(c => !c.Home && c != hold); linked.Grievance = 80;
            sim.Warp(2);
            SelfTest.Check(hold.Grievance > 10 && hold.CauseKey == "hive", "a holdout resents the hive");
            SelfTest.Check(linked.Grievance < 1, "a linked system feels nothing");
        }),
        ("rising_pulls_insurgents", () => {
            var (sim, h) = Calm(); h.SetDrive(h.Drive, "Ruthless");
            var cap = h.Colonies.First(c => c.Home);
            var rest = h.Colonies.Where(c => !c.Home).ToList();
            // a third of the empire in insurgency over the levy, one of them in open revolt
            var insurgent = rest.Take(Math.Max(3, rest.Count / 3)).ToList();
            foreach (var c in insurgent) { c.Unrest = 3; c.Grievance = 70; c.CauseKey = "levy"; }
            insurgent[0].Unrest = 4; insurgent[0].Grievance = 95;
            var s = sim.RevoltNow(h);
            SelfTest.Check(s != null && s.Colonies.Count >= insurgent.Count / 2, "the rising takes insurgent systems that share its cause");
            SelfTest.Check(sim.StrifeStats.GetValueOrDefault("civilwars") == 1, "and it is a civil war");
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
        ("hive_tech_calls_vote", () => {
            var (sim, h) = Calm();
            sim.CompleteTech(h, Defs.TechById("S8"));
            SelfTest.Check(h.Used.Contains("hiveVote"), "researching Hive Consensus puts it to the systems");
        }),
    };
}
