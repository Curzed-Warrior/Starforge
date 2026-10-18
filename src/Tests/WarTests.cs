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
    };
}
