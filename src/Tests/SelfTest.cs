using System;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.Tests;

/// <summary>
/// Headless self-tests, run with `--selftest[=filter]` (the filter is a substring of the test name). Each test prints
/// SELFTEST PASS/FAIL; the last line is SELFTEST DONE pass=N fail=M, and the process exits 1 if anything failed.
/// </summary>
public static class SelfTest
{
    class Failed : Exception { public Failed(string m) : base(m) { } }

    public static void Check(bool ok, string what) { if (!ok) throw new Failed(what); }

    /// <summary>A fresh universe, built exactly as Main._Ready builds a new game.</summary>
    public static Simulation NewSim(int seed = 20610)
    {
        var gal = new Galaxy(seed);
        gal.PlaceSolAndNeighbours();
        gal.GenerateStars(2600);
        return new Simulation(gal, seed);
    }

    public static int Run(string filter)
    {
        int pass = 0, fail = 0;
        foreach (var (name, body) in WarTests.All.Concat(FieldTests.All).Where(t => string.IsNullOrEmpty(filter) || t.Name.Contains(filter)))
        {
            try { body(); pass++; GD.Print($"SELFTEST PASS {name}"); }
            catch (Exception e) { fail++; GD.Print($"SELFTEST FAIL {name}: {(e is Failed ? e.Message : e.ToString())}"); }
        }
        GD.Print($"SELFTEST DONE pass={pass} fail={fail}");
        return fail;
    }
}
