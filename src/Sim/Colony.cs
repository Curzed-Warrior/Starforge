using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Starforge.Sim;

public enum MissionKind { Hub, Settle, Station, Survey }

/// <summary>A construction voyage inside a system: a convoy leaves the home body and does work on arrival.</summary>
public class Mission
{
    public Body Target, From;
    public MissionKind Kind;
    public double T, Dur;
}

public class Crisis { public string Id; public double T0; public Body Target; public double Acc; }

public class Rival { public string Name; public double Pop; public int Ships; public Body Planet; public double T0; public string Stance; }

public class Native { public string Name; public int Level; public string Contact; public double T, ClearAt; public bool Protected; }

/// <summary>
/// A civilisation's presence in one star system: the phone game's whole economy (hubs, foundries, swarm),
/// plus settlements, stations and megaprojects. The home system starts as Earth in 2061.
/// </summary>
public partial class Colony
{
    public Simulation Sim;
    public Civ Civ;
    public StarSystem Sys;
    public StarInfo Star;
    public bool Home;
    /// <summary>War in the field: the civ occupying this system (-1 when free).</summary>
    public int OccupiedBy = -1;
    public double Founded;
    public double L;                       // star luminosity, watts
    public double PlanetPower;             // planet-bound or fleet power, watts
    public double PowerCap;                // ceiling for PlanetPower at seed colonies
    public double Capture;                 // fraction of the star's output caught by the swarm
    public double Stock;                   // swarm units of refined mass
    public int Foundries = 1;
    public float Boost = 1;
    public double Pop, PopCap, PeakPop;
    public Body Seat;                      // capital world (Earth) or the fleet's parking body
    public readonly Dictionary<Body, int> Hubs = new();
    public readonly Dictionary<Body, float> Settled = new();
    public readonly Dictionary<Body, int> Stations = new();
    /// <summary>Megastructures raised here by the unseen voice, whether or not anyone here knows how to build them.</summary>
    public readonly HashSet<string> Raised = new();
    public readonly List<Mission> Missions = new();
    public readonly Dictionary<Body, Native> Natives = new();
    public float ElevatorP = -1, RingP = -1, TerraformP = -1;
    public int Habitats;
    public string Project; public float ProjectP; public bool ProjectDone;
    public Crisis Crisis;
    public Rival Rival;
    public (string Faction, double Pop, double At, double Back, bool Returned)? Splinter;
    public int Era = -1;
    public bool Complete;
    public double NextDecision, NextEvent, NextSeed;
    public int SeedsSent;
    public List<string> FrigateNames = new();
    public double VisitorT = -1; public int VisitorStage; public string VisitorOutcome, VisitorForced; public bool VisitorDone;
    public double HomeDark = -1;
    public double TrafficLevel;             // 0..1 visual busyness
    /// <summary>How much of the usual traffic is flying: ports shut in a plague, convoys hold in war and flare storms, a quarantine seals the system.</summary>
    public double TrafficFactor => (Crisis?.Id switch { "plague" => 0.06, "war" => 0.45, "storms" => 0.55, "integration" => 0.85, _ => 1.0 })
        * (Sim.Quarantines.Any(q => Star.Pos.DistanceTo(q.Center) < q.Radius) ? 0.25 : 1.0);
    /// <summary>A swarm round a star with living or lived-on worlds leaves the planets' plane open, so their skies stay lit
    /// (a Ruthless people don't bother).</summary>
    public bool EclipticOpen => Civ.Ethic != "Ruthless" && (Home || Settled.Keys.Any(b => b.Life || b.Greening > 0.3f) || Natives.Count > 0 || Settled.Count > 0);
    /// <summary>The most of the star the swarm may take: an open ecliptic leaves about a tenth of the sky clear.</summary>
    public double CaptureMax => EclipticOpen ? 0.92 : 1.0;
    public int Gen = 1;                     // seed-ship hops from the homeworld
    public double Region;                   // 0..1 how much of this star's surrounding region its daughters have wrapped
    /// <summary>Each simulated star stands for ~40 million real ones; a finished colony slowly harvests its region.</summary>
    public const double RegionStars = 3.8e7;
    public double GalacticPower => Power + Region * RegionStars * Defs.LSun * 0.6 * Civ.M.RegionPower;

    const double Tempo = 0.25;
    /// <summary>Most of the sky a swarm can add in a year (fraction of the star's light).</summary>
    public static double SwarmPace = 0.004;
    readonly List<Body> _settledTmp = new();
    public const double P0 = 2.6e13;        // humanity's power in 2061

    public Colony(Simulation sim, Civ civ, StarSystem sys, bool home, double year)
    {
        Sim = sim; Civ = civ; Sys = sys; Star = sys.Star; Home = home; Founded = year;
        L = Defs.LSun * Math.Max(Star.Lum, 0.0005);
        NextDecision = year + 0.5; NextEvent = year + 6 + civ.Rnd(0, 8); NextSeed = year + 30;
        Star.Owner = civ.Id; Star.Visited = true;
    }

    /// <summary>Routine news from here reaches the chronicle: the home system, the one being watched, or a young empire's
    /// few colonies. (Three hundred systems each eating their moons would otherwise be most of history.)</summary>
    public bool Newsworthy => Home || Sim.Focus == this || Civ.Colonies.Count <= 8;

    public double Power => PlanetPower + Capture * L + MegaPower;
    public double PFrac => Power / L;
    public double Kardashev => Defs.Kardashev(Power);
    Mods M => Civ.M;
    double Year => Sim.Year;

    // ---------- Rates ----------
    public double YieldOf(Body b) => b.Kind switch
    {
        BodyKind.Gas or BodyKind.IceGiant => 0.25 * M.GasYield,
        BodyKind.Belt => 1.6, BodyKind.Lava => 1.3, BodyKind.Rocky => 1.0, BodyKind.Moon => 1.1,
        BodyKind.Desert => 0.9, BodyKind.Ocean => 0.8, BodyKind.Terran => 0.8, BodyKind.Ice => 0.7, _ => 1.0
    };
    public static double MassUnits(Body b) => b.Mass * 5.43;
    public int HubCount
    {
        get
        {
            int n = 0;
            foreach (var kv in Hubs) if (kv.Value > 0) n++;
            foreach (var m in Missions) if (m.Kind == MissionKind.Hub) n++;
            return n;
        }
    }
    bool Busy(Body b) => Missions.Any(m => m.Target == b && m.Kind != MissionKind.Station);
    public double HubCost(Body b)
    {
        int l = Hubs.GetValueOrDefault(b);
        return M.HubCost * (l == 0 ? 0.4 * PFrac * Math.Pow(1.6, Math.Max(0, HubCount - 1)) : 0.6 * PFrac * Math.Pow(1.8, l));
    }
    public double FoundryCost => 0.5 * PFrac * Math.Pow(1.7, Foundries - 1);
    public double RpRate => (0.8 + 1.5 * Math.Log10(1 + Power / P0)) * M.Rp * (1 + 0.15 * Math.Log10(Math.Max(1, Pop / 0.04))) * (Home ? 1.0 : 0.5 / (1 + Civ.Colonies.Count / 25.0)) * MegaResearch * Sim.WarResearch(Civ);

    /// <summary>Advance by dy years in sub-steps (finer when watched).</summary>
    double _buildAcc;
    const double BuildTick = 0.02;

    public void Step(double dyTotal, bool fine)
    {
        // great works advance in steps of about a week of game time: the bookkeeping is heavy (and makes garbage), and
        // nothing the size of a megastructure visibly changes in a day. At high speed every step is a week or more anyway.
        _buildAcc += dyTotal;
        if (_buildAcc >= BuildTick) { BuildStep(_buildAcc); _buildAcc = 0; }
        if (Complete && Home) GrowRegion(dyTotal);
        if (Complete && !Home) { SlowStep(dyTotal); return; }
        double maxStep = fine ? 0.05 : 2.0;
        int n = Math.Max(1, (int)Math.Ceiling(dyTotal / maxStep));
        double dy = dyTotal / n;
        for (int k = 0; k < n; k++)
        {
            double P = PFrac;
            foreach (var (b, lvl) in Hubs)
            {
                if (lvl <= 0 || b.Mined >= 1) continue;
                double mass = MassUnits(b), limit = SurfaceOnly(b) ? 0.08 : 1, left = mass * (limit - b.Mined);
                if (left <= 0) continue;
                double take = Math.Min(left, Tempo * 0.16 * P * lvl * YieldOf(b) * M.Mine * MegaMine * Boost * dy);
                b.Mined = (float)Math.Min(1, b.Mined + take / mass);
                Stock += take;
                if (b.Mined >= 0.9999f)
                {
                    b.Mined = 1;
                    if (Newsworthy) Sim.Log(this, $"{b.Name} is gone. Every gram of it is in orbit now.", "loss", b);
                }
            }
            if (M.StarLift) Stock += Tempo * 0.3 * P * M.Mine * LiftShare * Boost * dy;
            double build = Math.Min(Stock, Tempo * 0.08 * P * Foundries * M.Foundry * Boost * dy);
            if (Civ.Emergency) build *= 0.1;          // the swarm waits: everything goes to the defences
            build *= 1 - Civ.MilShare;                 // the war industry's share
            if (M.Swarm)
            {
                // a swarm is raised collector by collector: however rich the system, it adds at most a few
                // tenths of a percent of the sky a year, so a full swarm is the work of centuries, not decades
                double add = Math.Min(build * M.Eff, SwarmPace * (1 + 0.25 * Math.Max(Era - 2, 0)) * dy);
                add = Math.Min(add, CaptureMax - Capture);
                if (add > 0) { Stock -= add / Math.Max(M.Eff, 1e-9); Capture += add; }
            }
            // planet-bound power grows toward the technology ceiling; foundries speed it up
            double cap = Home ? M.PlanetCap : PowerCap;
            double rate = 0.03 + 0.004 * Math.Min(Foundries, 10);
            PlanetPower += PlanetPower * rate * (1 - PlanetPower / cap) * dy;
            Sim.AddResearch(Civ, RpRate * dy);
            for (int i = Missions.Count - 1; i >= 0; i--)
            {
                var m = Missions[i];
                m.T += dy;
                if (m.T >= m.Dur) { Missions.RemoveAt(i); Arrive(m); }
            }
        }
        // settlements mature, population follows housing
        double homes = 0;
        _settledTmp.Clear();
        _settledTmp.AddRange(Settled.Keys);
        foreach (var b in _settledTmp)
        {
            Settled[b] = Math.Min(1f, Settled[b] + (float)(dyTotal / (Home ? 40 : 60)));
            homes += Settled[b] * Housing(b);
            if (b.Mined > 0.5f && b != Seat) Settled.Remove(b);
        }
        double habs = 40 * Math.Pow(Capture, 0.7) * (M.Habitat ? 1.5 : 0.3);
        PopCap = (0.05 + homes + habs + MegaHousing) * M.PopCap;
        Pop = Math.Clamp(Pop + Pop * 0.03 * M.Growth * Sim.WarGrowth(Civ) * (1 - Pop / Math.Max(PopCap, 0.01)) * Math.Min(dyTotal, 5), 0.001, 1e6);
        PeakPop = Math.Max(PeakPop, Pop);
        if (ElevatorP >= 0 && ElevatorP < 1) { ElevatorP = Math.Min(1, ElevatorP + (float)(dyTotal / 22)); if (ElevatorP >= 1) Sim.Log(this, Narrative.ElevatorDone(this), "era", Seat); }
        if (RingP >= 0 && RingP < 1) { RingP = Math.Min(1, RingP + (float)(dyTotal / 45)); if (RingP >= 1) Sim.Log(this, Narrative.RingDone(this), "era", Seat); }
        var mars = Home && TerraformP >= 0 ? Sys.Find("Mars") : null;
        if (TerraformP >= 0 && TerraformP < 1 && mars != null)
        {
            TerraformP = Math.Min(1, TerraformP + (float)(dyTotal / 260));
            if (TerraformP >= 0.5f && TerraformP - dyTotal / 260 < 0.5f) Sim.Log(this, "Mars has a sky now. It is thin and pink and it is enough to stand under without a helmet for a minute.", "era", mars);
            if (TerraformP >= 1) Sim.Log(this, "Mars is finished. Its first ocean is shallow and cold and full of things that came from Earth.", "era", mars);
        }
        int wantHabs = M.Habitat ? (int)Math.Min(60, 3 + Capture * 120) : 0;
        if (Habitats < wantHabs && Stock > 0.02 * PFrac) { Habitats++; Stock -= 0.02 * PFrac; if (Habitats == 1 && Home) Sim.Log(this, "The first O'Neill cylinder is spun up at L5. Eight kilometres of farmland, curving up into the sky.", "era", Seat); }
        if (fine) TrafficLevel = Math.Clamp(0.15 + Settled.Count * 0.08 + HubCount * 0.05 + Missions.Count * 0.1, 0, 1);

        if (Year >= NextDecision) { Decide(); NextDecision = Year + 1.2; }
        if (Year >= NextEvent) { Narrative.RandomEvent(this); NextEvent = Year + (Sim.Focus == this ? 6 + Civ.Rnd(0, 12) : 25 + Civ.Rnd(0, 40)); }
        if (Crisis != null) Narrative.CrisisStep(this, dyTotal);
        if (Rival != null || Splinter.HasValue) Narrative.RivalStep(this);
        if (Natives.Count > 0) Narrative.NativesStep(this, dyTotal);
        if (Home) Narrative.VisitorStep(this);
        ProjectStep(dyTotal);
        // eras by absolute power
        while (Era + 1 < Defs.Eras.Length && Power >= Defs.Eras[Era + 1].At)
        {
            Era++;
            if (Home || Sim.Focus == this) { Sim.Log(this, Defs.Eras[Era].Text.Replace("the Sun", Home ? "the Sun" : Sys.Name), "era", null); Narrative.SpeakerSays(this); }
            if (Home && Civ.Main) Sim.RaiseEra(this);
        }
        if (!Complete && Capture >= 0.9)
        {
            Complete = true;
            if (Home || Sim.Focus == this || Civ.Colonies.Count <= 8)
                Sim.Log(this, $"{Sys.Name}: the swarm is complete. From outside, the star has faded to a dull infrared smudge. Anyone watching would know exactly what that means.", "era", null);
            if (Home || Sim.Focus == this || Civ.Main && Civ.Colonies.Count < 8) Sim.Log(this, Epitaph(), "council", null);
            Sim.Completed(this);
        }
    }

    /// <summary>Daughter flotillas spread through the region around a finished system: logistic, and only once fold drives make it practical.</summary>
    void GrowRegion(double dy)
    {
        if (!Civ.M.Fold) return;
        double r = 0.012 * Civ.M.RegionRate;
        Region = Math.Min(1, Region + r * (Region + 0.0005) * (1 - Region) * dy);
    }

    /// <summary>A finished system only launches seeds and grows slowly.</summary>
    void SlowStep(double dy)
    {
        GrowRegion(dy);
        Sim.AddResearch(Civ, RpRate * dy);
        if (PopCap <= 0.01) PopCap = Math.Max(Pop * 1.3, 1);
        Pop = Math.Max(0.001, Pop + Pop * 0.01 * (1 - Pop / PopCap) * dy);
        if (Year >= NextDecision) { TrySeed(); NextDecision = Year + 3; }
    }

    /// <summary>Worlds people live on (and Earth's Moon) only get surface works unless the civilisation has turned ruthless.</summary>
    public bool SurfaceOnly(Body b) => (Settled.ContainsKey(b) || Home && b.Name == "Moon") && !(Civ.Ethic == "Ruthless" && Capture > 0.5);

    double Housing(Body b)
    {
        if (b == Seat && Home) return 11.5;
        if (b.Name == "Mars") return 1.2 + 5 * Math.Max(Math.Max(0, TerraformP), b.Greening);
        if (b.Name == "Moon") return 0.35;
        if (b.Name == "Venus") return 1.0;
        if (b.Life) return 4.5;
        if (b.Greening > 0) return 1.5 + 3 * b.Greening;      // a world the voice is bringing to life
        return b.Kind switch { BodyKind.Gas or BodyKind.IceGiant => 2.0, BodyKind.Belt => 0.4, BodyKind.Moon => 0.35, _ => 1.5 };
    }

    public bool CanSettle(Body b)
    {
        if (Settled.ContainsKey(b) || Busy(b) || b.Mined > 0.05f || Natives.TryGetValue(b, out var n) && n.Contact != "cleared") return false;
        if (Rival != null && Rival.Planet == b && Rival.Stance != "reunion") return false;
        if (Home)
        {
            return b.Name switch
            {
                "Moon" => true,
                "Mars" => M.Mars,
                "Venus" => M.Venus,
                "Main Belt" => M.Belt,
                "Mercury" => false,
                "Europa" or "Ganymede" or "Callisto" or "Titan" or "Io" or "Enceladus" or "Rhea" => M.Jovian,
                "Jupiter" or "Saturn" => M.Jovian && M.Skimmers,
                "Titania" or "Triton" => M.Jovian && (M.Outer || Civ.Techs.Contains("X3")),
                _ => false
            };
        }
        if (b.Life && Civ.Ethic == "Ruthless") return false;
        return b.Kind != BodyKind.Lava && (b.Life || M.Outer);
    }

    public bool CanMine(Body b, bool allowSacred)
    {
        int lvl = Hubs.GetValueOrDefault(b);
        if (b.Mined >= 0.98f || Busy(b) || lvl >= 4) return false;
        if (SurfaceOnly(b) && (b.Mined >= 0.075f || lvl >= 2)) return false;
        if (Natives.TryGetValue(b, out var n) && n.Contact != "cleared") return false;
        if (Rival != null && Rival.Planet == b && Rival.Stance != "reunion") return false;
        if (b == Seat) return false;
        if (Home)
        {
            bool ok = b.Name switch
            {
                "Moon" => true,
                "Mercury" or "Main Belt" => M.Belt,
                "Jupiter" or "Saturn" or "Uranus" or "Neptune" => M.Skimmers,
                "Venus" or "Mars" => M.Civ5(Civ) && allowSacred,
                "Earth" => false,
                _ => b.Kind == BodyKind.Moon && M.Jovian
            };
            if (!ok) return false;
        }
        if (b.Life && lvl == 0 && Defs.Ethics[Civ.Ethic].Life == 0 && !allowSacred) return false;
        return true;
    }

    // ---------- Decisions ----------
    void Decide()
    {
        Sim.PickResearch(Civ);
        if (TrySeed()) return;
        // shipyards
        if (Year - Founded > 12 && FrigateNames.Count < 6 && Stock > 0.1 * PFrac && Civ.R.NextDouble() < (Civ.Drive == "Expansionist" ? 0.08 : 0.04))
        {
            Stock -= 0.05 * PFrac;
            var name = Defs.ShipNames.Where(n => !FrigateNames.Contains(n)).OrderBy(_ => Civ.R.Next()).FirstOrDefault() ?? "Unnamed";
            FrigateNames.Add(name);
            if (Home || Sim.Focus == this) Sim.Log(this, $"The shipyards launch the {name}. {(Home && FrigateNames.Count == 1 ? "It is the first warship humanity has built outside a gravity well." : $"The fleet is {FrigateNames.Count} hulls strong.")}", "council", Seat);
            return;
        }
        // stations around the seat and settled worlds
        if (Stations.Values.Sum() < (Home ? 2 + Civ.SpineKnown : 1 + Civ.SpineKnown / 4) && Stock > 0.05 * PFrac && Civ.R.NextDouble() < (Home ? 0.35 : 0.2))
        {
            var where = Settled.Keys.Where(b => b.Kind != BodyKind.Belt).OrderBy(_ => Civ.R.Next()).FirstOrDefault() ?? Seat;
            Stock -= 0.03 * PFrac;
            Missions.Add(new Mission { Target = where, From = Seat, Kind = MissionKind.Station, Dur = where == Seat ? 0.3 : M.Fast ? 0.6 : 1.4 });
            return;
        }
        // megaprojects at home
        if (Home && M.Elevator && ElevatorP < 0 && Stock > 0.3 * PFrac) { Stock -= 0.3 * PFrac; ElevatorP = 0; Sim.Log(this, "Council decision: a space elevator. A carbon ribbon will run from the equator off Ecuador to a counterweight beyond geostationary orbit.", "council", Seat); return; }
        if (Home && M.OrbitalRing && RingP < 0 && ElevatorP >= 1 && Stock > 0.6 * PFrac) { Stock -= 0.6 * PFrac; RingP = 0; Sim.Log(this, "Council decision: an orbital ring. It will circle the whole Earth, and every city on the equator will have a door into space.", "council", Seat); return; }
        var mars = Sys.Find("Mars");
        if (Home && M.Terraform && TerraformP < 0 && mars != null && Settled.ContainsKey(mars) && Stock > 0.4 * PFrac) { Stock -= 0.4 * PFrac; TerraformP = 0; Sim.Log(this, "Council decision: terraform Mars. The first comet is already on its way. Nobody now alive will see it finished.", "council", mars); return; }
        // settle: crowded, curious, or a new frontier just opened
        bool crowded = Pop > 0.75 * PopCap;
        bool keen = Home && Civ.R.NextDouble() < 0.35;
        if ((crowded || keen) && Missions.Count(m => m.Kind == MissionKind.Settle) < 2)
        {
            var cand = Sys.All().Where(CanSettle).ToList();
            if (cand.Count > 0 && Stock >= 0.08 * PFrac)
            {
                var pl = cand.OrderByDescending(b => (b.Life && Civ.Ethic == "Reverent" ? 3 : 0) + (b.Name == "Mars" ? 5 : 0) + (b.Name == "Moon" ? 6 : 0) - b.WorldPos(0).Length() * 0.01).First();
                Stock -= Math.Min(Stock, 0.08 * PFrac);
                Missions.Add(new Mission { Target = pl, From = Seat, Kind = MissionKind.Settle, Dur = TransitYears(pl) });
                Sim.Log(this, Narrative.SettleDecision(this, pl), "council", pl);
                return;
            }
        }
        if (Capture >= 0.9) return;
        double mineRate = M.StarLift ? 0.3 * LiftShare : 0;
        foreach (var (b, lvl) in Hubs) if (lvl > 0 && b.Mined < 1) mineRate += 0.16 * lvl * YieldOf(b);
        foreach (var m in Missions) if (m.Kind == MissionKind.Hub) mineRate += 0.16 * YieldOf(m.Target);
        mineRate *= M.Mine;
        double foundRate = 0.08 * Foundries * M.Foundry;
        if (mineRate < 1.25 * foundRate || !M.Swarm)
        {
            var c = BestHub();
            if (c != null && Stock >= HubCost(c)) StartHub(c, false);
            else if (c == null && Stock >= FoundryCost) BuildFoundry();
        }
        else if (Stock >= FoundryCost) BuildFoundry();
    }

    public double TransitYears(Body b)
    {
        float d = (b.WorldPos(Year) - Seat.WorldPos(Year)).Length();
        return Math.Clamp(d / (M.Fast ? 120 : 45), 0.08, 6);
    }

    public Body BestHub()
    {
        float life = Defs.Ethics[Civ.Ethic].Life;
        Body Score(bool sacred)
        {
            Body best = null; double bs = -1;
            foreach (var b in Sys.All())
            {
                if (!CanMine(b, sacred)) continue;
                int lvl = Hubs.GetValueOrDefault(b);
                double lf = b.Life ? Math.Max(life, sacred ? 0.05 : 0) : 1;
                double s = YieldOf(b) * lf * Math.Pow(0.7, lvl) * (lvl > 0 ? 1 : 1.15) / (1 + b.Orbit / StarSystem.AU * 0.28);
                if (s > bs) { bs = s; best = b; }
            }
            return best;
        }
        return Score(false) ?? (Capture > 0.5 ? Score(true) : null);
    }

    public void StartHub(Body b, bool byPlayer)
    {
        int lvl = Hubs.GetValueOrDefault(b);
        Stock -= HubCost(b);
        Missions.Add(new Mission { Target = b, From = Seat, Kind = MissionKind.Hub, Dur = TransitYears(b) });
        if (b.Life && lvl == 0) Sim.NudgeConscience(Civ, byPlayer ? -0.1f : -0.18f);
        if (Natives.TryGetValue(b, out var n) && n.Contact != "cleared")
        {
            n.Contact = "cleared";
            Sim.NudgeConscience(Civ, -0.3f);
            Sim.Log(this, $"At the voice's command, {b.Name} is cleared of the {n.Name}. Even the Ruthless find it hard to watch.", "loss", b);
        }
        if (Settled.ContainsKey(b) && !SurfaceOnly(b))
        {
            Settled.Remove(b);
            Pop *= 0.95;
            Sim.NudgeConscience(Civ, -0.08f);
            if (Newsworthy) Sim.Log(this, $"{b.Name} is evacuated so it can be mined. Not everyone leaves in time.", "loss", b);
        }
        if (byPlayer)
        {
            string grief = b.Life && lvl == 0 && Civ.Ethic == "Reverent" ? " The Reverent weep, but they obey." : "";
            Sim.Log(this, $"A voice no one can locate speaks in the council chamber. A constructor turns toward {b.Name}.{grief}", "god", b);
            return;
        }
        if (Home || Sim.Focus == this) Sim.Log(this, Narrative.HubDecision(this, b, lvl), "council", b);
    }

    void BuildFoundry()
    {
        Stock -= FoundryCost; Foundries++;
        if (Home || Sim.Focus == this)
            Sim.Log(this, M.Swarm ? $"Council decision: light foundry {Foundries}. Collectors are the bottleneck." : $"Council decision: open orbital foundry {Foundries}. Industry is leaving the ground.", "council", Seat);
    }

    void Arrive(Mission m)
    {
        var b = m.Target;
        switch (m.Kind)
        {
            case MissionKind.Hub:
                int lvl = Hubs.GetValueOrDefault(b) + 1;
                Hubs[b] = lvl;
                if (lvl == 1 && (Home || Sim.Focus == this)) Sim.Log(this, Narrative.HubOpened(this, b), "event", b);
                break;
            case MissionKind.Settle:
                Settled[b] = Math.Max(Settled.GetValueOrDefault(b), 0.05f);
                Sim.Fx(this, "landing", b);
                if (Home || Sim.Focus == this) Sim.Log(this, Narrative.Landed(this, b), "era", b);
                if (Home && b.Name == "Mars" && !Civ.Used.Contains("ms_mars")) { Civ.Used.Add("ms_mars"); Sim.Milestone(this, "Mars", $"Year {(int)Sim.Year}. Six people step onto red dust under a butterscotch sky. Humanity is a two-planet species.", "body:Mars"); }
                break;
            case MissionKind.Station:
                Stations[b] = Stations.GetValueOrDefault(b) + 1;
                break;
        }
    }

    /// <summary>A closing summary of what it cost to wrap this star.</summary>
    public string Epitaph()
    {
        int years = (int)(Sim.Year - Founded);
        var gone = Sys.All().Where(b => b.Mined >= 0.99f).Select(b => b.Name).ToList();
        string worlds = gone.Count == 0 ? "No world was consumed." : gone.Count == 1 ? $"{gone[0]} was consumed." : $"{gone.Count} worlds were consumed, {gone[0]} first.";
        string natives = Natives.Count == 0 ? "" : Natives.Values.Any(n => n.Contact == "cleared") ? " The natives were cleared." : Natives.Values.Any(n => n.Contact == "uplift") ? " The natives sit on the council." : " The natives were left alone.";
        string proj = ProjectDone ? $" They built the {ProjectName}." : "";
        string peak = PeakPop < 1 ? $"{PeakPop * 1000:0} million" : $"{PeakPop:0.#} billion";
        return $"Epitaph for {Sys.Name}, generation {Gen}: {years} years from first light to the last collector. {worlds}{natives} Peak population {peak}.{proj}";
    }

    // ---------- Seeds ----------
    public bool TrySeed()
    {
        if (!M.Seed || Year < NextSeed || Civ.Fallen) return false;
        var d = Defs.Drives[Civ.Drive];
        bool ready = Complete || Capture >= d.SeedAt || Home && Power >= Defs.TypeI;
        double cost = Complete ? 0 : 0.3 * PFrac;
        if (!ready || Stock < cost) return false;
        var target = Sim.PickSeedTarget(this);
        if (target == null) { NextSeed = Year + (Complete ? 150 : 40); return false; }
        void Go(StarInfo t)
        {
            Stock -= cost;
            SeedsSent++;
            NextSeed = Year + (Civ.Drive == "Expansionist" ? 25 : Civ.Drive == "Industrious" ? 45 : 70) * (Complete ? 0.6 : 1) * (Civ.M.Fold ? 0.5 : 1) / Math.Max(Civ.Vigour, 0.05f) * (1 + Civ.Colonies.Count / 120.0);
            Sim.LaunchSeed(this, t);
        }
        if (Civ.Main && Civ.Policy.IsManual(Area.Colonisation))
        {
            // the council has picked a star; the launch waits on the player
            string key = "seed:" + Star.Id;
            if (Sim.Waiting(key)) return false;
            float ly = Star.Pos.DistanceTo(target.Pos) * Galaxy.LyPerUnit;
            var t = target;
            NextSeed = Year + 5;                                        // set first: an answer given at once sets its own
            Sim.Raise("Decision", $"Seed ship ready at {Sys.Name}", $"The council would send it to {t.Name} ({t.Cls} star), {ly:0.#} light-years away. {Sim.DepositsOf(t).Describe.Split('\n')[0]}", this, key,
                new() { ("Launch", () => { if (Sim.Settleable(Star, t) && Stock >= cost) Go(t); else NextSeed = Year; }), ("Not now", () => NextSeed = Year + 20) });
            return false;
        }
        Go(target);
        return true;
    }

    // ---------- Great projects ----------
    static readonly Dictionary<string, (string Name, double Years, string Start, string End)> Projects = new()
    {
        ["Industrious"] = ("Stellar Engine", 120, "The Forgewrights begin a mirror the size of half an orbit. When it is finished, the star will push itself.",
            "The Stellar Engine fires. The star, every planet left and the swarm around it begin, very slowly, to move. In ten thousand years they will be somewhere else, and they intend to choose where."),
        ["Contemplative"] = ("Archive of Everything", 110, "The Choristers begin rewiring the swarm. Every collector will become a neuron.",
            "The swarm stops being a power plant and becomes a mind. It thinks quietly for a decade. Then it asks the Speaker a question nobody can answer, and goes on thinking about it."),
        ["Expansionist"] = ("Fold Gate", 100, "The Wayfarers begin a gate ring at the edge of the system. Through it, every star within forty light-years will be one step away.",
            "The gate opens onto a hundred stars at once. Ships begin to leave and do not stop leaving."),
    };
    public string ProjectName => Project == null ? null : Project == "Ringworld" ? "Ringworld" : Projects[Project].Name;

    void ProjectStep(double dy)
    {
        if (Project == null && Capture >= 0.6 && Civ.SpineKnown >= 18 && (Home || Sim.Focus == this))
        {
            Project = Civ.Drive; ProjectP = 0;
            Sim.Log(this, $"Great project begun: {Projects[Project].Name}. {Projects[Project].Start}", "era", null);
        }
        if (Project == null || ProjectDone) return;
        ProjectP = (float)Math.Min(1, ProjectP + dy / Projects[Project].Years * (0.8 + 0.4 * Capture));
        if (ProjectP >= 1) { ProjectDone = true; Sim.Log(this, $"{Projects[Project].Name} complete. {Projects[Project].End}", "era", null); Sim.Milestone(this, Projects[Project].Name, Projects[Project].End, "star"); }
    }
}

static class ModsExt
{
    /// <summary>Venus and Mars become mineable only for a ruthless, late civilisation.</summary>
    public static bool Civ5(this Mods m, Civ c) => c.Ethic == "Ruthless" && m.Mine > 2;
}
