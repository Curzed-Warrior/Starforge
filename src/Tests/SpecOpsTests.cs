using System;
using System.Collections.Generic;
using System.Linq;
using Starforge.Sim;

namespace Starforge.Tests;

/// <summary>Stage 4 self-tests: special forces (docs/superpowers/plans/2026-10-20-special-forces.md).</summary>
public static class SpecOpsTests
{
    /// <summary>A civ that can afford an academy: the agency act, and materiel and manpower to spare.</summary>
    static void Fund(Civ c) { c.M.Acts.Add("spy"); c.Materiel = 1e30; c.Manpower = 1e9; }

    public static readonly List<(string Name, Action Body)> All = new()
    {
        ("peace_trains_best", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; Fund(h);
            sim.Warp(8);
            var t = sim.TeamsOf(h).First();
            SelfTest.Check(!t.Rushed && t.Quality == 1.0f, "a peacetime course is the full course");
        }),
        ("war_rushes_teams", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; Fund(h);
            sim.DeclareWar(h, sim.Civs.First(c => c.Alien && !c.Dead));
            sim.Warp(2.5);
            var t = sim.TeamsOf(h).First();
            SelfTest.Check(t.Rushed && t.Quality == 0.5f, "a wartime team is rushed out and worse");
        }),
        ("course_keeps_its_rule", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; Fund(h);
            sim.Warp(1); int n = h.Academy.Count;
            SelfTest.Check(n > 0, "a course has begun");
            sim.DeclareWar(h, sim.Civs.First(c => c.Alien && !c.Dead));
            sim.Warp(2);
            SelfTest.Check(h.Academy.Take(n).All(t => !t.Rushed) && !sim.TeamsOf(h).Any(t => !t.Rushed), "a peacetime course is not cut short by war");
        }),
        ("few_and_expensive", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; Fund(h);
            sim.Warp(20);
            SelfTest.Check(sim.TeamsOf(h).Count() + h.Academy.Count <= sim.TeamCap(h) && sim.TeamCap(h) <= 8, "never more than the cap");
            h.M.Acts.Remove("spy"); SelfTest.Check(sim.TeamCap(h) == 0, "no agency, no academy");
        }),
        ("dead_civ_teams_go", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; Fund(h);
            sim.Warp(8);
            var s = sim.Separate(h, h.Colonies.Where(c => !c.Home).Take(2).ToList(), h.Drive, peaceful: true);
            SelfTest.Check(!sim.TeamsOf(s).Any(), "a splinter is born with no teams");
            var o = sim.Civs.First(c => c.Alien && !c.Dead && c.Colonies.Count > 0); Fund(o); sim.Warp(8);
            SelfTest.Check(sim.TeamsOf(o).Any() || o.Academy.Count > 0, "the alien trains too");
            sim.Extinct(o);
            SelfTest.Check(!sim.Teams.Any(t => t.Civ == o.Id) && o.Academy.Count == 0, "a dead civ's teams are gone");
        }),
    };
}
