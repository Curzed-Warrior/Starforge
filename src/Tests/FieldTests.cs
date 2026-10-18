using System;
using System.Collections.Generic;
using System.Linq;
using Starforge.Sim;

namespace Starforge.Tests;

/// <summary>Stage 2 self-tests: war in the field (docs/superpowers/plans/2026-10-19-war-in-the-field.md).</summary>
public static class FieldTests
{
    /// <summary>Two civs with the same weapons, defences and fleet doctrine, so a battle test measures numbers and command only.</summary>
    static void Even(Civ a, Civ b) { foreach (var c in new[] { a, b }) { c.M.Arms.Clear(); c.M.Guard.Clear(); c.M.Fleet = 1; } }

    public static readonly List<(string Name, Action Body)> All = new()
    {
        ("war_has_reason", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
            var w = sim.DeclareWar(h, o);
            SelfTest.Check(w != null && WarModel.Reasons.Any(r => r.Id == w.Reason) && h.AtWar.Contains(o.Id) && sim.WarBetween(o, h) == w, "a war with a reason, both ways");
            SelfTest.Check(w.Claims.All(id => o.Colonies.Any(c => c.Star.Id == id)), "claims are the defender's systems");
        }),
        ("reconcile_atwar", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
            h.AtWar.Add(o.Id); o.AtWar.Add(h.Id); sim.Warp(0.5);
            SelfTest.Check(sim.WarBetween(h, o) != null, "a war started elsewhere gets a record");
            h.AtWar.Remove(o.Id); o.AtWar.Remove(h.Id); sim.Warp(0.5);
            SelfTest.Check(sim.WarBetween(h, o) == null, "and loses it when the war is ended elsewhere");
        }),
        ("truce_blocks", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
            var w = sim.DeclareWar(h, o); sim.EndWarWith(w, true, new());
            SelfTest.Check(sim.DeclareWar(h, o) == null && h.Truce[o.Id] > sim.Year, "no new war during the truce");
        }),
        ("fleet_travels", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var f = sim.FleetsOf(h).First();
            sim.SetManual(h, Area.Fleets, true);
            var to = sim.Gal.Stars[sim.Gal.Sol.Near[3]];
            double years = sim.TravelYears(h, sim.Gal.Stars[f.At], to);
            sim.Order(f, to.Id, "attack");
            SelfTest.Check(f.Moving && Math.Abs(f.Arrive - f.Depart - years) < 1e-6, "travel takes the drive's time");
            sim.Warp(years + 0.6);
            SelfTest.Check(!f.Moving && f.At == to.Id, $"and arrives ({years:0.0} years)");
        }),
        ("supply_drops_far", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var f = sim.FleetsOf(h).First();
            sim.SetManual(h, Area.Fleets, true);
            var far = sim.Gal.Stars.OrderByDescending(s => s.Pos.DistanceTo(sim.Gal.Sol.Pos)).First();
            f.At = far.Id; sim.Warp(2);
            SelfTest.Check(f.Supply < 0.6f, "out of supply far from home");
        }),
        ("manual_fleets_kept", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
            sim.SetManual(h, Area.Fleets, true); sim.DeclareWar(o, h);
            var f = sim.FleetsOf(h).First(); int at = f.At; sim.Warp(3);
            SelfTest.Check(f.At == at && !f.Moving, "the council does not move the player's fleets");
        }),
        ("orders_survive_transfer", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien && c.Colonies.Count > 1);
            sim.DeclareWar(h, o); var f = sim.FleetsOf(h).First();
            var target = o.Colonies.Last(); sim.Order(f, target.Star.Id, "attack");
            sim.TransferColony(target, h); sim.Warp(1);
            SelfTest.Check(target.Civ == h && target.OccupiedBy < 0 && (f.Target != target.Star.Id || f.Mission != "blockade"), "our side never besieges or occupies a system it now holds (it may stage there)");
            SelfTest.Check(sim.Armies.All(a => a.Fleet < 0 || sim.Fleets.Any(x => x.Id == a.Fleet)), "no army aboard a missing fleet");
        }),
        ("counters_matter", () => {
            var sim = SelfTest.NewSim(); var a = sim.Humanity; var b = sim.Civs.First(c => c.Alien);
            a.M.Arms.Clear(); a.M.Arms["kinetic"] = 4; b.M.Guard.Clear();
            double open = Battles.HullDamage(a, b); b.M.Guard["armor"] = 4;
            SelfTest.Check(Battles.HullDamage(a, b) < open * 0.75, "armour stops kinetics");
            b.M.Guard.Clear(); b.M.Guard["shield"] = 4;
            SelfTest.Check(Math.Abs(Battles.HullDamage(a, b) - open) < 1e-9, "shields do not");
        }),
        ("bigger_navy_wins", () => {
            var sim = SelfTest.NewSim(); var a = sim.Humanity; var b = sim.Civs.First(c => c.Alien);
            var fa = new Fleet { Civ = a.Id, Admiral = new Admiral { Skill = 1, Trait = "Tactician" } }; fa.Add(2, 300, 0f);
            var fb = new Fleet { Civ = b.Id, Admiral = new Admiral { Skill = 1, Trait = "Tactician" } }; fb.Add(2, 100, 0f);
            Even(a, b);
            var r = Battles.Resolve(sim, new BattleSide { Civ = a, Fleets = { fa } }, new BattleSide { Civ = b, Fleets = { fb } });
            SelfTest.Check(r.AWins && r.LostB > r.LostA && fa.Squadrons[0].Xp > 0, $"three to one wins, and learns ({r}, xp {fa.Squadrons.FirstOrDefault()?.Xp})");
        }),
        ("huge_battle_finite", () => {
            var sim = SelfTest.NewSim(); var a = sim.Humanity; var b = sim.Civs.First(c => c.Alien);
            var fa = new Fleet { Civ = a.Id, Admiral = new Admiral { Skill = 1.2f, Trait = "Aggressive" } }; fa.Add(Defs.Hulls.Length - 1, 1e6, 1f);
            var fb = new Fleet { Civ = b.Id, Admiral = new Admiral { Skill = 0.85f, Trait = "Cautious" } }; fb.Add(0, 1e6, 0f);
            var r = Battles.Resolve(sim, new BattleSide { Civ = a, Fleets = { fa } }, new BattleSide { Civ = b, Fleets = { fb } });
            SelfTest.Check(r.Rounds <= 6 && double.IsFinite(r.LostA) && double.IsFinite(r.LostB) && r.LostA >= 0 && r.LostB >= 0, "finite and bounded");
        }),
        ("morale_breaks", () => {
            var sim = SelfTest.NewSim(); var a = sim.Humanity; var b = sim.Civs.First(c => c.Alien);
            var fa = new Fleet { Civ = a.Id, Admiral = new Admiral { Skill = 1, Trait = "Tactician" } }; fa.Add(2, 100, 0f);
            var fb = new Fleet { Civ = b.Id, Admiral = new Admiral { Skill = 1, Trait = "Cautious" } }; fb.Add(2, 100, 0f); fb.Morale = 0.5f;
            Even(a, b);
            var r = Battles.Resolve(sim, new BattleSide { Civ = a, Fleets = { fa } }, new BattleSide { Civ = b, Fleets = { fb } });
            SelfTest.Check(r.BRetreats && fb.Ships > 0, $"a shaken, cautious fleet pulls out before it is destroyed ({r}, {fb.Ships})");
        }),
        ("landing_needs_orbit", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
            var col = o.Colonies.First(); var armies = new List<Army> { new() { Civ = h.Id, Troops = 1e6, Quality = 1 } };
            SelfTest.Check(!sim.HoldsOrbit(h, col), "no orbit without a fleet there");
            SelfTest.Check(sim.Land(col, h, armies) && col.OccupiedBy == h.Id, "an overwhelming landing takes the world");
        }),
        ("occupation_cuts_materiel", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var o = sim.Civs.First(c => c.Alien && c.Colonies.Count > 1);
            sim.DeclareWar(sim.Humanity, o);
            double before = o.Materiel; sim.Warp(1); double peace = o.Materiel - before;
            foreach (var c in o.Colonies) c.OccupiedBy = sim.Humanity.Id;
            before = o.Materiel; sim.Warp(1);
            SelfTest.Check(o.Materiel - before < peace * 0.1 + 1, $"occupied systems make nothing for their owner ({peace:0.0e0} then {o.Materiel - before:0.0e0})");
        }),
        ("garrison_holds", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
            var col = o.Colonies.First(); var armies = new List<Army> { new() { Civ = h.Id, Troops = 1e-9, Quality = 0.6f } };
            SelfTest.Check(!sim.Land(col, h, armies) && col.OccupiedBy < 0, "a token force is thrown back");
        }),
        ("terms_cede_claims", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien && c.Colonies.Count > 2);
            var w = sim.DeclareWar(h, o, "border_claim"); var claim = o.Colonies.First(c => w.Claims.Contains(c.Star.Id));
            sim.EndWarWith(w, true, new() { WarModel.TermById("cede_claims") });
            SelfTest.Check(claim.Civ == h && w.Ending == "surrender" && !h.AtWar.Contains(o.Id), "the claims change hands");
        }),
        ("ceasefire_keeps_occupation", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien && c.Colonies.Count > 1);
            var w = sim.DeclareWar(h, o); var col = o.Colonies.Last(); col.OccupiedBy = h.Id;
            sim.EndWarWith(w, true, new() { WarModel.TermById("ceasefire") });
            SelfTest.Check(w.Ending == "ceasefire" && col.OccupiedBy == h.Id && col.Civ == o, "the front freezes where it is");
        }),
        ("exhaustion_ends_wars", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
            var w = sim.DeclareWar(h, o, "border_claim"); w.ExhA = 95; w.ExhB = 95; w.Score = 0; w.Start -= 5;
            sim.Warp(3);
            SelfTest.Check(w.Ending != null, $"two exhausted sides stop ({w.Ending})");
        }),
        ("manual_peace_asks", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
            sim.SetManual(h, Area.Diplomacy, true);
            var w = sim.DeclareWar(o, h, "border_claim"); w.Start -= 5;
            foreach (var c in h.Colonies) c.OccupiedBy = o.Id;
            w.ExhB = 99; w.ExhA = 10;
            sim.Warp(3);
            SelfTest.Check(w.Ending == null && sim.Alerts.Any(a => a.Cat == "Decision" && !a.Done && a.Title.Contains("terms")), "the player is asked");
        }),
        ("war_side_dies", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien && c.Colonies.Count > 1);
            var w = sim.DeclareWar(h, o); o.Colonies.Last().OccupiedBy = h.Id;
            foreach (var c in o.Colonies.Where(c => c.OccupiedBy < 0).ToList()) sim.DestroyColony(c, "test");
            sim.Warp(1);
            SelfTest.Check(w.Ending != null && !sim.FleetsOf(o).Any() && sim.Colonies.All(c => c.OccupiedBy < 0 || !sim.Civs[c.OccupiedBy].Dead), "a dead side's war ends cleanly");
        }),
        ("wars_are_fought", () => {
            var sim = SelfTest.NewSim(); sim.Warp(650);                                  // first contact comes around 2620
            SelfTest.Check(sim.Wars.Count > 0, "wars are declared");
            var ended = sim.Wars.Where(w => w.Ending != null).ToList();
            SelfTest.Check(sim.Wars.Sum(w => w.Battles) >= sim.Wars.Count, $"battles in the wars ({sim.Wars.Sum(w => w.Battles)} in {sim.Wars.Count})");
            SelfTest.Check(ended.All(w => w.Ending != "none"), "every ended war has an ending");
        }),
        ("field_state_saved", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
            var w = sim.DeclareWar(h, o); var f = sim.FleetsOf(h).First(); sim.Order(f, o.Colonies[0].Star.Id, "attack");
            o.Colonies.Last().OccupiedBy = h.Id; o.Colonies.Last().DefenceLoss = 0.5f; h.Truce[5] = 2500;
            SaveGame.Save(sim, 20610, "user://selftest_field.json");
            var (s2, _) = SaveGame.Load("user://selftest_field.json");
            var f2 = s2.FleetsOf(s2.Humanity).First(x => x.Id == f.Id);
            SelfTest.Check(s2.Wars.Count == sim.Wars.Count && s2.WarBetween(s2.Humanity, s2.Civs[o.Id]).Reason == w.Reason, "wars round-trip");
            SelfTest.Check(f2.To == f.To && f2.Arrive == f.Arrive && s2.Civs[o.Id].Colonies.Last().OccupiedBy == h.Id && s2.Civs[o.Id].Colonies.Last().DefenceLoss == 0.5f && s2.Humanity.Truce[5] == 2500, "movement, occupation and truces round-trip");
        }),
        ("peace_returns_occupation", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien && c.Colonies.Count > 1);
            sim.DeclareWar(h, o); var col = o.Colonies.Last(); col.OccupiedBy = h.Id;
            h.AtWar.Remove(o.Id); o.AtWar.Remove(h.Id); sim.Warp(0.5);                   // peace imposed from outside (a god power, an act, the law)
            SelfTest.Check(col.OccupiedBy < 0, "a war ended elsewhere hands back what was occupied");
        }),
        ("dead_occupier_cleared", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var o = sim.Civs.First(c => c.Alien && c.Colonies.Count > 1);
            var q = sim.Civs.Last(c => c.Alien && c != o && c.Colonies.Count > 0);
            var col = o.Colonies.Last(); col.OccupiedBy = q.Id;
            foreach (var c in q.Colonies.ToList()) sim.DestroyColony(c, "test");
            sim.Warp(0.5);
            SelfTest.Check(q.Dead && col.OccupiedBy < 0, "nobody occupies for the dead");
        }),
        ("attack_skips_allied_holds", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien && c.Colonies.Count > 2);
            var q = sim.Civs.Last(c => c.Alien && c != o && c.Colonies.Count > 0);
            sim.DeclareWar(h, o); q.AtWar.Add(o.Id); o.AtWar.Add(q.Id);
            foreach (var c in o.Colonies.Skip(1)) c.OccupiedBy = q.Id;
            sim.Warp(1);
            var held = o.Colonies.Skip(1).Select(c => c.Star.Id).ToHashSet();
            SelfTest.Check(sim.FleetsOf(h).All(f => f.Mission != "attack" || !held.Contains(f.Target)), "no attack on a system an ally already holds");
        }),
        ("council_liberates", () => {
            var sim = SelfTest.NewSim(); var h = sim.Humanity; Civ o = null; Colony col = null;
            for (int tries = 0; tries < 20 && col == null; tries++)
            {
                sim.Warp(25);
                foreach (var c in sim.Civs.Where(c => c.Alien && !c.Dead && c.Colonies.Count > 1 && sim.FleetsOf(c).Any(f => f.Mission == "attack")))
                {
                    var at = sim.Gal.Stars[sim.FleetsOf(c).First(f => f.Mission == "attack").At];
                    col = c.Colonies.Where(x => x.Star != at && sim.TravelYears(c, at, x.Star) <= 8).FirstOrDefault();
                    if (col != null) { o = c; break; }
                }
            }
            SelfTest.Check(col != null, "some empire has a system within its fleets' reach");
            sim.DeclareWar(h, o); col.OccupiedBy = h.Id; sim.Warp(1);
            SelfTest.Check(sim.FleetsOf(o).Any(f => f.Mission == "attack" && f.Target == col.Star.Id), "attack fleets go to free their own occupied system");
        }),
        ("armies_disembark_home", () => {
            var sim = SelfTest.NewSim(); sim.Warp(20); var h = sim.Humanity; var f = sim.FleetsOf(h).First();
            foreach (var a in sim.ArmiesOf(h)) { a.Fleet = f.Id; a.At = f.At; }
            sim.Warp(1);
            SelfTest.Check(sim.ArmiesOf(h).Any() && sim.ArmiesOf(h).All(a => a.Fleet < 0), "at peace, troops come ashore");
        }),
        ("ceded_capital_not_home", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien && c.Colonies.Count > 1);
            var cap = h.Colonies.First(c => c.Home); sim.TransferColony(cap, o);
            SelfTest.Check(!cap.Home && o.Colonies.Count(c => c.Home) == 0, "a conquered capital is not the conqueror's capital");
        }),
        ("salvage_after_battle", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
            o.Techs.Add("UMB-3"); h.Gates["GE6"] = 1;
            sim.AfterBattle(h, o, o.Colonies[0]);
            SelfTest.Check(h.GateP.GetValueOrDefault("GE6:salvage") >= 1, "wrecks still teach");
        }),
        ("manual_loser_not_recalled", () => {
            var sim = SelfTest.NewSim(); var a = sim.Humanity; var b = sim.Civs.First(c => c.Alien); sim.Warp(2);
            sim.SetManual(a, Area.Fleets, true);
            var fa = new Fleet { Civ = a.Id, Admiral = new Admiral { Skill = 1, Trait = "Tactician" }, Target = 77, At = 77, Mission = "attack" }; fa.Add(2, 100, 0f);
            var fb = new Fleet { Civ = b.Id, Admiral = new Admiral { Skill = 1, Trait = "Tactician" } }; fb.Add(2, 300, 0f);
            Even(a, b);
            var r = Battles.Resolve(sim, new BattleSide { Civ = a, Fleets = { fa } }, new BattleSide { Civ = b, Fleets = { fb } });
            SelfTest.Check(!r.AWins && (r.ARetreats || fa.Target == 77), "the player's unbroken fleet keeps the player's orders");
        }),
        ("player_told_of_occupation", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
            sim.DeclareWar(o, h); var col = h.Colonies.First();
            sim.Land(col, o, new List<Army> { new() { Civ = o.Id, Troops = 1e6, Quality = 1 } });
            SelfTest.Check(sim.Alerts.Any(x => x.Cat == "War" && x.Title.Contains(col.Sys.Name)), "the player hears a system was taken");
        }),
    };
}
