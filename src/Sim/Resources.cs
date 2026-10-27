using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

/// <summary>
/// What a star system has to offer, after Stellaris's deposits: its light (energy), the metal in its rocky worlds and
/// belts, the volatiles (water, nitrogen, hydrogen) in its ices and giants, its living worlds, and rarely a strategic
/// deposit that some great works cannot do without. Read from the system's actual worlds (generated from the star's
/// seed, so it is the same every time) and surveyed once per star.
/// </summary>
public class Deposits
{
    public float Energy;              // solar luminosities
    public double Metals;             // Earth masses of workable rock and metal
    public double Volatiles;          // Earth masses of ices and gases
    public int LifeWorlds;
    public readonly List<string> Strategic = new();

    public const string ExoticGases = "Exotic Gases", RareCrystals = "Rare Crystals", DarkMatter = "Dark Matter", Neutronium = "Neutronium";

    static double MetalShare(BodyKind k) => k switch
    {
        BodyKind.Lava => 1.2, BodyKind.Rocky => 1.0, BodyKind.Desert => 0.9, BodyKind.Terran => 0.8, BodyKind.Moon => 0.9,
        BodyKind.Belt => 3.0, BodyKind.Ocean => 0.3, BodyKind.Ice => 0.3, BodyKind.Gas => 0.01, BodyKind.IceGiant => 0.03, _ => 0.5
    };
    static double VolatileShare(BodyKind k) => k switch
    {
        BodyKind.Ice => 0.5, BodyKind.Ocean => 0.3, BodyKind.IceGiant => 0.3, BodyKind.Gas => 0.1, BodyKind.Moon => 0.15, BodyKind.Terran => 0.02, _ => 0
    };

    public static Deposits Survey(StarSystem sys)
    {
        var star = sys.Star;
        var d = new Deposits { Energy = star.Lum };
        foreach (var b in sys.All())
        {
            double m = Math.Max(b.Mass, b.Kind == BodyKind.Belt ? 0.0005 : 0.001);
            d.Metals += m * MetalShare(b.Kind);
            d.Volatiles += m * VolatileShare(b.Kind);
            if (b.Life) d.LifeWorlds++;
        }
        var r = new Random(star.Seed ^ 0x5eed);
        bool giants = sys.Bodies.Any(b => b.Kind is BodyKind.Gas or BodyKind.IceGiant);
        bool rocks = sys.Bodies.Any(b => b.Kind is BodyKind.Rocky or BodyKind.Lava or BodyKind.Belt);
        // helium-3 and deuterium skimmed from giants; crystals grown in hot rock; dark matter clumps; a degenerate companion
        if (giants && r.NextDouble() < (sys.Name == "Sol" ? 1 : 0.3)) d.Strategic.Add(ExoticGases);
        if (rocks && r.NextDouble() < 0.2) d.Strategic.Add(RareCrystals);
        if (r.NextDouble() < (star.Special != null ? 0.6 : 0.05)) d.Strategic.Add(DarkMatter);
        bool dwarfCompanion = sys.Second != null && sys.Second.Temp > 7000 && sys.Second.Radius < 3f;
        if (dwarfCompanion || star.Lum > 50 && r.NextDouble() < 0.4 || r.NextDouble() < 0.03) d.Strategic.Add(Neutronium);
        return d;
    }

    static string Grade(double v, double poor, double rich, double abundant) => v < poor ? "poor" : v < rich ? "moderate" : v < abundant ? "rich" : "abundant";
    public string MetalGrade => Grade(Metals, 0.5, 2.5, 6);
    public string VolatileGrade => Grade(Volatiles, 0.3, 5, 40);
    public bool VolatileRich => Volatiles >= 5;

    /// <summary>How attractive a system is to settle: light, metal, water, life, and anything rare.</summary>
    public double Value => 0.6 * Math.Log10(1 + Energy * 4) + 0.5 * Math.Log10(1 + Metals) + 0.3 * Math.Log10(1 + Volatiles)
                           + 0.8 * Math.Min(LifeWorlds, 2) + 0.7 * Strategic.Count;

    public string Describe => $"Metals {MetalGrade} · volatiles {VolatileGrade}" + (LifeWorlds > 0 ? $" · {LifeWorlds} living world{(LifeWorlds > 1 ? "s" : "")}" : "")
                              + (Strategic.Count > 0 ? "\n" + string.Join(", ", Strategic) : "");
}

public partial class Simulation
{
    readonly Dictionary<int, Deposits> _deposits = new();

    /// <summary>The survey of a star's system (generated, not kept, if nobody has been there).</summary>
    public Deposits DepositsOf(StarInfo s)
    {
        if (_deposits.TryGetValue(s.Id, out var d)) return d;
        var sys = Systems.TryGetValue(s, out var known) ? known : StarSystem.Generate(s);
        return _deposits[s.Id] = Deposits.Survey(sys);
    }
}

public partial class Colony
{
    public Deposits Res => Sim.DepositsOf(Star);

    /// <summary>The strategic deposit a great work needs: without it anywhere in the empire it is built with substitutes, at a quarter the pace.</summary>
    public static string Needs(string id) => id switch
    {
        "gateway" or "relay" or "wormhole" or "umbralfort" => Deposits.DarkMatter,
        "kugel" or "antimatter" => Deposits.ExoticGases,
        "beam" or "shield" or "aegis" or "sanctum" or "suncitadel" or "gatebastion" => Deposits.RareCrystals,
        "forge" or "penrose" or "decompressor" or "birch" or "ringworld" or "alderson" or "starforge" => Deposits.Neutronium,
        _ => null
    };

    /// <summary>How fast a work goes without the strategic deposit it needs (the Economy tab's shortages say it too).</summary>
    public const double WithoutDepositPace = 0.25;

    /// <summary>Material pace for a work here: strategic deposits held empire-wide, and water and air for habitats.</summary>
    double MaterialPace(string id)
    {
        double f = 1;
        if (Needs(id) is string need && !Civ.Strategic.Contains(need)) f *= WithoutDepositPace;
        if (SpecOf(id).Kind == WorkKind.Habitat && !Res.VolatileRich)
            f *= Civ.Levy.TryGetValue(this, out var l) && l.Any(x => x.From.Res.VolatileRich) ? 0.8 : 0.4;   // shipped in, or scraped together
        return f;
    }
}
