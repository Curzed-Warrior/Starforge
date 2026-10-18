using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Starforge.Sim;

public class Slot
{
    public readonly string Area;
    public string Id;                 // tech being researched, or null
    public double P;                  // progress toward it
    public List<string> Hand = new(), LastHand = new();
    public string Pinned;             // the voice wants this one next
    public Slot(string area) { Area = area; }
}

public class Leader
{
    public string Name, Drive;
    public double Since, Born;
    public bool Eternal;
}

/// <summary>A polity: humanity (and later its splinters) or an alien species. Owns colonies, shares one council and tech base.</summary>
public class Civ
{
    public int Id;
    public string Name, Species;
    public Color Color;
    public bool Human, Alien, Main;
    public int Age;                 // 0 young .. 3 ancient
    public bool Ancient => Alien && Age >= 2 && !Dead;
    public string Drive, Ethic;
    public float Conscience;
    public Dictionary<string, float> Inf = new();
    public Dictionary<char, float> W = new();
    public HashSet<string> Techs = new();
    public Mods M = new();
    /// <summary>Three research slots (Physics, Engineering, Society), each working one tech drawn from a hand of three.</summary>
    public readonly Dictionary<string, Slot> Slots = new() { ["Physics"] = new Slot("Physics"), ["Engineering"] = new Slot("Engineering"), ["Society"] = new Slot("Society") };
    /// <summary>Research nobody had a slot for, waiting for one to open.</summary>
    public double RpBank;
    public readonly Dictionary<string, int> Pity = new();                 // keystone id -> draws it sat in the pool unpicked
    public readonly Dictionary<string, string> ExclusivePick = new();     // exclusive group -> the member this council leans toward
    public readonly Dictionary<string, double> Banned = new();            // tech id -> year the voice's ban lifts
    /// <summary>Exotic stockpiles (Umbral, Abyssal, Anima, Stellarine, Lumen) and the 11 danger meters.</summary>
    public readonly Dictionary<string, double> Pools = new(), Meters = new();
    public readonly Dictionary<string, double> CrisisAt = new();          // meter -> year its crisis may next recur
    public readonly HashSet<string> Fed = new();                          // use: techs whose pool is keeping them running
    /// <summary>Gate events: 0 closed (absent), 1 open (waiting for a key), 2 done.</summary>
    public readonly Dictionary<string, int> Gates = new();
    public readonly Dictionary<string, double> GateP = new();             // progress toward a gate's key (projects, raids, salvage wins)
    public double GateT(string g) => GateP.TryGetValue("at:" + g, out var y) ? y : 1e9;
    public string Path;                                                   // ascension path: S G C P D
    public bool Exalted, Fallen;
    public double Strain, NextApoRoll, NextExaltedAct;
    public int ApoScore => Techs.Sum(id => Defs.TryTech(id)?.Apo ?? 0);
    /// <summary>Index (into Defs.Hulls) of the biggest hull this civ's yards can lay down.</summary>
    public int Hull { get { int k = 0; for (int i = 0; i < Defs.Hulls.Length; i++) if (M.Hulls.Contains(Defs.Hulls[i])) k = i; return k; } }
    /// <summary>Things this civ does of its own accord, unlocked by act: effects; year each may next be tried.</summary>
    public readonly Dictionary<string, double> ActAt = new();
    public double WyrmUntil, OvermindUntil, NextYearly, LastYearly = 2061;
    public Civ Overlord;                                                  // an Imperial mandate's subject answers to this civ
    public int Federation = -1;                                           // id of the federation's founder, or -1
    public bool Custodian, Imperial;
    public float VoiceRelation;                                           // an Exalted civ's feelings toward the voice
    public Leader Leader;
    public double Digital;
    public List<string> Used = new();
    public List<Colony> Colonies = new();
    /// <summary>Logistics between this civ's systems, replanned yearly (see Colony.PlanLevies): where each system's
    /// spare industry is sent, and what share of its own works budget leaves.</summary>
    public readonly Dictionary<Colony, List<(Colony From, double F)>> Levy = new();
    public readonly Dictionary<Colony, double> LevyOut = new();
    public double LevyYear = -1e9;
    /// <summary>Strategic deposits held somewhere in the empire (Deposits).</summary>
    public readonly HashSet<string> Strategic = new();
    /// <summary>A state of emergency: every system's industry and stockpile goes to critical works (Colony.EvaluateEmergency).</summary>
    public bool Emergency;
    public double EmergencySince, ThreatSeen = -1e9, LastLoss = -1e9;
    public string EmergencyCause;
    /// <summary>The system each civ-unique great work is (to be) built in, replanned with the levies.</summary>
    public readonly Dictionary<string, Colony> Sites = new();
    public int Gen = 1;
    public bool Dead;
    /// <summary>What the council runs by itself and what waits on the player (Policy).</summary>
    public Policy Policy = new();
    public double Founded;
    public Dictionary<int, float> Relations = new();     // civ id -> -100..100
    public HashSet<int> Met = new();
    public HashSet<int> AtWar = new();
    public double NextElection, Splintered = -1;
    public float Vigour = 1f;
    /// <summary>War culture (Culture.cs): set from the species' lore when the civ takes its drive, then drifts.</summary>
    public Culture Culture;
    /// <summary>Every citizen serves: more manpower, felt as a burden by a people of low Duty.</summary>
    public bool MandatoryService;
    /// <summary>War economy (WarEconomy.cs): the mobilisation level in force (0..3), the share of industry turned to war,
    /// how well the war lines run, stocks of materiel and trained manpower, and the strain of it on the people.</summary>
    public int Mobilisation;
    public double MilShare = 0.04, Efficiency = 1, Materiel, Manpower, WarStrain;
    /// <summary>Where the military share stood when the level last changed, and when.</summary>
    public double MilFrom = 0.04, MilSince;
    internal (double, float) WarCacheKey = (double.NaN, 0); internal double WarCacheG, WarCacheR;
    /// <summary>The building slips and what is on them (WarEconomy.YardStep).</summary>
    public List<Slip> Yard = new();
    /// <summary>Recruits in training (WarEconomy.BarracksStep).</summary>
    public List<Training> Barracks = new();
    /// <summary>How many believe the unseen voice is real (0..1), and how many fear it.</summary>
    public float Faith, Dread;
    public int FaithStage;     // how hungrily this polity spreads: old empires are slow, young ones restless
    readonly Random _r;

    public Civ(int id, int seed)
    {
        Id = id;
        _r = new Random(seed);
    }

    public Random R => _r;
    public float Rnd(float a, float b) => a + (float)_r.NextDouble() * (b - a);
    public T Choose<T>(IList<T> l) => l[_r.Next(l.Count)];

    public void SetDrive(string drive, string ethic)
    {
        Drive = drive; Ethic = ethic;
        if (Culture == null) { Culture = Cultures.Lore(Human ? "Human" : Species); MandatoryService = Culture.Duty > 0.5f; }
        W = new Dictionary<char, float>(Defs.Drives[drive].W);
        Conscience = Defs.Ethics[ethic].Conscience;
        foreach (var k in Defs.Drives.Keys) Inf[k] = k == drive ? 0.5f : 0.25f;
    }

    public void ComputeMods()
    {
        M = new Mods();
        SpineKnown = 0;
        foreach (var t in Defs.Techs) if (Techs.Contains(t.Id)) { TechData.Apply(t, M, t.Uses.Length == 0 || Fed.Contains(t.Id)); if (t.Spine) SpineKnown++; }
        if (Fallen) { M.Rp *= 0.5f; }
    }

    public bool PrereqDone(Tech t) => t.Req.All(g => g.Any(Techs.Contains));
    public bool Knows(string id) => Techs.Contains(id);
    /// <summary>How many of the 38 spine techs are known: the old measure of a civ's progress, which economy rules still lean on.</summary>
    public int SpineKnown;
    public float Meter(string m) => (float)Meters.GetValueOrDefault(m);
    public void AddMeter(string m, double d) => Meters[m] = Math.Clamp(Meters.GetValueOrDefault(m) + d, 0, 200);
    public IEnumerable<Tech> Researching => Slots.Values.Where(s => s.Id != null).Select(s => Defs.TechById(s.Id));

    public void ShiftInf(string drive, float amt)
    {
        Inf[drive] = Math.Max(0.02f, Inf[drive] + amt);
        float sum = Inf.Values.Sum();
        foreach (var k in Inf.Keys.ToList()) Inf[k] /= sum;
    }

    public Leader NewLeader(string drive, double now) =>
        new() { Name = Alien ? AlienName() : Choose(Defs.Given) + " " + Choose(Defs.Surn), Drive = drive, Since = now, Born = now - (Alien ? Rnd(120, 400) : Rnd(45, 70)) };

    static readonly Dictionary<string, (string[] Syl, string Pattern)> Tongues = new()
    {
        ["Vessh"] = (new[] { "ve", "ss", "kh", "ra", "th", "ul", "ix", "sha", "or", "ze" }, "{0}{1}{2}'{3}{4}"),
        ["Orun"] = (new[] { "o", "ru", "nu", "ma", "lo", "um", "ba", "do", "an" }, "{0}{1}{2} {3}{4}{5}"),
        ["Tallow"] = (new[] { "li", "ae", "il", "ya", "en", "ia", "lu", "sae", "wen" }, "{0}{1}{2}-{3}{4}"),
        ["Hask"] = (new[] { "hk", "71", "ax", "q", "0", "rr", "k3", "tz", "x9" }, "{0}{1}-{2}{3}{4}"),
        ["Ninefold"] = (new[] { "nine", "of", "the", "far", "low", "high", "chorus", "seventh", "root" }, "{0} {1} {2}"),
        ["Lattice"] = (new[] { "∴", "◇", "ka", "ru", "e", "o" }, "{0}{1}{2}"),
    };

    string AlienName()
    {
        if (!Tongues.TryGetValue(Species ?? "", out var t)) t = Tongues["Orun"];
        var parts = new object[6];
        for (int i = 0; i < 6; i++) parts[i] = Choose(t.Syl);
        string n = string.Format(t.Pattern, parts);
        return char.ToUpper(n[0]) + n[1..];
    }

    public double Power => Colonies.Sum(c => c.Power);
    public double GalacticPower => Colonies.Sum(c => c.GalacticPower);
    public double Pop => Colonies.Sum(c => c.Pop);
    public string Faction(string drive) => Alien ? AlienFaction(drive) : Defs.Drives[drive].Faction;
    string AlienFaction(string d) => d switch { "Expansionist" => "Outward Choir", "Contemplative" => "Still Ones", _ => "Makers" };

    public string FmtPop()
    {
        double b = Pop;
        string n = b < 1 ? Math.Round(b * 1000) + " million" : b < 1000 ? b.ToString("0.0") + " billion" : (b / 1000).ToString("0.00") + " trillion";
        return Digital > 0.01 ? $"{n} ({Math.Round(Digital * 100)}% digital)" : n;
    }
}
