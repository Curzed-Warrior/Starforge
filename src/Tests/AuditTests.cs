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
        ("autosave_snapshot_owns_its_data", () => {
            // finding 6: the autosave serialises on a worker while the game runs on, so its snapshot must share nothing
            var sim = SelfTest.NewSim(); sim.Warp(300); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
            if (sim.WarBetween(h, o) == null) sim.DeclareWar(h, o);
            SaveGame.Save(sim, 1, "user://selftest_snap_a.json");
            var snap = SaveGame.Snapshot(sim, 1);
            foreach (var f in sim.Fleets) { foreach (var sq in f.Squadrons) sq.Ships += 7; f.Squadrons.Add(new Squadron { Hull = 0, Ships = 1 }); if (f.Admiral != null) f.Admiral.Wins += 3; }
            foreach (var w in sim.Wars) { w.Claims.Add(12345); w.Battles += 9; }
            foreach (var a in sim.Armies) a.Troops += 5;
            h.Policy.Weight["x"] = 3; h.Policy.AskAfter["y"] = 9; h.Truce[o.Id] = 9999;
            h.Yard.Add(new Slip { Hull = 0, Progress = 0.5 }); foreach (var s in h.Yard) s.Progress += 0.1;
            h.Barracks.Add(new Training { Troops = 1, Left = 2 }); h.Academy.Add(new Training { Troops = 0, Left = 3 });
            h.Culture.Martial += 0.1f; h.Colonies[0].Stock *= 2; h.Colonies[0].Raised.Add("snapshot-test");
            sim.Warp(5);
            SaveGame.Write(snap, Godot.ProjectSettings.GlobalizePath("user://selftest_snap_b.json"));
            string Read(string p) => System.IO.File.ReadAllText(Godot.ProjectSettings.GlobalizePath(p));
            SelfTest.Check(Read("user://selftest_snap_a.json") == Read("user://selftest_snap_b.json"), "a snapshot written later is the save as it was when taken");
        }),
        ("history_kept_on_reload", () => {
            // finding 7: a save keeps every turning point (era, breakthrough, omen, the voice) and caps only routine lines
            var sim = SelfTest.NewSim(); sim.Warp(5); var home = sim.HomeColony;
            sim.Log(home, "An age begins (test).", "era", null);
            for (int i = 0; i < 5000; i++) sim.Log(home, $"routine {i}", "event", null);
            SelfTest.Check(sim.Chronicle.Any(e => e.Text == "An age begins (test)."), "memory keeps the turning point past the routine cap");
            SaveGame.Save(sim, 1, "user://selftest_history.json");
            var (s2, _) = SaveGame.Load("user://selftest_history.json");
            SelfTest.Check(s2.Chronicle.Any(e => e.Text == "An age begins (test)."), "the turning point survives a reload");
            SelfTest.Check(s2.Chronicle.Count(e => !Simulation.Lasting(e.Kind)) == 800 && s2.Chronicle[^1].Text == "routine 4999", "routine lines are capped, newest kept");
            SelfTest.Check(s2.Chronicle.Zip(s2.Chronicle.Skip(1)).All(p => p.First.Year <= p.Second.Year), "in order");
        }),
        ("regime_keeps_conscience", () => {
            // finding 8: a regime change is a new government over the same people, so their drifted conscience stays
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien && c.Drive != h.Drive);
            o.Conscience = 0.61f;
            var w = sim.DeclareWar(h, o); sim.EndWarWith(w, true, new() { WarModel.TermById("regime") });
            SelfTest.Check(o.Drive == h.Drive, "the loser takes the winner's drive");
            SelfTest.Check(Math.Abs(o.Conscience - 0.61f) < 1e-5, $"conscience {o.Conscience} kept its drift");
        }),
    };
}
