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
            // the full levy weight, felt through the government (less what clamping at 0 hides)
            SelfTest.Check(t0 > sim.GrievanceTarget(c, out _) + 0.5 * WarData.D("strife", "causes", "levy") * sim.GovFactor(h), "shipping its industry away is resented");
        }),
        ("hive_feels_none", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; h.Techs.Add("S8"); h.ComputeMods();
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
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; int humans0 = sim.Civs.Count(c => c.Human);
            for (int i = 0; i < 30; i++)
            {
                h.M.Hive = false;
                foreach (var c in h.Colonies) { c.Grievance = 99; c.Unrest = 4; }
                sim.Warp(1);
            }
            SelfTest.Check(sim.Civs.Count(c => c.Human) > humans0, "systems in revolt did rise");
            SelfTest.Check(h.Colonies.Any(c => c.Home) && !h.Dead, "the capital stays with the union");
            SelfTest.Check(sim.Civs.All(c => c.Colonies.Count(x => x.Home) <= 1), "never two capitals");
        }),
        ("splinter_capital_stays", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
            var s = sim.Separate(h, h.Colonies.Where(c => !c.Home).Take(4).ToList(), h.Drive, peaceful: true);
            var cap = s.Colonies[0];
            sim.SwitchPlayer(s);
            var t = sim.Separate(s, s.Colonies.ToList(), s.Drive, peaceful: true);
            SelfTest.Check(cap.Civ == s && t != null && !t.Colonies.Contains(cap), "a splinter's capital never leaves it either");
            SelfTest.Check(sim.HomeColony.Civ == sim.Humanity, "the player's home system is their own");
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
        ("mars_can_rise", () => {
            var sim = SelfTest.NewSim(); sim.Warp(150); var h = sim.Humanity; var home = h.Colonies.First(c => c.Home);
            var mars = home.Settled.Keys.FirstOrDefault(b => b.Name == "Mars");
            if (mars == null) { home.Settled[home.Sys.Find("Mars")] = 1f; mars = home.Sys.Find("Mars"); }
            home.WorldGrievance[mars] = 95; home.WorldUnrest[mars] = 4; home.Rival = null; home.Crisis = null; h.M.Hive = false;
            for (int i = 0; i < 40 && home.Rival == null; i++) { home.WorldGrievance[mars] = 95; home.WorldUnrest[mars] = 4; sim.Warp(1); }
            SelfTest.Check(home.Rival != null && home.Rival.Planet == mars, "a Mars that feels used can rise");
        }),
        ("far_worlds_resent_more", () => {
            var sim = SelfTest.NewSim(); sim.Warp(300); var home = sim.Humanity.Colonies.First(c => c.Home);
            var moon = home.Sys.Find("Moon"); var titan = home.Sys.Find("Titan");
            // distance alone: no mining on either, nothing that soothes the whole system, a government that feels it
            home.Hubs.Remove(moon); home.Hubs.Remove(titan); home.ElevatorP = home.RingP = -1; sim.Humanity.Faith = 0; sim.Humanity.M.Hive = false;
            SelfTest.Check(sim.WorldTarget(home, titan) > sim.WorldTarget(home, moon), "Titan feels further from Earth than the Moon");
        }),
        ("admiral_coup", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
            var w = sim.DeclareWar(h, o); w.ExhA = 80;
            var f = sim.FleetsOf(h).First(); f.Admiral.Wins = 5; f.Admiral.Loyalty = 0.2f; f.Admiral.Trait = "Ambitious";
            string name = f.Admiral.Name;
            for (int i = 0; i < 60 && h.JuntaUntil <= sim.Year; i++) { w.ExhA = 80; sim.Warp(1); }
            SelfTest.Check(h.JuntaUntil > sim.Year && h.Leader.Name == name, "a victorious, ambitious admiral takes the council");
        }),
        ("no_double_junta", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; h.JuntaUntil = sim.Year + 60; string lead = h.Leader.Name;
            var o = sim.Civs.First(c => c.Alien); var w = sim.DeclareWar(h, o);
            for (int i = 0; i < 40; i++)
            {
                w.ExhA = 80;
                foreach (var f in sim.FleetsOf(h)) { f.Admiral.Wins = 9; f.Admiral.Loyalty = 0; }
                sim.Warp(1);
                if (!w.Open) break;
            }
            SelfTest.Check(h.Leader.Name == lead || h.Leader.Since < 2064, "a junta is not overthrown by its own admirals");
            SelfTest.Check(sim.StrifeStats.GetValueOrDefault("coups") == 0, "no coup inside a junta");
        }),
        ("junta_holds_no_election", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; h.JuntaUntil = sim.Year + 30;
            string lead = h.Leader.Name, d0 = h.Drive, other = Defs.Drives.Keys.First(k => k != d0);
            foreach (var k in h.Inf.Keys.ToList()) h.Inf[k] = k == other ? 0.9f : 0.05f;
            h.NextElection = sim.Year;
            sim.Warp(2);
            SelfTest.Check(h.Drive == d0 && h.Leader.Name == lead, "a junta rules by decree: no elections");
        }),
        ("revolution", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; string d0 = h.Drive;
            foreach (var c in h.Colonies.Where(c => !c.Home)) { c.Grievance = 70; c.Unrest = 3; c.Movement = "x"; }
            // kept in insurgency every year, so only a revolution can end it (the yearly pull alone would calm them)
            for (int i = 0; i < 40 && h.Drive == d0 && sim.StrifeStats.GetValueOrDefault("revolutions") == 0; i++)
            {
                h.M.Hive = false;
                foreach (var c in h.Colonies.Where(c => !c.Home)) { c.Grievance = 70; c.Unrest = 3; c.Movement ??= "x"; }
                sim.Warp(1);
            }
            SelfTest.Check(h.Drive != d0 || sim.StrifeStats.GetValueOrDefault("revolutions") > 0, "a people pushed too far overturns its council");
        }),
        ("switch_player_clean", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; sim.SetManual(h, Area.Research, true);
            var s = sim.Separate(h, h.Colonies.Where(c => !c.Home).Take(2).ToList(), h.Drive, peaceful: true);
            SelfTest.Check(sim.Alerts.Any(a => a.Cat == "Decision" && !a.Done && a.Key == "lead:" + s.Id), "the player is offered the splinter");
            sim.Raise("Decision", "test", "test", h.Colonies[0], "k", new() { ("ok", () => { }) });
            sim.SwitchPlayer(s);
            SelfTest.Check(sim.Humanity == s && s.Main && !h.Main && s.Policy.IsManual(Area.Research), "the player leads the splinter, with their policies");
            SelfTest.Check(sim.Alerts.All(a => a.Done), "the old civ's decisions are closed");
            SelfTest.Check(sim.HomeColony.Civ == s && sim.Focus.Civ == s && !h.Policy.IsManual(Area.Research), "the view follows; the old civ is the council's");
            sim.Warp(2);
            SelfTest.Check(sim.Civs.Count(c => c.Main) == 1, "one player");
        }),
        ("strife_saved", () => {
            var sim = SelfTest.NewSim(); sim.Warp(150); var h = sim.Humanity; var home = h.Colonies.First(c => c.Home);
            var b = home.Sys.Find("Mars"); home.WorldGrievance[b] = 42; home.WorldUnrest[b] = 2; h.JuntaUntil = 2300;
            var c = h.Colonies.FirstOrDefault(x => !x.Home) ?? home; c.Grievance = 33; c.Unrest = 1; c.Movement = "m"; c.Autonomy = true; c.InvestedUntil = 2400; c.Cause = "why";
            SaveGame.Save(sim, 20610, "user://selftest_strife.json");
            var (s2, _) = SaveGame.Load("user://selftest_strife.json");
            var h2 = s2.Humanity; var home2 = h2.Colonies.First(x => x.Home); var c2 = h2.Colonies.First(x => x.Star.Id == c.Star.Id);
            SelfTest.Check(home2.WorldGrievance[home2.Sys.Find("Mars")] == 42 && home2.WorldUnrest[home2.Sys.Find("Mars")] == 2 && h2.JuntaUntil == 2300, "worlds and the junta round-trip");
            SelfTest.Check(c2.Grievance == 33 && c2.Unrest == 1 && c2.Movement == "m" && c2.Autonomy && c2.InvestedUntil == 2400 && c2.Cause == "why", "a system's strife round-trips");
        }),
        ("switched_save_loads", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
            var s = sim.Separate(h, h.Colonies.Where(c => !c.Home).Take(2).ToList(), h.Drive, peaceful: true);
            sim.SwitchPlayer(s);
            SaveGame.Save(sim, 20610, "user://selftest_switch.json");
            var (s2, _) = SaveGame.Load("user://selftest_switch.json");
            SelfTest.Check(s2.Humanity.Id == s.Id && s2.HomeColony != null && s2.HomeColony.Civ == s2.Humanity, "a game led from a splinter loads");
            s2.Warp(1);
        }),
        ("crackdown_heard_once_a_year", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
            var cs = h.Colonies.Where(x => !x.Home).Take(3).ToList(); cs[2].Grievance = 10;
            sim.Respond(cs[0], Simulation.Answer.Crackdown); sim.Respond(cs[1], Simulation.Answer.Crackdown);
            SelfTest.Check(Math.Abs(cs[2].Grievance - 10 - WarData.D("strife", "answers", "crackdownSpread")) < 1e-3, "word of crackdowns spreads once a year, however many there are");
        }),
        ("crackdown_word_thins_in_a_vast_empire", () => {
            // up to strife.answers.crackdownSpreadFull systems every other one hears the full spread; beyond, it thins as
            // the square root of the size, so one crackdown cannot set a vast empire alight
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
            double full = WarData.D("strife", "answers", "crackdownSpread"), at = WarData.D("strife", "answers", "crackdownSpreadFull");
            SelfTest.Check(h.Colonies.Count <= at && Math.Abs(sim.CrackdownSpread(h) - full) < 1e-9, "a small empire hears it all");
            var from = h.Colonies.First(x => !x.Home);
            foreach (var st in sim.Gal.Stars.Where(st => st.Owner < 0 && st.Special == null && !st.Dead).Take(400).ToList()) sim.Found(h, st, from, false);
            int n = h.Colonies.Count;
            SelfTest.Check(Math.Abs(sim.CrackdownSpread(h) - full * Math.Sqrt(at / n)) < 1e-9, $"{n} systems: {sim.CrackdownSpread(h):0.##}");
            var cs = h.Colonies.Where(x => !x.Home).Take(2).ToList(); cs[1].Grievance = 10;
            sim.Respond(cs[0], Simulation.Answer.Crackdown);
            SelfTest.Check(Math.Abs(cs[1].Grievance - 10 - sim.CrackdownSpread(h)) < 1e-3, "and that is what each system gains");
        }),
        ("crackdown_not_every_year", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; h.M.Hive = false; h.SetDrive(h.Drive, "Ruthless");
            var c = h.Colonies.First(x => !x.Home);
            c.Grievance = 70; c.Unrest = 3; c.Movement = "x"; c.CauseKey = "levy";
            sim.Respond(c, Simulation.Answer.Crackdown);
            SelfTest.Check(sim.Year + 4 < h.Policy.AskAfter.GetValueOrDefault("interior:" + c.Star.Id), "a crushed movement is next answered years later");
        }),
        ("lifetime_away", () => {
            var sim = SelfTest.NewSim(); var h = sim.Humanity; var home = h.Colonies[0];
            var star = sim.Gal.Stars.Where(x => x.Owner < 0 && x.Pos.DistanceTo(home.Star.Pos) * Galaxy.LyPerUnit is > 8 and < 20).First();
            var c = sim.Found(h, star, home, false); h.M.ShipSpeed = 0.1f; h.M.Fold = false;
            SelfTest.Check(sim.DistanceFactor(c, home) > 0.9, "a colony a century away by sublight ship is a lifetime away");
            h.M.Fold = true;
            SelfTest.Check(sim.DistanceFactor(c, home) < 0.1, "with fold drives the same colony is close");
        }),
        ("reformed_movement_waits", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; h.SetDrive(h.Drive, "Ruthless");
            var c = h.Colonies.First(x => !x.Home); c.Grievance = 70; c.Unrest = 3; c.Movement = "x";
            sim.Respond(c, Simulation.Answer.Crackdown);
            c.Grievance = 70; c.Movement = null; sim.Rise(c);
            SelfTest.Check(c.Grievance == 70, "a movement that re-forms within five years of the last answer is not answered again yet");
        }),
        ("revolution_not_yearly", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
            int n = 0; double last = 0;
            for (int i = 0; i < 40; i++)
            {
                h.M.Hive = false;
                foreach (var c in h.Colonies.Where(c => !c.Home)) { c.Grievance = 70; c.Unrest = 3; c.Movement ??= "x"; }
                sim.Warp(1);
                double at = h.ActAt.GetValueOrDefault("strife:revolution");
                if (at != last) { n++; last = at; }
            }
            SelfTest.Check(n >= 1 && n <= 40 / WarData.D("strife", "revolution", "cooldownYears") + 1, "a new council is given time before the next revolution");
        }),
        ("risings_scale_with_civs_not_systems", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400);
            var big = sim.Civs.Where(c => c.Alien && !c.Dead).OrderByDescending(c => c.Colonies.Count).First();
            int risings = 0;
            for (int i = 0; i < 10; i++)
            {
                big.M.Hive = false;
                foreach (var c in big.Colonies.Where(c => !c.Home)) { c.Grievance = 99; c.Unrest = 4; }
                int before = sim.Civs.Count; sim.Warp(1); if (sim.Civs.Count > before && sim.Civs.Skip(before).Any(c => c.Species == big.Species)) risings++;
            }
            // one roll a year for the whole civ (chancePerYear), however many of its systems are in revolt
            SelfTest.Check(risings <= 5, $"risings are a civ's event, not every system's ({risings} in 10 years, {big.Colonies.Count} systems)");
        }),
        ("rebels_take_their_share", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
            double ships0 = sim.FleetsOf(h).Sum(f => f.Ships);
            var bloc = h.Colonies.Where(c => !c.Home).OrderByDescending(c => c.Pop).Take(3).ToList();
            double share = bloc.Sum(c => c.Pop) / h.Pop;
            var s = sim.Separate(h, bloc, h.Drive, peaceful: false);
            double got = sim.FleetsOf(s).Sum(f => f.Ships);
            SelfTest.Check(ships0 > 0 && got > 0 && Math.Abs(got + sim.FleetsOf(h).Sum(f => f.Ships) - ships0) < 1e-6 * ships0, "the crews from the bloc's worlds take their ships with them");
            SelfTest.Check(sim.FleetsOf(s).All(f => f.At == bloc[0].Star.Id && f.Admiral != null), "and gather at the rising's first system under their own admiral");
        }),
        ("player_rehomed_on_extinction", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
            var s = sim.Separate(h, h.Colonies.Where(c => !c.Home).Take(2).ToList(), h.Drive, peaceful: false);
            sim.SwitchPlayer(s);
            sim.EndWarWith(sim.WarBetween(h, s), true, new() { WarModel.TermById("reunion") });
            SelfTest.Check(s.Dead && sim.Humanity == h && h.Main && !s.Main && sim.HomeColony.Civ == h, "a player whose polity is gone leads the human polity that remains");
        }),
        ("occupied_never_rises", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien && !c.Dead);
            var cs = h.Colonies.Where(c => !c.Home).Take(2).ToList(); cs[0].OccupiedBy = o.Id;
            var s = sim.Separate(h, cs, h.Drive, peaceful: true);
            SelfTest.Check(cs[0].Civ == h && cs[0].OccupiedBy == o.Id && cs[1].Civ == s, "a system under occupation cannot rise; the occupier keeps it");
        }),
        ("independence_terms_side_aware", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
            var s = sim.Separate(h, h.Colonies.Where(c => !c.Home).Take(2).ToList(), h.Drive, peaceful: false);
            var w = sim.WarBetween(h, s);
            string T((bool aWins, List<Term> t) r) => (r.aWins ? "A:" : "B:") + string.Join("+", r.t.Select(x => x.Id));
            SelfTest.Check(T(sim.IndependenceTerms(w, 90, 0, 100, false)) == "A:reunion", "a crushed rebellion returns to the union");
            SelfTest.Check(T(sim.IndependenceTerms(w, 30, 0, 50, false)) == "A:autonomy", "a beaten rebellion gets autonomy");
            sim.Colonies.First(c => c.Civ == s).OccupiedBy = h.Id;
            SelfTest.Check(T(sim.IndependenceTerms(w, 40, 0, 40, false)) == "A:partition", "holding rebel systems, the union partitions");
            SelfTest.Check(T(sim.IndependenceTerms(w, -40, 100, 0, false)) == "B:independence", "the rebels winning are free");
        }),
    };
}
