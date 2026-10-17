using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;

namespace Starforge.View;

/// <summary>Frame profiler for --hitch: `using (Prof.T("name"))` around a section; Main prints the slow frames.</summary>
public static class Prof
{
    public static readonly bool On = OS.GetCmdlineUserArgs().Contains("--hitch");
    static readonly Dictionary<string, long> _acc = new();

    public readonly struct Span : IDisposable
    {
        readonly string _name; readonly long _t0;
        public Span(string name) { _name = name; _t0 = On ? Stopwatch.GetTimestamp() : 0; }
        public void Dispose() { if (On) _acc[_name] = _acc.GetValueOrDefault(_name) + Stopwatch.GetTimestamp() - _t0; }
    }

    public static Span T(string name) => new(name);

    /// <summary>The frame's slowest sections, then reset.</summary>
    public static string Flush()
    {
        string s = string.Join(" ", _acc.Where(kv => kv.Value * 1000.0 / Stopwatch.Frequency > 0.5).OrderByDescending(kv => kv.Value)
            .Select(kv => $"{kv.Key}={kv.Value * 1000.0 / Stopwatch.Frequency:0.0}"));
        _acc.Clear();
        return s;
    }
}
