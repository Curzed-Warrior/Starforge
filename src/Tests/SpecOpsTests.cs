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

    /// <summary>
    /// Humanity at year 400 with every mission's act and one fully trained team at its capital, and a met alien civ that
    /// has a yard, strategic deposits, a fleet and more than one colony.
    /// </summary>
    public static (Simulation sim, Civ h, Civ o, Team t) Ready()
    {
        var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; Fund(h);
        foreach (var a in new[] { "sabotage", "incite", "steal_tech" }) h.M.Acts.Add(a);
        var o = sim.Civs.Where(c => c.Alien && !c.Dead && c.Colonies.Count > 1 && sim.FleetsOf(c).Any()
                                    && c.Colonies.Any(x => x.Res.Strategic.Count > 0) && c.Colonies.Any(x => sim.SlipsOf(x) > 0))
                        .OrderBy(c => c.Colonies[0].Star.Pos.DistanceTo(h.Colonies[0].Star.Pos)).First();
        h.Met.Add(o.Id); o.Met.Add(h.Id);
        var t = new Team { Id = sim.NextTeamId++, Civ = h.Id, At = h.Colonies.First(x => x.Home).Star.Id, Quality = 1, Name = "Test Group" };
        sim.Teams.Add(t);
        return (sim, h, o, t);
    }

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
        ("sabotage_yard_lands", () => {
            var (sim, h, o, t) = Ready(); var c = o.Colonies.First(x => sim.SlipsOf(x) > 0);
            sim.Succeed(t, c, "sabotage_yard");
            SelfTest.Check(sim.WorkingSlips(c) == 0 && sim.SlipsOf(c) > 0 && c.YardDownUntil > sim.Year, "a sabotaged yard builds nothing for years");
        }),
        ("sabotage_deposit_lands", () => {
            var (sim, h, o, t) = Ready(); var c = o.Colonies.First(x => x.Res.Strategic.Count > 0);
            var only = c.Res.Strategic.Where(x => !o.Colonies.Where(y => y != c).Any(y => y.Res.Strategic.Contains(x))).ToList();
            sim.Succeed(t, c, "sabotage_deposit"); sim.Warp(1.1);
            SelfTest.Check(c.DepositsDownUntil > sim.Year && only.All(x => !o.Strategic.Contains(x)), "deposits held nowhere else stop feeding the empire");
        }),
        ("assassinate_replaces_admiral", () => {
            var (sim, h, o, t) = Ready(); var f = sim.FleetsOf(o).First(); var c = o.Colonies.First(); f.At = c.Star.Id; f.To = -1;
            string before = f.Admiral.Name; f.Admiral.Wins = 7;
            sim.Succeed(t, c, "assassinate");
            SelfTest.Check(f.Admiral.Name != before && f.Admiral.Wins == 0, "the admiral is dead; a green one takes over");
        }),
        ("incite_feeds_unrest", () => {
            var (sim, h, o, t) = Ready(); o.M.Hive = false; var c = o.Colonies.First(x => !x.Home);
            double t0 = sim.GrievanceTarget(c, out _); sim.Succeed(t, c, "incite");
            SelfTest.Check(sim.GrievanceTarget(c, out var cause) >= Math.Min(100, t0 + 25 * sim.GovFactor(o)) - 1e-9 && cause != null, "incitement feeds stage 3's grievance");
            SelfTest.Check(c.IncitedUntil > sim.Year, "for years");
        }),
        ("prepare_weakens_garrison", () => {
            var (sim, h, o, t) = Ready(); var c = o.Colonies.First(x => !x.Home); sim.DeclareWar(h, o);
            double g0 = sim.GarrisonStrength(c, h); sim.Succeed(t, c, "prepare_invasion");
            SelfTest.Check(g0 > 0 && sim.GarrisonStrength(c, h) < g0 * 0.6 && sim.GarrisonStrength(c, null) == sim.GarrisonStrength(c), "weaker only against the civ that prepared it");
        }),
        ("capture_is_an_incident", () => {
            var (sim, h, o, t) = Ready(); float r0 = o.Relations.GetValueOrDefault(h.Id);
            sim.Captured(t, o);
            SelfTest.Check(!sim.Teams.Contains(t) && o.Relations[h.Id] <= Math.Max(-100, r0 - 24) && sim.Alerts.Any(a => a.Cat == "Incident"), "a captured team is a diplomatic incident");
        }),
        ("counter_intel_defends", () => {
            var (sim, h, o, t) = Ready();
            // known skills and no guards yet, so neither chance sits at a clamp
            sim.Teams.RemoveAll(x => x.Civ == o.Id); h.M.Spy = o.M.Spy = 1; h.M.Stealth = 1;
            double p0 = sim.CaptureChance(t, o, "steal_tech");
            o.M.Acts.Add("spy"); for (int i = 0; i < 3; i++) sim.Teams.Add(new Team { Id = sim.NextTeamId++, Civ = o.Id, At = o.Colonies[0].Star.Id, Quality = 1 });
            double p1 = sim.CaptureChance(t, o, "steal_tech");
            SelfTest.Check(p1 > p0 * 1.5, "guarding teams catch more spies");
            t.Quality = 0.5f; SelfTest.Check(sim.CaptureChance(t, o, "steal_tech") > p1, "rushed teams are caught more often");
        }),
        ("target_changes_hands", () => {
            var (sim, h, o, t) = Ready(); var c = o.Colonies.First(x => !x.Home);
            sim.Dispatch(t, c, "incite"); sim.TransferColony(c, h);
            for (int i = 0; i < 40 && t.Mission != "guard"; i++) sim.Warp(1);
            SelfTest.Check(t.Mission == "guard" && c.IncitedUntil <= sim.Year, $"the team aborts and nothing lands on the new owner ({t.Mission} to={t.To} arrive={t.Arrive:0} year={sim.Year:0})");
        }),
        ("no_admiral_no_mission", () => {
            var (sim, h, o, t) = Ready(); var c = o.Colonies.First(x => !x.Home);
            foreach (var f in sim.Fleets.Where(f => f.At == c.Star.Id)) { f.At = o.Colonies.First(x => x != c).Star.Id; f.To = -1; }
            SelfTest.Check(!sim.CanTarget(h, c, "assassinate"), "nobody to kill, no mission");
        }),
        ("acts_not_doubled", () => {
            var (sim, h, o, t) = Ready(); h.ActAt.Clear();
            SelfTest.Check(!sim.ActOptions(h).Contains("steal_tech") && !sim.ActOptions(h).Contains("incite"), "with teams, the acts give way to the missions");
            sim.Teams.RemoveAll(x => x.Civ == h.Id);
            SelfTest.Check(sim.ActOptions(h).Contains("steal_tech") && sim.ActOptions(h).Contains("incite"), "without teams, the old acts still run");
        }),
    };
}
