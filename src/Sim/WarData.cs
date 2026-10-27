using System.Text.Json;
using Godot;

namespace Starforge.Sim;

/// <summary>
/// data/war.json: every tunable number of the war system (culture lore and drift, mobilisation levels, the war economy,
/// armies, experience, war reasons and terms, placeholder colours). Loaded once, on first use, like progression.json.
/// </summary>
public static class WarData
{
    public const string Path = "res://data/war.json";
    static JsonDocument _doc;
    public static JsonElement Root
    {
        get
        {
            _doc ??= JsonDocument.Parse(DataFiles.Read(Path));
            return _doc.RootElement;
        }
    }

    /// <summary>Walk the document: object members by name, array items by index ("3").</summary>
    public static JsonElement P(params string[] path)
    {
        var e = Root;
        foreach (var p in path) e = e.ValueKind == JsonValueKind.Array ? e[int.Parse(p)] : e.GetProperty(p);
        return e;
    }
    static readonly System.Collections.Generic.Dictionary<string, double> _d = new();
    /// <summary>A number, cached by path: the rules are read thousands of times a simulated year (slips, squadrons,
    /// every system's grievance), and walking the JSON each time was a third of a long game's run time.</summary>
    public static double D(params string[] path)
    {
        string key = string.Join('/', path);
        if (!_d.TryGetValue(key, out double v)) _d[key] = v = P(path).GetDouble();
        return v;
    }
    // the common short paths without building a key string per call (C# picks these over the params form)
    static readonly System.Collections.Generic.Dictionary<(string, string, string), double> _d3 = new();
    public static double D(string a, string b) => D3(a, b, null);
    public static double D(string a, string b, string c) => D3(a, b, c);
    static double D3(string a, string b, string c)
    {
        if (!_d3.TryGetValue((a, b, c), out double v)) _d3[(a, b, c)] = v = c == null ? P(a, b).GetDouble() : P(a, b, c).GetDouble();
        return v;
    }
    public static string S(params string[] path) => P(path).GetString();

    /// <summary>A placeholder cube's colour (placeholders.kind, a hex string).</summary>
    public static Color Colour(string kind) => Color.FromHtml(S("placeholders", kind));
}
