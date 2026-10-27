using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

/// <summary>
/// The Intelligence area (war-and-strife spec §4.8): where special forces go and what they do. Left to the council, a
/// ready team is sent where it does the most good for the least risk; held by the player, each ready team waits for
/// orders. At war, teams serve the war; in peace, they steal from rivals and stir them up.
/// </summary>
public partial class Simulation
{
    /// <summary>How many of a civ's colonies a council looks at as targets: the nearest to where its teams work from, plus its fleets' targets.</summary>
    const int TargetsPerCiv = 12;

    /// <summary>
    /// Every (colony, mission) a ready team could take on, scored by its value times the odds of success, less the risk
    /// of capture weighted by the council's caution; best first.
    /// </summary>
    List<(Colony Target, string Mission, double Score)> ScoredMissions(Civ c, Team t)
    {
        var list = new List<(Colony, string, double)>();
        var home = Home(c);
        if (home == null) return list;
        double caution = c.Ethic == "Reverent" ? 2 : c.Ethic == "Ruthless" ? 0.5 : 1;
        foreach (var (col, m, v, d) in Targets(c, home))
        {
            double score = v * SuccessChance(t, d, m) * Math.Max(0, 1 - CaptureChance(t, d, m) * caution);
            if (score > 0) list.Add((col, m, score));
        }
        return list.OrderByDescending(x => x.Item3).ToList();
    }

    /// <summary>Which academy pass is running (AcademyStep): within one, a civ's targets are worked out once for all its teams. -1 outside.</summary>
    int _offerPass = -1, _passes;
    readonly Dictionary<int, (int Pass, List<(Colony, string, double, double)> List)> _targets = new();

    /// <summary>
    /// What a civ's teams could work on, with what each is worth and the holder's counter-intelligence: the same for
    /// every team of the civ (only the odds depend on the team), so an academy pass works it out once per civ.
    /// </summary>
    List<(Colony Col, string Mission, double Value, double Ci)> Targets(Civ c, Colony home)
    {
        if (_offerPass >= 0 && _targets.TryGetValue(c.Id, out var hit) && hit.Pass == _offerPass) return hit.List;
        var list = new List<(Colony, string, double, double)>();
        var aims = FleetsOf(c).Select(f => f.Target).Where(id => id >= 0).ToHashSet();
        double reach = S("travelYearsMax");
        var ci = new Dictionary<int, double>();
        var steal = new Dictionary<int, bool>();      // whether a power has anything worth stealing depends on the power, not the system
        // our systems each power occupies, found once (not once per power)
        var held = new Dictionary<int, List<Colony>>();
        foreach (var x in c.Colonies) if (x.OccupiedBy >= 0) { if (!held.TryGetValue(x.OccupiedBy, out var l)) held[x.OccupiedBy] = l = new(); l.Add(x); }
        foreach (var o in Known(c))
        {
            // at war, teams work from the front facing this enemy; otherwise from the capital
            var from = Staging(c, o) ?? home;
            var near = Nearest(held.TryGetValue(o.Id, out var occ) ? o.Colonies.Concat(occ) : o.Colonies, aims, from.Star.Pos);
            foreach (var col in near)
            {
                var def = Holder(col);
                if (def == c || def.Dead || !c.Met.Contains(def.Id) || TravelYears(c, (Staging(c, def) ?? home).Star, col.Star) > reach) continue;
                if (!ci.TryGetValue(def.Id, out double d)) ci[def.Id] = d = CounterIntel(def);
                bool war = c.AtWar.Contains(def.Id), rival = c.Relations.GetValueOrDefault(def.Id) < 0;
                foreach (var m in Missions)
                {
                    double v = MissionValue(c, col, m, war, rival);
                    if (v <= 0) continue;
                    bool fits = m == "steal_tech" ? steal.TryGetValue(def.Id, out bool st) ? st : steal[def.Id] = Fits(c, col, def, m) : Fits(c, col, def, m);
                    if (fits) list.Add((col, m, v, d));
                }
            }
        }
        if (_offerPass >= 0) _targets[c.Id] = (_offerPass, list);
        return list;
    }

    /// <summary>
    /// The TargetsPerCiv systems a council looks at: its fleets' targets first, then the nearest to `at`; ties keep their
    /// order. One pass that keeps the best few, not a sort of a thousand-system empire.
    /// </summary>
    static List<Colony> Nearest(IEnumerable<Colony> cols, HashSet<int> aims, Godot.Vector3 at)
    {
        var best = new List<(bool Aim, float D, Colony C)>(TargetsPerCiv + 1);
        foreach (var x in cols)
        {
            bool aim = aims.Contains(x.Star.Id); float d = x.Star.Pos.DistanceSquaredTo(at);
            if (best.Count == TargetsPerCiv && !Before(aim, d, best[^1])) continue;
            int i = best.Count;
            while (i > 0 && Before(aim, d, best[i - 1])) i--;
            best.Insert(i, (aim, d, x));
            if (best.Count > TargetsPerCiv) best.RemoveAt(best.Count - 1);
        }
        return best.Select(b => b.C).ToList();
        static bool Before(bool aim, float d, (bool Aim, float D, Colony C) o) => aim && !o.Aim || aim == o.Aim && d < o.D;
    }

    /// <summary>The council's pick for a ready team. Null keeps the team at home.</summary>
    public (Colony Target, string Mission)? ChooseMission(Civ c, Team t) =>
        ScoredMissions(c, t) is { Count: > 0 } l ? (l[0].Target, l[0].Mission) : null;

    /// <summary>What a mission there is worth to the council (0: not worth doing).</summary>
    double MissionValue(Civ c, Colony col, string m, bool war, bool rival)
    {
        if (m == "assassinate" && c.Ethic == "Reverent") return 0;           // a Reverent council will not order a killing
        if (war)
            return m switch
            {
                "prepare_invasion" => FleetsOf(c).Any(f => f.Target == col.Star.Id) ? 3 : 0,
                "assassinate" => AdmiralAt(col) is { } f ? 2 * (f.Admiral.Wins + 1) : 0,
                "sabotage_yard" => 2,
                "steal_tech" => 1,
                "incite" => 0.6 + col.Grievance / 100,
                "sabotage_deposit" => 1.5,
                _ => 0,
            };
        return m switch
        {
            "steal_tech" => 1.5,
            "incite" => rival ? 0.6 + col.Grievance / 100 : 0,
            "sabotage_deposit" => rival ? 1.5 : 0,
            _ => 0,
        };
    }

    string MissionLabel(string m, Colony c) => m switch
    {
        "sabotage_yard" => $"Sabotage the yards at {c.Sys.Name}",
        "sabotage_deposit" => $"Sabotage the deposits at {c.Sys.Name}",
        "assassinate" => $"Assassinate Admiral {AdmiralAt(c)?.Admiral.Name ?? "?"} at {c.Sys.Name}",
        "incite" => $"Stir up {c.Sys.Name}",
        "steal_tech" => $"Steal {N(Holder(c))}'s secrets at {c.Sys.Name}",
        "prepare_invasion" => $"Prepare {c.Sys.Name} for invasion",
        _ => m,
    };

    /// <summary>
    /// A team is ready (home on guard): the council sends it, or, when the player holds Intelligence, it waits for their
    /// orders. Past three teams one always stays home on guard.
    /// </summary>
    public void OfferTeam(Civ c, Team t)
    {
        if (t.Mission != "guard" || t.To >= 0) return;
        var mine = TeamsOf(c).ToList();
        if (mine.Count >= 3 && !mine.Any(x => x != t && x.Mission == "guard")) return;
        string key = "intel:" + t.Id;
        if (Year < c.Policy.AskAfter.GetValueOrDefault(key)) return;
        if (c.Main && c.Policy.IsManual(Area.Intelligence))
        {
            if (Waiting(key)) return;
            var top = ScoredMissions(c, t).Take(3).ToList();
            var choices = top.Select(p => (MissionLabel(p.Mission, p.Target), (Action)(() =>
            {
                if (Teams.Contains(t) && CanTarget(c, p.Target, p.Mission)) Dispatch(t, p.Target, p.Mission);
            }))).ToList();
            choices.Add(("Keep them home (counter-intelligence)", () => { c.Policy.AskAfter[key] = Year + 3; }));
            Raise("Decision", $"{t.Name} is ready", top.Count == 0
                ? "Nothing within reach is worth the risk. They can guard our own worlds."
                : $"The {(t.Rushed ? "rushed" : "fully trained")} team awaits orders.", Home(c), key, choices);
            return;
        }
        if (ChooseMission(c, t) is { } pick) Dispatch(t, pick.Target, pick.Mission);
        else c.Policy.AskAfter[key] = Year + 3;
    }
}
