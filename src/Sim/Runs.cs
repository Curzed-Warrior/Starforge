using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Starforge.Sim;

/// <summary>
/// Headless runs shared by the game (`Main`, launched with --warp/--trace/--techlog/--savetest) and the engine-free
/// runner in tools/headless: fast-forwarding history with a trace, the war and strife statistics, and the save
/// round-trip test. Nothing here touches the scene tree.
/// </summary>
public static class Runs
{
    /// <summary>A new universe, built exactly as a new game builds it.</summary>
    public static Simulation NewGame(int seed)
    {
        var gal = new Galaxy(seed);
        gal.PlaceSolAndNeighbours();
        gal.GenerateStars(2600);
        return new Simulation(gal, seed);
    }

    /// <summary>Print every chronicle entry as it is written; returns a counter of lines printed.</summary>
    public static Func<int> TraceChronicle(Simulation sim)
    {
        int n = 0;
        sim.Logged += e => { n++; GD.Print($"  {e.Year:0000.0} [{e.Kind}] {(e.Civ.Main ? "" : e.Civ.Name + ": ")}{e.System}: {e.Text}"); };
        foreach (var c in sim.Civs.Where(x => x.Alien)) GD.Print($"ALIEN {c.Name} home={c.Colonies[0].Star.Name} cols={c.Colonies.Count} seat={c.Colonies[0].Seat?.Name}");
        return () => n;
    }

    /// <summary>One line of war statistics at the end of a traced warp (docs/superpowers/plans/2026-10-19-war-in-the-field.md, Task 8).</summary>
    public static void PrintWarStats(Simulation sim, int logLines, double years)
    {
        var wars = sim.Wars; var ended = wars.Where(w => w.Ending != null).ToList();
        string endings = string.Join(",", new[] { "surrender", "compromise", "ceasefire", "status quo", "collapse" }.Select(e => $"{e}:{ended.Count(w => w.Ending == e)}"));
        int fought = sim.Civs.Count(c => wars.Any(w => w.Has(c.Id)));
        GD.Print($"WARSTATS wars={wars.Count} ended={ended.Count} battles={wars.Sum(w => w.Battles)} perwar={(wars.Count > 0 ? wars.Average(w => w.Battles) : 0):0.0} " +
                 $"nobattle={ended.Count(w => w.Battles == 0)} endings={endings} civsfought={fought}/{sim.Civs.Count} chronicle={logLines * 100 / Math.Max(years, 1):0}");
    }

    /// <summary>One line of strife statistics at the end of a traced warp (docs/superpowers/plans/2026-10-19-infighting.md, Task 7).</summary>
    public static void PrintStrifeStats(Simulation sim, int logLines, double years)
    {
        int N(string k) => sim.StrifeStats.GetValueOrDefault(k);
        int humans = sim.Civs.Count(c => c.Human && !c.Dead && c.Colonies.Count > 0);
        GD.Print($"STRIFESTATS movements={N("movements")} revolts={N("revolts")} peaceful={N("peaceful")} civilwars={N("civilwars")} coups={N("coups")} " +
                 $"revolutions={N("revolutions")} uprisings={N("uprisings")} firstSolUprising={(sim.FirstSolUprising > 0 ? sim.FirstSolUprising.ToString("0") : "none")} " +
                 $"humanity={humans} fractured={(humans >= 2 ? "yes" : "no")} chronicle={logLines * 100 / Math.Max(years, 1):0}");
        GD.Print($"FRACTURESTATS humanPeaceful={N("humanPeaceful")} humanWar={N("humanWar")} humanCivil={N("humanCivil")} hiveVotes={N("hiveVotes")} " +
                 $"votes=[{string.Join(" ", sim.HiveVotes)}] humanSystems=[{string.Join(",", sim.Civs.Where(c => c.Human && !c.Dead && c.Colonies.Count > 0).Select(c => c.Colonies.Count))}]");
    }

    /// <summary>One line of special-forces statistics at the end of a traced warp (docs/superpowers/plans/2026-10-20-special-forces.md, Task 5).</summary>
    public static void PrintSpecOpsStats(Simulation sim)
    {
        int N(string k) => sim.SpecStats.GetValueOrDefault(k);
        string by = string.Join(",", Simulation.Missions.Select(m => $"{m}:{N("m:" + m)}"));
        int after = sim.Incidents.Count(i => sim.Wars.Any(w => w.Start >= i.Year && w.Start <= i.Year + 10
            && (w.A.Contains(i.Att) && w.B.Contains(i.Def) || w.A.Contains(i.Def) && w.B.Contains(i.Att))));
        string Rate(string s, string t) => N(t) > 0 ? $"{100.0 * N(s) / N(t):0}%" : "-";
        GD.Print($"SPECOPSSTATS teams={sim.PeakTeams} trained={N("trained")} rushed={N("rushed")} missions={N("missions")} success={N("success")} captured={N("captured")} " +
                 $"aborted={N("aborted")} incidents={sim.Incidents.Count} bymission={by} successTrained={Rate("successTrained", "triedTrained")} " +
                 $"successRushed={Rate("successRushed", "triedRushed")} warsAfterIncident={after}");
    }

    /// <summary>
    /// Fast-forward `years`. With `trace`, a status line every ten years; with `techlog`, the year each tier of each
    /// branch is first reached and a summary of every civ at the end.
    /// </summary>
    public static void Warp(Simulation sim, double years, bool trace, bool techlog)
    {
        var firsts = new SortedDictionary<string, double>();
        Action<Civ, Tech> rec = (civ, t) =>
        {
            string who = civ.Main ? "Humanity" : civ.Name;
            string k = $"{who} {t.B} T{t.Tier:00}";
            if (!firsts.ContainsKey(k)) firsts[k] = sim.Year;
            if (civ.Main && (t.Spine || t.Keystone || t.Gate != null || t.Path != null)) GD.Print($"TECH {sim.Year:0000.0} {t.Id} {t.Name}");
        };
        if (techlog) Simulation.TechLog += rec;
        double end = sim.Year + years, next = sim.Year;
        try
        {
            while (sim.Year < end)
            {
                sim.Step(Math.Min(0.5, end - sim.Year));
                if (trace && sim.Year >= next)
                {
                    next += 10;
                    var h = sim.HomeColony; var hu = sim.Humanity;
                    GD.Print($"D {sim.Year:0000} P={hu.Power:0.00e0}W K={Defs.Kardashev(hu.Power):0.000} KG={Defs.Kardashev(hu.GalacticPower):0.000} pop={hu.Pop:0.0} cap={h.Capture:0.0e0} stock={h.Stock / h.PFrac:0.00}P fnd={h.Foundries} hubs={string.Join(",", h.Hubs.Select(kv => kv.Key.Name + kv.Value))} set={string.Join(",", h.Settled.Select(kv => kv.Key.Name))} techs={hu.Techs.Count} res={string.Join("/", hu.Slots.Values.Select(sl => sl.Id + ":" + sl.P.ToString("0")))} rp={h.RpRate:0.0} cols={hu.Colonies.Count} ships={sim.Ships.Count} civs={string.Join(" ", sim.Civs.Where(c => !c.Dead).Select(c => c.Name.Split(' ').Last() + ":" + c.Colonies.Count))}");
                }
            }
        }
        finally { if (techlog) Simulation.TechLog -= rec; }
        if (!techlog) return;
        foreach (var (k, y) in firsts) if (k.StartsWith("Humanity")) GD.Print($"FIRST {k} {y:0000}");
        var hum = sim.Humanity;
        GD.Print($"TECHLOG year={sim.Year:0} humanity techs={hum.Techs.Count} spine={hum.Techs.Count(id => Defs.TechById(id).Spine)} path={hum.Path} hull={Defs.Hulls[hum.Hull]} war={sim.WarStrength(hum):0.0} pools=[{string.Join(",", hum.Pools.Select(kv => kv.Key + "=" + kv.Value.ToString("0.00")))}] meters=[{string.Join(",", hum.Meters.Where(kv => kv.Value > 0.5).Select(kv => kv.Key + "=" + kv.Value.ToString("0")))}] gates=[{string.Join(",", hum.Gates.Select(kv => kv.Key + ":" + kv.Value))}]");
        GD.Print("BUILT " + string.Join(" ", sim.HomeColony.Built.OrderBy(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value:0.00}")));
        var unknown = Defs.Techs.Where(t => !hum.Knows(t.Id)).ToList();
        GD.Print($"UNKNOWN {unknown.Count}: avail={unknown.Count(t => sim.Available(hum, t))} excl={unknown.Count(t => !Simulation.ExclusiveOk(hum, t))} gate={unknown.Count(t => t.Gate != null && hum.Gates.GetValueOrDefault(t.Gate) < 2)} prereq={unknown.Count(t => !hum.PrereqDone(t))} tier={unknown.Count(t => !Simulation.TierOk(hum, t))} cond={unknown.Count(t => !sim.Gate(hum, t))} banned={hum.Banned.Count}");
        GD.Print("AVAIL " + string.Join(" ", unknown.Where(t => sim.Available(hum, t)).Select(t => t.Id)));
        foreach (var c in sim.Civs.Where(c => !c.Dead && c.Colonies.Count > 0))
            GD.Print($"CIV {c.Name} age={c.Age} techs={c.Techs.Count} maxT={c.Techs.Max(id => Defs.TechById(id).Tier)} cols={c.Colonies.Count} war={sim.WarStrength(c):0} apo={c.ApoScore} exalted={c.Exalted} fallen={c.Fallen} path={c.Path} acts={c.M.Acts.Count} gates={string.Join(",", c.Gates.Where(kv => kv.Value == 2).Select(kv => kv.Key))}");
    }

    /// <summary>
    /// Save, load, save again and compare (the two files must be byte-identical), then run the loaded universe on.
    /// Returns true when the round trip held.
    /// </summary>
    public static bool SaveTest(Simulation sim, int seed)
    {
        // exercise the voice's edits to the universe so they round-trip too
        var sol = sim.Gal.Sol; var nb = sim.Gal.Stars[sol.Near[5]];
        sim.KindleStar(sol.Pos); sim.ForgeWorld(sim.SystemFor(nb), BodyKind.Ocean); sim.OpenLane(sol, nb); sim.Quarantine(sim.Gal.Stars[sol.Near[9]], 5);
        sim.RewriteLaw("research2", true); sim.SnuffStar(sim.Gal.Stars[sol.Near[12]]); sim.MoveStar(sim.Gal.Stars[sol.Near[14]], sol.Pos + new Vector3(0.7f, 0, 0.2f));
        sim.GiftTech(sim.Humanity, Defs.TechById("UMB-2")); sim.SteerBan(sim.Humanity, Defs.TechById("S9"), 50);
        // and the player's policies
        var pol = sim.Humanity.Policy; pol.Manual[(int)Area.Research] = true; pol.Weight["War"] = 1.6f; pol.Declined.Add("ringworld"); pol.PauseOn.Remove("Contact"); pol.AskAfter["war:3"] = 2400;
        sim.HomeColony.WorksOverride = 0.4;
        SaveGame.Save(sim, seed, "user://t1.json");
        var (s2, _) = SaveGame.Load("user://t1.json");
        SaveGame.Save(s2, seed, "user://t2.json");
        string a = FileAccess.GetFileAsString("user://t1.json"), b = FileAccess.GetFileAsString("user://t2.json");
        GD.Print($"SAVETEST equal={a == b} len={a.Length} colonies={sim.Colonies.Count}/{s2.Colonies.Count} ships={sim.Ships.Count}/{s2.Ships.Count}");
        if (a != b) { int i = 0; while (i < a.Length && i < b.Length && a[i] == b[i]) i++; GD.Print("diff at " + i + ": " + a.Substring(Math.Max(0, i - 120), Math.Min(240, a.Length - Math.Max(0, i - 120)))); GD.Print("vs: " + b.Substring(Math.Max(0, i - 120), Math.Min(240, b.Length - Math.Max(0, i - 120)))); }
        // and the loaded universe must keep running
        s2.Step(200);
        GD.Print($"SAVETEST after 200y: year={s2.Year:0} humanity={s2.Humanity.Colonies.Count}");
        return a == b;
    }
}
