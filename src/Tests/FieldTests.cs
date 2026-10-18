using System;
using System.Collections.Generic;
using System.Linq;
using Starforge.Sim;

namespace Starforge.Tests;

/// <summary>Stage 2 self-tests: war in the field (docs/superpowers/plans/2026-10-19-war-in-the-field.md).</summary>
public static class FieldTests
{
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
    };
}
