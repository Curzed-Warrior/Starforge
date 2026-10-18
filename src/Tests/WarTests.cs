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
    };
}
