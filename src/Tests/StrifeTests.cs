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
    };
}
