using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

/// <summary>Why a war is fought: its stakes tier (limited, principle, survival) and the terms that can end it.</summary>
public record Reason(string Id, string Tier, string Name, string[] Terms);
/// <summary>A peace term, priced in war score; core terms are what the war was for.</summary>
public record Term(string Id, string Name, double Price, bool Core);

/// <summary>
/// How wars end (pure functions over war.json; stage 2 wires them into real wars). The loser concedes score in
/// proportion to how badly it is losing and how tired it is, less for a stubborn people; the terms are bought from
/// the reason's list with that budget. Wars of survival offer no terms: they end in collapse.
/// </summary>
public static class WarModel
{
    static List<Reason> _reasons;
    static Dictionary<string, Term> _terms;
    /// <summary>The one term of a war of survival, and what a war against a civ that is gone ends in.</summary>
    public static readonly Term Collapse = new("collapse", "Collapse", 0, true);

    public static IEnumerable<Reason> Reasons
    {
        get
        {
            _reasons ??= WarData.P("reasons").EnumerateArray().Select(r => new Reason(r.GetProperty("id").GetString(),
                r.GetProperty("tier").GetString(), r.GetProperty("name").GetString(),
                r.GetProperty("terms").EnumerateArray().Select(t => t.GetString()).ToArray())).ToList();
            return _reasons;
        }
    }
    public static Reason ReasonById(string id) => Reasons.First(r => r.Id == id);
    public static Term TermById(string id)
    {
        _terms ??= WarData.P("terms").EnumerateArray().Select(t => new Term(t.GetProperty("id").GetString(), t.GetProperty("name").GetString(),
            t.GetProperty("price").GetDouble(), t.GetProperty("core").GetBoolean())).ToDictionary(t => t.Id);
        return _terms[id];
    }

    /// <summary>War score the loser will give up. Survival wars concede nothing until full exhaustion, then everything.</summary>
    public static double Concede(Civ loser, string reasonId, double loserScore, double exhaustion)
    {
        var tier = ReasonById(reasonId).Tier;
        if (tier == "survival") return exhaustion >= 100 ? double.PositiveInfinity : 0;
        return WarData.D("stakes", tier, "concede") * (Math.Max(0, -loserScore) + 0.6 * exhaustion) * (1 - 0.4 * loser.Culture.Resolve);
    }

    /// <summary>Buy the reason's priced terms in order while the budget lasts; two exhausted sides fall back on a ceasefire.</summary>
    public static List<Term> ChooseTerms(string reasonId, double concede, bool bothExhausted)
    {
        var r = ReasonById(reasonId);
        if (r.Tier == "survival") return double.IsPositiveInfinity(concede) ? new() { Collapse } : new();
        var taken = new List<Term>();
        double left = concede;
        foreach (var t in r.Terms.Select(TermById).Where(t => t.Price > 0))
            if (t.Price <= left) { taken.Add(t); left -= t.Price; }
        if (taken.Count == 0 && bothExhausted && WarData.P("stakes", r.Tier, "ceasefire").GetBoolean() && r.Terms.Contains("ceasefire"))
            taken.Add(TermById("ceasefire"));
        return taken;
    }

    /// <summary>What kind of ending a set of terms makes.</summary>
    public static string Ending(List<Term> terms, string reasonId)
    {
        if (terms.Count == 0) return "none";
        if (terms.Count == 1 && terms[0].Id == "collapse") return "collapse";
        var priced = terms.Where(t => t.Price > 0).ToList();
        if (priced.Count > 0)
        {
            var core = ReasonById(reasonId).Terms.Select(TermById).Where(t => t.Core).ToList();
            return core.Count > 0 && core.All(c => terms.Any(t => t.Id == c.Id)) ? "surrender" : "compromise";
        }
        if (terms.Any(t => t.Id == "ceasefire")) return "ceasefire";
        return "status quo";
    }
}
