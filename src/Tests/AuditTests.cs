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
        ("corrupt_save_falls_back", () => {
            // a save cut off mid-write loads its backup (the save before it) instead of failing
            var sim = SelfTest.NewSim(); sim.Warp(5);
            SaveGame.Save(sim, 20610, "user://selftest_atomic.json");
            sim.Warp(5);
            SaveGame.Save(sim, 20610, "user://selftest_atomic.json");
            var full = Godot.ProjectSettings.GlobalizePath("user://selftest_atomic.json");
            var text = System.IO.File.ReadAllText(full);
            System.IO.File.WriteAllText(full, text[..(text.Length / 2)]);
            var (s2, _) = SaveGame.Load("user://selftest_atomic.json");
            SelfTest.Check(Math.Abs(s2.Year - (sim.Year - 5)) < 0.01, $"loaded the backup (year {s2.Year:0.0})");
            SelfTest.Check(!System.IO.File.Exists(full + ".tmp"), "no temporary file is left behind");
        }),
        ("history_export", () => {
            var sim = SelfTest.NewSim(); sim.Warp(60);
            string md = HistoryExport.Markdown(sim, 20610);
            SelfTest.Check(md.StartsWith("# A history of humanity") && md.Contains("## The 21st century") && md.Contains("| Systems |"), "a titled book, by century, with a closing table");
            SelfTest.Check(sim.Chronicle.Count == 0 || md.Contains(sim.Chronicle[^1].Text.Trim()), "the latest entry is in it");
        }),
    };
}
