using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Starforge.Sim;

/// <summary>One parsed effect from the tech catalogue's little effect language (see docs/TECH_INTEGRATION.md §3.2).</summary>
public class Effect
{
    public string Kind;
    public string[] A;
    public override string ToString() => Kind + ":" + string.Join(",", A);
}

/// <summary>
/// Loads data/techs.json (built from data/tech_catalogue.txt by tools/build_techs.py) and data/progression.json.
/// The game's whole tech tree, research rules, hulls, meters and gate events are data, not code.
/// </summary>
public static class TechData
{
    public const string TechsPath = "res://data/techs.json", ProgPath = "res://data/progression.json";

    public static List<Tech> Load(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<Tech>();
        foreach (var e in doc.RootElement.GetProperty("techs").EnumerateArray())
        {
            var t = new Tech
            {
                Id = e.GetProperty("id").GetString(),
                B = e.GetProperty("branch").GetString()[0],
                Line = e.GetProperty("line").GetString(),
                Tier = e.GetProperty("tier").GetInt32(),
                Name = e.GetProperty("name").GetString(),
                Fx = e.GetProperty("fx").GetString(),
                Start = e.GetProperty("start").GetBoolean(),
                Rare = e.GetProperty("rare").GetBoolean(),
                Danger = e.GetProperty("danger").GetBoolean(),
                Keystone = e.GetProperty("keystone").GetBoolean(),
                Event = e.GetProperty("event").GetBoolean(),
                Atrocity = e.GetProperty("atrocity").GetBoolean(),
                Exclusive = e.GetProperty("exclusive").ValueKind == JsonValueKind.String ? e.GetProperty("exclusive").GetString() : null,
                Req = e.GetProperty("req").EnumerateArray().Select(g => g.EnumerateArray().Select(x => x.GetString()).ToArray()).ToArray(),
                Effects = e.GetProperty("effects").EnumerateArray().Select(x => new Effect
                {
                    Kind = x.GetProperty("kind").GetString(),
                    A = x.GetProperty("args").EnumerateArray().Select(a => a.GetString()).ToArray(),
                }).ToList(),
            };
            t.Spine = t.Line == "SPINE";
            t.Gate = t.Effects.FirstOrDefault(x => x.Kind == "gate")?.A[0];
            t.Cond = t.Effects.FirstOrDefault(x => x.Kind == "cond")?.A[0];
            t.Uses = t.Effects.Where(x => x.Kind == "use").Select(x => x.A[0]).ToArray();
            t.Apo = t.Effects.Where(x => x.Kind == "apotheosis").Sum(x => int.Parse(x.A[0]));
            t.Path = t.Effects.FirstOrDefault(x => x.Kind == "path")?.A[0];
            t.Log = Defs.SpineLog.TryGetValue(t.Id, out var log) ? log : null;
            list.Add(t);
        }
        // tier order: ComputeMods applies effects in this order, so ">=" and "*" compose the same way every time
        return list.OrderBy(t => t.Tier).ThenBy(t => t.Spine ? 0 : 1).ToList();
    }

    // ---------- effects onto Mods ----------
    static readonly Dictionary<string, FieldInfo> _fields = typeof(Mods).GetFields().ToDictionary(f => f.Name);

    /// <summary>The standing effects of one known tech. One-off effects (meter, event, digital, conscience) happen in CompleteTech.</summary>
    public static void Apply(Tech t, Mods m, bool fed)
    {
        foreach (var e in t.Effects)
        {
            switch (e.Kind)
            {
                case "mod":
                    if (!fed) break;
                    var f = _fields[e.A[0]];
                    double x = double.Parse(e.A[2], System.Globalization.CultureInfo.InvariantCulture);
                    double cur = Convert.ToDouble(f.GetValue(m));
                    double v = e.A[1] switch { "*" => cur * x, ">=" => Math.Max(cur, x), _ => Math.Min(cur, x) };
                    f.SetValue(m, f.FieldType == typeof(double) ? v : (object)(float)v);
                    break;
                case "flag":
                    if (!fed) break;
                    if (_fields.TryGetValue(e.A[0], out var bf) && bf.FieldType == typeof(bool)) bf.SetValue(m, true);
                    else m.Flags.Add(e.A[0]);
                    break;
                case "arms":
                    if (fed) m.Arms[e.A[0]] = Math.Max(m.Arms.GetValueOrDefault(e.A[0]), int.Parse(e.A[1]));
                    break;
                case "guard":
                    if (fed) m.Guard[e.A[0]] = Math.Max(m.Guard.GetValueOrDefault(e.A[0]), int.Parse(e.A[1]));
                    break;
                case "hull": m.Hulls.Add(e.A[0]); break;
                case "mega": m.Megas.Add(e.A[0]); break;
                case "res": m.Produce[e.A[0]] = m.Produce.GetValueOrDefault(e.A[0]) + double.Parse(e.A[1], System.Globalization.CultureInfo.InvariantCulture); break;
                case "use": m.Upkeep[e.A[0]] = m.Upkeep.GetValueOrDefault(e.A[0]) + 1; break;
                case "act": m.Acts.Add(e.A[0]); break;
                case "vis": m.Vis.Add(e.A[0]); break;
            }
        }
    }

    // ---------- progression.json ----------
    public static JsonElement Prog;
    static JsonDocument _progDoc;
    public static void LoadProgression(string json) { _progDoc = JsonDocument.Parse(json); Prog = _progDoc.RootElement; }
    public static JsonElement P(params string[] path)
    {
        var e = Prog;
        foreach (var p in path) e = e.GetProperty(p);
        return e;
    }
    public static double D(params string[] path) => P(path).GetDouble();
    public static string S(params string[] path) => P(path).GetString();
}
