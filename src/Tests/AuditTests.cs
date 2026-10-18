using System;
using System.Collections.Generic;
using System.Linq;
using Starforge.Sim;

namespace Starforge.Tests;

/// <summary>Regression tests for defects found in the October 2026 audit (docs/AUDIT.md).</summary>
public static class AuditTests
{
    public static readonly List<(string Name, Action Body)> All = new()
    {
        ("kindled_stars_keep_specials", () => {
            // a kindled star must not change which stars the regenerated galaxy makes into the Shard, Vault and Gates
            var sim = SelfTest.NewSim(1);
            var before = sim.Gal.Stars.Select(s => (s.Name, s.Special, s.Dead)).ToList();
            for (int i = 0; i < 3; i++) sim.KindleStar(sim.Gal.Sol.Pos);
            SaveGame.Save(sim, 1, "user://selftest_kindled.json");
            var (s2, _) = SaveGame.Load("user://selftest_kindled.json");
            SelfTest.Check(s2.Gal.Stars.Count == sim.Gal.Stars.Count, $"star count {s2.Gal.Stars.Count} vs {sim.Gal.Stars.Count}");
            for (int i = 0; i < before.Count; i++)
            {
                var a = before[i]; var b = s2.Gal.Stars[i];
                SelfTest.Check(a.Name == b.Name && a.Special == b.Special && a.Dead == b.Dead, $"star {i}: {a.Name}/{a.Special} became {b.Name}/{b.Special}");
            }
            SelfTest.Check(s2.Gal.Stars.Skip(before.Count).All(s => s.Kindled), "the kindled stars come back as kindled");
        }),
    };
}
