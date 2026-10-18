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
    };
}
