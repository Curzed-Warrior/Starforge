using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

/// <summary>Effects of known technology, recomputed whenever a tech completes (or an exotic pool runs dry or refills).</summary>
public class Mods
{
    public float Mine = 1, Foundry = 1, Eff = 1, GasYield = 1, Rp = 1, HubCost = 1, Shield = 1, Comets = 1, PopCap = 1, Growth = 1;
    public double PlanetCap = 1.2e14;          // ceiling of planet-bound power, watts
    public float ShipSpeed = 0f;               // interstellar cruise speed, fraction of c (0 = cannot)
    public float RegionRate = 1f, RegionPower = 1f;
    // the tech bible's levers (docs/TECH_INTEGRATION.md 3.3)
    public float Fleet = 1, Ground = 1, WarLoss = 1, Stealth = 1, Sense = 1, Range = 1, Thrust = 1, Diplo = 1, Unity = 1, Admin = 1,
        Trade = 1, Terra = 1, Uplift = 1, Habs = 1, Settle = 1, Build = 1, Spy = 1, Base = 0;
    public bool GateNet, Ark, CoreTap, Transcend;
    public bool StarLift, Habitat, Fast, Seed, Gate, Hive, Deterrent, Fold, Probes;
    public bool Moon = true, Mars = true, Venus, Belt, Jovian, Outer, Mercury, Elevator, OrbitalRing, Terraform, Swarm, Skimmers;
    public readonly HashSet<string> Flags = new(), Hulls = new() { "Shuttle" }, Megas = new(), Acts = new(), Vis = new();
    public readonly Dictionary<string, int> Arms = new(), Guard = new();
    public readonly Dictionary<string, double> Produce = new(), Upkeep = new();
    public bool Has(string flag) => Flags.Contains(flag);
}

public class Tech
{
    public string Id, Name, Fx, Log, Line;      // Line: "SPINE", "PWR", "EWP", ...
    public char B;                              // branch: X I E S L M W Q
    public int Tier;
    public double Cost;
    /// <summary>
    /// What it costs a civilisation that already knows n things: each discovery stands on more before it, so the
    /// price climbs past the first sixty (×2 by 100 known, ×8 by 200, ×19 by 300). Keeps the late tree from
    /// collapsing in a few decades once a civilisation spans many stars and its research multiplies.
    /// </summary>
    public double CostFor(Civ c) => Spine ? Cost : Cost * (1 + Math.Pow(Math.Max(0, c.Techs.Count - 60) / 40.0, 1.6));     // the spine is the pacing itself
    public string[][] Req = Array.Empty<string[]>();   // AND of OR-groups
    public bool Start, Rare, Danger, Keystone, Event, Atrocity, Spine;
    public string Exclusive, Gate, Cond, Path;
    public string[] Uses = Array.Empty<string>();
    public int Apo;
    public List<Effect> Effects = new();
    public readonly List<Tech> Unlocks = new();
    public string LogText => Log ?? Fx + ".";
}

public static class Defs
{
    public const double LSun = 3.828e26;     // watts
    public const double SwarmKg = 1.1e24;    // the game's unit of mass: one full swarm of collectors
    public const double TypeI = 1e16, TypeII = 1e26;

    public static double Kardashev(double watts) => (Math.Log10(Math.Max(watts, 1)) - 6.0) / 10.0;

    public static readonly (char Id, string Name, string Blurb, string Dream)[] Branches =
    {
        ('X', "Starflight", "Rockets, drives, sails, seeds", "other suns, and the dark between them."),
        ('I', "Industry", "Mining, replicators, disassembly", "machines that build machines that build worlds."),
        ('E', "Energy", "Fusion, collectors, the swarm", "a star wrapped so tightly it forgets how to shine."),
        ('S', "Society", "Habitats, government, trade, minds", "minds that outlast bodies, and bodies that outlast stars."),
        ('L', "Life", "Genetics, xenobiology, terraforming", "worlds that grow, and people grown to fit them."),
        ('M', "Mind", "Computing, sensors, psionics", "a thought large enough to hold a galaxy."),
        ('W', "War", "Weapons, armour, fleets, fortresses", "a fleet no star could stand against."),
        ('Q', "Beyond", "Umbral, chronal, glyph, stellar, lucent", "the doors behind the universe, and what waits behind them."),
    };
    public static (char Id, string Name, string Blurb, string Dream) Branch(char b) => Branches.First(x => x.Id == b);

    public static readonly double[] TierCost = { 0, 6, 15, 35, 80, 180, 400, 1200, 2600, 18000, 60000 };

    /// <summary>Every tech, in tier order, loaded from data/techs.json.</summary>
    public static List<Tech> Techs { get { if (_techs == null) LoadTechs(); return _techs; } }
    static List<Tech> _techs;
    static Dictionary<string, Tech> _byId;
    public static double CostScaleNonSpine = 0.35;
    public static void LoadTechs()
    {
        TechData.LoadProgression(Godot.FileAccess.GetFileAsString(TechData.ProgPath));
        CostScaleNonSpine = TechData.D("research", "costScaleNonSpine");
        var args = Godot.OS.GetCmdlineUserArgs();
        if (Array.Find(args, x => x.StartsWith("--costscale=")) is string cs) CostScaleNonSpine = double.Parse(cs[12..], System.Globalization.CultureInfo.InvariantCulture);
        if (TechData.P("research").TryGetProperty("spineBias", out var sb)) Simulation.SpineBias = sb.GetDouble();
        if (Array.Find(args, x => x.StartsWith("--spinebias=")) is string sbs) Simulation.SpineBias = double.Parse(sbs[12..], System.Globalization.CultureInfo.InvariantCulture);
        _techs = TechData.Load(Godot.FileAccess.GetFileAsString(TechData.TechsPath));
        var mult = TechData.P("research", "nonSpineTierMult").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        foreach (var t in _techs) t.Cost = t.Spine ? TierCost[t.Tier] : Math.Max(1, TierCost[t.Tier] * CostScaleNonSpine * mult[t.Tier]);
        _byId = _techs.ToDictionary(t => t.Id);
        var dw = TechData.P("research", "driveWeights");
        foreach (var name in Drives.Keys.ToList())
            Drives[name] = Drives[name] with { W = dw.GetProperty(name).EnumerateObject().ToDictionary(p => p.Name[0], p => (float)p.Value.GetDouble()) };
        foreach (var p in TechData.P("research", "branchFaction").EnumerateObject()) BranchFaction[p.Name[0]] = p.Value.GetString();
        ExclusiveGroups = _techs.Where(t => t.Exclusive != null).GroupBy(t => t.Exclusive).ToDictionary(g => g.Key, g => g.Select(t => t.Id).ToArray());
        foreach (var t in _techs) foreach (var g in t.Req) foreach (var r in g) if (_byId.TryGetValue(r, out var rt)) rt.Unlocks.Add(t);
    }
    public static Dictionary<string, string[]> ExclusiveGroups = new();
    /// <summary>Hull classes, smallest to largest, and their size relative to a shuttle (progression.json hulls).</summary>
    public static string[] Hulls => _hulls ??= LoadArr("hulls", "order").Select(e => e.GetString()).ToArray();
    public static float[] HullScale => _hullScale ??= LoadArr("hulls", "scale").Select(e => (float)e.GetDouble()).ToArray();
    static string[] _hulls; static float[] _hullScale;
    static IEnumerable<System.Text.Json.JsonElement> LoadArr(params string[] path) { if (_techs == null) LoadTechs(); return TechData.P(path).EnumerateArray(); }
    public static Tech TechById(string id) { if (_techs == null) LoadTechs(); return _byId[id]; }
    public static Tech TryTech(string id) { if (_techs == null) LoadTechs(); return id != null && _byId.TryGetValue(id, out var t) ? t : null; }

    /// <summary>The chronicle's line for each spine tech when it completes.</summary>
    public static readonly Dictionary<string, string> SpineLog = new()
    {
        ["X0"] = "Rockets land, refuel and fly again the same week.",
        ["X1"] = "A reactor-heated engine cuts the road to Mars in half.",
        ["X2"] = "The first fusion torch lights over the Pacific. It is visible at noon.",
        ["X3"] = "A thousand lightsails ride a laser out of the system. Twenty years to Alpha Centauri.",
        ["X4"] = "A seed ship's keel is laid in orbit. Its crew has not been born yet.",
        ["X5"] = "Antimatter by the kilogram. The ships get faster; the arguments about safety get louder.",
        ["X6"] = "Space is folded for the first time. The test ship arrives before its own light.",
        ["X7"] = "They can now kill a star system from forty light-years away. Nobody votes to use it. Nobody votes to dismantle it.",
        ["X8"] = "A fold gate opens. It stays open.",
        ["X9"] = "The gates are joined into one network. Any two stars it touches are a single step apart.",
        ["X10"] = "Ten thousand ships turn their backs on the Milky Way. Two and a half million years of dark lie ahead of them.",
        ["I1"] = "The first mass driver flings lunar regolith into orbit at two kilometres a second.",
        ["I2"] = "A captured asteroid worth more than Earth's economy is parked at L1.",
        ["I3"] = "Foundries now build their own successors.",
        ["I4"] = "Skimmer fleets dive into Jupiter's upper atmosphere and come back heavy.",
        ["I5"] = "Whole worlds now come apart in years, not centuries.",
        ["I6"] = "Magnetic funnels begin pulling mass off the star.",
        ["I7"] = "Dying stars are refuelled; young ones are slowed down. Nothing will be wasted for a trillion years.",
        ["I8"] = "Stellar-mass black holes are fed matter by the planetload and give back energy by the galaxy-load.",
        ["E1"] = "The last coal plant on Earth closes. Nobody notices.",
        ["E2"] = "Kilometre-wide mirrors beam power down to rectennas in the deserts.",
        ["E3"] = "Collector film is now thinner than a soap bubble.",
        ["E4"] = "The swarm learns to fold itself away from flares.",
        ["E5"] = "Collectors now hover on light pressure alone.",
        ["E6"] = "Nested shells run computation on the swarm's waste heat.",
        ["E7"] = "The swarm learns to lean the star's own light against its flares.",
        ["E8"] = "Black holes made of pure light now power the deep fleets.",
        ["E9"] = "Sagittarius A* is fed and tapped. The galactic core flares, and then, for the first time in its history, it is working for someone.",
        ["S1"] = "The first kilometre-tall arcology opens in Lagos. It houses two million people and grows its own food.",
        ["S2"] = "A sealed habitat runs for ten years without a single resupply.",
        ["S3"] = "The first person to live to two hundred gives a short speech. She is not tired yet.",
        ["S4"] = "The first spin-gravity habitat opens. Children are born under a sun that never sets.",
        ["S5"] = "Comets are steered into Mars. The first rain falls there in four billion years.",
        ["S6"] = "Half the population chooses to become software.",
        ["S7"] = "The last biological council votes itself out of existence.",
        ["S8"] = "Disagreement is engineered out. The last schism was the last.",
        ["S9"] = "Every mind that ever lived in the polity is remembered, and every one of them can still be asked.",
        ["S10"] = "Something that used to be humanity steps out of the universe for a moment, and comes back changed. It will not say where it went.",
    };

    // ---------- Drives, ethics, factions (from the phone game) ----------
    public record Drive(string Name, string Faction, string Creed, Dictionary<char, float> W, float SeedAt, string About);
    public static readonly Dictionary<string, Drive> Drives = new()
    {
        ["Expansionist"] = new("Expansionist", "Wayfarers", "go further", new() { ['I'] = 1.3f, ['E'] = 1.0f, ['S'] = 0.7f, ['X'] = 2.2f }, 0.0002f, "They look outward. Every system is a stepping stone."),
        ["Contemplative"] = new("Contemplative", "Choristers", "think deeper", new() { ['I'] = 0.8f, ['E'] = 1.2f, ['S'] = 2.2f, ['X'] = 0.9f }, 0.3f, "They turn inward. Power is for thinking with."),
        ["Industrious"] = new("Industrious", "Forgewrights", "build more", new() { ['I'] = 2.2f, ['E'] = 1.5f, ['S'] = 0.8f, ['X'] = 0.9f }, 0.1f, "They build because building is what they are."),
    };
    public static readonly Dictionary<char, string> BranchFaction = new() { ['X'] = "Expansionist", ['S'] = "Contemplative", ['I'] = "Industrious", ['E'] = "Industrious" };
    public static readonly Dictionary<string, (float Life, float Conscience, string About)> Ethics = new()
    {
        ["Reverent"] = (0f, 0.82f, "Living worlds are sacred to them, until there is nothing else left."),
        ["Pragmatic"] = (0.3f, 0.5f, "They regret what they consume, and consume it anyway."),
        ["Ruthless"] = (1f, 0.18f, "Matter is matter."),
    };

    public static readonly (double At, string Name, string Text)[] Eras =
    {
        (0, "The Cradle", "Era of the Cradle. Humanity has one world and a few footholds."),
        (2e15, "The Inner System", "Era of the Inner System. Industry leaves Earth's gravity well for good."),
        (TypeI, "Type I", "Type I. Humanity now commands as much power as falls on its whole planet."),
        (1e21, "The Swarm", "Era of the Swarm. The Sun has dimmed by a measurable fraction, visible from neighbouring stars."),
        (0.1 * LSun, "Twilight", "Era of Twilight. A tenth of the Sun's light is gone from the sky."),
        (0.5 * LSun, "Eclipse", "Era of Eclipse. More of the Sun is caught than escapes."),
    };

    public static readonly string[] Given = { "Ilse", "Tomas", "Oyelaran", "Mirren", "Kesh", "Anaïs", "Dov", "Sefa", "Rook", "Hal", "Imre", "Lune", "Wren", "Casimir", "Noor", "Ede", "Saoirse", "Amara", "Jun", "Tavi", "Priya", "Mateo", "Yusuf", "Linnea", "Kofi", "Mei" };
    public static readonly string[] Surn = { "Varo", "Okonkwo-Lind", "Achterberg", "Sel", "Marchetti", "Ivo", "Danaher", "Quell", "Ruiz-Abara", "Tamm", "Holloway", "Nakashima", "Brand", "Oduya", "Adeyemi", "Castellanos", "Lindqvist", "Rahman", "Okafor", "Novak" };

    public static readonly Dictionary<string, string[]> Quotes = new()
    {
        ["Expansionist"] = new[] { "Every star we pass is a star we owe a visit.", "Stay too long anywhere and it becomes a grave.", "The dark between stars is only a corridor.",
            "Our grandchildren will be born under suns we have not named yet.", "If something out there is watching, let it watch us leave.", "Earth is a cradle. Nobody lives in a cradle forever." },
        ["Contemplative"] = new[] { "We did not cross the dark to build. We crossed it to understand.", "Every collector is a thought the star never had.", "Silence is not empty. Listen to it.",
            "What we make of this star, it makes of us.", "One day the swarm will ask better questions than we do." },
        ["Industrious"] = new[] { "A star is a resource that has not been organised yet.", "Build first. Mourn later, if there is time.", "Nothing out there was doing anything useful before we came.",
            "Measure twice, dismantle once.", "The work is the prayer." },
    };
    public static readonly Dictionary<string, string[]> EthicColor = new()
    {
        ["Reverent"] = new[] { " And we will not forget what it cost.", " May the Sun forgive us.", "", "" },
        ["Pragmatic"] = new[] { "", "", " We will do what is necessary, and no more." },
        ["Ruthless"] = new[] { " Whatever it takes.", " Nobody will count the cost.", "", "" },
    };

    public static readonly string[] ShipNames = { "Tsiolkovsky", "Long Vigil", "Threnody", "Quiet Lantern", "Meridian", "Hesper", "Ninth Wake", "Patient Fire", "Cold Harbour",
        "Anamnesis", "Gagarin", "Tereshkova", "Ad Astra", "Kepler's Daughter", "Pale Blue", "Endurance", "Sagan", "Vostok Dawn", "Mae Jemison", "Valentina", "Hope of Lagos", "Breath of Kerala" };
}
