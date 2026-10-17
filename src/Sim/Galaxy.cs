using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Starforge.Sim;

/// <summary>A real, simulated star the civilizations can reach. The visual galaxy has far more points than these.</summary>
public class StarInfo
{
    public int Id;
    public Vector3 Pos;          // galaxy units (disk radius = Galaxy.Radius)
    public float Temp;           // kelvin
    public float Lum;            // solar luminosities
    public string Cls;           // spectral class, e.g. "G2V"
    public string Name;
    public int Seed;
    public float Capture;        // fraction of output captured by a swarm (0..1)
    public int Owner = -1;       // lineage index, -1 = nobody
    public bool Visited;
    public bool Targeted;        // a seed ship is already on its way
    public List<int> Near = new();
    public bool Dead;            // put out by the voice (or something else)
    public bool Kindled;         // lit by the voice after the galaxy was made
    public string Special;       // "vault", "shard", "gate": the exotic layer's places (see GateEvents)
}

/// <summary>
/// A barred spiral galaxy. Shared shape model for the visual point cloud and the sim stars,
/// so the reachable stars sit on the arms you can see.
/// </summary>
public class Galaxy
{
    public const float Radius = 1000f;
    public int Seed;
    public int Arms;
    public float Pitch;          // radians
    public float BarAngle, BarLength;
    public float Twist;          // arm phase offset
    public List<StarInfo> Stars = new();

    public Galaxy(int seed)
    {
        Seed = seed;
        var r = new Random(seed);
        Arms = r.NextDouble() < 0.6 ? 2 : 4;
        Pitch = Mathf.DegToRad(Arms == 2 ? 11f + (float)r.NextDouble() * 5f : 14f + (float)r.NextDouble() * 6f);
        BarAngle = (float)r.NextDouble() * Mathf.Tau;
        BarLength = Radius * (0.12f + (float)r.NextDouble() * 0.1f);
        Twist = BarAngle;
    }

    /// <summary>Angle of arm k's spine at radius rad (logarithmic spiral starting at the bar end).</summary>
    public float ArmAngle(int k, float rad)
    {
        float r0 = Mathf.Max(BarLength, 1f);
        return Twist + k * Mathf.Tau / Arms + Mathf.Log(Mathf.Max(rad, r0) / r0) / Mathf.Tan(Pitch);
    }

    public static float Gauss(Random r)
    {
        double u1 = 1.0 - r.NextDouble(), u2 = r.NextDouble();
        return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(Math.Tau * u2));
    }

    /// <summary>Kind of population a sample belongs to; drives colour, brightness and thickness.</summary>
    public enum Pop { Bulge, Bar, Arm, Disk, Halo }

    /// <summary>Sample a point of the galaxy. armDist = 0 on an arm spine, larger between arms.</summary>
    public Vector3 Sample(Random r, out Pop pop, out float armDist)
    {
        double roll = r.NextDouble();
        armDist = 1f;
        if (roll < 0.13)
        {
            pop = Pop.Bulge;
            var v = new Vector3(Gauss(r), Gauss(r) * 0.75f, Gauss(r)) * Radius * 0.075f;
            float s = 1f + Mathf.Abs(Gauss(r)) * 0.6f;     // a bit of a cuspy tail
            return v * s;
        }
        if (roll < 0.19)
        {
            pop = Pop.Bar;
            float along = Gauss(r) * BarLength * 0.55f;
            var v = new Vector3(along, Gauss(r) * Radius * 0.02f, Gauss(r) * BarLength * 0.16f);
            return v.Rotated(Vector3.Up, BarAngle);
        }
        if (roll < 0.205)
        {
            pop = Pop.Halo;
            var v = new Vector3(Gauss(r), Gauss(r), Gauss(r)).Normalized() * Radius * (0.2f + (float)Math.Pow(r.NextDouble(), 2) * 1.1f);
            return v;
        }
        // disk: exponential radial profile
        float rad;
        do rad = -Mathf.Log(1f - (float)r.NextDouble() * 0.995f) * Radius * 0.33f; while (rad > Radius * 1.15f || rad < BarLength * 0.8f);
        int k = r.Next(Arms);
        // star formation comes in clumps strung along the arms
        bool arm = roll < 0.72 && r.NextDouble() < 0.55 + 0.45 * Mathf.Sin(rad * 0.045f + k * 2.1f + Mathf.Sin(rad * 0.013f) * 2f);
        float theta;
        if (arm)
        {
            pop = Pop.Arm;
            float spread = (0.07f + 0.12f * Mathf.Abs(Gauss(r))) * (0.7f + 0.5f * rad / Radius);
            float off = Gauss(r) * spread;
            armDist = Mathf.Abs(off) / 0.3f;
            theta = ArmAngle(k, rad) + off;
            // feathering spurs
            theta += 0.05f * Mathf.Sin(rad * 0.05f + k * 3f);
        }
        else
        {
            pop = Pop.Disk;
            theta = (float)r.NextDouble() * Mathf.Tau;
            float best = 9f;
            for (int a = 0; a < Arms; a++)
            {
                float d = Mathf.Abs(Mathf.Wrap(theta - ArmAngle(a, rad), -Mathf.Pi, Mathf.Pi));
                best = Mathf.Min(best, d);
            }
            armDist = best / 0.3f;
        }
        float thick = (pop == Pop.Arm ? 0.012f : 0.03f) * Radius * (1f - 0.5f * rad / Radius);
        return new Vector3(Mathf.Cos(theta) * rad, Gauss(r) * thick, Mathf.Sin(theta) * rad);
    }

    static readonly string Syll = "ka|ri|on|ve|sa|tor|al|eth|mi|dra|cy|nos|ul|zen|qua|bel|or|iv|tes|ar|lu|phe|ga|rho|xi|um|cas|dor|ae|myr";

    public static string MakeName(Random r)
    {
        var s = Syll.Split('|');
        int n = 2 + (r.NextDouble() < 0.35 ? 1 : 0);
        string w = "";
        for (int i = 0; i < n; i++) w += s[r.Next(s.Length)];
        w = char.ToUpper(w[0]) + w[1..];
        if (r.NextDouble() < 0.3) w += " " + "ABCDEFGHI"[r.Next(9)];
        return w;
    }

    public const float LyPerUnit = 50f;
    public StarInfo Sol;

    // Real neighbours: name, class, RA, Dec (degrees, J2000), distance (ly), luminosity (solar)
    static readonly (string n, string c, float ra, float dec, float ly, float lum)[] Local =
    {
        ("Alpha Centauri", "G2V", 219.90f, -60.83f, 4.37f, 1.5f), ("Barnard's Star", "M4V", 269.45f, 4.69f, 5.96f, 0.0035f),
        ("Wolf 359", "M6V", 164.12f, 7.01f, 7.86f, 0.0014f), ("Lalande 21185", "M2V", 165.83f, 35.97f, 8.31f, 0.02f),
        ("Sirius", "A1V", 101.29f, -16.72f, 8.6f, 25f), ("Luyten 726-8", "M5V", 24.76f, -17.95f, 8.73f, 0.002f),
        ("Ross 154", "M3V", 282.46f, -23.84f, 9.7f, 0.004f), ("Ross 248", "M6V", 355.48f, 44.18f, 10.3f, 0.0018f),
        ("Epsilon Eridani", "K2V", 53.23f, -9.46f, 10.47f, 0.34f), ("Lacaille 9352", "M0V", 346.47f, -35.85f, 10.72f, 0.033f),
        ("Ross 128", "M4V", 176.94f, 0.80f, 11.0f, 0.0036f), ("Procyon", "F5V", 114.83f, 5.22f, 11.46f, 6.9f),
        ("61 Cygni", "K5V", 316.73f, 38.75f, 11.4f, 0.15f), ("Groombridge 34", "M1V", 4.6f, 44.0f, 11.6f, 0.02f),
        ("Epsilon Indi", "K5V", 330.84f, -56.79f, 11.87f, 0.22f), ("Tau Ceti", "G8V", 26.02f, -15.94f, 11.91f, 0.52f),
        ("Gliese 1061", "M5V", 53.98f, -44.51f, 12.0f, 0.0017f), ("Luyten's Star", "M3V", 111.85f, 5.23f, 12.35f, 0.009f),
        ("Teegarden's Star", "M7V", 43.25f, 16.88f, 12.5f, 0.0007f), ("Kapteyn's Star", "M1V", 77.92f, -45.02f, 12.83f, 0.012f),
        ("Lacaille 8760", "M0V", 319.31f, -38.87f, 12.95f, 0.07f), ("Kruger 60", "M3V", 337.0f, 57.7f, 13.1f, 0.01f),
        ("Altair", "A7V", 297.70f, 8.87f, 16.7f, 10.6f), ("Gliese 581", "M3V", 229.86f, -7.72f, 20.5f, 0.013f),
        ("Gliese 667", "M1V", 259.75f, -34.99f, 23.6f, 0.014f), ("Vega", "A0V", 279.23f, 38.78f, 25.0f, 40f),
        ("Fomalhaut", "A3V", 344.41f, -29.62f, 25.1f, 16.6f), ("Pollux", "K0III", 116.33f, 28.03f, 33.8f, 43f),
        ("Arcturus", "K1III", 213.92f, 19.18f, 36.7f, 170f), ("TRAPPIST-1", "M8V", 346.62f, -5.04f, 40.7f, 0.0005f),
        ("Capella", "G3III", 79.17f, 45.99f, 42.9f, 78f), ("Kepler-442", "K1V", 285.36f, 39.28f, 1206f, 0.12f),
        ("Aldebaran", "K5III", 68.98f, 16.51f, 65.3f, 439f), ("Regulus", "B8IV", 152.09f, 11.97f, 79.3f, 316f),
        ("Betelgeuse", "M2I", 88.79f, 7.41f, 548f, 100000f), ("Rigel", "B8I", 78.63f, -8.2f, 860f, 120000f),
        ("Deneb", "A2I", 310.36f, 45.28f, 2615f, 196000f), ("Kepler-452", "G2V", 296.0f, 44.28f, 1800f, 1.2f),
    };

    /// <summary>FNV-1a: string.GetHashCode is randomised per process, which would reshuffle every named star's planets.</summary>
    public static int StableHash(string s)
    {
        uint h = 2166136261;
        foreach (char ch in s) { h ^= ch; h *= 16777619; }
        return (int)(h & 0x7fffffff);
    }

    static float ClassTemp(string cls) => cls[0] switch
    {
        'O' => 35000f, 'B' => 15000f, 'A' => 9000f, 'F' => 6600f, 'G' => 5700f, 'K' => 4500f, _ => 3200f
    };

    public class Landmark { public string Name, About; public int Kind; public Vector3 Pos; public float Radius; public Color A, B; }
    public List<Landmark> Landmarks = new();

    Vector3 _toGc, _rot;
    /// <summary>A real sky position (J2000 RA/Dec in degrees, distance in light-years) placed relative to Sol.</summary>
    public Vector3 FromSky(float raDeg, float decDeg, float ly)
    {
        double aG = Mathf.DegToRad(192.85948f), dG = Mathf.DegToRad(27.12825f), lN = Mathf.DegToRad(122.93192f);
        double a = Mathf.DegToRad(raDeg), d = Mathf.DegToRad(decDeg);
        double sb = Math.Sin(d) * Math.Sin(dG) + Math.Cos(d) * Math.Cos(dG) * Math.Cos(a - aG);
        double b = Math.Asin(sb);
        double l = lN - Math.Atan2(Math.Cos(d) * Math.Sin(a - aG), Math.Sin(d) * Math.Cos(dG) - Math.Cos(d) * Math.Sin(dG) * Math.Cos(a - aG));
        var dir = _toGc * (float)(Math.Cos(b) * Math.Cos(l)) + _rot * (float)(Math.Cos(b) * Math.Sin(l)) + Vector3.Up * (float)Math.Sin(b);
        return Sol.Pos + dir * (ly / LyPerUnit);
    }

    void PlaceLandmarks()
    {
        Landmarks.Add(new Landmark { Name = "Orion Nebula", Kind = 0, Pos = FromSky(83.82f, -5.39f, 1344f), Radius = 0.7f, A = new Color(1f, 0.25f, 0.4f), B = new Color(0.25f, 0.85f, 0.8f),
            About = "A stellar nursery 1,344 light-years from Earth. Its young stars have blown a glowing cavity into the cloud that made them." });
        Landmarks.Add(new Landmark { Name = "Crab Nebula", Kind = 1, Pos = FromSky(83.63f, 22.01f, 6500f), Radius = 0.35f, A = new Color(1f, 0.35f, 0.25f), B = new Color(0.45f, 0.65f, 1f),
            About = "The wreck of a star that exploded in 1054. At its heart a neutron star spins thirty times a second." });
        Landmarks.Add(new Landmark { Name = "Pillars of Creation", Kind = 2, Pos = FromSky(274.7f, -13.8f, 7000f), Radius = 0.45f, A = new Color(1f, 0.55f, 0.3f), B = new Color(0.3f, 0.75f, 0.7f),
            About = "Columns of gas and dust in the Eagle Nebula, four light-years tall, slowly being eaten by the light of the stars they made." });
    }

    /// <summary>Put Sol on the inner edge of a spur between two arms, then place its real neighbours around it.</summary>
    public void PlaceSolAndNeighbours()
    {
        float rad = Radius * 0.53f;
        float th = ArmAngle(0, rad) + Mathf.Pi / Arms * 0.72f;
        var sol = new Vector3(Mathf.Cos(th) * rad, 1.3f, Mathf.Sin(th) * rad);
        Sol = new StarInfo { Id = 0, Pos = sol, Temp = 5772f, Lum = 1f, Cls = "G2V", Name = "Sol", Seed = 1, Visited = true };
        Stars.Add(Sol);
        var toGc = new Vector3(-sol.X, 0, -sol.Z).Normalized();
        var rot = toGc.Cross(Vector3.Up).Normalized();
        _toGc = toGc; _rot = rot;
        // equatorial -> galactic (J2000)
        double aG = Mathf.DegToRad(192.85948f), dG = Mathf.DegToRad(27.12825f), lN = Mathf.DegToRad(122.93192f);
        foreach (var (n, c, ra, dec, ly, lum) in Local)
        {
            double a = Mathf.DegToRad(ra), d = Mathf.DegToRad(dec);
            double sb = Math.Sin(d) * Math.Sin(dG) + Math.Cos(d) * Math.Cos(dG) * Math.Cos(a - aG);
            double b = Math.Asin(sb);
            double l = lN - Math.Atan2(Math.Cos(d) * Math.Sin(a - aG), Math.Sin(d) * Math.Cos(dG) - Math.Cos(d) * Math.Sin(dG) * Math.Cos(a - aG));
            var dir = toGc * (float)(Math.Cos(b) * Math.Cos(l)) + rot * (float)(Math.Cos(b) * Math.Sin(l)) + Vector3.Up * (float)Math.Sin(b);
            var s = new StarInfo
            {
                Id = Stars.Count, Pos = sol + dir * (ly / LyPerUnit), Cls = c, Name = n, Lum = lum,
                Temp = ClassTemp(c), Seed = StableHash(n)
            };
            Stars.Add(s);
        }
        PlaceLandmarks();
    }

    public void GenerateStars(int count)
    {
        var r = new Random(Seed * 7919 + 13);
        var used = new HashSet<string>();
        foreach (var s in Stars) used.Add(s.Name);
        while (Stars.Count < count)
        {
            var p = Sample(r, out var pop, out _);
            if (pop == Pop.Halo || pop == Pop.Bulge && r.NextDouble() < 0.7) continue;   // few reachable stars in the crowded core
            if (Sol != null && p.DistanceTo(Sol.Pos) < 1.6f) continue;                    // the real neighbourhood is already placed
            var st = new StarInfo { Id = Stars.Count, Pos = p, Seed = r.Next() };
            // spectral class: weighted like the phone game, with some giants
            double c = r.NextDouble();
            (string cls, float t, float l) = c < 0.30 ? ("M", 3200f, 0.04f) : c < 0.55 ? ("K", 4600f, 0.35f) : c < 0.77 ? ("G", 5700f, 1f)
                : c < 0.91 ? ("F", 6700f, 3.2f) : c < 0.975 ? ("A", 9500f, 22f) : ("B", 18000f, 800f);
            int sub = r.Next(10);
            st.Cls = cls + sub + "V";
            st.Temp = t * (1f + (4.5f - sub) * 0.03f);
            st.Lum = l * (0.7f + (float)r.NextDouble() * 0.6f);
            string name;
            do name = MakeName(r); while (!used.Add(name));
            st.Name = name;
            Stars.Add(st);
        }
        // neighbour graph for seed-ship travel
        foreach (var s in Stars)
        {
            var list = new List<(float d, int id)>();
            foreach (var o in Stars) if (o != s) list.Add((o.Pos.DistanceSquaredTo(s.Pos), o.Id));
            list.Sort((a, b) => a.d.CompareTo(b.d));
            for (int i = 0; i < 48 && i < list.Count; i++) s.Near.Add(list[i].id);
        }
        PlaceSpecials();
    }

    /// <summary>
    /// The exotic layer's places, fixed at creation: one Vault, one dormant Shard (a dead star that can be rekindled),
    /// and four sealed Gates on the rim. They exist from the start but stay hidden until someone perceives them.
    /// </summary>
    public void PlaceSpecials()
    {
        var r = new Random(Seed * 31 + 7);
        var sol = Sol?.Pos ?? Vector3.Zero;
        StarInfo Pick(Func<StarInfo, bool> ok) { var c = Stars.Where(s => s.Special == null && s != Sol && ok(s)).ToList(); return c.Count == 0 ? null : c[r.Next(c.Count)]; }
        float Rad(StarInfo s) => new Vector2(s.Pos.X, s.Pos.Z).Length();
        var vault = Pick(s => s.Pos.DistanceTo(sol) > 320 && Rad(s) > 300 && Rad(s) < 700);
        if (vault != null) vault.Special = "vault";
        var shard = Pick(s => s.Pos.DistanceTo(sol) > 180 && s.Pos.DistanceTo(sol) < 420 && s.Lum > 0.5f);
        if (shard != null) { shard.Special = "shard"; shard.Dead = true; shard.Lum = 0.0005f; shard.Temp = 1800f; shard.Name = "the Shard"; }
        for (int q = 0; q < 4; q++)
        {
            float a0 = q * Mathf.Tau / 4, a1 = a0 + Mathf.Tau / 4;
            var g = Pick(s => { float an = Mathf.PosMod(Mathf.Atan2(s.Pos.Z, s.Pos.X), Mathf.Tau); return Rad(s) > Radius * 0.82f && an >= a0 && an < a1; })
                 ?? Pick(s => Rad(s) > Radius * 0.7f);
            if (g != null) { g.Special = "gate"; g.Name = "Gate of " + g.Name; }
        }
    }

    /// <summary>Rebuild one star's neighbour list (and tell its neighbours about it) after it is created or moved.</summary>
    public void Relink(StarInfo s)
    {
        var list = new List<(float d, int id)>();
        foreach (var o in Stars) if (o != s) list.Add((o.Pos.DistanceSquaredTo(s.Pos), o.Id));
        list.Sort((a, b) => a.d.CompareTo(b.d));
        s.Near.Clear();
        for (int i = 0; i < 48 && i < list.Count; i++) s.Near.Add(list[i].id);
        foreach (var (d, id) in list.Take(48))
        {
            var o = Stars[id];
            if (o.Near.Contains(s.Id)) continue;
            int at = o.Near.FindIndex(n => Stars[n].Pos.DistanceSquaredTo(o.Pos) > d);
            if (at < 0) continue;
            o.Near.Insert(at, s.Id);
            if (o.Near.Count > 48) o.Near.RemoveAt(o.Near.Count - 1);
        }
    }

    /// <summary>Approximate black-body colour (normalised) for a temperature in kelvin.</summary>
    public static Color Blackbody(float k)
    {
        float t = k / 100f;
        float r, g, b;
        if (t <= 66) { r = 255; g = 99.47f * Mathf.Log(t) - 161.12f; }
        else { r = 329.7f * Mathf.Pow(t - 60, -0.1332f); g = 288.12f * Mathf.Pow(t - 60, -0.0755f); }
        if (t >= 66) b = 255; else if (t <= 19) b = 0; else b = 138.52f * Mathf.Log(t - 10) - 305.04f;
        var col = new Color(Mathf.Clamp(r, 0, 255) / 255f, Mathf.Clamp(g, 0, 255) / 255f, Mathf.Clamp(b, 0, 255) / 255f);
        float m = Mathf.Max(col.R, Mathf.Max(col.G, col.B));
        return new Color(col.R / m, col.G / m, col.B / m);
    }
}
