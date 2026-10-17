using System;
using System.Collections.Generic;
using Godot;

namespace Starforge.Sim;

public enum BodyKind { Rocky, Terran, Ocean, Desert, Lava, Ice, Gas, IceGiant, Belt, Moon }

/// <summary>A planet, moon or belt. Orbits are circular; positions are in system units around the star at the origin.</summary>
public class Body
{
    public string Name;
    public int Index;
    public Body Parent;              // null = orbits the star
    public BodyKind Kind;
    public float Radius;             // system units (Earth = 1)
    public float Orbit;              // system units from parent centre
    public float Period;             // years
    public float Phase;              // radians at t = 0
    public float Incl;               // orbital inclination, radians
    public float Tilt;               // axial tilt, radians
    public float Day = 1f;           // rotation period, years (negative = retrograde)
    public string Tex;               // texture key for real bodies, null = procedural
    public Color A = new(0.4f, 0.38f, 0.35f), B = new(0.7f, 0.65f, 0.6f);
    public int ProcSeed;
    public bool Ring; public float RingIn, RingOut; public string RingTex;
    public Color Atmo = new(0, 0, 0);            // rayleigh tint; black = none
    public float AtmoHeight = 0.025f;            // fraction of radius
    public float AtmoDensity = 1f;
    public bool Clouds;
    public float BeltWidth;                      // for belts
    public double Mass;                          // in Earth masses
    public float Mined;                          // 0..1 disassembled
    public bool Life;
    public float Greening;                       // 0..1 life seeded by the unseen voice
    public bool Ignited;                         // a gas giant set burning as a small star
    public float Orbit0 = -1;                    // the orbit it was born on, if the unseen voice has moved it
    public List<Body> Moons = new();

    public Vector3 LocalPos(double t) => LocalPosD(t).F;
    public D3 LocalPosD(double t)
    {
        double a = Phase + Math.Tau * t / Period;
        double x = Math.Cos(a) * Orbit, z = Math.Sin(a) * Orbit;
        if (Incl == 0) return new D3(x, 0, z);
        double c = Math.Cos(Incl), s = Math.Sin(Incl);      // rotated about +X
        return new D3(x, -z * s, z * c);
    }

    public Vector3 WorldPos(double t) => WorldPosD(t).F;
    /// <summary>Where the world is, in double precision (orbits run to hundreds of thousands of units at true scale).</summary>
    public D3 WorldPosD(double t) => Parent == null ? LocalPosD(t) : Parent.WorldPosD(t) + LocalPosD(t);
}

public class StarSystem
{
    /// <summary>
    /// True scale. One system unit is the Earth's radius (6,371 km); stars, worlds, moons and orbits keep their real
    /// proportions, so a 3 km battleship and a 700,000 km sun share one ruler.
    /// </summary>
    public const float AU = 23481f;                 // Earth radii per astronomical unit
    public const float SunR = 109.2f;               // the Sun's radius in Earth radii
    public const float Km = 1f / 6371f;             // one kilometre in system units
    /// <summary>Kepler's third law around a body of `earthMasses`: period in years for an orbit of `a` system units.</summary>
    public static float MoonPeriod(float a, double earthMasses) => (float)Math.Sqrt(Math.Pow(a / AU, 3) / Math.Max(earthMasses * 3.003e-6, 1e-12));

    public StarInfo Star;
    public string Name;
    public float StarRadius;
    public Color StarColor;          // surface colour (normalised)
    public string StarTex;
    public bool Giant;
    /// <summary>A second star in a wide orbit (Alpha Centauri B, Sirius B, random binaries).</summary>
    public class Companion { public string Name; public float Radius, Temp, Orbit, Period, Phase, Incl; public Color Color; public float Bright = 3f;
        public Vector3 Pos(double t) { double a = Phase + Mathf.Tau * t / Period; return new Vector3((float)Math.Cos(a) * Orbit, 0, (float)Math.Sin(a) * Orbit).Rotated(Vector3.Right, Incl); } }
    public Companion Second;
    public List<Body> Bodies = new();          // star-orbiting bodies
    public IEnumerable<Body> All()
    {
        foreach (var b in Bodies) { yield return b; foreach (var m in b.Moons) yield return m; }
    }
    public Body Find(string name)
    {
        foreach (var b in All()) if (b.Name == name) return b;
        return null;
    }

    // ---------- Sol ----------
    const float D2R = Mathf.Pi / 180f;

    public static StarSystem Sol(StarInfo star)
    {
        var s = new StarSystem { Star = star, Name = "Sol", StarRadius = SunR, StarColor = new Color(1f, 0.93f, 0.82f), StarTex = "8k_sun" };
        var rnd = new Random(1969);
        float Ph() => (float)(rnd.NextDouble() * Mathf.Tau);
        Body P(string name, BodyKind k, float r, float au, float period, float tilt, float day, string tex, double mass)
        {
            var b = new Body { Name = name, Kind = k, Radius = r, Orbit = au * AU, Period = period, Phase = Ph(), Tilt = tilt * D2R, Day = day, Tex = tex, Mass = mass, Index = s.Bodies.Count };
            s.Bodies.Add(b);
            return b;
        }
        Body Mn(Body parent, string name, float r, float orbit, float period, string tex, Color a, Color b)
        {
            var m = new Body { Name = name, Kind = BodyKind.Moon, Parent = parent, Radius = r, Orbit = orbit, Period = period, Phase = Ph(), Tex = tex, A = a, B = b, Day = period, Mass = 0.01, ProcSeed = rnd.Next(1000) };
            parent.Moons.Add(m);
            return m;
        }
        const float Day = 1f / 365.25f;
        // radii in Earth radii, orbits in AU (planets) or Earth radii (moons), all as measured
        var mercury = P("Mercury", BodyKind.Rocky, 0.383f, 0.387f, 0.2408f, 0.03f, 58.6f * Day, "8k_mercury", 0.055);
        var venus = P("Venus", BodyKind.Rocky, 0.949f, 0.723f, 0.6152f, 177f, -243f * Day, "4k_venus_atmosphere", 0.815);
        venus.Atmo = new Color(1.0f, 0.85f, 0.55f); venus.AtmoHeight = 0.04f; venus.AtmoDensity = 2.2f;
        var earth = P("Earth", BodyKind.Terran, 1.0f, 1f, 1f, 23.44f, 0.9973f * Day, "8k_earth_daymap", 1.0);
        earth.Atmo = new Color(0.18f, 0.41f, 1.0f); earth.Clouds = true; earth.Life = true;
        Mn(earth, "Moon", 0.2727f, 60.27f, 27.32f * Day, "8k_moon", new(0.5f, 0.5f, 0.5f), new(0.7f, 0.7f, 0.7f));
        var mars = P("Mars", BodyKind.Desert, 0.532f, 1.524f, 1.881f, 25.2f, 1.026f * Day, "8k_mars", 0.107);
        mars.Atmo = new Color(0.9f, 0.6f, 0.45f); mars.AtmoHeight = 0.018f; mars.AtmoDensity = 0.35f;
        Mn(mars, "Phobos", 0.00177f, 1.469f, 0.32f * Day, null, new(0.3f, 0.27f, 0.25f), new(0.45f, 0.4f, 0.36f));
        Mn(mars, "Deimos", 0.00097f, 3.687f, 1.26f * Day, null, new(0.35f, 0.31f, 0.28f), new(0.5f, 0.45f, 0.4f));
        var belt = P("Main Belt", BodyKind.Belt, 0.074f, 2.7f, 4.6f, 0f, 1f, null, 0.0005);      // radius: Ceres
        belt.BeltWidth = 1.0f * AU;
        var jupiter = P("Jupiter", BodyKind.Gas, 10.97f, 5.203f, 11.86f, 3.1f, 0.414f * Day, "8k_jupiter", 317.8);
        jupiter.Atmo = new Color(0.9f, 0.8f, 0.65f); jupiter.AtmoHeight = 0.02f; jupiter.AtmoDensity = 0.5f;
        Mn(jupiter, "Io", 0.286f, 66.2f, 1.769f * Day, null, new(0.75f, 0.65f, 0.25f), new(0.95f, 0.85f, 0.45f));
        Mn(jupiter, "Europa", 0.245f, 105.3f, 3.551f * Day, null, new(0.65f, 0.55f, 0.45f), new(0.92f, 0.88f, 0.82f));
        Mn(jupiter, "Ganymede", 0.413f, 168f, 7.155f * Day, null, new(0.4f, 0.37f, 0.33f), new(0.68f, 0.64f, 0.58f));
        Mn(jupiter, "Callisto", 0.378f, 295.6f, 16.69f * Day, null, new(0.25f, 0.23f, 0.2f), new(0.5f, 0.47f, 0.42f));
        var saturn = P("Saturn", BodyKind.Gas, 9.14f, 9.537f, 29.46f, 26.7f, 0.444f * Day, "8k_saturn", 95.2);
        saturn.Ring = true; saturn.RingIn = 1.24f; saturn.RingOut = 2.27f; saturn.RingTex = "8k_saturn_ring_alpha";
        saturn.Atmo = new Color(0.95f, 0.85f, 0.6f); saturn.AtmoHeight = 0.02f; saturn.AtmoDensity = 0.5f;
        var titan = Mn(saturn, "Titan", 0.404f, 191.9f, 15.95f * Day, null, new(0.7f, 0.5f, 0.2f), new(0.9f, 0.7f, 0.35f));
        titan.Atmo = new Color(1f, 0.6f, 0.2f); titan.AtmoHeight = 0.12f; titan.AtmoDensity = 3f;
        Mn(saturn, "Enceladus", 0.0395f, 37.4f, 1.37f * Day, null, new(0.85f, 0.88f, 0.9f), new(1f, 1f, 1f));
        Mn(saturn, "Rhea", 0.120f, 82.7f, 4.52f * Day, null, new(0.6f, 0.6f, 0.6f), new(0.8f, 0.8f, 0.78f));
        var uranus = P("Uranus", BodyKind.IceGiant, 3.98f, 19.19f, 84f, 97.8f, -0.718f * Day, "2k_uranus", 14.5);
        uranus.Atmo = new Color(0.55f, 0.85f, 0.9f); uranus.AtmoHeight = 0.025f; uranus.AtmoDensity = 0.6f;
        uranus.Ring = true; uranus.RingIn = 1.6f; uranus.RingOut = 2.0f;
        Mn(uranus, "Titania", 0.124f, 68.6f, 8.7f * Day, null, new(0.5f, 0.48f, 0.47f), new(0.7f, 0.68f, 0.66f));
        var neptune = P("Neptune", BodyKind.IceGiant, 3.86f, 30.07f, 164.8f, 28.3f, 0.671f * Day, "2k_neptune", 17.1);
        neptune.Atmo = new Color(0.35f, 0.5f, 1.0f); neptune.AtmoHeight = 0.025f; neptune.AtmoDensity = 0.6f;
        Mn(neptune, "Triton", 0.212f, 55.7f, -5.88f * Day, null, new(0.7f, 0.62f, 0.6f), new(0.9f, 0.85f, 0.85f));
        return s;
    }

    // ---------- Procedural systems (other stars) ----------
    public static StarSystem Generate(StarInfo star)
    {
        var r = new Random(star.Seed);
        float R(float a, float b) => a + (float)r.NextDouble() * (b - a);
        var col = Galaxy.Blackbody(star.Temp);
        // Stefan-Boltzmann: R/Rsun = sqrt(L) / (T/Tsun)^2, at true scale
        float rSolar = Mathf.Sqrt(Mathf.Max(star.Lum, 1e-4f)) / Mathf.Pow(star.Temp / 5772f, 2f);
        float sr = Mathf.Clamp(SunR * rSolar, 8f, 2e5f);
        var s = new StarSystem { Star = star, Name = star.Name, StarRadius = sr, StarColor = col, Giant = rSolar > 8f };
        int n = 3 + r.Next(6);
        float orbit = sr * 6f + 0.2f * AU * Mathf.Sqrt(Mathf.Max(star.Lum, 0.05f));
        string sname = star.Name;
        if (sname == "Alpha Centauri") s.Second = new Companion { Name = "Alpha Centauri B", Radius = sr * 0.86f, Temp = 5260f, Bright = 4f };
        else if (sname == "Sirius") s.Second = new Companion { Name = "Sirius B", Radius = 0.92f, Temp = 25000f, Bright = 9f };
        else if (sname == "Procyon") s.Second = new Companion { Name = "Procyon B", Radius = 1.35f, Temp = 7700f, Bright = 6f };
        else if (sname == "61 Cygni") s.Second = new Companion { Name = "61 Cygni B", Radius = sr * 0.9f, Temp = 3900f, Bright = 2f };
        else if (r.NextDouble() < 0.28) s.Second = new Companion { Name = sname + " B", Radius = Mathf.Max(sr * (0.25f + (float)r.NextDouble() * 0.4f), 8f), Temp = 2900f + (float)r.NextDouble() * 1500f, Bright = 1.6f };
        float hab = AU * Mathf.Sqrt(star.Lum);       // where Earth would sit
        for (int i = 0; i < n; i++)
        {
            float temp = hab / orbit;                 // >1 hot, <1 cold
            BodyKind k;
            double roll = r.NextDouble();
            if (temp > 1.6f) k = roll < 0.5 ? BodyKind.Lava : BodyKind.Rocky;
            else if (temp > 0.75f) k = roll < 0.3 ? BodyKind.Rocky : roll < 0.55 ? BodyKind.Desert : roll < 0.8 ? BodyKind.Ocean : BodyKind.Terran;
            else if (temp > 0.4f) k = roll < 0.45 ? BodyKind.Gas : roll < 0.65 ? BodyKind.Desert : roll < 0.8 ? BodyKind.Belt : BodyKind.Ice;
            else k = roll < 0.4 ? BodyKind.IceGiant : roll < 0.75 ? BodyKind.Gas : BodyKind.Ice;
            var b = new Body { Name = s.Name + " " + "bcdefghijk"[i], Kind = k, Index = i, Orbit = orbit, ProcSeed = r.Next(100000), Phase = R(0, Mathf.Tau) };
            b.Period = Mathf.Pow(orbit / Mathf.Max(hab, 1f), 1.5f) / Mathf.Sqrt(Mathf.Max(star.Lum, 0.05f) / 1f) * 1f;
            b.Period = Mathf.Max(b.Period, 0.05f);
            b.Tilt = R(-0.5f, 0.5f);
            b.Day = R(0.6f, 3f) / 365f;
            Dress(b, k, temp, r);
            // jitter colours so no two worlds match
            b.A = Jit(b.A, r); b.B = Jit(b.B, r);
            if (k is BodyKind.Gas or BodyKind.IceGiant or BodyKind.Rocky or BodyKind.Desert or BodyKind.Terran)
            {
                int nm = k is BodyKind.Gas ? 2 + r.Next(4) : k is BodyKind.IceGiant ? 1 + r.Next(3) : r.Next(3) == 0 ? 1 : 0;
                for (int m = 0; m < nm; m++)
                {
                    var mb = new Body { Name = b.Name + " " + (char)('I' + 0) + new string('I', m), Kind = BodyKind.Moon, Parent = b, Radius = R(0.08f, 0.4f) * (k is BodyKind.Gas ? 1f : 0.6f),
                        Orbit = b.Radius * (k is BodyKind.Gas or BodyKind.IceGiant ? 5f + m * 7f + R(0, 4f) : 20f + m * 25f + R(0, 15f)), Phase = R(0, Mathf.Tau), ProcSeed = r.Next(100000), Mass = 0.01 };
                    mb.Name = b.Name + " " + Roman(m + 1);
                    mb.Period = Mathf.Max(MoonPeriod(mb.Orbit, b.Mass), 0.002f);
                    mb.Day = mb.Period;
                    bool icy = temp < 0.5f;
                    mb.A = icy ? new(0.55f, 0.58f, 0.6f) : new(0.28f, 0.25f, 0.22f);
                    mb.B = icy ? new(0.9f, 0.92f, 0.95f) : new(0.55f, 0.5f, 0.45f);
                    mb.A = Jit(mb.A, r); mb.B = Jit(mb.B, r);
                    b.Moons.Add(mb);
                }
            }
            s.Bodies.Add(b);
            orbit = orbit * R(1.45f, 1.8f) + b.Radius * 3f;
        }
        if (s.Second != null)
        {
            var c = s.Second;
            c.Orbit = orbit * R(1.4f, 2.0f) + 0.9f * AU;
            c.Period = 80f + R(0f, 120f);
            c.Phase = R(0, Mathf.Tau);
            c.Incl = R(-0.3f, 0.3f);
            c.Color = Galaxy.Blackbody(c.Temp);
        }
        return s;
    }

    /// <summary>Size, colours and air for a world of a given kind and warmth (hab/orbit; >1 hot).</summary>
    static void Dress(Body b, BodyKind k, float temp, Random r)
    {
        float R(float a, float c) => a + (float)r.NextDouble() * (c - a);
        switch (k)
        {
            case BodyKind.Lava: b.Radius = R(0.35f, 0.8f); b.A = new(0.09f, 0.06f, 0.05f); b.B = new(0.3f, 0.2f, 0.14f); b.Mass = R(0.1f, 0.6f); break;
            case BodyKind.Rocky: b.Radius = R(0.35f, 0.9f); b.A = new(0.22f, 0.2f, 0.18f); b.B = new(0.5f, 0.45f, 0.4f); b.Mass = R(0.1f, 0.8f); break;
            case BodyKind.Desert: b.Radius = R(0.5f, 1.1f); b.A = new(0.42f, 0.27f, 0.15f); b.B = new(0.78f, 0.58f, 0.36f); b.Mass = R(0.3f, 1.5f);
                b.Atmo = new Color(0.9f, 0.65f, 0.45f); b.AtmoDensity = R(0.08f, 0.3f); b.AtmoHeight = 0.02f; break;
            case BodyKind.Ocean: b.Radius = R(0.9f, 1.5f); b.A = new(0.02f, 0.08f, 0.2f); b.B = new(0.1f, 0.3f, 0.4f); b.Mass = R(1f, 4f);
                b.Atmo = new Color(0.18f, 0.41f, 1f); b.Clouds = true; b.Life = r.NextDouble() < 0.6; break;
            case BodyKind.Terran: b.Radius = R(0.8f, 1.3f); b.A = new(0.04f, 0.12f, 0.25f); b.B = new(0.25f, 0.32f, 0.18f); b.Mass = R(0.6f, 2f);
                b.Atmo = new Color(0.18f, 0.41f, 1f); b.Clouds = true; b.Life = r.NextDouble() < 0.8; break;
            case BodyKind.Ice: b.Radius = R(0.4f, 1f); b.A = new(0.55f, 0.65f, 0.72f); b.B = new(0.9f, 0.95f, 1f); b.Mass = R(0.1f, 1f); break;
            case BodyKind.Gas:
                b.Radius = R(8f, 13f); b.Mass = R(60f, 400f);
                {
                    // palettes: Jovian cream, rust, hot dark, teal ammonia, violet haze, sulphur
                    var pals = new (Color A, Color B)[] { (new(0.45f, 0.32f, 0.2f), new(0.88f, 0.75f, 0.58f)), (new(0.42f, 0.16f, 0.08f), new(0.85f, 0.55f, 0.35f)),
                        (new(0.12f, 0.08f, 0.1f), new(0.45f, 0.25f, 0.22f)), (new(0.2f, 0.4f, 0.42f), new(0.7f, 0.86f, 0.82f)),
                        (new(0.3f, 0.22f, 0.4f), new(0.78f, 0.7f, 0.86f)), (new(0.55f, 0.48f, 0.18f), new(0.92f, 0.88f, 0.6f)) };
                    var pal = temp > 1.4f ? pals[2] : pals[r.Next(pals.Length)];
                    b.A = pal.A; b.B = pal.B;
                }
                b.Atmo = new Color(0.9f, 0.8f, 0.65f); b.AtmoDensity = 0.5f; b.AtmoHeight = 0.02f; b.Ring = r.NextDouble() < 0.45; b.RingIn = R(1.3f, 1.5f); b.RingOut = b.RingIn + R(0.4f, 1.1f); break;
            case BodyKind.IceGiant: b.Radius = R(3.4f, 4.5f); b.A = new(0.3f, 0.5f, 0.62f); b.B = new(0.6f, 0.82f, 0.9f); b.Mass = R(10f, 25f);
                b.Atmo = new Color(0.45f, 0.7f, 1f); b.AtmoDensity = 0.6f; b.AtmoHeight = 0.025f; b.Ring = r.NextDouble() < 0.3; b.RingIn = 1.5f; b.RingOut = 1.9f; break;
            case BodyKind.Belt: b.Radius = 0.074f; b.BeltWidth = R(0.3f, 0.9f) * AU; b.Mass = R(0.0003f, 0.01f); break;
        }
    }

    /// <summary>A world added to a system after the fact (the voice forging one, or a civ building one).</summary>
    public static Body Forge(StarSystem sys, BodyKind k, float orbit, Random r)
    {
        float hab = AU * Mathf.Sqrt(Mathf.Max(sys.Star.Lum, 0.0005f));
        int i = sys.Bodies.Count;
        // a forged world gets a name of its own rather than a catalogue letter
        string name = Galaxy.MakeName(r);
        while (sys.Find(name) != null) name = Galaxy.MakeName(r);
        var b = new Body { Name = name, Kind = k, Index = i, Orbit = orbit, ProcSeed = r.Next(100000), Phase = (float)r.NextDouble() * Mathf.Tau };
        b.Period = Mathf.Max(0.05f, Mathf.Pow(orbit / Mathf.Max(hab, 1f), 1.5f) / Mathf.Sqrt(Mathf.Max(sys.Star.Lum, 0.05f)));
        b.Tilt = (float)(r.NextDouble() - 0.5); b.Day = (0.6f + (float)r.NextDouble() * 2.4f) / 365f;
        Dress(b, k, hab / orbit, r);
        b.A = Jit(b.A, r); b.B = Jit(b.B, r);
        if (k is BodyKind.Terran or BodyKind.Ocean) b.Life = true;
        sys.Bodies.Add(b);
        return b;
    }

    static Color Jit(Color c, Random r) => new(
        Mathf.Clamp(c.R * (0.85f + (float)r.NextDouble() * 0.3f), 0, 1),
        Mathf.Clamp(c.G * (0.85f + (float)r.NextDouble() * 0.3f), 0, 1),
        Mathf.Clamp(c.B * (0.85f + (float)r.NextDouble() * 0.3f), 0, 1));

    public static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", _ => n.ToString() };
}
