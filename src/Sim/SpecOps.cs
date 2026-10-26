using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

/// <summary>
/// Special forces (war-and-strife spec §4.6): a few expensive elite teams, trained in an academy and sent into systems
/// their civ does not control. Quality comes from training time, by the armies' rule: a course begun in peacetime is the
/// full course and turns out the best teams; one begun at war is rushed, quicker and worse.
/// </summary>
public partial class Simulation
{
    static double S(params string[] path) => WarData.D(new[] { "specops" }.Concat(path).ToArray());

    /// <summary>How many teams (trained or in training) a civ may keep: few, more with a bigger empire. None without an intelligence agency.</summary>
    public int TeamCap(Civ c)
    {
        if (!c.M.Acts.Contains("spy")) return 0;
        return (int)Math.Min(S("teamsMax"), S("teamsBase") + Math.Floor(Math.Log2(1 + c.Colonies.Count)));
    }

    /// <summary>Yearly: finished courses become teams at home, teams are paid for (the newest disbanded when they cannot be), and new courses begin up to the cap.</summary>
    void AcademyStep(Civ c, double dy)
    {
        var home = c.Colonies.FirstOrDefault(x => x.Home) ?? c.Colonies.FirstOrDefault();
        if (home == null) return;
        int cap = TeamCap(c);
        if (cap == 0 && c.Academy.Count == 0 && !TeamsOf(c).Any()) return;

        for (int i = c.Academy.Count - 1; i >= 0; i--)
        {
            var t = c.Academy[i];
            t.Left -= dy;
            if (t.Left > 1e-9) continue;
            c.Academy.RemoveAt(i);
            int n = TeamsOf(c).Count();
            Teams.Add(new Team
            {
                Id = NextTeamId++, Civ = c.Id, At = home.Star.Id, Rushed = t.Rushed,
                Name = Forces.Ordinals[n % Forces.Ordinals.Length] + " Special Group",
                Quality = (float)S(t.Rushed ? "rushedQuality" : "trainedQuality"),
            });
            SpecCount(t.Rushed ? "rushed" : "trained");
            PeakTeams = Math.Max(PeakTeams, Teams.Count);
        }

        double kg = S("kgPerTeam"), upk = S("upkeepPerYear") * kg * dy;
        var mine = TeamsOf(c).OrderByDescending(t => t.Id).ToList();
        double bill = upk * mine.Count;
        foreach (var t in mine)
        {
            if (c.Materiel >= bill) break;
            Teams.Remove(t); bill -= upk;
        }
        c.Materiel = Math.Max(0, c.Materiel - bill);

        // a team on its way to a target that has changed hands, gone or become pointless is recalled
        foreach (var t in TeamsOf(c).Where(t => t.To >= 0 && t.Mission != "guard").ToList())
            if (!StillOn(t, ColonyAtAny(t.Target))) { SpecCount("aborted"); t.To = -1; Dispatch(t, null, "guard"); }

        // a team home for a year is ready for its next mission
        foreach (var t in TeamsOf(c).Where(t => t.Mission == "guard" && t.To < 0 && Year >= t.Arrive + 1).ToList()) OfferTeam(c, t);

        bool war = c.AtWar.Count > 0;
        double men = S("manpowerPerTeam");
        while (TeamsOf(c).Count() + c.Academy.Count < cap && c.Materiel >= kg && c.Manpower >= men)
        {
            c.Materiel -= kg; c.Manpower -= men;
            c.Academy.Add(new Training { Troops = 1, Left = S(war ? "rushedYears" : "trainYears"), Rushed = war, At = home.Star.Id });
        }
    }

    // ---------- Missions ----------
    public static readonly string[] Missions = { "sabotage_yard", "sabotage_deposit", "assassinate", "incite", "steal_tech", "prepare_invasion" };
    static string MissionAct(string mission) => WarData.P("specops", "missions", mission, "act").GetString();
    static double M(string mission, string key) => S("missions", mission, key);

    /// <summary>This colony's slips that can work: none while its yards are sabotaged (the slips stay, idle).</summary>
    public int WorkingSlips(Colony c) => c.YardDownUntil > Year ? 0 : SlipsOf(c);

    /// <summary>The fleet whose admiral a team would kill at this colony: the holder's biggest fleet parked there.</summary>
    Fleet AdmiralAt(Colony c) =>
        Fleets.Where(f => f.Civ == Holder(c).Id && f.At == c.Star.Id && !f.Moving && f.Admiral != null).OrderByDescending(f => f.Ships).FirstOrDefault();

    /// <summary>
    /// Can this civ's teams work on this colony? Only in systems it does not control, held by a civ it has met; the
    /// mission's act must be known; the colony must be within reach of where the teams work from (Staging); and there
    /// must be something to work on.
    /// </summary>
    public bool CanTarget(Civ att, Colony c, string mission)
    {
        if (c == null || c.Civ == null || c.Civ.Dead || !c.Civ.Colonies.Contains(c)) return false;
        var holder = Holder(c);
        if (holder == att || holder.Dead || !att.Met.Contains(holder.Id)) return false;
        var from = Staging(att, holder);
        if (from == null || TravelYears(att, from.Star, c.Star) > S("travelYearsMax")) return false;
        return Fits(att, c, holder, mission);
    }

    readonly Dictionary<(int, int), (double Year, Colony At)> _staging = new();
    /// <summary>
    /// Where a civ's teams work from against a holder: at war, the system facing them across the front (Frontier), which
    /// they reach through their own space in specops.deployYears; in peace, the capital. Worked out once a step per pair.
    /// </summary>
    public Colony Staging(Civ att, Civ holder)
    {
        if (!att.AtWar.Contains(holder.Id)) return Home(att);
        var key = (att.Id, holder.Id);
        if (_staging.TryGetValue(key, out var s) && s.Year == Year && s.At?.Civ == att) return s.At;
        var at = Frontier(att, new() { holder }) ?? Home(att);
        _staging[key] = (Year, at);
        return at;
    }

    /// <summary>CanTarget past the reach and ownership checks: the act is known and there is something to work on.</summary>
    internal bool Fits(Civ att, Colony c, Civ holder, string mission)
    {
        if (!att.M.Acts.Contains(MissionAct(mission))) return false;
        return mission switch
        {
            // acts of war only at war; and never against a world that is not its holder's own (an occupied colony is wrecked for its owner)
            "sabotage_yard" => att.AtWar.Contains(holder.Id) && c.Civ == holder && WorkingSlips(c) > 0,
            "sabotage_deposit" => c.Civ == holder && c.Res.Strategic.Count > 0 && c.DepositsDownUntil <= Year,
            "assassinate" => att.AtWar.Contains(holder.Id) && AdmiralAt(c) != null,
            "incite" => c.Civ == holder && !holder.M.Hive,
            "steal_tech" => holder.Techs.Any(id => Available(att, Defs.TechById(id))),
            "prepare_invasion" => att.AtWar.Contains(holder.Id),
            _ => false,
        };
    }

    /// <summary>Is a sent team's mission still on: the same civ holds the target, and the mission can still be done there?</summary>
    bool StillOn(Team t, Colony c) => c != null && Holder(c).Id == t.TargetCiv && CanTarget(Civs[t.Civ], c, t.Mission);

    /// <summary>Send a team: to a colony on a mission, or ("guard") home, where it adds to counter-intelligence.</summary>
    public void Dispatch(Team t, Colony target, string mission)
    {
        var civ = Civs[t.Civ];
        bool guard = mission == "guard" || target == null;
        var dest = guard ? Home(civ)?.Star : target.Star;
        if (dest == null) return;
        int was = t.TargetCiv;
        t.Mission = guard ? "guard" : mission;
        t.Target = guard ? -1 : target.Star.Id;
        t.TargetCiv = guard ? -1 : Holder(target).Id;
        t.Due = 0;
        if (t.At == dest.Id) { t.To = -1; t.Arrive = Year; if (!guard) t.Due = Year + S("onSiteYears"); return; }
        // out through our own space to where we work from (Staging), then across; home the same way back
        var from = Gal.Stars[t.At];
        bool inOwn = ColonyAtAny(t.At)?.Civ == civ;
        double years;
        if (!guard)
        {
            var front = Staging(civ, Holder(target));
            years = inOwn && front != null && front.Star != from ? S("deployYears") + TravelYears(civ, front.Star, dest) : TravelYears(civ, from, dest);
        }
        else
        {
            var back = !inOwn && was >= 0 && was < Civs.Count ? Frontier(civ, new() { Civs[was] }) : null;
            years = back != null && back.Star != dest ? TravelYears(civ, from, back.Star) + S("deployYears") : TravelYears(civ, from, dest);
        }
        t.To = dest.Id; t.Arrive = Year + years;
    }

    /// <summary>A civ's defence against spies: its own espionage skill, raised by the teams it keeps at home on guard.</summary>
    public double CounterIntel(Civ d) =>
        d.M.Spy * (1 + S("guardPerTeam") * TeamsOf(d).Where(t => t.Mission == "guard" && t.To < 0).Sum(t => t.Quality));

    /// <summary>A team's craft: its training, its experience and its civ's espionage skill.</summary>
    double Craft(Team t) => Math.Max(0.05, t.Quality * (1 + t.Xp) * Civs[t.Civ].M.Spy);

    public double CaptureChance(Team t, Civ def, string mission) => CaptureChance(t, CounterIntel(def), mission);
    double CaptureChance(Team t, double ci, string mission) =>
        Math.Clamp(M(mission, "risk") * ci / (Craft(t) * Math.Max(0.2, Civs[t.Civ].M.Stealth)), S("captureMin"), S("captureMax"));

    public double SuccessChance(Team t, Civ def, string mission) => SuccessChance(t, CounterIntel(def), mission);
    double SuccessChance(Team t, double ci, string mission) =>
        Math.Min(0.95, M(mission, "success") * Craft(t) / Math.Sqrt(Math.Max(0.05, ci)));

    /// <summary>Per tick: teams travel; a team's time on site ends in capture, success or quiet failure, and it heads home.</summary>
    void SpecOpsStep(double dy)
    {
        if (Teams.Count == 0) return;
        foreach (var t in Teams.ToList())
        {
            if (t.To >= 0)
            {
                if (Year < t.Arrive) continue;
                t.At = t.To; t.To = -1;
                if (t.Mission != "guard") t.Due = Year + S("onSiteYears");
                continue;
            }
            if (t.Mission == "guard" || Year < t.Due) continue;
            var c = ColonyAtAny(t.Target);
            var att = Civs[t.Civ];
            string mission = t.Mission;
            if (!StillOn(t, c)) { SpecCount("aborted"); Dispatch(t, null, "guard"); continue; }
            var def = Holder(c);
            SpecCount("missions"); SpecCount("m:" + mission); SpecCount(t.Rushed ? "triedRushed" : "triedTrained");
            if (_r.NextDouble() < CaptureChance(t, def, mission)) { Captured(t, def, c); continue; }
            if (_r.NextDouble() < SuccessChance(t, def, mission)) { Succeed(t, c, mission); SpecCount("success"); SpecCount(t.Rushed ? "successRushed" : "successTrained"); }
            t.Xp = Math.Min(1f, t.Xp + (float)S("xpPerMission"));
            Dispatch(t, null, "guard");
        }
    }

    /// <summary>Counters for the trace's SPECOPSSTATS line.</summary>
    public readonly Dictionary<string, int> SpecStats = new();
    /// <summary>The most teams alive at once, across every civ (the trace).</summary>
    public int PeakTeams;
    void SpecCount(string what) => SpecStats[what] = SpecStats.GetValueOrDefault(what) + 1;

    /// <summary>Incidents so far: whose team, whose world, when (the trace counts the wars that follow them).</summary>
    public readonly List<(int Att, int Def, double Year)> Incidents = new();

    /// <summary>
    /// A team is caught: it is lost, and its civ's hand is shown. Relations fall both ways, its spy networks are exposed
    /// a little more (the Spy meter), and the player hears of it whichever side they are on.
    /// </summary>
    public void Captured(Team t, Civ def, Colony at = null)
    {
        var att = Civs[t.Civ];
        Teams.Remove(t);
        SpecCount("captured");
        float r = (float)Math.Max(-100, def.Relations.GetValueOrDefault(att.Id) + S("incidentRelation"));
        def.Relations[att.Id] = r; att.Relations[def.Id] = r;
        att.AddMeter("Spy", S("spyMeterOnCapture"));
        Incidents.Add((att.Id, def.Id, Year));
        at ??= Home(def);
        string sys = at?.Sys.Name ?? "their worlds";
        Say(att, at, $"{Cap(N(att))}'s {t.Name} is caught at {sys}. {Cap(N(def))} parade them before the cameras.", "loss");
        if (att.Main || def.Main)
            Raise("Incident", att.Main ? $"{t.Name} captured at {sys}" : $"Spies of {N(att)} caught at {sys}",
                  att.Main ? $"{Cap(N(def))} have caught one of our special groups and are telling the galaxy."
                           : $"A special group of {N(att)} was caught working at {sys}. Relations with them have soured.", at);
    }

    /// <summary>A mission succeeds. The chronicle tells it when the player is either side.</summary>
    public void Succeed(Team t, Colony c, string mission)
    {
        var att = Civs[t.Civ]; var def = Holder(c);
        string sys = c.Sys.Name, what;
        switch (mission)
        {
            case "sabotage_yard":
                c.YardDownUntil = Year + M(mission, "years");
                // what was on its slips is lost (the civ's Yard list holds slips colony by colony)
                int first = def.Colonies.TakeWhile(x => x != c).Sum(SlipsOf);
                for (int i = first; i < Math.Min(def.Yard.Count, first + SlipsOf(c)); i++) { def.Yard[i].Progress = 0; def.Yard[i].Paid = 0; }
                what = $"The shipyards at {sys} burn. Nothing will be launched there for years.";
                break;
            case "sabotage_deposit":
                c.DepositsDownUntil = Year + M(mission, "years");
                what = $"The deposits at {sys} are wrecked. It will be years before they yield again.";
                break;
            case "assassinate":
                var f = AdmiralAt(c);
                if (f == null) return;
                string dead = f.Admiral.Name;
                f.Admiral = NewAdmiral(def); f.Morale *= 0.7f;
                what = $"Admiral {dead} of {N(def)} is killed at {sys}. {f.Name} has a new commander nobody knows.";
                break;
            case "incite":
                c.IncitedUntil = Year + M(mission, "years");
                what = $"Agitators are at work in {sys}. Old grievances there are louder than they were.";
                break;
            case "steal_tech":
                var tech = StealTech(att, def);
                what = tech == null ? null : $"{Cap(N(att))} come home from {sys} with the secrets of {tech.Name}.";
                break;
            case "prepare_invasion":
                c.PreparedBy = att.Id; c.PreparedUntil = Year + M(mission, "years");
                what = $"Saboteurs ready {sys} for invasion. Its defenders will not know what hit them.";
                break;
            default: return;
        }
        if (what != null && (att.Main || def.Main)) Log(c, what, def.Main ? "loss" : "event", null);
    }
}
