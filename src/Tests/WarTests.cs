using System;
using System.Collections.Generic;
using System.Linq;
using Starforge.Sim;

namespace Starforge.Tests;

/// <summary>The war system's self-tests (docs/superpowers/plans/2026-10-18-war-foundation.md).</summary>
public static class WarTests
{
    public static readonly List<(string Name, Action Body)> All = new()
    {
        ("wardata_loads", () => {
            SelfTest.Check(WarData.P("mobilisation").GetArrayLength() == 4, "four mobilisation levels");
            SelfTest.Check(WarData.S("mobilisation", "3", "id") == "total", "last level is total war");
            SelfTest.Check(WarData.P("reasons").GetArrayLength() >= 10, "at least ten reasons");
            SelfTest.Check(WarData.Colour("factory").R > 0.8f && WarData.Colour("factory").B < 0.4f, "factories are orange");
        }),
        ("culture_start", () => {
            var sim = SelfTest.NewSim();
            SelfTest.Check(Math.Abs(sim.Humanity.Culture.Duty - (-0.3f)) < 1e-4, "humanity starts from Human lore");
            var vessh = sim.Civs.First(c => c.Species == "Vessh");
            SelfTest.Check(vessh.Culture.Martial > 0.6f && vessh.MandatoryService, "the Vessh are a conscript warrior people");
        }),
        ("culture_drift", () => {
            var sim = SelfTest.NewSim(); var h = sim.Humanity; float m0 = h.Culture.Martial;
            Cultures.Drift(h, 10, atWar: true);
            SelfTest.Check(h.Culture.Martial > m0, "war hardens");
            for (int i = 0; i < 100; i++) Cultures.Event(h, "victory");
            SelfTest.Check(h.Culture.Martial <= 1f, "clamped");
            SelfTest.Check(Cultures.Describe(h.Culture).Contains("warrior"), "described");
        }),
        ("splinter_inherits_culture", () => {
            var sim = SelfTest.NewSim(); sim.Warp(400);
            var h = sim.Humanity; h.Culture.Martial = 0.42f;
            var cols = h.Colonies.Where(c => !c.Home).Take(2).ToList();
            if (cols.Count < 2) return;                                          // no colonies yet on this seed: nothing to check
            var s = sim.SplinterForTest(h, h.Drive, cols);
            SelfTest.Check(Math.Abs(s.Culture.Martial - 0.42f) < 1e-4 && s.Culture != h.Culture, "copied, not shared");
            SelfTest.Check(sim.FleetsOf(h).All(f => f.Civ == h.Id), "parent keeps its fleets");
        }),
        ("fleets_raised", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2);
            var fl = sim.FleetsOf(sim.Humanity).ToList();
            SelfTest.Check(fl.Count == 1 && fl[0].Name == "Home Fleet" && fl[0].Admiral != null, "one home fleet with an admiral");
            var vessh = sim.Civs.First(c => c.Species == "Vessh");
            SelfTest.Check(sim.FleetsOf(vessh).Count() == Math.Clamp((int)Math.Log2(1 + vessh.Colonies.Count), 1, 6), "fleets scale with size");
        }),
        ("squadrons_merge", () => {
            var f = new Fleet(); f.Add(2, 10, 0f); f.Add(2, 10, 0.6f); f.Add(4, 1, 0f);
            SelfTest.Check(f.Squadrons.Count == 2 && Math.Abs(f.Squadrons[0].Xp - 0.3f) < 1e-4 && f.Ships == 21, "same hull merges, xp averages");
            SelfTest.Check(Forces.Level(0.9f) == "Elite" && Forces.Level(0.1f) == "Green", "levels");
        }),
        ("mobilisation_retools", () => {
            var sim = SelfTest.NewSim(); var h = sim.Humanity;
            sim.SetManual(h, Area.Production, true); h.Policy.Mobilisation = 2;          // war economy, 0.45
            sim.Warp(1.5);
            SelfTest.Check(h.MilShare > 0.15 && h.MilShare < 0.35, $"halfway after 1.5 of 3 years ({h.MilShare:0.00})");
            SelfTest.Check(h.Efficiency < 0.8, "new lines start inefficient");
            sim.Warp(6);
            SelfTest.Check(Math.Abs(h.MilShare - 0.45) < 0.02 && h.Efficiency > 0.9, $"settled and efficient ({h.MilShare:0.00}, {h.Efficiency:0.00})");
            SelfTest.Check(sim.WarGrowth(h) < 1 && sim.WarResearch(h) < 1, "the war costs growth and research");
        }),
        ("manual_override_strains", () => {
            var sim = SelfTest.NewSim(); var h = sim.Humanity; h.Culture.Martial = -0.8f;   // pacifist: comfort level 1
            sim.SetManual(h, Area.Production, true); h.Policy.Mobilisation = 3; sim.Warp(1);
            SelfTest.Check(h.WarStrain > 5.0 * (1 + 0.4), "total war on a pacifist people costs more than the base strain");
            SelfTest.Check(h.Mobilisation == 3, "the player's choice is not refused");
        }),
        ("council_mobilises_at_peace", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2);
            SelfTest.Check(sim.CouncilMobilisation(sim.Humanity) == 0 && sim.Humanity.MilShare < 0.05, "peacetime by default");
        }),
        ("yards_build_ships", () => {
            var sim = SelfTest.NewSim(); sim.Warp(30);
            var h = sim.Humanity;
            SelfTest.Check(sim.FleetsOf(h).Sum(f => f.Ships) >= 1, "peacetime yards launch something in thirty years");
            SelfTest.Check(h.Yard.Count <= sim.Slips(h), "never more work than slips");
        }),
        ("slips_bound_output", () => {
            var sim = SelfTest.NewSim(); var h = sim.Humanity;
            h.Materiel = 1e30;                                                           // a late-game stockpile
            double before = sim.FleetsOf(h).Sum(f => f.Ships); sim.Warp(10);
            double built = sim.FleetsOf(h).Sum(f => f.Ships) - before;
            SelfTest.Check(built <= sim.Slips(h) * 10 / Simulation.BuildYears(0) + 1, "slips and build time bound the output");
            SelfTest.Check(!double.IsNaN(h.Materiel) && !double.IsInfinity(h.Materiel), "no overflow");
        }),
        ("upkeep_mothballs", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity;
            var f = sim.FleetsOf(h).First(); f.Add(Defs.Hulls.Length - 1, 500, 0f); h.Materiel = 0;   // 500 worldships: far beyond 2060s industry
            sim.Warp(3);
            SelfTest.Check(f.Ships < 500, "a navy the economy cannot pay for is laid up");
        }),
        ("armies_trained", () => {
            var sim = SelfTest.NewSim(); sim.Warp(20); var h = sim.Humanity;
            SelfTest.Check(sim.ArmiesOf(h).Any(), "a peacetime garrison is raised");
            SelfTest.Check(sim.ArmiesOf(h).All(a => !a.Rushed && Math.Abs(a.Quality - 1f) < 1e-4), "trained in peace, full quality");
        }),
        ("rushed_in_war", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity;
            var foe = sim.Civs.First(c => c.Alien); h.AtWar.Add(foe.Id); foe.AtWar.Add(h.Id);
            h.Materiel = 1e20; sim.Warp(3);
            SelfTest.Check(sim.ArmiesOf(h).Any(a => a.Rushed && a.Quality < 1f), "wartime recruits are rushed and worse");
        }),
        ("dead_civ_skipped", () => {
            var sim = SelfTest.NewSim(); var c = sim.Civs.First(x => x.Alien);
            foreach (var col in c.Colonies.ToList()) sim.DestroyColony(col, "test");
            sim.Warp(3);
            SelfTest.Check(c.Dead && !sim.FleetsOf(c).Any() && !sim.ArmiesOf(c).Any(), "a dead civ keeps no forces and throws nothing");
        }),
        ("ending_limited_compromise", () => {
            var sim = SelfTest.NewSim(); var h = sim.Humanity; h.Culture.Resolve = 0;
            double give = WarModel.Concede(h, "border_claim", loserScore: -40, exhaustion: 50);
            SelfTest.Check(Math.Abs(give - 70) < 1e-6, "limited stakes concede score + 0.6 x exhaustion");
            var t = WarModel.ChooseTerms("border_claim", give, bothExhausted: false);
            SelfTest.Check(t.Any(x => x.Id == "cede_claims") && WarModel.Ending(t, "border_claim") == "surrender", "the claims change hands");
        }),
        ("ending_survival_no_terms", () => {
            var sim = SelfTest.NewSim(); var h = sim.Humanity;
            SelfTest.Check(WarModel.Concede(h, "extermination", -90, 95) == 0, "no terms before collapse");
            SelfTest.Check(double.IsPositiveInfinity(WarModel.Concede(h, "extermination", -90, 100)), "collapse at full exhaustion");
            SelfTest.Check(WarModel.Ending(WarModel.ChooseTerms("extermination", 0, true), "extermination") == "none", "fight on");
        }),
        ("ending_ceasefire", () => {
            var t = WarModel.ChooseTerms("independence", 5, bothExhausted: true);
            SelfTest.Check(t.Count == 1 && t[0].Id == "ceasefire", "two exhausted sides freeze the front");
            SelfTest.Check(WarModel.ChooseTerms("ideology", 999, false).All(x => WarModel.ReasonById("ideology").Terms.Contains(x.Id)), "only the reason's terms");
        }),
        ("resolve_holds_out", () => {
            var sim = SelfTest.NewSim(); var h = sim.Humanity;
            h.Culture.Resolve = 1; double hard = WarModel.Concede(h, "border_claim", -40, 50);
            h.Culture.Resolve = -1; double soft = WarModel.Concede(h, "border_claim", -40, 50);
            SelfTest.Check(hard < soft, "an unbreakable people concedes less");
        }),
    };
}
