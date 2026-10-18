using System;
using System.Collections.Generic;
using System.Linq;
using Starforge.Sim;

namespace Starforge.Tests;

/// <summary>Stage 3 self-tests: infighting (docs/superpowers/plans/2026-10-19-infighting.md).</summary>
public static class StrifeTests
{
    public static readonly List<(string Name, Action Body)> All = new()
    {
        ("ladder_hysteresis", () => {
            SelfTest.Check(Simulation.Stage(30, 0) == 1 && Simulation.Stage(20, 1) == 1 && Simulation.Stage(17, 1) == 0, "up at 25, down below 18");
            SelfTest.Check(Simulation.Stage(90, 0) == 4, "straight to open revolt");
        }),
        ("levy_breeds_grievance", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
            var c = h.Colonies.First(x => !x.Home); h.LevyOut[c] = 1; c.Autonomy = false; h.M.Hive = false;
            double t0 = sim.GrievanceTarget(c, out _); h.LevyOut[c] = 0;
            SelfTest.Check(t0 > sim.GrievanceTarget(c, out _) + 30 * sim.GovFactor(h), "shipping its industry away is resented");
        }),
        ("hive_feels_none", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; h.M.Hive = true;
            foreach (var c in h.Colonies) c.Grievance = 80;
            sim.Warp(2);
            SelfTest.Check(h.Colonies.All(c => c.Grievance < 1 && c.Unrest == 0), "a hive has no grievance");
        }),
        ("strikes_stop_levy", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
            var c = h.Colonies.First(x => !x.Home); c.Unrest = 2; c.Grievance = 60;
            SelfTest.Check(c.LocalWorksShipped() == 0, "a striking system ships nothing");
        }),
        ("transfer_resets_movement", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; var o = sim.Civs.First(x => x.Alien && !x.Dead);
            var c = h.Colonies.First(x => !x.Home); c.Grievance = 90; c.Unrest = 4; c.Movement = "Free Worlds";
            sim.TransferColony(c, o);
            SelfTest.Check(c.Grievance == 0 && c.Unrest == 0 && c.Movement == null, "a new owner starts the books afresh");
        }),
        ("council_answers", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; h.SetDrive(h.Drive, "Reverent");
            var c = h.Colonies.First(x => !x.Home); h.Materiel = 1e30;
            sim.Rise(c);
            SelfTest.Check(c.Movement != null && c.InvestedUntil > sim.Year, "a Reverent council invests");
        }),
        ("manual_interior_asks", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; sim.SetManual(h, Area.Interior, true);
            var c = h.Colonies.First(x => !x.Home); sim.Rise(c);
            SelfTest.Check(sim.Alerts.Any(a => a.Cat == "Decision" && !a.Done && a.Key == "interior:" + c.Star.Id), "the player is asked");
        }),
        ("self_rule_relieves", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; h.M.Hive = false;
            var c = h.Colonies.First(x => !x.Home); h.LevyOut[c] = 1;
            double before = sim.GrievanceTarget(c, out _); sim.Respond(c, Simulation.Answer.SelfRule);
            SelfTest.Check(sim.GrievanceTarget(c, out _) < before - 15 && c.LocalWorksShipped() == 0, "self-rule calms and keeps its industry home");
        }),
        ("peaceful_separation", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; h.SetDrive(h.Drive, "Reverent");
            var bloc = h.Colonies.Where(c => !c.Home).Take(2).ToList();
            var s = sim.Separate(h, bloc, h.Drive, peaceful: true);
            SelfTest.Check(!h.AtWar.Contains(s.Id) && bloc.All(c => c.Civ == s) && h.Truce[s.Id] > sim.Year, "let go in peace");
        }),
        ("secession_war", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
            var bloc = h.Colonies.Where(c => !c.Home).Take(3).ToList();
            var s = sim.Separate(h, bloc, h.Drive, peaceful: false);
            var w = sim.WarBetween(h, s);
            SelfTest.Check(w != null && w.Reason == "independence" && w.A[0] == h.Id, "the union fights to keep them");
        }),
        ("capital_never_leaves", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
            foreach (var c in h.Colonies) { c.Grievance = 99; c.Unrest = 4; }
            sim.Warp(3);
            SelfTest.Check(h.Colonies.Any(c => c.Home) && !h.Dead, "the capital stays with the union");
            SelfTest.Check(sim.Civs.All(c => c.Colonies.Count(x => x.Home) <= 1), "never two capitals");
        }),
        ("civil_war_terms", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
            var s = sim.Separate(h, h.Colonies.Where(c => !c.Home).Take(2).ToList(), h.Drive, peaceful: false);
            var w = sim.WarBetween(h, s);
            sim.EndWarWith(w, true, new() { WarModel.TermById("autonomy") });
            SelfTest.Check(s.Overlord == h && h.Overlord != s, "the union wins: autonomy within it");
            var s2 = sim.Separate(h, h.Colonies.Where(c => !c.Home).Take(2).ToList(), h.Drive, peaceful: false);
            var w2 = sim.WarBetween(h, s2);
            sim.EndWarWith(w2, false, new() { WarModel.TermById("independence") });
            SelfTest.Check(s2.Overlord == null && !s2.Dead && w2.Ending != null, "the rebels win: free");
        }),
        ("fleets_defect", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
            var bloc = h.Colonies.Where(c => !c.Home).Take(2).ToList(); var f = sim.FleetsOf(h).First();
            f.At = bloc[0].Star.Id; f.To = -1; f.Admiral.Loyalty = 0.3f;
            var s = sim.Separate(h, bloc, h.Drive, peaceful: false);
            SelfTest.Check(f.Civ == s.Id, "a disloyal admiral takes the fleet over");
        }),
        ("no_overlord_cycle", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
            var s = sim.Separate(h, h.Colonies.Where(c => !c.Home).Take(2).ToList(), h.Drive, peaceful: false);
            h.Overlord = s;
            sim.EndWarWith(sim.WarBetween(h, s), true, new() { WarModel.TermById("autonomy") });
            SelfTest.Check(!(s.Overlord == h && h.Overlord == s), "never each other's subject");
        }),
    };
}
