using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.Headless;

/// <summary>
/// The simulation without the engine. Same arguments as the game's headless modes:
///   --selftest[=filter]                 the self-tests (exit 1 if any failed)
///   --warp=YEARS [--trace] [--techlog]  fast-forward a new game (--seed=N), printing the trace
///   --savetest                          the save round trip (after any --warp)
///   --seeds=A,B,C                       repeat the warp for several seeds (a stats line per seed)
/// res:// is the repository root (found from the working directory upward); user:// is a temp folder.
/// </summary>
public static class Program
{
    public static int Main(string[] argv)
    {
        var args = argv.SkipWhile(a => a == "--").ToArray();
        OS.UserArgs = args;
        Godot.FileAccess.ResRoot = FindRoot();
        string Arg(string k) => args.FirstOrDefault(a => a.StartsWith(k + "="))?[(k.Length + 1)..];

        if (args.FirstOrDefault(a => a == "--selftest" || a.StartsWith("--selftest=")) is string st)
            return Starforge.Tests.SelfTest.Run(st.Length > 11 ? st[11..] : null) > 0 ? 1 : 0;

        var seeds = (Arg("--seeds") ?? Arg("--seed") ?? "20610").Split(',').Select(int.Parse).ToArray();
        double warp = double.Parse(Arg("--warp") ?? "0", System.Globalization.CultureInfo.InvariantCulture);
        bool trace = args.Contains("--trace"), techlog = args.Contains("--techlog"), quiet = args.Contains("--quiet");
        int failed = 0;
        foreach (int seed in seeds)
        {
            var sw = Stopwatch.StartNew();
            var sim = Runs.NewGame(seed);
            Func<int> lines = () => 0;
            if (trace && !quiet) lines = Runs.TraceChronicle(sim);
            else if (trace) { int n = 0; sim.Logged += _ => n++; lines = () => n; }
            double y0 = sim.Year;
            try
            {
                if (warp > 0) Runs.Warp(sim, warp, trace && !quiet, techlog);
            }
            catch (Exception ex) { GD.PrintErr($"SEED {seed} CRASH at {sim.Year:0.0}: {ex}"); failed++; continue; }
            if (trace) { Runs.PrintWarStats(sim, lines(), sim.Year - y0); Runs.PrintStrifeStats(sim, lines(), sim.Year - y0); Runs.PrintSpecOpsStats(sim); }
            if (args.Contains("--strifedump")) StrifeDump(sim);
            if (args.Contains("--savetime"))
            {
                // what an autosave costs the frame it runs in (the game saves on the main thread)
                for (int i = 0; i < 3; i++)
                {
                    var t = Stopwatch.StartNew(); SaveGame.Save(sim, seed, "user://timed.json"); double ms = t.Elapsed.TotalMilliseconds;
                    t.Restart(); SaveGame.Load("user://timed.json"); double lms = t.Elapsed.TotalMilliseconds;
                    GD.Print($"SAVETIME save={ms:0}ms load={lms:0}ms bytes={new FileInfo(Godot.FileAccess.Globalize("user://timed.json")).Length}");
                }
            }
            if (args.Contains("--savetest") && !Runs.SaveTest(sim, seed)) failed++;
            GD.Print($"RUN seed={seed} year={sim.Year:0} civs={sim.Civs.Count(c => !c.Dead)} colonies={sim.Colonies.Count} secs={sw.Elapsed.TotalSeconds:0.0}");
        }
        return failed > 0 ? 1 : 0;
    }

    /// <summary>Per living civ: systems, mean grievance, the ladder's histogram and the commonest causes.</summary>
    static void StrifeDump(Simulation sim)
    {
        foreach (var c in sim.Civs.Where(c => !c.Dead && c.Colonies.Count > 0).OrderByDescending(c => c.Colonies.Count))
        {
            var cols = c.Colonies.Where(x => !x.Home).ToList();
            if (cols.Count == 0) continue;
            var hist = string.Join("/", Enumerable.Range(0, 5).Select(i => cols.Count(x => x.Unrest == i)));
            var causes = string.Join(" ", cols.Where(x => x.CauseKey != null).GroupBy(x => x.CauseKey).OrderByDescending(g => g.Count()).Take(4).Select(g => $"{g.Key}:{g.Count()}"));
            GD.Print($"STRIFE {c.Name} {(c.Human ? "human" : "alien")} {c.Ethic} gov={sim.GovFactor(c):0.00} hive={c.M.Hive} digital={c.Digital:0.00} folds={sim.Folds(c)} systems={cols.Count} " +
                     $"grievance mean={cols.Average(x => x.Grievance):0.0} max={cols.Max(x => x.Grievance):0.0} ladder={hist} autonomy={cols.Count(x => x.Autonomy)} causes=[{causes}]");
        }
    }

    static string FindRoot()
    {
        for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "project.godot"))) return d.FullName;
        throw new Exception("run from inside the Starforge repository (no project.godot found above the working directory)");
    }
}
