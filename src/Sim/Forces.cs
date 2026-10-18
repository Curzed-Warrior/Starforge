using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

/// <summary>A fleet's commander. Skill multiplies the fleet in battle (stage 2); low loyalty is how coups start.</summary>
public class Admiral
{
    public string Name, Trait;
    public float Skill, Loyalty;
    public double Born;
    public int Wins, Losses;
}

/// <summary>Ships of one hull class inside a fleet, counted as a number (late empires field millions).</summary>
public class Squadron
{
    public int Hull;
    public double Ships;
    public float Xp;
    public string Level => Forces.Level(Xp);
}

public class Fleet
{
    public int Id, Civ, At;
    public string Name, Mission = "defend", Stance = "balanced";
    public List<Squadron> Squadrons = new();
    public Admiral Admiral;
    public float Morale = 1, Supply = 1;
    /// <summary>Movement (Field.cs): the star it is flying to (-1 when in port), when it left and arrives, and where its orders send it.</summary>
    public int To = -1, Target = -1;
    public double Depart, Arrive;
    public bool Moving => To >= 0;
    public double Ships => Squadrons.Sum(s => s.Ships);

    /// <summary>New hulls join the squadron of their class; experience averages by ship count.</summary>
    public void Add(int hull, double ships, float xp)
    {
        if (ships <= 0) return;
        var sq = Squadrons.FirstOrDefault(s => s.Hull == hull);
        if (sq == null) { Squadrons.Add(new Squadron { Hull = hull, Ships = ships, Xp = xp }); return; }
        sq.Xp = (float)((sq.Xp * sq.Ships + xp * ships) / (sq.Ships + ships));
        sq.Ships += ships;
    }
}

/// <summary>
/// An elite special-forces team (SpecOps.cs). Quality is fixed by the course it took: full in peacetime, rushed at war.
/// At is the star it is at; To the star it is flying to (-1 when there); Target the star of the colony it works on;
/// Due when its time on site ends.
/// </summary>
public class Team
{
    public int Id, Civ, At, Target = -1, To = -1;
    /// <summary>The civ that held the target when the team was sent: if anyone else holds it by the time it acts, the mission is off.</summary>
    public int TargetCiv = -1;
    public string Name, Mission = "guard";
    public float Quality, Xp;
    public bool Rushed;
    public double Arrive, Due;
}

/// <summary>Ground troops, counted in soldiers. Rushed armies train faster and fight worse.</summary>
public class Army
{
    public int Id, Civ, At;
    public double Troops;
    public float Quality, Xp;
    public bool Rushed;
    /// <summary>The fleet carrying it, or -1 when it is on the ground.</summary>
    public int Fleet = -1;
    /// <summary>Shipping between own systems: where it is bound (-1 when on the ground) and when it lands.</summary>
    public int To = -1;
    public double Arrive;
}

public static class Forces
{
    public static readonly string[] Traits = { "Aggressive", "Cautious", "Tactician", "Logistician", "Raider", "Beloved", "Ambitious", "Butcher" };
    internal static readonly string[] Ordinals = { "First", "Second", "Third", "Fourth", "Fifth", "Sixth", "Seventh", "Eighth" };
    static readonly string[] Missions = { "attack", "attack", "raid" };

    /// <summary>Green, Regular, Veteran or Elite, from experience.levels / experience.at in war.json.</summary>
    public static string Level(float xp)
    {
        var at = WarData.P("experience", "at");
        int i = 0;
        for (int k = 0; k < at.GetArrayLength(); k++) if (xp >= at[k].GetDouble()) i = k;
        return WarData.S("experience", "levels", i.ToString());
    }

    public static string FleetName(int i) => i == 0 ? "Home Fleet" : Ordinals[(i - 1) % Ordinals.Length] + " Fleet";
    public static string FleetMission(int i) => i == 0 ? "defend" : Missions[(i - 1) % Missions.Length];
}

public partial class Simulation
{
    public readonly List<Fleet> Fleets = new();
    public readonly List<Army> Armies = new();
    public readonly List<Team> Teams = new();
    public int NextFleetId = 1, NextArmyId = 1, NextTeamId = 1;

    public IEnumerable<Fleet> FleetsOf(Civ c) => Fleets.Where(f => f.Civ == c.Id);
    public IEnumerable<Army> ArmiesOf(Civ c) => Armies.Where(a => a.Civ == c.Id);
    public IEnumerable<Team> TeamsOf(Civ c) => Teams.Where(t => t.Civ == c.Id);

    // ---------- Hull data (the simulation owns it; View/Ships draws from it) ----------
    /// <summary>
    /// Real hull lengths, nose to tail. Big for the era (a battleship is three kilometres, a worldship a hundred and
    /// fifty) and still nothing beside a world: Earth is twelve thousand kilometres across.
    /// </summary>
    static readonly Dictionary<string, float> LengthsKm = new()
    {
        ["Shuttle"] = 0.04f, ["Corvette"] = 0.12f, ["Frigate"] = 0.3f, ["Destroyer"] = 0.6f, ["Cruiser"] = 1.2f, ["Battlecruiser"] = 2f,
        ["Battleship"] = 3f, ["Carrier"] = 4f, ["Titan"] = 12f, ["Juggernaut"] = 20f, ["Colossus"] = 35f, ["Dreadnought"] = 50f,
        ["Worldship"] = 150f, ["Leviathan"] = 80f, ["GlyphArk"] = 200f, ["Devourer"] = 400f, ["Seraph"] = 8f,
    };
    public static float LengthKm(int cls) => LengthsKm.TryGetValue(Defs.Hulls[cls], out var k) ? k : 0.3f * Defs.HullScale[cls];
    /// <summary>Hull mass: a frigate's, scaled by the cube of length.</summary>
    public static double HullKg(int cls) => WarData.D("economy", "frigateKg") * Math.Pow(LengthKm(cls) / WarData.D("economy", "frigateLengthKm"), 3);
    /// <summary>Years on a slip, from length.</summary>
    public static double BuildYears(int cls) =>
        Math.Clamp(WarData.D("economy", "buildYearsPerKm") * LengthKm(cls), WarData.D("economy", "buildYearsMin"), WarData.D("economy", "buildYearsMax"));

    /// <summary>The council's yardstick for a navy until stage 2 brings real battle strength: tonnes of hull.</summary>
    public double NavyStrength(Civ c) => FleetsOf(c).Sum(f => f.Squadrons.Sum(s => s.Ships * HullKg(s.Hull)));

    /// <summary>
    /// Yearly: one fleet per doubling of colonies (one to six), all based at the home star, each under an admiral;
    /// admirals retire with age and are replaced. A shrinking empire keeps the fleets it already has.
    /// </summary>
    void NavyStep(Civ civ, double dy)
    {
        var home = civ.Colonies.FirstOrDefault(c => c.Home) ?? civ.Colonies.FirstOrDefault();
        if (home == null) return;
        var mine = FleetsOf(civ).ToList();
        int want = Math.Clamp((int)Math.Log2(1 + civ.Colonies.Count), 1, 6);
        for (int i = mine.Count; i < want; i++)
        {
            var f = new Fleet { Id = NextFleetId++, Civ = civ.Id, At = home.Star.Id, Name = Forces.FleetName(i), Mission = Forces.FleetMission(i), Admiral = NewAdmiral(civ) };
            Fleets.Add(f); mine.Add(f);
        }
        double retire = civ.Digital > 0.9 ? 400 : civ.Alien ? 300 : 85;
        foreach (var f in mine)
            if (f.Admiral == null || Year - f.Admiral.Born > retire) f.Admiral = NewAdmiral(civ);
    }

    Admiral NewAdmiral(Civ civ)
    {
        string trait;
        do trait = civ.Choose(Forces.Traits); while (trait == "Butcher" && civ.Ethic == "Reverent");
        return new Admiral
        {
            Name = civ.NewLeader(civ.Drive, Year).Name, Trait = trait,
            Skill = civ.Rnd(0.85f, 1.2f), Loyalty = trait == "Ambitious" ? 0.45f : civ.Rnd(0.65f, 0.95f),
            Born = Year - (civ.Alien ? civ.Rnd(80, 160) : civ.Rnd(40, 60)),
        };
    }

    void DisbandForces(Civ civ)
    {
        Fleets.RemoveAll(f => f.Civ == civ.Id);
        Armies.RemoveAll(a => a.Civ == civ.Id);
        Teams.RemoveAll(t => t.Civ == civ.Id);
        civ.Academy.Clear();
    }
}
