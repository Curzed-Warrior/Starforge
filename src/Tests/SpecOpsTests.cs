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
            // known skills, no guards yet and the lowest-risk mission, so no chance sits at a clamp
            sim.Teams.RemoveAll(x => x.Civ == o.Id); h.M.Spy = o.M.Spy = 1; h.M.Stealth = 1;
            double p0 = sim.CaptureChance(t, o, "sabotage_deposit");
            o.M.Acts.Add("spy"); for (int i = 0; i < 3; i++) sim.Teams.Add(new Team { Id = sim.NextTeamId++, Civ = o.Id, At = o.Colonies[0].Star.Id, Quality = 1 });
            double p1 = sim.CaptureChance(t, o, "sabotage_deposit");
            SelfTest.Check(p1 > p0 * 1.5, "guarding teams catch more spies");
            t.Quality = 0.5f; SelfTest.Check(sim.CaptureChance(t, o, "sabotage_deposit") > p1, "rushed teams are caught more often");
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
            SelfTest.Check(!sim.ActOptions(h).Contains("steal_tech"), "with teams, tech theft is the teams' work");
            // the incite act shifts a whole people's beliefs and the sabotage act burns laboratories: no mission does either
            SelfTest.Check(sim.ActOptions(h).Contains("incite") && sim.ActOptions(h).Contains("sabotage"), "acts with no mission of their own still run");
            sim.Teams.RemoveAll(x => x.Civ == h.Id);
            SelfTest.Check(sim.ActOptions(h).Contains("steal_tech"), "without teams, the old act still runs");
        }),
        ("war_aims_at_the_front", () => {
            // aliens are centuries away at sublight; a secession war is next door
            var (sim, h, _, t) = Ready();
            var s = sim.Separate(h, h.Colonies.Where(x => !x.Home).Take(3).ToList(), h.Drive, peaceful: false);
            h.Met.Add(s.Id); s.Met.Add(h.Id);
            var c = s.Colonies.FirstOrDefault(x => sim.CanTarget(h, x, "prepare_invasion"));
            SelfTest.Check(c != null && h.AtWar.Contains(s.Id), "a reachable enemy world");
            var f = sim.FleetsOf(h).First(); f.Target = c.Star.Id;
            var pick = sim.ChooseMission(h, t);
            SelfTest.Check(pick?.Mission is "prepare_invasion" or "assassinate" or "sabotage_yard", $"at war, teams serve the war (got {pick?.Mission ?? "none"})");
        }),
        ("reverent_never_assassinate", () => {
            var (sim, h, o, t) = Ready(); h.SetDrive(h.Drive, "Reverent"); sim.DeclareWar(h, o);
            for (int i = 0; i < 20; i++) SelfTest.Check(sim.ChooseMission(h, t)?.Mission != "assassinate", "a Reverent council will not order a killing");
        }),
        ("manual_intel_asks", () => {
            var (sim, h, o, t) = Ready(); sim.SetManual(h, Area.Intelligence, true);
            sim.OfferTeam(h, t);
            SelfTest.Check(sim.Alerts.Any(a => a.Cat == "Decision" && !a.Done && a.Key == "intel:" + t.Id) && t.Mission == "guard", "the player chooses");
        }),
        ("switch_player_teams", () => {
            var (sim, h, o, t) = Ready(); sim.SetManual(h, Area.Intelligence, true); sim.OfferTeam(h, t);
            var s = sim.Separate(h, h.Colonies.Where(c => !c.Home).Take(2).ToList(), h.Drive, peaceful: true);
            sim.SwitchPlayer(s);
            SelfTest.Check(t.Civ == h.Id && !sim.TeamsOf(s).Any() && sim.Alerts.Where(a => a.Key?.StartsWith("intel:") == true).All(a => a.Done), "teams stay with the old civ");
        }),
        ("specops_saved", () => {
            var (sim, h, o, t) = Ready(); var c = o.Colonies.First(x => !x.Home);
            sim.Dispatch(t, c, "incite"); t.Xp = 0.3f; h.Academy.Add(new Training { Troops = 1, Left = 2, Rushed = true, At = h.Colonies[0].Star.Id });
            c.PreparedBy = h.Id; c.PreparedUntil = 2500; c.YardDownUntil = 2490; c.DepositsDownUntil = 2480; c.IncitedUntil = 2470;
            SaveGame.Save(sim, 20610, "user://selftest_specops.json");
            var (s2, _) = SaveGame.Load("user://selftest_specops.json");
            var t2 = s2.Teams.First(x => x.Id == t.Id); var c2 = s2.Civs[o.Id].Colonies.First(x => x.Star.Id == c.Star.Id);
            SelfTest.Check(t2.Mission == "incite" && t2.Target == c.Star.Id && t2.Xp == 0.3f && t2.To == t.To && s2.Humanity.Academy.Count == h.Academy.Count
                && s2.NextTeamId == sim.NextTeamId, "teams and the academy round-trip");
            SelfTest.Check(c2.PreparedBy == h.Id && c2.PreparedUntil == 2500 && c2.YardDownUntil == 2490 && c2.DepositsDownUntil == 2480 && c2.IncitedUntil == 2470, "their work round-trips");
        }),
        ("third_party_cede_aborts", () => {
            // two splinters next door, so the target is within reach whoever holds it
            var (sim, h, _, t) = Ready(); h.M.Hive = false;
            var a = sim.Separate(h, h.Colonies.Where(x => !x.Home).Take(2).ToList(), h.Drive, peaceful: true);
            var b = sim.Separate(h, h.Colonies.Where(x => !x.Home).Take(2).ToList(), h.Drive, peaceful: true);
            a.M.Hive = b.M.Hive = false;
            foreach (var x in new[] { a, b }) { h.Met.Add(x.Id); x.Met.Add(h.Id); }
            var c = a.Colonies.First(); a.Relations[h.Id] = h.Relations[a.Id] = -50;
            SelfTest.Check(sim.CanTarget(h, c, "incite"), "the first holder is a fair target");
            sim.Dispatch(t, c, "incite"); t.To = -1; t.At = c.Star.Id; t.Due = sim.Year;     // already on site
            sim.TransferColony(c, b);
            SelfTest.Check(sim.CanTarget(h, c, "incite"), "the new holder would be a target too");
            sim.Warp(1);
            SelfTest.Check(t.Mission == "guard" && c.IncitedUntil <= sim.Year, "a world that changed hands is not hit for its new owner");
        }),
        ("peace_ends_war_missions", () => {
            var (sim, h, o, t) = Ready(); var c = o.Colonies.First(x => sim.SlipsOf(x) > 0);
            var f = sim.FleetsOf(o).First(); f.At = c.Star.Id; f.To = -1;
            h.AtWar.Remove(o.Id); o.AtWar.Remove(h.Id);
            SelfTest.Check(!sim.Fits(h, c, o, "sabotage_yard") && !sim.Fits(h, c, o, "assassinate"), "no yard sabotage or killing in peace");
            h.AtWar.Add(o.Id); o.AtWar.Add(h.Id);
            SelfTest.Check(sim.Fits(h, c, o, "sabotage_yard") && sim.Fits(h, c, o, "assassinate"), "at war, both");
        }),
        ("no_self_sabotage", () => {
            var (sim, h, o, t) = Ready(); sim.DeclareWar(h, o);
            var mine = h.Colonies.First(x => !x.Home && sim.SlipsOf(x) > 0 || x.Res.Strategic.Count > 0 && !x.Home); mine.OccupiedBy = o.Id;
            SelfTest.Check(!sim.Fits(h, mine, o, "sabotage_yard") && !sim.Fits(h, mine, o, "sabotage_deposit"), "a council does not wreck its own occupied world");
            SelfTest.Check(sim.Fits(h, mine, o, "prepare_invasion"), "but it prepares its liberation");
        }),
    };
}
