using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Starforge.Sim;

public class LogEntry
{
    public double Year;
    public Civ Civ;
    public Colony Colony;
    public string System, Text, Kind;
    public object Where;          // Body, "visitor", or null (the star)
    public int StarId = -1;       // kept even after the colony is gone
}

public class SeedShip
{
    public Civ Civ;
    public Colony From;
    public StarInfo Origin, Target;
    public double Depart, Arrive;
    public string Name;
    public bool Hostile;           // a war fleet rather than settlers
    public float Progress(double year) => (float)Math.Clamp((year - Depart) / Math.Max(Arrive - Depart, 1e-6), 0, 1);
}

/// <summary>The whole universe of the game: every civilisation, colony and ship in flight, and the clock.</summary>
public partial class Simulation
{
    public Galaxy Gal;
    public double Year = 2061.0;
    public readonly List<Civ> Civs = new();
    public readonly List<Colony> Colonies = new();
    public readonly List<SeedShip> Ships = new();
    public readonly List<LogEntry> Chronicle = new();
    public readonly Dictionary<StarInfo, StarSystem> Systems = new();
    public Civ Humanity;
    public static string StartDrive, StartEthic;      // chosen on the title screen; null = let fate decide
    public Colony HomeColony;
    public Colony Focus;
    public double AnsweredAt = -1;
    public event Action<LogEntry> Logged;
    public event Action<Colony, string, Body> Effect;
    /// <summary>A turning point: colony, title, text, and what the camera should look at
    /// ("seat", "ship", "star", "starbase", "body:Name", "mega:Name", "galaxy", "none").</summary>
    public event Action<Colony, string, string, string> MilestoneReached;
    public event Action<Civ, Tech> TechCompleted;
    public event Action<Colony> EraReached;
    public void RaiseEra(Colony c) => EraReached?.Invoke(c);
    public event Action<Colony> ColonyFounded;
    public bool TypeIIIAnnounced;
    double _nextGalaxyCheck;
    readonly Random _r;
    public static Simulation Current;

    Simulation(Galaxy gal, int seed, bool blank)
    {
        Current = this;
        Gal = gal;
        _r = new Random(seed ^ 0x5eed);
        Systems[gal.Sol] = StarSystem.Sol(gal.Sol);
    }

    /// <summary>An empty universe, to be filled from a save.</summary>
    public static Simulation Blank(Galaxy gal, int seed) => new(gal, seed, true);

    public Simulation(Galaxy gal, int seed)
    {
        Current = this;
        Gal = gal;
        _r = new Random(seed);
        // ---- humanity, 2061 ----
        Humanity = new Civ(0, seed + 1) { Name = "Humanity", Species = "Human", Human = true, Main = true, Color = new Color(0.55f, 0.8f, 1f), Founded = Year };
        var drives = Defs.Drives.Keys.ToList();
        string d0 = drives[_r.Next(drives.Count)], e0 = new[] { "Reverent", "Pragmatic", "Pragmatic", "Ruthless" }[_r.Next(4)];
        Humanity.SetDrive(StartDrive ?? d0, StartEthic ?? e0);
        foreach (var t in Defs.Techs) if (t.Start) Humanity.Techs.Add(t.Id);
        Humanity.ComputeMods();
        Humanity.NextElection = Year + 4;
        Humanity.Vigour = 1.3f;
        Civs.Add(Humanity);
        var sol = StarSystem.Sol(gal.Sol);
        Systems[gal.Sol] = sol;
        var home = new Colony(this, Humanity, sol, true, Year)
        {
            PlanetPower = 2.6e13, Pop = 9.7, Seat = sol.Find("Earth"), Stock = 0, Foundries = 1
        };
        home.Settled[home.Seat] = 1f;
        home.Settled[sol.Find("Moon")] = 0.12f;
        home.Stations[home.Seat] = 2;
        home.Hubs[sol.Find("Moon")] = 1;
        home.Stock = 0.5 * home.PFrac;
        home.Era = 0;
        home.FrigateNames.Clear();
        Colonies.Add(home);
        Humanity.Colonies.Add(home);
        HomeColony = home;
        Focus = home;
        Humanity.Leader = Humanity.NewLeader(Humanity.Drive, Year);
        PickResearch(Humanity);

        SpawnAliens();
    }

    // ---------- Aliens ----------
    static readonly (string Name, string Species, Color Color)[] AlienDefs =
    {
        ("the Vessh Hegemony", "Vessh", new Color(1f, 0.45f, 0.3f)),
        ("the Orun Concord", "Orun", new Color(0.55f, 1f, 0.55f)),
        ("the Tallow Choir", "Tallow", new Color(1f, 0.85f, 0.35f)),
        ("the Hask", "Hask", new Color(0.85f, 0.4f, 1f)),
        ("the Ninefold", "Ninefold", new Color(0.4f, 0.95f, 0.95f)),
        ("the Quiet Lattice", "Lattice", new Color(0.95f, 0.95f, 0.95f)),
    };

    void SpawnAliens()
    {
        int n = 5;
        var candidates = Gal.Stars.Where(s => s.Owner < 0 && s.Pos.DistanceTo(Gal.Sol.Pos) > 220f && s.Lum > 0.2f && s.Lum < 5f).ToList();
        for (int i = 0; i < n && candidates.Count > 0; i++)
        {
            StarInfo home = null;
            for (int t = 0; t < 50; t++)
            {
                var c = candidates[_r.Next(candidates.Count)];
                if (Civs.Where(x => x.Alien).All(x => x.Colonies[0].Star.Pos.DistanceTo(c.Pos) > 300f)) { home = c; break; }
            }
            if (home == null) continue;
            var d = AlienDefs[i];
            int age = i switch { 0 => 3, 1 => 2, 2 => 1, 3 => 2, _ => 0 };   // 0 young .. 3 ancient
            var civ = new Civ(Civs.Count, _r.Next()) { Name = d.Name, Species = d.Species, Alien = true, Color = d.Color, Founded = Year - 500, Age = age };
            var drives = Defs.Drives.Keys.ToList();
            civ.SetDrive(drives[_r.Next(drives.Count)], new[] { "Reverent", "Pragmatic", "Ruthless" }[_r.Next(3)]);
            int maxTier = age switch { 0 => 1, 1 => 4, 2 => 5, _ => 6 };
            GrantUpTo(civ, maxTier);
            civ.Leader = civ.NewLeader(civ.Drive, Year);
            civ.NextElection = Year + 10 + _r.Next(20);
            civ.Vigour = i switch { 0 => 0.3f, 1 => 0.6f, _ => 1f };
            Civs.Add(civ);
            var col = Found(civ, home, null, age >= 1);
            if (age >= 1)
            {
                col.Capture = 0.92; col.Complete = true; col.PopCap = 40; col.Pop = 30;
                // their great work has stood for longer than humanity has had writing
                col.Project = d.Species switch { "Vessh" => "Ringworld", "Orun" => "Contemplative", "Tallow" => "Expansionist", "Hask" => "Industrious", _ => null };
                col.ProjectDone = col.Project != null; col.ProjectP = 1f;
                // a head start of daughter colonies
                int kids = age switch { 1 => 2, 2 => 6, _ => 14 };
                var near = Gal.Stars.Where(s => s.Owner < 0 && s.Pos.DistanceTo(home.Pos) < 30f + 18f * age).OrderBy(s => s.Pos.DistanceTo(home.Pos)).Take(kids).ToList();
                foreach (var s in near)
                {
                    var k = Found(civ, s, null, false);
                    k.Capture = 0.3 + _r.NextDouble() * 0.65;
                    if (k.Capture >= 0.9) k.Complete = true;
                    k.Pop = 2 + _r.NextDouble() * 6;
                    k.PopCap = k.Pop * 1.5;
                }
            }
            else { col.Capture = 0; col.PlanetPower = 5e13; col.Pop = 6; }
            candidates.Remove(home);
        }
    }

    public StarSystem SystemFor(StarInfo s)
    {
        if (!Systems.TryGetValue(s, out var sys)) { sys = StarSystem.Generate(s); Systems[s] = sys; }
        return sys;
    }

    public Colony Found(Civ civ, StarInfo star, Colony from, bool asHomeworld)
    {
        var sys = SystemFor(star);
        var c = new Colony(this, civ, sys, false, Year);
        var seat = sys.Bodies.Where(b => b.Kind != BodyKind.Belt).OrderByDescending(b => (b.Life ? 10 : 0) + (b.Kind is BodyKind.Terran or BodyKind.Ocean ? 5 : 0) + (b.Kind is BodyKind.Rocky or BodyKind.Desert ? 2 : 0) - b.Index * 0.1).FirstOrDefault() ?? sys.Bodies[0];
        c.Seat = seat;
        c.Settled[seat] = asHomeworld ? 1f : 0.05f;
        c.PlanetPower = 4e17 * (civ.M.Gate ? 3 : 1);
        c.PowerCap = 2e19;
        c.Pop = asHomeworld ? 8 : 0.04;
        c.Stock = 0.6 * c.PFrac * (civ.M.Gate ? 3 : 1);
        c.Era = 2;
        Narrative.SeedNatives(c);
        if (c.Natives.ContainsKey(seat)) { c.Natives.Remove(seat); }
        Colonies.Add(c);
        civ.Colonies.Add(c);
        ColonyFounded?.Invoke(c);
        return c;
    }

    // ---------- Logging ----------
    public void Log(Colony c, string text, string kind, object where)
    {
        var civ = c?.Civ ?? Humanity;
        // the chronicle follows humanity, the watched system, and anyone humanity has met
        // (a met empire's own losses only when humanity is part of them: a big empire at war loses dozens of worlds a year,
        // and every one of those lines would bury humanity's history)
        bool relevant = civ.Main || c == Focus || kind == "god" || civ.Met.Contains(Humanity.Id) && (kind == "era" || kind == "omen" || kind == "loss" && civ.AtWar.Contains(Humanity.Id));
        // far-flung human colonies only make the chronicle with turning points, not routine news
        if (civ.Main && c != null && !c.Home && c != Focus && (kind == "event" || kind == "council")) relevant = false;
        if (!relevant) return;
        var e = new LogEntry { Year = Year, Civ = civ, Colony = c, System = c?.Sys.Name ?? "", Text = text, Kind = kind, Where = where, StarId = c?.Star.Id ?? -1 };
        Chronicle.Add(e);
        if (Chronicle.Count > 4000) Chronicle.RemoveRange(0, 1000);
        Logged?.Invoke(e);
    }
    public void Fx(Colony c, string kind, Body b) => Effect?.Invoke(c, kind, b);
    public void Milestone(Colony c, string title, string text, string focus = "seat")
    {
        if (c == null || c.Civ.Main) Raise("Milestone", title, text, c);
        MilestoneReached?.Invoke(c, title, text, focus);
    }

    public void NudgeConscience(Civ civ, float d)
    {
        civ.Conscience = Math.Clamp(civ.Conscience + d, 0.02f, 0.98f);
        string ethic = civ.Conscience > 0.66f ? "Reverent" : civ.Conscience > 0.34f ? "Pragmatic" : "Ruthless";
        if (ethic == civ.Ethic) return;
        bool harder = Defs.Ethics[ethic].Conscience < Defs.Ethics[civ.Ethic].Conscience;
        civ.Ethic = ethic;
        var c = civ.Colonies.FirstOrDefault();
        Log(c, harder ? $"{(civ.Human ? "Humanity's" : civ.Name + "'s")} conscience hardens. They are {ethic} now." : $"Something softens in {(civ.Human ? "humanity" : civ.Name)}. They are {ethic} now.", "era", null);
    }

    // ---------- Councils ----------
    void Election(Civ civ)
    {
        var home = civ.Colonies.FirstOrDefault();
        if (home == null) return;
        if (civ.OvermindUntil > Year || civ.Junta(Year)) return;      // a junta rules by decree
        if (civ.M.Hive)
        {
            if (!civ.Used.Contains("hive")) { civ.Used.Add("hive"); Log(home, "Election day passes without an election. There is only agreement now.", "omen", null); }
            return;
        }
        var ranked = civ.Inf.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
        string top = ranked[0], second = ranked[1];
        if (civ.Inf[top] - civ.Inf[second] < 0.05f / civ.M.Unity && civ.Inf[second] > 0.33f && Year - civ.Founded > 30)
        {
            string leaving = top == civ.Drive ? second : top;
            civ.Inf[leaving] *= 0.4f; civ.ShiftInf(civ.Drive, 0);
            // with an interstellar presence, the far colonies break away as a new polity
            var frontier = civ.Colonies.Where(c => !c.Home).OrderByDescending(c => c.Star.Pos.DistanceTo(home.Star.Pos)).Take(Math.Max(0, (int)(civ.Colonies.Count / (3 * civ.M.Admin)))).ToList();
            if (frontier.Count >= 2)
            {
                var split = Separate(civ, frontier, leaving, peaceful: civ.Ethic != "Ruthless");
                Log(home, $"Schism. {frontier.Count} colonies of the {civ.Faction(leaving)} declare themselves {split.Name}, and close their borders.", "loss", null);
                if (civ.Main) Raise("Unrest", "Schism", $"{frontier.Count} colonies of the {civ.Faction(leaving)} break away as {split.Name}.", home);
                return;
            }
            var hc = civ.Colonies.First(c => c.Home || c == civ.Colonies[0]);
            double lost = hc.Pop * 0.15;
            hc.Pop -= lost; hc.Stock *= 0.88;
            hc.Splinter ??= (civ.Faction(leaving), lost, Year, Year + 50 + _r.NextDouble() * 40, false);
            string amt = lost < 1 ? Math.Round(lost * 1000) + " million" : lost.ToString("0.0") + " billion";
            Log(hc, $"Schism. {amt} {civ.Faction(leaving)} take a share of the stockpile and leave for the dark between stars. They do not say where.", "loss", null);
            if (civ.Main) Raise("Unrest", "Schism", $"{amt} {civ.Faction(leaving)} leave {hc.Sys.Name} with a share of the stockpile.", hc);
            return;
        }
        if (top != civ.Drive && civ.Inf[top] > civ.Inf[civ.Drive] + 0.05f)
        {
            string old = civ.Drive;
            civ.Drive = top;
            civ.Leader = civ.NewLeader(top, Year);
            foreach (var b in civ.W.Keys.ToList()) civ.W[b] = Math.Min(6, 0.5f * civ.W[b] + 0.5f * Defs.Drives[top].W[b]);
            Log(home, $"The {civ.Faction(top)} take the council from the {civ.Faction(old)}. Speaker {civ.Leader.Name} sets a new course: {Defs.Drives[top].Creed}.", "era", null);
            Narrative.SpeakerSays(home);
        }
        else if (civ.Main) Log(home, $"Council elections. The {civ.Faction(civ.Drive)} hold power with {Math.Round(civ.Inf[civ.Drive] * 100)}% of the vote.", "council", null);
    }

    static readonly string[] SplinterNames = { "the Long Night", "the Second Dawn", "the Unbound", "the Far Choir", "the Kindred of {0}", "the Free Worlds of {0}", "the {0} Compact" };

    Civ Splinter(Civ parent, string drive, List<Colony> cols)
    {
        var civ = new Civ(Civs.Count, _r.Next())
        {
            Human = parent.Human, Alien = parent.Alien, Species = parent.Species, Founded = Year,
            Color = Color.FromHsv((parent.Color.H + 0.2f + (float)_r.NextDouble() * 0.6f) % 1f, 0.6f, 1f)
        };
        string tmpl = SplinterNames[_r.Next(SplinterNames.Length)];
        civ.Name = string.Format(tmpl, cols[0].Sys.Name);
        civ.Culture = parent.Culture?.Copy(); civ.MandatoryService = parent.MandatoryService;      // a breakaway takes its culture with it
        civ.SetDrive(drive, parent.Ethic == "Reverent" ? "Pragmatic" : parent.Ethic);
        foreach (var t in parent.Techs) civ.Techs.Add(t);
        civ.ComputeMods();
        civ.Leader = civ.NewLeader(drive, Year);
        civ.NextElection = Year + 25;
        civ.Vigour = parent.Vigour;
        civ.Met.Add(parent.Id); parent.Met.Add(civ.Id);
        civ.Relations[parent.Id] = -30; parent.Relations[civ.Id] = -30;
        Civs.Add(civ);
        foreach (var c in cols)
        {
            parent.Colonies.Remove(c);
            civ.Colonies.Add(c);
            c.Civ = civ;
            c.Star.Owner = civ.Id;
        }
        PickResearch(civ);
        return civ;
    }

    // ---------- Seeds ----------
    public bool Folds(Civ civ) => civ.M.Fold && !Laws.Contains("nofold");
    public float ShipSpeed(Civ civ) => Laws.Contains("nofold") ? Math.Min(civ.M.ShipSpeed, 0.15f) : civ.M.ShipSpeed;
    public float MaxRangeGu(Civ civ) => (Folds(civ) ? 90f : ShipSpeed(civ) >= 0.15f ? 2.2f : ShipSpeed(civ) > 0 ? 0.6f : 0f) * civ.M.Range;
    /// <summary>Can a seed ship settle here at all? Dead stars, guarded places and sealed regions are out.</summary>
    public bool Settleable(StarInfo from, StarInfo s) => s.Owner < 0 && !s.Targeted && !s.Dead && s.Special == null && !Blocked(from, s);

    public StarInfo PickSeedTarget(Colony from)
    {
        float range = MaxRangeGu(from.Civ);
        if (range <= 0) return null;
        if (from.Civ.Main && Guided != null && Settleable(from.Star, Guided) && Guided.Pos.DistanceTo(from.Star.Pos) <= range)
        {
            var g = Guided; Guided = null; return g;
        }
        // nearest 48 neighbours only: interior systems with no free neighbours stop looking
        StarInfo best = null; double bs = double.MaxValue;
        foreach (int id in from.Star.Near)
        {
            var s = Gal.Stars[id];
            float d = s.Pos.DistanceTo(from.Star.Pos);
            if (!Settleable(from.Star, s)) continue;
            if (d > range && LaneFrom(from.Star, s) == null) break;              // Near is sorted by distance (lanes are listed first)
            // nearer is better, and richer is better: light, metal, water, life and anything rare (Deposits.Value)
            double score = d * (s.Lum > 1000 ? 4 : 1) / (0.5 + DepositsOf(s).Value) * (0.8 + _r.NextDouble() * 0.4);
            if (score < bs) { bs = score; best = s; }
        }
        // neighbourhood exhausted but the drives reach further: search the whole galaxy (rare; the caller backs off)
        if (best == null && range > 5f)
        {
            var origin = from.Star.Pos;
            foreach (var s in Gal.Stars)
            {
                if (!Settleable(from.Star, s)) continue;
                float d = s.Pos.DistanceTo(origin);
                if (d > range) continue;
                double score = d * (0.8 + _r.NextDouble() * 0.4);
                if (score < bs) { bs = score; best = s; }
            }
        }
        return best;
    }

    public void LaunchSeed(Colony from, StarInfo target)
    {
        var civ = from.Civ;
        float ly = from.Star.Pos.DistanceTo(target.Pos) * Galaxy.LyPerUnit;
        double years = LaneFrom(from.Star, target) != null ? 1 : ly / Math.Max(ShipSpeed(civ), 0.01f) + 2;
        var name = Defs.ShipNames[_r.Next(Defs.ShipNames.Length)];
        Ships.Add(new SeedShip { Civ = civ, From = from, Origin = from.Star, Target = target, Depart = Year, Arrive = Year + years, Name = name });
        target.Targeted = true;
        if (civ.Main && (from.Home || from == Focus || civ.Colonies.Count < 12))
            Log(from, $"Seed ship {name} departs {from.Sys.Name} for {target.Name}, {ly:0.#} light-years away. {(years > 30 ? $"It will arrive in {years:0} years. Its crew will be the grandchildren of the people who left." : "It will be there within a lifetime.")}", "era", null);
        Fx(from, "seedlaunch", from.Seat);
        if (civ.Main && !civ.Used.Contains("ms_firstseed"))
        {
            civ.Used.Add("ms_firstseed");
            Milestone(from, "The Long Voyage", $"Year {(int)Year}. The seed ship {name} leaves for {target.Name}, {ly:0.#} light-years away. {(years > 80 ? "Nobody aboard will live to see it arrive." : "Some of the crew will live to see it arrive.")}", "ship");
        }
    }

    void ShipArrives(SeedShip s)
    {
        var t = s.Target;
        t.Targeted = false;
        if (t.Owner < 0)
        {
            var c = Found(s.Civ, t, s.From, false);
            c.Gen = (s.From?.Gen ?? 1) + 1;
            if (s.Civ.Main && (s.Civ.Colonies.Count <= 12 || s.From == Focus))
                Log(c, $"The {s.Name} arrives at {t.Name} ({t.Cls}). Generation {c.Gen}. {Narrative2.FirstLook(c)}", "era", null);
            Fx(c, "arrival", c.Seat);
            if (s.Civ.Main && s.Civ.Colonies.Count == 2) Milestone(c, "A Second Sun", $"Year {Year:0}. The {s.Name} has reached {t.Name}. For the first time, humanity lives under two suns.", "ship");
        }
        else if (t.Owner != s.Civ.Id)
        {
            var other = Civs[t.Owner];
            Meet(s.Civ, other, t);
        }
    }

    // ---------- Contact and war ----------
    void Meet(Civ a, Civ b, StarInfo where)
    {
        if (a.Met.Contains(b.Id) || a == b) return;
        a.Met.Add(b.Id); b.Met.Add(a.Id);
        float rel = 0;
        if (a.Ethic == "Ruthless" || b.Ethic == "Ruthless") rel -= 35;
        if (a.Ethic == "Reverent" && b.Ethic == "Reverent") rel += 30;
        if (a.Drive == b.Drive) rel += 10;
        rel += (float)(_r.NextDouble() * 30 - 15);
        a.Relations[b.Id] = rel; b.Relations[a.Id] = rel;
        if (a.Main || b.Main)
        {
            var other = a.Main ? b : a;
            var col = (a.Main ? a : b).Colonies.OrderBy(c => c.Star.Pos.DistanceTo(where.Pos)).FirstOrDefault();
            string text = other.Alien
                ? $"First contact. Near {where.Name}, humanity meets {other.Name}. {Narrative2.AlienFirstContact(other, rel)}"
                : $"Contact with {other.Name}, near {where.Name}. They are human, and they remember us differently than we remember them.";
            Log(col, text, "omen", null);
            Raise("Contact", other.Alien ? "First contact" : "Contact", text, col);
            if (other.Alien && !Humanity.Used.Contains("firstcontact")) { Humanity.Used.Add("firstcontact"); Milestone(col, "We Are Not Alone", $"Year {Year:0}. {other.Name} — {Narrative2.AlienBlurb(other)}"); }
        }
    }

    const float Cell = 25f;
    readonly Dictionary<(int, int), List<Colony>> _grid = new();
    static (int, int) CellOf(Vector3 p) => ((int)Math.Floor(p.X / Cell), (int)Math.Floor(p.Z / Cell));

    /// <summary>Nearest colony of civ b within range of position p, via the spatial grid.</summary>
    Colony NearestOf(Civ b, Vector3 p, float range)
    {
        int r = (int)Math.Ceiling(range / Cell);
        var (cx, cz) = CellOf(p);
        Colony best = null; float bd = range;
        for (int x = cx - r; x <= cx + r; x++)
            for (int z = cz - r; z <= cz + r; z++)
                if (_grid.TryGetValue((x, z), out var list))
                    foreach (var c in list)
                    {
                        if (c.Civ != b) continue;
                        float d = c.Star.Pos.DistanceTo(p);
                        if (d < bd) { bd = d; best = c; }
                    }
        return best;
    }

    void GalaxyCheck()
    {
        _grid.Clear();
        foreach (var c in Colonies)
        {
            var k = CellOf(c.Star.Pos);
            if (!_grid.TryGetValue(k, out var l)) _grid[k] = l = new List<Colony>();
            l.Add(c);
        }
        // which civ pairs share a border (or are in contact range)
        var near = new Dictionary<(int, int), (Colony A, Colony B, float D)>();
        foreach (var c in Colonies)
        {
            var (cx, cz) = CellOf(c.Star.Pos);
            for (int x = cx - 2; x <= cx + 2; x++)
                for (int z = cz - 2; z <= cz + 2; z++)
                    if (_grid.TryGetValue((x, z), out var list))
                        foreach (var o in list)
                        {
                            if (o.Civ.Id <= c.Civ.Id) continue;
                            float d = o.Star.Pos.DistanceTo(c.Star.Pos);
                            var key = (c.Civ.Id, o.Civ.Id);
                            if (!near.TryGetValue(key, out var cur) || d < cur.D) near[key] = (c, o, d);
                        }
        }
        foreach (var (key, v) in near)
        {
            var a = Civs[key.Item1]; var b = Civs[key.Item2];
            if (!a.Met.Contains(b.Id) && v.D < Math.Max(MaxRangeGu(a) * a.M.Sense, MaxRangeGu(b) * b.M.Sense) * 0.5f + 3f) Meet(a, b, v.B.Star);
        }
        // relations drift; war and peace
        var byCiv = Civs.Where(c => !c.Dead && c.Colonies.Count > 0).ToList();
        foreach (var a in byCiv)
            foreach (var bid in a.Met.ToList())
            {
                if (bid >= Civs.Count) continue;
                var b = Civs[bid];
                if (b.Dead || a.Id > b.Id) continue;
                float rel = a.Relations.GetValueOrDefault(b.Id);
                bool border = near.TryGetValue((a.Id, b.Id), out var nv) && nv.D < 25f;
                rel += border ? -1.5f : 0.5f;
                if (a.Ethic == "Reverent" && b.Ethic == "Reverent") rel += 1f;
                if (a.Ethic == "Ruthless" || b.Ethic == "Ruthless") rel -= 0.8f;
                rel += 0.3f * (a.M.Diplo - 1) + 0.3f * (b.M.Diplo - 1);
                bool allied = a.Federation >= 0 && a.Federation == b.Federation || a.Overlord == b || b.Overlord == a;
                if (allied) rel += 1f;
                // an Atrocity record is remembered by everyone who has met them
                rel -= 0.02f * (a.Meter("Atrocity") + b.Meter("Atrocity"));
                rel = Math.Clamp(rel, -100, 100);
                a.Relations[b.Id] = rel; b.Relations[a.Id] = rel;
                bool war = a.AtWar.Contains(b.Id);
                if (!war && rel < WarData.D("field", "declareRelation") && !allied && !Laws.Contains("nowar")
                    && Year >= a.Truce.GetValueOrDefault(b.Id) && CanReach(a, b) && _r.NextDouble() < 1.0 / Math.Max(1f, Math.Min(a.M.Stealth, b.M.Stealth)))
                {
                    // the harder conscience declares; if that is the player and the military is theirs, the council asks first
                    var declarer = a.Conscience <= b.Conscience ? a : b; var other = declarer == a ? b : a;
                    if (declarer.Main && declarer.Policy.IsManual(Area.Military))
                    {
                        string key = "war:" + other.Id;
                        if (!Waiting(key) && Year >= declarer.Policy.AskAfter.GetValueOrDefault(key))
                        {
                            string reason = ChooseReason(declarer, other);
                            Raise("Decision", $"Declare war on {Narrative2.Called(other)}?", $"Relations with {Narrative2.Called(other)} have collapsed ({rel:0}). The council is ready to go to war, over {WarModel.ReasonById(reason).Name.ToLower()}.", declarer.Colonies[0], key,
                                new() { ("Declare war", () => { if (!declarer.AtWar.Contains(other.Id) && !other.Dead) DeclareWar(declarer, other, reason); }),
                                        ("Hold", () => declarer.Policy.AskAfter[key] = Year + 20) });
                        }
                    }
                    else DeclareWar(declarer, other);
                }
            }
        DarkStars();
        // Type III watch
        if (!TypeIIIAnnounced && Defs.Kardashev(Humanity.GalacticPower) >= 3.0)
        {
            TypeIIIAnnounced = true;
            Log(HomeColony, "Type III. Humanity now commands the light of a hundred billion suns. Seen from Andromeda, a whole quarter of the Milky Way has gone dark.", "era", null);
            Milestone(HomeColony, "Type III", $"Year {Year:0}. Humanity now commands the light of a hundred billion suns. Seen from Andromeda, a quarter of the Milky Way has gone dark.", "galaxy");
        }
        if (!Humanity.Used.Contains("typeii") && HomeColony.Home && HomeColony.Capture >= 0.9)
        {
            Humanity.Used.Add("typeii");
            Milestone(HomeColony, "Type II", $"Year {Year:0}. The Sun is wrapped. Every photon it makes now works for humanity before it is allowed to leave.", "star");
        }
        if (!Humanity.Used.Contains("typei") && Humanity.Power >= Defs.TypeI)
        {
            Humanity.Used.Add("typei");
            Milestone(HomeColony, "Type I", $"Year {Year:0}. Humanity commands as much power as falls on the whole Earth.", "seat");
        }
    }

    /// <summary>
    /// Long before anyone meets, astronomy finds them: a finished swarm is a star gone dark in the visible and bright in
    /// the infrared, plain across the galaxy to anyone with a big enough mirror (the lightsail era's arrays). One empire
    /// at a time, the old ones first, so humanity spends the centuries before contact knowing it is not alone.
    /// </summary>
    void DarkStars()
    {
        if (!Humanity.Knows("X3") || _r.NextDouble() > 0.15) return;
        var seen = Civs.Where(o => o.Alien && !o.Dead && !Humanity.Met.Contains(o.Id) && !Humanity.Used.Contains("seen:" + o.Id) && o.Colonies.Any(x => x.Complete))
            .OrderByDescending(o => o.Age).FirstOrDefault();
        if (seen == null) return;
        Humanity.Used.Add("seen:" + seen.Id);
        var home = seen.Colonies[0];
        int dark = seen.Colonies.Count(x => x.Complete);
        float ly = home.Star.Pos.DistanceTo(Gal.Sol.Pos) * Galaxy.LyPerUnit;
        bool first = !Humanity.Used.Contains("darkstars");
        Humanity.Used.Add("darkstars");
        string text = $"Astronomers find {(dark == 1 ? "a star" : $"{dark} stars")} near {home.Star.Name}, {ly:#,0} light-years away, shining only in the infrared. "
            + (first ? "Someone has wrapped them. Whoever they are, they were doing it before humanity learned to write." : "Another builder. The dark patches on the sky charts are starting to look like a map.");
        Log(HomeColony, text, "omen", null);
        if (first) Milestone(HomeColony, "Dark Stars", $"Year {Year:0}. {text}", "galaxy");
        else Raise("Milestone", "Dark stars", text, HomeColony);
    }

    public void StartWar(Civ a, Civ b) => DeclareWar(a, b);

    public void EndWar(Civ a, Civ b) { if (WarBetween(a, b) is { } w) EndWarWith(w, true, new() { WarModel.TermById("status_quo") }); }

    public void DestroyColony(Colony c, string text)
    {
        Log(c, text, "loss", null);
        if (c.Civ.Main) Raise("War", $"{c.Sys.Name} lost", text, c);
        c.Civ.Colonies.Remove(c);
        Colonies.Remove(c);
        c.Star.Owner = -1;
        c.Star.Capture = 0;
        if (c.Civ.Colonies.Count == 0) Extinct(c.Civ);
    }

    void Extinct(Civ civ)
    {
        civ.Dead = true;
        DisbandForces(civ);
        foreach (var o in Civs) { o.AtWar.Remove(civ.Id); }
        // a player whose polity is gone goes on leading the largest human polity left
        if (civ.Main && Civs.Where(c => c.Human && !c.Dead && c.Colonies.Count > 0).OrderByDescending(c => c.Colonies.Count).FirstOrDefault() is { } heir)
            SwitchPlayer(heir);
        Log(Humanity.Colonies.FirstOrDefault(), $"{Narrative2.Called(civ, true)} {(civ.Main ? "are" : "are")} gone. The chronicle keeps their name.", "loss", null);
    }

    public void Completed(Colony c) { c.Star.Capture = 1; }

    // ---------- The clock ----------
    public void Step(double dy)
    {
        if (dy <= 0) return;
        for (int i = Thawing.Count - 1; i >= 0; i--)
        {
            var tb = Thawing[i];
            tb.Greening = Math.Min(1f, tb.Greening + (float)dy / 90f);          // about a century to full life
            if (tb.Greening >= 1f) { tb.Life = true; Thawing.RemoveAt(i); Log(HomeColony, $"{tb.Name} has rivers now, and forests, and weather of its own.", "event", tb); }
        }
        foreach (var civ in Civs) { civ.Faith = Math.Max(0, civ.Faith - (float)dy * 0.002f); civ.Dread = Math.Max(0, civ.Dread - (float)dy * 0.004f); }
        double target = Year + dy;
        // big jumps are cut into chunks so ships, research and councils keep their order
        double chunk = Math.Max(dy / Math.Ceiling(dy / 2.0), 1e-6);
        while (Year < target - 1e-9)
        {
            double d = Math.Min(chunk, target - Year);
            StepOnce(d);
        }
    }

    /// <summary>Run the clock forward (tests and warps): half-year steps, so councils and wars keep their order.</summary>
    public void Warp(double years) { double end = Year + years; while (Year < end - 1e-9) Step(Math.Min(0.5, end - Year)); }
    /// <summary>Test hook: break colonies away as a new polity.</summary>
    public Civ SplinterForTest(Civ parent, string drive, List<Colony> cols) => Splinter(parent, drive, cols);

    public static bool Profile;
    long _worstT; Colony _worst;
    void StepOnce(double dy)
    {
        var sw = Profile ? System.Diagnostics.Stopwatch.StartNew() : null;
        _worstT = 0; _worst = null;
        long t0 = 0, t1 = 0, t2 = 0;
        // index loop: colonies can be destroyed or change hands mid-step
        for (int i = 0; i < Colonies.Count; i++)
        {
            var c = Colonies[i];
            long cs = sw != null ? sw.ElapsedTicks : 0;
            c.Step(dy, c == Focus || c.Home && dy < 0.3);
            if (sw != null && sw.ElapsedTicks - cs > _worstT) { _worstT = sw.ElapsedTicks - cs; _worst = c; }
            c.Star.Capture = (float)c.Capture;
            if (i < Colonies.Count && Colonies[i] != c) i--;
        }
        Year += dy;
        FieldStep(dy);
        if (sw != null) t0 = sw.ElapsedTicks;
        foreach (var civ in Civs.ToList())
        {
            if (civ.Dead || civ.Colonies.Count == 0) continue;
            ResearchStep(civ);
            if (Year >= civ.NextYearly)
            {
                double yd = Math.Max(1, Year - civ.LastYearly);
                civ.LastYearly = Year; civ.NextYearly = Year + 1;
                PoolStep(civ, yd);
                GateStep(civ, yd);
                MeterStep(civ, yd);
                ActStep(civ, yd);
                ApoStep(civ, yd);
                Cultures.Drift(civ, yd, civ.AtWar.Count > 0);
                NavyStep(civ, yd);
                MobiliseStep(civ, yd);
                YardStep(civ, yd);
                BarracksStep(civ, yd);
                StrifeStep(civ, yd);
            }
            if (Year >= civ.NextElection) { Election(civ); civ.NextElection = Year + 25; }
            foreach (var k in civ.Inf.Keys.ToList()) civ.Inf[k] = Math.Max(0.02f, civ.Inf[k] + (float)((_r.NextDouble() - 0.5) * 0.004 * dy));
            civ.ShiftInf(civ.Drive, 0);
            var L = civ.Leader;
            if (L != null)
            {
                double age = Year - L.Born;
                if (age > 95 && civ.Digital < 0.9 && _r.NextDouble() < 0.03 * dy)
                {
                    civ.Leader = civ.NewLeader(civ.Drive, Year);
                    if (civ.Main) Log(HomeColony, $"Speaker {L.Name} dies in office at {(int)age}. {civ.Leader.Name} of the {civ.Faction(civ.Drive)} succeeds them.", "council", null);
                }
                else if (civ.Digital >= 0.9 && !L.Eternal)
                {
                    L.Eternal = true;
                    if (civ.Main) Log(HomeColony, $"Speaker {L.Name} uploads. They will not die in office now, or ever. Nobody can remember the last real election.", "omen", null);
                }
            }
        }
        if (sw != null) t1 = sw.ElapsedTicks;
        for (int i = Ships.Count - 1; i >= 0; i--)
        {
            var s = Ships[i];
            if (Year >= s.Arrive) { Ships.RemoveAt(i); ShipArrives(s); }
        }
        if (sw != null) t2 = sw.ElapsedTicks;
        if (Year >= _nextGalaxyCheck) { GalaxyCheck(); _nextGalaxyCheck = Year + 2; }
        if (Year >= _nextSample)
        {
            _nextSample = Year + 2;
            Timeline.Add((Year, Defs.Kardashev(Humanity.GalacticPower), Humanity.Pop, Humanity.Colonies.Count));
            if (Timeline.Count > 3000) { var keep = Timeline.Where((_, i) => i % 2 == 0).ToList(); Timeline.Clear(); Timeline.AddRange(keep); }
        }
        if (sw != null && sw.ElapsedMilliseconds > 15)
        {
            double ms(long t) => t * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Godot.GD.Print($"SLOW step {sw.ElapsedMilliseconds}ms colonies={ms(t0):0.0} civs={ms(t1 - t0):0.0} ships={ms(t2 - t1):0.0} galaxy={ms(sw.ElapsedTicks - t2):0.0} n={Colonies.Count} worst={ms(_worstT):0.0}ms {_worst?.Sys.Name} complete={_worst?.Complete} home={_worst?.Home} civ={_worst?.Civ.Name}");
        }
    }
}

/// <summary>Flavour text that needs the simulation's wider view.</summary>
public static class Narrative2
{
    public static string Called(Civ c, bool capital = false)
    {
        string n = c.Human && c.Main ? "humanity" : c.Name;
        return capital && n.Length > 0 ? char.ToUpper(n[0]) + n[1..] : n;
    }

    public static string FirstLook(Colony c)
    {
        var life = c.Sys.All().FirstOrDefault(b => b.Life);
        int n = c.Sys.Bodies.Count;
        if (life != null) return $"There are {n} worlds here, and one of them is alive: {life.Name}.";
        var gas = c.Sys.Bodies.Count(b => b.Kind is BodyKind.Gas or BodyKind.IceGiant);
        return $"{n} worlds, {gas} of them giants. Nothing lives here. Everything here can be used.";
    }

    public static string AlienBlurb(Civ c) => c.Species switch
    {
        "Vessh" => "colonial, ancient, and very sure of themselves. Their stars went dark long before ours began to dim.",
        "Orun" => "slow, deliberate, and hard to read. They answer every message exactly one year later.",
        "Tallow" => "a choir of minds that speak in light. They seem delighted that we exist.",
        "Hask" => "machine-descended and patient. They want to know how much our Sun weighs.",
        "Ninefold" => "nine species in one polity, still arguing about whether to answer.",
        _ => "silent. Their worlds glow, and nothing more."
    };

    public static string AlienFirstContact(Civ other, float rel) =>
        rel > 15 ? "The first message back is a map of their stars, with ours marked on it as a friend."
        : rel < -20 ? "Their first message is a warning, repeated in every language we have ever broadcast."
        : "Nobody is sure yet what they want.";
}
