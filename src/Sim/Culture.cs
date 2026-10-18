using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

/// <summary>
/// A people's war culture, beside its drive and ethic: three axes from -1 to 1. Martial (pacifist ↔ warrior) sets how far
/// mobilisation goes before people resist; Duty (fiercely free ↔ every citizen a soldier) sets manpower and how
/// mandatory service lands; Resolve (brittle ↔ unbreakable) sets how hard they hold out in a losing war.
/// </summary>
public class Culture
{
    public float Martial, Duty, Resolve;
    public Culture Copy() => new() { Martial = Martial, Duty = Duty, Resolve = Resolve };
    public void Add(string axis, float d)
    {
        switch (axis)
        {
            case "Martial": Martial = Math.Clamp(Martial + d, -1f, 1f); break;
            case "Duty": Duty = Math.Clamp(Duty + d, -1f, 1f); break;
            case "Resolve": Resolve = Math.Clamp(Resolve + d, -1f, 1f); break;
        }
    }
    public float Get(string axis) => axis switch { "Martial" => Martial, "Duty" => Duty, _ => Resolve };
}

/// <summary>Starting lore per species (data/war.json culture.start) and how a culture drifts with its history.</summary>
public static class Cultures
{
    public static readonly string[] Axes = { "Martial", "Duty", "Resolve" };

    public static Culture Lore(string species)
    {
        var start = WarData.P("culture", "start");
        var e = species != null && start.TryGetProperty(species, out var s) ? s : start.GetProperty("default");
        return new Culture { Martial = (float)e.GetProperty("Martial").GetDouble(), Duty = (float)e.GetProperty("Duty").GetDouble(), Resolve = (float)e.GetProperty("Resolve").GetDouble() };
    }

    /// <summary>A turn of history moves the culture once: victory, defeat, exhaustion (culture.drift.kind).</summary>
    public static void Event(Civ c, string kind) => Apply(c, kind, 1);

    /// <summary>Years at war harden a people; years at peace soften it.</summary>
    public static void Drift(Civ c, double years, bool atWar) => Apply(c, atWar ? "warYear" : "peaceYear", years);

    static void Apply(Civ c, string kind, double k)
    {
        if (c.Culture == null || !WarData.P("culture", "drift").TryGetProperty(kind, out var d)) return;
        foreach (var p in d.EnumerateObject()) c.Culture.Add(p.Name, (float)(p.Value.GetDouble() * k));
    }

    /// <summary>"a martial people, dutiful": the named band of each axis that has one.</summary>
    public static string Describe(Culture k)
    {
        var parts = new List<string>();
        foreach (var a in Axes)
        {
            int i = Math.Clamp((int)Math.Round((k.Get(a) + 1) * 2), 0, 4);
            string n = WarData.S("culture", "names", a, i.ToString());
            if (n.Length > 0) parts.Add(n);
        }
        return parts.Count == 0 ? "of no strong war culture" : string.Join(", ", parts);
    }
}
