using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Starforge.Sim;

/// <summary>Whole-universe snapshot. The galaxy and every system regenerate from seeds; only what changed is stored.</summary>
public static class SaveGame
{
    public class LeaderDto { public string Name, Drive; public double Since, Born; public bool Eternal; }
    public class CivDto
    {
        public int Id; public string Name, Species, Drive, Ethic, ResearchId; public float[] Color; public bool Human, Alien, Main, Dead;
        public float Conscience, Vigour = 1, Faith, Dread; public int FaithStage; public Dictionary<string, float> Inf; public Dictionary<string, float> W; public List<string> Techs, Used;
        public double ResearchP, Digital, Founded, NextElection; public int Gen; public LeaderDto Leader;
        public Dictionary<int, float> Relations; public List<int> Met, AtWar;
        // the tech bible's state
        public Dictionary<string, SlotDto> Slots; public double RpBank; public Dictionary<string, int> Pity; public Dictionary<string, string> ExclusivePick;
        public Dictionary<string, double> Banned, Pools, Meters, CrisisAt, GateP, ActAt; public Dictionary<string, int> Gates;
        public string Path; public bool Exalted, Fallen, Custodian, Imperial; public double Strain, NextApoRoll, NextExaltedAct, WyrmUntil, OvermindUntil, NextYearly, LastYearly;
        public float VoiceRelation; public int Overlord = -1, Federation = -1, Age;
        public Policy Policy;
        // the war foundation (missing in older saves: culture from lore, peacetime economy)
        public float[] Culture; public bool? MandatoryService; public int Mobilisation; public double? MilShare, MilFrom, Efficiency;
        public double MilSince, Materiel, Manpower, WarStrain; public List<Slip> Yard; public List<Training> Barracks;
        public Dictionary<int, double> Truce; public int TributeTo = -1; public double TributeUntil;
        // infighting (zero in older saves)
        public double JuntaUntil;
    }
    public class SlotDto { public string Id, Pinned; public double P; public List<string> Hand, LastHand; }
    public class StarDto { public int Id, Seed; public float X, Y, Z, Lum, Temp; public bool Dead, Kindled; public string Name, Cls, Special; }
    public class MissionDto { public string Target, From; public int Kind; public double T, Dur; }
    public class NativeDto { public string Name, Contact; public int Level; public double T, ClearAt; public bool Protected; }
    public class ColonyDto
    {
        public int Civ, Star; public bool Home, Complete, ProjectDone, VisitorDone;
        public double Region, Founded, PlanetPower, PowerCap, Capture, Stock, Pop, PopCap, PeakPop, NextDecision, NextEvent, NextSeed, VisitorT, HomeDark;
        public int Foundries, Habitats, Era, SeedsSent, VisitorStage, Gen = 1; public float Boost, ElevatorP, RingP, TerraformP, ProjectP;
        public List<string> Raised = new();
        public Dictionary<string, float> Built;
        public string Seat, Project, VisitorOutcome;
        public Dictionary<string, int> Hubs, Stations; public Dictionary<string, float> Settled;
        public List<MissionDto> Missions; public Dictionary<string, NativeDto> Natives; public List<string> Frigates;
        public string CrisisId, CrisisTarget; public double CrisisT0, CrisisAcc;
        public string RivalName, RivalPlanet, RivalStance; public double RivalPop, RivalT0; public int RivalShips;
        public string SplFaction; public double SplPop, SplAt, SplBack; public bool SplReturned, HasSplinter;
        public double? WorksOverride;
        public int OccupiedBy = -1; public double BlockadedUntil; public float DefenceLoss;
        // infighting (zero in older saves); the home system's worlds by body name
        public float Grievance; public int Unrest; public string Cause, CauseKey, Movement; public bool Autonomy; public double InvestedUntil;
        public Dictionary<string, float> WorldGrievance; public Dictionary<string, int> WorldUnrest;
    }
    public class ShipDto { public int Civ, Origin, Target, FromStar; public double Depart, Arrive; public string Name; }
    public class LogDto { public double Year; public int Civ, Star; public string System, Text, Kind; }
    public class SaveDto
    {
        public int Version = 2, Seed, StarCount; public double Year, AnsweredAt, ArkYear = -1; public bool TypeIII;
        public Dictionary<string, double> Cooldown;
        public List<CivDto> Civs; public List<ColonyDto> Colonies; public List<ShipDto> Ships; public List<LogDto> Log;
        public Dictionary<int, Dictionary<string, float>> Mined;
        public List<double[]> Timeline;
        public Dictionary<int, Dictionary<string, float[]>> Worlds;     // greening, ignited, orbit, period, orbit0
        public List<StarDto> Stars;                                     // stars the voice made, moved, put out, or rekindled
        public Dictionary<int, List<float[]>> Forged;                   // kind, orbit, radius, seed per forged world
        public Dictionary<int, List<string>> ForgedNames;
        public List<int[]> Lanes; public List<float[]> Quarantines; public List<string> Laws, Revealed; public double GateSealYear = 2600;
        public List<Fleet> Fleets; public List<Army> Armies; public int NextFleetId, NextArmyId;
        public List<War> Wars; public int NextWarId;
    }

    static readonly JsonSerializerOptions Opt = new() { IncludeFields = true, WriteIndented = false, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };

    public static void Save(Simulation sim, int seed, string path)
    {
        var d = new SaveDto
        {
            Seed = seed, StarCount = sim.Gal.Stars.Count, Year = sim.Year, AnsweredAt = sim.AnsweredAt, ArkYear = sim.ArkYear, TypeIII = sim.TypeIIIAnnounced,
            Civs = sim.Civs.Select(c => new CivDto
            {
                Id = c.Id, Name = c.Name, Species = c.Species, Drive = c.Drive, Ethic = c.Ethic,
                Color = new[] { c.Color.R, c.Color.G, c.Color.B }, Human = c.Human, Alien = c.Alien, Main = c.Main, Dead = c.Dead,
                Conscience = c.Conscience, Vigour = c.Vigour, Faith = c.Faith, Dread = c.Dread, FaithStage = c.FaithStage, Inf = new(c.Inf), W = c.W.ToDictionary(k => k.Key.ToString(), k => k.Value), Techs = c.Techs.ToList(), Used = c.Used.ToList(),
                Digital = c.Digital, Founded = c.Founded, NextElection = c.NextElection, Gen = c.Gen,
                Leader = c.Leader == null ? null : new LeaderDto { Name = c.Leader.Name, Drive = c.Leader.Drive, Since = c.Leader.Since, Born = c.Leader.Born, Eternal = c.Leader.Eternal },
                Relations = new(c.Relations), Met = c.Met.ToList(), AtWar = c.AtWar.ToList(),
                Slots = c.Slots.ToDictionary(k => k.Key, k => new SlotDto { Id = k.Value.Id, Pinned = k.Value.Pinned, P = k.Value.P, Hand = k.Value.Hand.ToList(), LastHand = k.Value.LastHand.ToList() }),
                RpBank = c.RpBank, Pity = new(c.Pity), ExclusivePick = new(c.ExclusivePick), Banned = new(c.Banned), Pools = new(c.Pools), Meters = new(c.Meters),
                CrisisAt = new(c.CrisisAt), GateP = new(c.GateP), ActAt = new(c.ActAt), Gates = new(c.Gates), Path = c.Path, Exalted = c.Exalted, Fallen = c.Fallen,
                Custodian = c.Custodian, Imperial = c.Imperial, Strain = c.Strain, NextApoRoll = c.NextApoRoll, NextExaltedAct = c.NextExaltedAct, WyrmUntil = c.WyrmUntil,
                OvermindUntil = c.OvermindUntil, NextYearly = c.NextYearly, LastYearly = c.LastYearly, VoiceRelation = c.VoiceRelation, Overlord = c.Overlord?.Id ?? -1,
                Federation = c.Federation, Age = c.Age, Policy = c.Policy,
                Culture = new[] { c.Culture.Martial, c.Culture.Duty, c.Culture.Resolve }, MandatoryService = c.MandatoryService, Mobilisation = c.Mobilisation,
                MilShare = c.MilShare, MilFrom = c.MilFrom, MilSince = c.MilSince, Efficiency = c.Efficiency, Materiel = c.Materiel, Manpower = c.Manpower,
                WarStrain = c.WarStrain, Yard = c.Yard, Barracks = c.Barracks, Truce = c.Truce, TributeTo = c.TributeTo, TributeUntil = c.TributeUntil,
                JuntaUntil = c.JuntaUntil,
            }).ToList(),
            Fleets = sim.Fleets, Armies = sim.Armies, NextFleetId = sim.NextFleetId, NextArmyId = sim.NextArmyId, Wars = sim.Wars, NextWarId = sim.NextWarId,
            Colonies = sim.Colonies.Select(c => new ColonyDto
            {
                Civ = c.Civ.Id, Star = c.Star.Id, Home = c.Home, Complete = c.Complete, ProjectDone = c.ProjectDone, VisitorDone = c.VisitorDone,
                Region = c.Region, Founded = c.Founded, PlanetPower = c.PlanetPower, PowerCap = c.PowerCap, Capture = c.Capture, Stock = c.Stock, Pop = c.Pop, PopCap = c.PopCap, PeakPop = c.PeakPop,
                NextDecision = c.NextDecision, NextEvent = c.NextEvent, NextSeed = c.NextSeed, VisitorT = c.VisitorT, HomeDark = c.HomeDark,
                Foundries = c.Foundries, Habitats = c.Habitats, Era = c.Era, SeedsSent = c.SeedsSent, VisitorStage = c.VisitorStage, Gen = c.Gen, Raised = c.Raised.ToList(), Built = new(c.Built),
                Boost = c.Boost, ElevatorP = c.ElevatorP, RingP = c.RingP, TerraformP = c.TerraformP, ProjectP = c.ProjectP,
                Seat = c.Seat?.Name, Project = c.Project, VisitorOutcome = c.VisitorOutcome,
                Hubs = c.Hubs.ToDictionary(k => k.Key.Name, k => k.Value), Stations = c.Stations.ToDictionary(k => k.Key.Name, k => k.Value),
                Settled = c.Settled.ToDictionary(k => k.Key.Name, k => k.Value),
                Missions = c.Missions.Select(m => new MissionDto { Target = m.Target.Name, From = m.From?.Name, Kind = (int)m.Kind, T = m.T, Dur = m.Dur }).ToList(),
                Natives = c.Natives.ToDictionary(k => k.Key.Name, k => new NativeDto { Name = k.Value.Name, Contact = k.Value.Contact, Level = k.Value.Level, T = k.Value.T, ClearAt = k.Value.ClearAt, Protected = k.Value.Protected }),
                Frigates = c.FrigateNames.ToList(),
                CrisisId = c.Crisis?.Id, CrisisTarget = c.Crisis?.Target?.Name, CrisisT0 = c.Crisis?.T0 ?? 0, CrisisAcc = c.Crisis?.Acc ?? 0,
                RivalName = c.Rival?.Name, RivalPlanet = c.Rival?.Planet?.Name, RivalStance = c.Rival?.Stance, RivalPop = c.Rival?.Pop ?? 0, RivalT0 = c.Rival?.T0 ?? 0, RivalShips = c.Rival?.Ships ?? 0,
                WorksOverride = c.WorksOverride, OccupiedBy = c.OccupiedBy, BlockadedUntil = c.BlockadedUntil, DefenceLoss = c.DefenceLoss,
                Grievance = c.Grievance, Unrest = c.Unrest, Cause = c.Cause, CauseKey = c.CauseKey, Movement = c.Movement, Autonomy = c.Autonomy, InvestedUntil = c.InvestedUntil,
                WorldGrievance = c.WorldGrievance.Count == 0 ? null : c.WorldGrievance.ToDictionary(k => k.Key.Name, k => k.Value),
                WorldUnrest = c.WorldUnrest.Count == 0 ? null : c.WorldUnrest.ToDictionary(k => k.Key.Name, k => k.Value),
                HasSplinter = c.Splinter.HasValue, SplFaction = c.Splinter?.Faction, SplPop = c.Splinter?.Pop ?? 0, SplAt = c.Splinter?.At ?? 0, SplBack = c.Splinter?.Back ?? 0, SplReturned = c.Splinter?.Returned ?? false,
            }).ToList(),
            Ships = sim.Ships.Select(s => new ShipDto { Civ = s.Civ.Id, Origin = s.Origin.Id, Target = s.Target.Id, FromStar = s.From?.Star.Id ?? -1, Depart = s.Depart, Arrive = s.Arrive, Name = s.Name }).ToList(),
            Log = sim.Chronicle.Skip(Math.Max(0, sim.Chronicle.Count - 800)).Select(e => new LogDto { Year = e.Year, Civ = e.Civ.Id, Star = e.StarId, System = e.System, Text = e.Text, Kind = e.Kind }).ToList(),
            Timeline = sim.Timeline.Select(t => new[] { t.Year, t.K, t.Pop, t.Systems }).ToList(),
            Mined = sim.Systems.OrderBy(kv => kv.Key.Id).Where(kv => kv.Value.All().Any(b => b.Mined > 0)).ToDictionary(kv => kv.Key.Id, kv => kv.Value.All().Where(b => b.Mined > 0).ToDictionary(b => b.Name, b => b.Mined)),
            Stars = sim.Edited.OrderBy(st => st.Id).Select(st => new StarDto { Id = st.Id, Seed = st.Seed, X = st.Pos.X, Y = st.Pos.Y, Z = st.Pos.Z, Lum = st.Lum, Temp = st.Temp, Dead = st.Dead, Kindled = st.Kindled, Name = st.Name, Cls = st.Cls, Special = st.Special }).ToList(),
            Forged = sim.Forged.ToDictionary(kv => kv.Key, kv => kv.Value.Select(f => new[] { f.Kind, f.Orbit, f.Radius, f.Seed }).ToList()),
            ForgedNames = sim.Forged.ToDictionary(kv => kv.Key, kv => kv.Value.Select(f => f.Name).ToList()),
            Lanes = sim.Lanes.Select(l => new[] { l.A.Id, l.B.Id }).ToList(),
            Quarantines = sim.Quarantines.Select(q => new[] { q.Center.X, q.Center.Y, q.Center.Z, q.Radius }).ToList(),
            Laws = sim.Laws.ToList(), Revealed = sim.Revealed.ToList(), GateSealYear = sim.GateSealYear,
            Worlds = sim.Systems.OrderBy(kv => kv.Key.Id).Where(kv => kv.Value.All().Any(b => b.Greening > 0 || b.Ignited || b.Orbit0 >= 0)).ToDictionary(kv => kv.Key.Id,
                kv => kv.Value.All().Where(b => b.Greening > 0 || b.Ignited || b.Orbit0 >= 0).ToDictionary(b => b.Name, b => new[] { b.Greening, b.Ignited ? 1f : 0f, b.Orbit, b.Period, b.Orbit0 })),
        };
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        f.StoreString(JsonSerializer.Serialize(d, Opt));
    }

    public static (Simulation Sim, int Seed) Load(string path)
    {
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        var d = JsonSerializer.Deserialize<SaveDto>(f.GetAsText(), Opt);
        float rescale = d.Version < 2 ? StarSystem.AU / 70f : 1f;        // version 1 saved orbits at the old compressed scale (Earth's orbit = 70)
        var gal = new Galaxy(d.Seed);
        gal.PlaceSolAndNeighbours();
        // StarCount includes the stars kindled since the galaxy was made: regenerate only the original ones (an extra
        // generated star would shift where the Shard, the Vault and the Gates are placed), and the kindled are added back below
        gal.GenerateStars(d.StarCount - (d.Stars?.Count(x => x.Kindled) ?? 0));
        var sim = Simulation.Blank(gal, d.Seed);
        sim.Year = d.Year; sim.AnsweredAt = d.AnsweredAt; sim.ArkYear = d.ArkYear; sim.TypeIIIAnnounced = d.TypeIII;
        // stars the voice changed: kindled ones are new, the rest are edits of generated stars
        foreach (var sd in (d.Stars ?? new()).OrderBy(x => x.Id))
        {
            StarInfo st;
            if (sd.Kindled && sd.Id >= gal.Stars.Count) { st = new StarInfo { Id = gal.Stars.Count }; gal.Stars.Add(st); }
            else if (sd.Id < gal.Stars.Count) st = gal.Stars[sd.Id];
            else continue;
            st.Seed = sd.Seed; st.Pos = new Vector3(sd.X, sd.Y, sd.Z); st.Lum = sd.Lum; st.Temp = sd.Temp; st.Dead = sd.Dead; st.Kindled = sd.Kindled;
            st.Name = sd.Name; st.Cls = sd.Cls; st.Special = sd.Special;
            gal.Relink(st);
            sim.Edited.Add(st);
        }
        foreach (var (starId, list) in d.Forged ?? new())
        {
            var sys = sim.SystemFor(gal.Stars[starId]);
            for (int i = 0; i < list.Count; i++)
            {
                var fw = list[i];
                fw[1] *= rescale;
                var b = StarSystem.Forge(sys, (BodyKind)(int)fw[0], fw[1], new Random((int)fw[3]));
                if (d.ForgedNames != null && d.ForgedNames.TryGetValue(starId, out var names) && i < names.Count) b.Name = names[i];
                if (!sim.Forged.TryGetValue(starId, out var fl)) sim.Forged[starId] = fl = new();
                fl.Add((b.Name, (int)fw[0], fw[1], fw[2], (int)fw[3]));
            }
        }
        foreach (var l in d.Lanes ?? new()) { var a = gal.Stars[l[0]]; var b = gal.Stars[l[1]]; sim.Lanes.Add((a, b)); if (!a.Near.Contains(b.Id)) a.Near.Insert(0, b.Id); if (!b.Near.Contains(a.Id)) b.Near.Insert(0, a.Id); }
        foreach (var q in d.Quarantines ?? new()) sim.Quarantines.Add((new Vector3(q[0], q[1], q[2]), q[3]));
        foreach (var l in d.Laws ?? new()) sim.Laws.Add(l);
        foreach (var r in d.Revealed ?? new()) sim.Revealed.Add(r);
        sim.GateSealYear = d.GateSealYear;
        foreach (var cd in d.Civs.OrderBy(c => c.Id))
        {
            var c = new Civ(cd.Id, cd.Id * 977 + d.Seed)
            {
                Name = cd.Name, Species = cd.Species, Drive = cd.Drive, Ethic = cd.Ethic, Age = cd.Age,
                Color = new Color(cd.Color[0], cd.Color[1], cd.Color[2]), Human = cd.Human, Alien = cd.Alien, Main = cd.Main, Dead = cd.Dead,
                Conscience = cd.Conscience, Vigour = cd.Vigour, Faith = cd.Faith, Dread = cd.Dread, FaithStage = cd.FaithStage, Digital = cd.Digital, Founded = cd.Founded, NextElection = cd.NextElection, Gen = cd.Gen,
            };
            c.Inf = new(cd.Inf);
            c.W = cd.W.ToDictionary(k => k.Key[0], k => k.Value);
            foreach (var t in cd.Techs) if (Defs.TryTech(t) != null) c.Techs.Add(t);
            foreach (var t in Defs.Techs) if (t.Start) c.Techs.Add(t.Id);          // older saves gain the tier-0 baseline
            c.Used.AddRange(cd.Used);
            if (cd.Slots != null)
            {
                foreach (var (area, sd) in cd.Slots)
                    if (c.Slots.TryGetValue(area, out var sl)) { sl.Id = Defs.TryTech(sd.Id)?.Id; sl.Pinned = sd.Pinned; sl.P = sd.P; sl.Hand = sd.Hand ?? new(); sl.LastHand = sd.LastHand ?? new(); }
            }
            else if (cd.ResearchId != null && Defs.TryTech(cd.ResearchId) is { } old) { var sl = c.Slots[Simulation.AreaOf(old.B)]; sl.Id = old.Id; sl.P = cd.ResearchP; }
            c.RpBank = cd.RpBank;
            static void Fill<TK, TV>(Dictionary<TK, TV> to, Dictionary<TK, TV> from) { if (from != null) foreach (var kv in from) to[kv.Key] = kv.Value; }
            Fill(c.Pity, cd.Pity); Fill(c.ExclusivePick, cd.ExclusivePick); Fill(c.Banned, cd.Banned); Fill(c.Pools, cd.Pools); Fill(c.Meters, cd.Meters);
            Fill(c.CrisisAt, cd.CrisisAt); Fill(c.GateP, cd.GateP); Fill(c.ActAt, cd.ActAt); Fill(c.Gates, cd.Gates);
            c.Path = cd.Path; c.Exalted = cd.Exalted; c.Fallen = cd.Fallen; c.Custodian = cd.Custodian; c.Imperial = cd.Imperial; c.Strain = cd.Strain;
            c.NextApoRoll = cd.NextApoRoll; c.NextExaltedAct = cd.NextExaltedAct; c.WyrmUntil = cd.WyrmUntil; c.OvermindUntil = cd.OvermindUntil;
            c.Policy = cd.Policy ?? new Policy();                                   // pending proposals are not saved: the council raises them again
            int areas = Enum.GetValues<Area>().Length;
            if (c.Policy.Manual.Length < areas) Array.Resize(ref c.Policy.Manual, areas);   // saved before newer areas existed
            c.Culture = cd.Culture is { Length: 3 } cu ? new Culture { Martial = cu[0], Duty = cu[1], Resolve = cu[2] } : Cultures.Lore(c.Human ? "Human" : c.Species);
            c.MandatoryService = cd.MandatoryService ?? c.Culture.Duty > 0.5f;
            c.Mobilisation = cd.Mobilisation; c.MilShare = cd.MilShare ?? Simulation.Level(0).GetProperty("military").GetDouble();
            c.MilFrom = cd.MilFrom ?? c.MilShare; c.MilSince = cd.MilSince; c.Efficiency = cd.Efficiency ?? 1;
            if (cd.Truce != null) foreach (var kv in cd.Truce) c.Truce[kv.Key] = kv.Value;
            c.TributeTo = cd.TributeTo; c.TributeUntil = cd.TributeUntil; c.JuntaUntil = cd.JuntaUntil;
            c.Materiel = cd.Materiel; c.Manpower = cd.Manpower; c.WarStrain = cd.WarStrain;
            if (cd.Yard != null) c.Yard.AddRange(cd.Yard);
            if (cd.Barracks != null) c.Barracks.AddRange(cd.Barracks);
            c.NextYearly = cd.NextYearly; c.LastYearly = cd.LastYearly > 0 ? cd.LastYearly : d.Year; c.VoiceRelation = cd.VoiceRelation; c.Federation = cd.Federation;
            c.ComputeMods();
            if (cd.Leader != null) c.Leader = new Leader { Name = cd.Leader.Name, Drive = cd.Leader.Drive, Since = cd.Leader.Since, Born = cd.Leader.Born, Eternal = cd.Leader.Eternal };
            foreach (var kv in cd.Relations) c.Relations[kv.Key] = kv.Value;
            foreach (var m in cd.Met) c.Met.Add(m);
            foreach (var m in cd.AtWar) c.AtWar.Add(m);
            sim.Civs.Add(c);
            if (c.Main) sim.Humanity = c;
        }
        foreach (var (starId, bodies) in d.Worlds ?? new())
        {
            var sys = sim.SystemFor(gal.Stars[starId]);
            foreach (var (name, v) in bodies)
            {
                var b = sys.Find(name); if (b == null) continue;
                b.Greening = v[0]; b.Ignited = v[1] > 0; if (v[0] >= 1) b.Life = true;
                if (v.Length > 4 && v[4] >= 0) { b.Orbit = v[2] * rescale; b.Period = v[3]; b.Orbit0 = v[4] * rescale; }
                if (v[0] > 0 && v[0] < 1) sim.Thawing.Add(b);
            }
        }
        foreach (var (starId, bodies) in d.Mined)
        {
            var sys = sim.SystemFor(gal.Stars[starId]);
            foreach (var (name, m) in bodies) { var b = sys.Find(name); if (b != null) b.Mined = m; }
        }
        foreach (var cd in d.Colonies)
        {
            var star = gal.Stars[cd.Star];
            var sys = sim.SystemFor(star);
            var civ = sim.Civs[cd.Civ];
            var c = new Colony(sim, civ, sys, cd.Home, cd.Founded)
            {
                Complete = cd.Complete, ProjectDone = cd.ProjectDone, VisitorDone = cd.VisitorDone,
                Region = cd.Region, PlanetPower = cd.PlanetPower, PowerCap = cd.PowerCap, Capture = cd.Capture, Stock = cd.Stock, Pop = cd.Pop, PopCap = cd.PopCap, PeakPop = cd.PeakPop,
                NextDecision = cd.NextDecision, NextEvent = cd.NextEvent, NextSeed = cd.NextSeed, VisitorT = cd.VisitorT, HomeDark = cd.HomeDark,
                Foundries = cd.Foundries, Habitats = cd.Habitats, Era = cd.Era, SeedsSent = cd.SeedsSent, VisitorStage = cd.VisitorStage, Gen = cd.Gen,
                Boost = cd.Boost, ElevatorP = cd.ElevatorP, RingP = cd.RingP, TerraformP = cd.TerraformP, ProjectP = cd.ProjectP,
                Seat = sys.Find(cd.Seat ?? "") ?? sys.Bodies.FirstOrDefault(), Project = cd.Project, VisitorOutcome = cd.VisitorOutcome,
            };
            foreach (var (n, v) in cd.Hubs) if (sys.Find(n) is { } b) c.Hubs[b] = v;
            foreach (var (n, v) in cd.Stations) if (sys.Find(n) is { } b) c.Stations[b] = v;
            foreach (var (n, v) in cd.Settled) if (sys.Find(n) is { } b) c.Settled[b] = v;
            foreach (var m in cd.Missions) if (sys.Find(m.Target) is { } b) c.Missions.Add(new Mission { Target = b, From = m.From == null ? null : sys.Find(m.From), Kind = (MissionKind)m.Kind, T = m.T, Dur = m.Dur });
            foreach (var (n, nd) in cd.Natives) if (sys.Find(n) is { } b) c.Natives[b] = new Native { Name = nd.Name, Contact = nd.Contact, Level = nd.Level, T = nd.T, ClearAt = nd.ClearAt, Protected = nd.Protected };
            c.FrigateNames.AddRange(cd.Frigates);
            foreach (var r in cd.Raised ?? new()) c.Raised.Add(r);
            foreach (var (k, v) in cd.Built ?? new()) c.Built[k] = v;
            c.LegacyBuilt = cd.Built == null;
            if (cd.CrisisId != null) c.Crisis = new Crisis { Id = cd.CrisisId, T0 = cd.CrisisT0, Acc = cd.CrisisAcc, Target = cd.CrisisTarget == null ? null : sys.Find(cd.CrisisTarget) };
            if (cd.RivalName != null) c.Rival = new Rival { Name = cd.RivalName, Pop = cd.RivalPop, Ships = cd.RivalShips, Planet = sys.Find(cd.RivalPlanet ?? ""), T0 = cd.RivalT0, Stance = cd.RivalStance };
            c.WorksOverride = cd.WorksOverride; c.OccupiedBy = cd.OccupiedBy; c.BlockadedUntil = cd.BlockadedUntil; c.DefenceLoss = cd.DefenceLoss;
            c.Grievance = cd.Grievance; c.Unrest = cd.Unrest; c.Cause = cd.Cause; c.CauseKey = cd.CauseKey; c.Movement = cd.Movement; c.Autonomy = cd.Autonomy; c.InvestedUntil = cd.InvestedUntil;
            foreach (var (n, v) in cd.WorldGrievance ?? new()) if (sys.Find(n) is { } b) c.WorldGrievance[b] = v;
            foreach (var (n, v) in cd.WorldUnrest ?? new()) if (sys.Find(n) is { } b) c.WorldUnrest[b] = v;
            if (cd.HasSplinter) c.Splinter = (cd.SplFaction, cd.SplPop, cd.SplAt, cd.SplBack, cd.SplReturned);
            star.Capture = (float)c.Capture;
            sim.Colonies.Add(c);
            civ.Colonies.Add(c);
            if (c.Home && civ.Main) sim.HomeColony = c;
        }
        foreach (var sd in d.Ships)
        {
            gal.Stars[sd.Target].Targeted = true;
            sim.Ships.Add(new SeedShip { Civ = sim.Civs[sd.Civ], Origin = gal.Stars[sd.Origin], Target = gal.Stars[sd.Target], From = sim.Colonies.FirstOrDefault(c => c.Star.Id == sd.FromStar), Depart = sd.Depart, Arrive = sd.Arrive, Name = sd.Name });
        }
        foreach (var e in d.Log)
            sim.Chronicle.Add(new LogEntry { Year = e.Year, Civ = sim.Civs[e.Civ], Colony = sim.Colonies.FirstOrDefault(c => c.Star.Id == e.Star), System = e.System, Text = e.Text, Kind = e.Kind, StarId = e.Star });
        if (d.Timeline != null) foreach (var t in d.Timeline) sim.Timeline.Add((t[0], t[1], t[2], (int)t[3]));
        foreach (var cd in d.Civs) if (cd.Overlord >= 0 && cd.Overlord < sim.Civs.Count) sim.Civs[cd.Id].Overlord = sim.Civs[cd.Overlord];
        foreach (var c in sim.Civs) sim.RefreshFed(c);
        // a player who leads a splinter has no home system: their first system stands in for it
        sim.HomeColony ??= sim.Humanity?.Colonies.FirstOrDefault(x => x.Home) ?? sim.Humanity?.Colonies.FirstOrDefault();
        sim.Focus = sim.HomeColony;
        if (d.Fleets != null) sim.Fleets.AddRange(d.Fleets);
        if (d.Wars != null) sim.Wars.AddRange(d.Wars);
        sim.NextWarId = Math.Max(d.NextWarId, sim.Wars.Select(x => x.Id + 1).DefaultIfEmpty(1).Max());
        if (d.Armies != null) sim.Armies.AddRange(d.Armies);
        sim.NextFleetId = Math.Max(d.NextFleetId, sim.Fleets.Select(x => x.Id + 1).DefaultIfEmpty(1).Max());
        sim.NextArmyId = Math.Max(d.NextArmyId, sim.Armies.Select(x => x.Id + 1).DefaultIfEmpty(1).Max());
        return (sim, d.Seed);
    }
}
