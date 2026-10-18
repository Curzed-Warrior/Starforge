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
            SelfTest.Check(target.Civ != h || f.Target != target.Star.Id || !f.Moving, "a fleet stops chasing a system its own side now holds");
            SelfTest.Check(sim.Armies.All(a => a.Fleet < 0 || sim.Fleets.Any(x => x.Id == a.Fleet)), "no army aboard a missing fleet");
        }),
    };
}
