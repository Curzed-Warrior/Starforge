using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// The living detail of one world, built only while the camera is close to it: cities and arcologies on the
/// surface, spaceports with rockets climbing to orbit, a sky full of satellites and freighters, shipyards,
/// mines and mass drivers. Everything is derived from the colony's numbers, so nothing is simulated
/// when nobody is looking.
/// </summary>
public partial class CloseUp : Node3D
{
    SystemView _sv;
    BodyView _bv;
    Colony _c;
    string _key = "";
    float _clock;
    Random _r = new(11);

    Node3D _surf, _local;
    ShaderMaterial _towerMat, _orbMat, _ringMat;
    MultiMeshInstance3D _orbiters, _ships, _glows;
    readonly List<Vector3> _pads = new();         // spin-local unit vectors
    readonly List<Node3D> _yards = new();
    readonly List<MeshInstance3D> _yardSparks = new();
    readonly List<(Vector3 At, Vector3 Dir, float Len)> _drivers = new();

    enum FlightKind { Launch, Land, Skim, Depart }
    class Flight { public FlightKind K; public Vector3 Up, East; public float T0, Dur, Alt, Arc; public Color Col; }
    readonly List<Flight> _flights = new();
    const int FlightMax = 400, TrailN = 14, GlowMax = FlightMax * (TrailN + 1) + 200;
    float[] _shipBuf = new float[FlightMax * 12];
    float[] _glowBuf = new float[GlowMax * 20];

    const float Range = 16f;   // in planet radii

    public void Setup(SystemView sv) { _sv = sv; }

    // ---------- choosing a world ----------
    public void Update(Colony c, float dt, Camera3D cam)
    {
        _clock += dt;
        _c = c;
        BodyView best = null;
        float bestK = Range;
        if (c != null && !Works.HiddenParts.Contains("closeup"))
            foreach (var bv in _sv.Bodies)
            {
                if (bv.B.Kind == BodyKind.Belt || bv.B.Mined >= 0.999f) continue;
                float k = cam.GlobalPosition.DistanceTo(bv.TrueView) / bv.B.Radius;
                if (k < bestK && Presence(c, bv.B) > 0) { bestK = k; best = bv; }
            }
        string key = best == null ? "" : $"{best.B.Name}|{Tier(c, best.B)}";
        if (key != _key) { Clear(); _key = key; if (best != null) Build(best, c); }
        if (_bv == null) return;
        Animate(dt, cam, bestK);
    }

    static float Dev(Colony c, Body b)
    {
        float s = c.Settled.TryGetValue(b, out var v) ? v : 0;
        if (c.Home && b == c.Seat) s = 1f + (float)Math.Min(1.5, c.Pop / 20e9);
        return s;
    }
    static float Presence(Colony c, Body b) => Dev(c, b) + c.Hubs.GetValueOrDefault(b) + c.Stations.GetValueOrDefault(b) * 0.2f;
    static int Era(Colony c) => Math.Max(c.Era, 0);
    /// <summary>Rebuild only when the world has visibly changed.</summary>
    static string Tier(Colony c, Body b) => $"{(int)(Dev(c, b) * 6)}|{Era(c)}|{c.Hubs.GetValueOrDefault(b)}|{Math.Min(c.Stations.GetValueOrDefault(b), 8)}|{(c.TerraformP >= 1 ? 1 : 0)}";

    void Clear()
    {
        _bv?.SetSites(null, 0, 0);
        _surf?.QueueFree(); _local?.QueueFree();
        _surf = _local = null; _bv = null;
        _ringMat = null; _pads.Clear(); _yards.Clear(); _yardSparks.Clear(); _drivers.Clear(); _flights.Clear();
    }

    public static Vector3 LatLon(float lat, float lon)
    {
        // matches Godot's SphereMesh UVs, so places line up with the texture
        float u = (lon + 180f) / 360f * Mathf.Tau, la = Mathf.DegToRad(lat);
        return new Vector3(Mathf.Sin(u) * Mathf.Cos(la), Mathf.Sin(la), Mathf.Cos(u) * Mathf.Cos(la));
    }

    static Image _earthSea;
    /// <summary>Is this spot on Earth open water? (the specular map, shrunk once and kept)</summary>
    static bool Sea(Vector3 up)
    {
        if (_earthSea == null)
        {
            var img = SystemView.Tex("8k_earth_specular_map")?.GetImage();
            if (img == null) return false;
            if (img.IsCompressed()) img.Decompress();
            img.Resize(512, 256);
            _earthSea = img;
        }
        float u = Mathf.PosMod(Mathf.Atan2(up.X, up.Z) / Mathf.Tau, 1f);
        float v = 0.5f - Mathf.Asin(Mathf.Clamp(up.Y, -1, 1)) / Mathf.Pi;
        return _earthSea.GetPixel(Mathf.Clamp((int)(u * 512), 0, 511), Mathf.Clamp((int)(v * 256), 0, 255)).R > 0.5f;
    }

    static readonly (float, float)[] Megacities =
    {
        (35.7f, 139.7f), (28.6f, 77.2f), (31.2f, 121.5f), (-23.5f, -46.6f), (19.4f, -99.1f), (30.0f, 31.2f), (19.1f, 72.9f),
        (39.9f, 116.4f), (23.8f, 90.4f), (34.7f, 135.5f), (40.7f, -74.0f), (24.9f, 67.0f), (-34.6f, -58.4f), (41.0f, 29.0f),
        (22.6f, 88.4f), (6.5f, 3.4f), (14.6f, 121.0f), (-22.9f, -43.2f), (34.05f, -118.24f), (55.75f, 37.6f), (48.86f, 2.35f),
        (51.5f, -0.13f), (-6.2f, 106.8f), (-12.05f, -77.04f), (37.57f, 126.98f), (13.75f, 100.5f), (41.88f, -87.63f),
        (-26.2f, 28.05f), (-33.87f, 151.2f), (1.35f, 103.8f), (43.65f, -79.38f), (25.2f, 55.27f), (52.52f, 13.4f), (-37.8f, 144.96f),
    };
    static readonly (float, float)[] Spaceports =
    {
        (28.5f, -80.6f), (26.0f, -97.15f), (5.2f, -52.8f), (45.9f, 63.3f), (19.6f, 110.95f), (13.7f, 80.2f), (30.4f, 131.0f), (0f, -160f), (-2f, 40f),
    };

    static List<(Vector3 Up, float W)> Sites(Body b, float dev01, bool earth, Random rnd)
    {
        var sites = new List<(Vector3 Up, float W)>();
        if (earth) foreach (var (la, lo) in Megacities) sites.Add((LatLon(la, lo), 0.6f + (float)rnd.NextDouble() * 0.6f));
        else
        {
            int n = 3 + (int)(dev01 * 22);
            for (int i = 0; i < n; i++) sites.Add((LatLon(((float)rnd.NextDouble() - 0.5f) * 120f, (float)rnd.NextDouble() * 360f - 180f), 0.3f + (float)rnd.NextDouble() * 0.7f));
        }
        return sites;
    }
    static Random SeedFor(Colony c, Body b) => new(Galaxy.StableHash(b.Name) ^ Era(c) * 7919);
    static bool IsEarth(Colony c, Body b) => c.Home && b == c.Seat && b.Name == "Earth";

    /// <summary>A settlement worth hanging the camera over (spin-local direction), or null if the world has none.</summary>
    public static Vector3? PickSite(Colony c, Body b, Random pick, Basis spin, Vector3 sunDir)
    {
        if (c == null || b.Kind is BodyKind.Gas or BodyKind.IceGiant or BodyKind.Belt || Dev(c, b) <= 0) return null;
        var sites = Sites(b, Mathf.Clamp(Dev(c, b), 0, 1), IsEarth(c, b), SeedFor(c, b));
        // big ones, and usually in daylight; now and then a city at night for its lights
        bool night = pick.Next(4) == 0;
        var ranked = sites.OrderByDescending(s => s.W * 0.5f + (night ? -1 : 1) * (spin * s.Up).Dot(sunDir)).Take(3).ToList();
        return ranked[pick.Next(ranked.Count)].Up;
    }

    const float K = StarSystem.Km;
    /// <summary>Lit floors per system unit: one every four metres.</summary>
    const float Floors = 1f / (0.004f * StarSystem.Km);

    // ---------- building ----------
    void Build(BodyView bv, Colony c)
    {
        _bv = bv;
        var b = bv.B;
        float R = b.Radius;
        float dev = Dev(c, b), dev01 = Mathf.Clamp(dev, 0, 1);
        int era = Era(c), hub = c.Hubs.GetValueOrDefault(b), st = c.Stations.GetValueOrDefault(b);
        bool earth = IsEarth(c, b);
        bool gas = b.Kind is BodyKind.Gas or BodyKind.IceGiant;
        bool open = b.Life || b.Name == "Mars" && c.TerraformP >= 1;     // can people walk outside?
        var rnd = SeedFor(c, b);
        var lightCol = c.Civ.Alien ? c.Civ.Color.Lerp(Colors.White, 0.3f) : new Color(1f, 0.74f, 0.45f);

        _surf = new Node3D(); bv.Spin.AddChild(_surf);
        _local = new Node3D(); bv.AddChild(_local);

        _towerMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/tower.gdshader") };
        _towerMat.SetShaderParameter("light_col", new Vector3(lightCol.R, lightCol.G, lightCol.B));
        _towerMat.SetShaderParameter("floors", Floors);

        // --- surface: cities, domes, arcologies ---
        var towers = new List<(Vector3 Up, float Ang, Vector3 Size, Color Col)>();
        var spires = new List<(Vector3 Up, float Ang, Vector3 Size, Color Col)>();
        var domes = new List<(Vector3 Up, float Size)>();
        var paint = new List<Vector4>();
        if (!gas && dev > 0)
        {
            var sites = Sites(b, dev01, earth, rnd);
            float grow = 1f + era * 0.35f;
            foreach (var (up, w) in sites)
            {
                bool domed = !open && era < 4;
                // a real city's footprint: ten to forty kilometres across its core, growing with the age
                float spread = (8f + 26f * w * dev01) * K * (domed ? 0.8f : 1f) * (1f + era * 0.12f);
                int count = (int)((40 + 260 * w * dev01) * (domed ? 0.5f : 1f) * Mathf.Min(grow, 2.5f));
                var (e1, e2) = Tangents(up);
                for (int i = 0; i < count; i++)
                {
                    // dense, tall core; low sprawl outward
                    float rr = Mathf.Pow((float)rnd.NextDouble(), 1.6f) * spread;
                    float ang = (float)rnd.NextDouble() * Mathf.Tau;
                    var p = (up + (e1 * Mathf.Cos(ang) + e2 * Mathf.Sin(ang)) * rr / R).Normalized();
                    if (earth && Sea(p)) continue;
                    float core = 1f - rr / spread;
                    // towers from eighty metres to a kilometre and more at the core; footprints of fifty to a hundred and thirty
                    float h = (0.08f + 0.9f * core * core * w * (float)rnd.NextDouble()) * K * Mathf.Min(grow, 1.8f);
                    float fw = (0.05f + 0.08f * (float)rnd.NextDouble()) * K * Mathf.Min(grow, 1.6f);
                    towers.Add((p, (float)rnd.NextDouble() * Mathf.Pi, new Vector3(fw, h, fw * (0.6f + (float)rnd.NextDouble() * 0.8f)),
                        new Color((float)rnd.NextDouble(), 0.2f + 0.3f * dev01, h > 0.9f * K ? 1f : 0f, (float)rnd.NextDouble())));
                }
                if (domed)
                    for (int j = 0, nd = 3 + (int)(w * 5); j < nd; j++)
                    {
                        float ra = j == 0 ? 0 : spread * (0.3f + 0.5f * (float)rnd.NextDouble()), aa = (float)rnd.NextDouble() * Mathf.Tau;
                        domes.Add(((up + (e1 * Mathf.Cos(aa) + e2 * Mathf.Sin(aa)) * ra / R).Normalized(), spread * (j == 0 ? 0.45f : 0.18f + 0.15f * (float)rnd.NextDouble())));
                    }
                if (paint.Count < 48) paint.Add(new Vector4(up.X, up.Y, up.Z, spread * (domed ? 1.1f : 1.6f) / R));
                // arcologies: from the Type I era, each great city raises a spire that pierces the weather
                if (era >= 2 && w > 0.5f)
                {
                    int k = 1 + (era >= 4 ? 2 : 0);
                    for (int j = 0; j < k; j++)
                    {
                        var p = (up + (e1 * (j - 1) + e2 * (j % 2)) * spread * 0.35f / R * (j == 0 ? 0 : 1)).Normalized();
                        // arcologies: three to six kilometres tall, piercing the weather
                        float h = (3f + 3f * w) * K * (era >= 4 ? 1.6f : 1f) * (j == 0 ? 1f : 0.6f);
                        spires.Add((p, 0, new Vector3(0.6f * K, h, 0.6f * K), new Color(0.8f, 0.7f, 1f, (float)rnd.NextDouble())));
                    }
                }
            }
            // spaceports: real ones on Earth, the biggest sites elsewhere
            if (earth) foreach (var (la, lo) in Spaceports) _pads.Add(LatLon(la, lo));
            else for (int i = 0; i < Math.Min(sites.Count, 2 + era); i++) _pads.Add(sites[i].Up);
        }
        // mining rigs and mass drivers on worked rocky bodies
        if (!gas && hub > 0)
        {
            int nr = 2 + hub * 2;
            for (int i = 0; i < nr; i++)
            {
                var up = LatLon(((float)rnd.NextDouble() - 0.5f) * 100f, (float)rnd.NextDouble() * 360f - 180f);
                var (e1, e2) = Tangents(up);
                for (int j = 0; j < 14; j++)
                {
                    var p = (up + (e1 * ((float)rnd.NextDouble() - 0.5f) + e2 * ((float)rnd.NextDouble() - 0.5f)) * (40f * K / R)).Normalized();
                    towers.Add((p, (float)rnd.NextDouble() * 3f, new Vector3(0.25f * K, (0.06f + 0.15f * (float)rnd.NextDouble()) * K, 0.4f * K), new Color(0.15f, 0.25f, 1f, (float)rnd.NextDouble())));
                }
                if (i < hub) _drivers.Add((up, e1, 60f * K));
                if (paint.Count < 48) paint.Add(new Vector4(up.X, up.Y, up.Z, -0.045f - 0.02f * hub));
                if (_pads.Count < 3) _pads.Add(up);
            }
        }
        bv.SetSites(paint.ToArray(), paint.Count, open ? 1f : 0f);
        AddInstances(_surf, c.Civ.Alien ? Spire() : Tower(), _towerMat, towers, R);   // other minds build in other shapes
        AddInstances(_surf, Spire(), _towerMat, spires, R);
        if (domes.Count > 0)
        {
            var dm = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(0.6f, 0.8f, 1f, 0.22f), Metallic = 0.9f, Roughness = 0.05f,
                EmissionEnabled = true, Emission = lightCol * 0.25f, RimEnabled = true, Rim = 1f, CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };
            var hemi = new SphereMesh { Radius = 1, Height = 1, IsHemisphere = true, RadialSegments = 24, Rings = 8 };
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = hemi, InstanceCount = domes.Count };
            for (int i = 0; i < domes.Count; i++)
                mm.SetInstanceTransform(i, new Transform3D(Upright(domes[i].Up, 0).Scaled(new Vector3(domes[i].Size, domes[i].Size * 0.6f, domes[i].Size)), domes[i].Up * R * 0.999f));
            _surf.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = dm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
        foreach (var (at, dir, len) in _drivers)
        {
            var rail = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.08f * K, 0.08f * K, len) }, MaterialOverride = Works.HullMat(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            rail.Transform = new Transform3D(Basis.LookingAt(dir, at), at * R * 1.002f + dir * len * 0.5f);
            _surf.AddChild(rail);
        }

        // --- megastructures: elevators, an orbital ring, floating cities on the giants ---
        if (!gas && !earth && era >= 2 && dev >= 0.5f)
        {
            int ne = Math.Min(1 + (era - 2), 3);
            for (int i = 0; i < ne; i++)
            {
                var up = LatLon(0, -180f + 360f * i / ne + (float)rnd.NextDouble() * 30f);
                float top = R * (2.2f + 0.4f * i);
                var tether = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.03f * K, BottomRadius = 0.05f * K, Height = top - R, RadialSegments = 6 }, MaterialOverride = Works.HullMat(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                tether.Transform = new Transform3D(Upright(up, 0), up * (R + (top - R) * 0.5f));
                _surf.AddChild(tether);
                var cw = new MeshInstance3D { Mesh = Works.StationMesh(), MaterialOverride = Works.HullMat(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                cw.Transform = new Transform3D(Upright(up, 0).Scaled(Vector3.One * 8f * K), up * top);
                _surf.AddChild(cw);
                var beacon = Spectacle.Sprite(lightCol, 20f * K, 2f);
                beacon.Position = up * top;
                _surf.AddChild(beacon);
            }
        }
        if (!gas && !earth && era >= 3 && dev >= 0.7f)
        {
            var rm = new ShaderMaterial { Shader = Works.RingShader() };
            rm.SetShaderParameter("progress", 1f);
            _ringMat = rm;
            _local.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = R * 1.105f, OuterRadius = R * 1.105f + 8f * K, Rings = 512, RingSegments = 4 }, MaterialOverride = rm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Basis = new Basis(Vector3.Right, b.Tilt) });
        }
        if (gas && (hub > 0 || dev > 0) && era >= 1)
        {
            // aerostat refineries and floating cities riding the cloud tops
            for (int i = 0, n = 3 + hub * 3 + (int)(dev01 * 14); i < n; i++)
            {
                var up = LatLon(((float)rnd.NextDouble() - 0.5f) * 70f, (float)rnd.NextDouble() * 360f - 180f);
                var plat = new MeshInstance3D { Mesh = Works.StationMesh(), MaterialOverride = Works.HullMat(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                plat.Transform = new Transform3D(Upright(up, (float)rnd.NextDouble() * 3f).Scaled(Vector3.One * (15f + 15f * (float)rnd.NextDouble()) * K), up * R * 1.012f);
                _surf.AddChild(plat);
                var l = Spectacle.Sprite(new Color(1f, 0.6f, 0.3f), 8f * K, 3f);
                l.Position = up * R * 1.016f;
                _surf.AddChild(l);
            }
        }

        // --- orbit: a sky full of satellites, tugs and freighters, all moved by the GPU ---
        int sats = (int)Mathf.Clamp((earth ? 600 : 0) + 2500 * dev01 * (0.4f + era * 0.45f) + st * 150 + hub * 300, 0, 16000);
        if (sats > 0)
        {
            _orbMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/orbiter.gdshader"), RenderPriority = 4 };
            _orbMat.SetShaderParameter("planet_r", R);
            _orbMat.SetShaderParameter("size", 0.03f * K);           // satellites tens of metres across: points of light
            var quad = new QuadMesh { Size = new Vector2(2, 2), Material = _orbMat };
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = quad, InstanceCount = sats };
            var buf = new float[sats * 20];
            // a handful of shells, like the constellations that wrapped Earth in the 2020s
            int shells = 3 + era;
            for (int i = 0; i < sats; i++)
            {
                int o = i * 20;
                buf[o] = 1; buf[o + 5] = 1; buf[o + 10] = 1;
                bool big = rnd.NextDouble() < 0.12;
                int sh = i % shells;
                var shr = new Random(sh * 97 + 13);
                float rad = big ? R * (1.15f + (float)rnd.NextDouble() * 2.2f) : R * (1.03f + 0.025f * sh + (float)rnd.NextDouble() * 0.006f);
                float inc = big ? ((float)rnd.NextDouble() - 0.5f) * 1.2f : (float)(shr.NextDouble() * 1.5);
                // satellites in a shell share planes: trains of lights along the same track
                float node = big ? (float)rnd.NextDouble() * Mathf.Tau : (rnd.Next(18) / 18f) * Mathf.Tau;
                float ph = (float)rnd.NextDouble() * Mathf.Tau * (rnd.NextDouble() < (big ? 0.5 : 0.08) ? -1 : 1);
                var col = big ? (rnd.NextDouble() < 0.5 ? new Color(1f, 0.75f, 0.45f) : new Color(0.6f, 0.8f, 1f)) : new Color(0.85f, 0.9f, 1f);
                buf[o + 12] = col.R; buf[o + 13] = col.G; buf[o + 14] = col.B; buf[o + 15] = big ? 1f : 0.6f;
                buf[o + 16] = rad; buf[o + 17] = inc; buf[o + 18] = node; buf[o + 19] = ph;
            }
            mm.Buffer = buf;
            _orbiters = new MultiMeshInstance3D { Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, CustomAabb = new Aabb(-Vector3.One * R * 5, Vector3.One * R * 10) };
            _local.AddChild(_orbiters);
        }

        // --- shipyards: lattice docks with a hull taking shape inside ---
        int yards = era >= 1 && (dev > 0.4f || hub > 1) ? Math.Min(1 + era / 2 + (earth ? 1 : 0), 4) : 0;
        for (int i = 0; i < yards; i++)
        {
            var y = BuildYard(R, rnd);
            _local.AddChild(y);
            _yards.Add(y);
        }

        // --- flights: ships and glows animated each frame ---
        var shipMesh = Works.ShipHullMesh();
        var smm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = shipMesh, InstanceCount = FlightMax, VisibleInstanceCount = 0 };
        _ships = new MultiMeshInstance3D { Multimesh = smm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, CustomAabb = new Aabb(-Vector3.One * R * 10, Vector3.One * R * 20) };
        _local.AddChild(_ships);
        var quadG = new QuadMesh { Size = new Vector2(2, 2), Material = Works.GlowMat() };
        var gmm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = quadG, InstanceCount = GlowMax, VisibleInstanceCount = 0 };
        _glows = new MultiMeshInstance3D { Multimesh = gmm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, CustomAabb = new Aabb(-Vector3.One * R * 10, Vector3.One * R * 20) };
        _local.AddChild(_glows);
    }

    static (Vector3, Vector3) Tangents(Vector3 up)
    {
        var e1 = up.Cross(Mathf.Abs(up.Y) > 0.95f ? Vector3.Right : Vector3.Up).Normalized();
        return (e1, up.Cross(e1).Normalized());
    }
    static Basis Upright(Vector3 up, float spin)
    {
        var (e1, e2) = Tangents(up);
        return new Basis(e1, up, e2) * new Basis(Vector3.Up, spin);
    }

    static Mesh _tower, _spire;
    static Mesh Tower() => _tower ??= new BoxMesh { Size = Vector3.One };
    static Mesh Spire()
    {
        if (_spire != null) return _spire;
        var st = new SurfaceTool();
        st.AppendFrom(new CylinderMesh { TopRadius = 0.15f, BottomRadius = 1f, Height = 1f, RadialSegments = 8, Rings = 1 }, 0, new Transform3D(Basis.Identity, new Vector3(0, 0.5f, 0)));
        // buttresses flare out at the base
        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.Tau / 3;
            st.AppendFrom(new BoxMesh { Size = new Vector3(0.25f, 0.35f, 2.4f) }, 0, new Transform3D(new Basis(Vector3.Up, a), new Vector3(0, 0.17f, 0)));
        }
        return _spire = st.Commit();
    }

    void AddInstances(Node3D parent, Mesh mesh, Material mat, List<(Vector3 Up, float Ang, Vector3 Size, Color Col)> list, float R)
    {
        if (list.Count == 0) return;
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = mesh, InstanceCount = list.Count };
        var buf = new float[list.Count * 16];
        for (int i = 0; i < list.Count; i++)
        {
            var (up, ang, size, col) = list[i];
            var bs = Upright(up, ang).Scaled(size);
            // box meshes are centred: lift by half the height so they stand on the ground
            var pos = up * (R * 0.998f) + (mesh is BoxMesh ? up * size.Y * 0.5f : Vector3.Zero);
            int o = i * 16;
            buf[o] = bs.X.X; buf[o + 1] = bs.Y.X; buf[o + 2] = bs.Z.X; buf[o + 3] = pos.X;
            buf[o + 4] = bs.X.Y; buf[o + 5] = bs.Y.Y; buf[o + 6] = bs.Z.Y; buf[o + 7] = pos.Y;
            buf[o + 8] = bs.X.Z; buf[o + 9] = bs.Y.Z; buf[o + 10] = bs.Z.Z; buf[o + 11] = pos.Z;
            buf[o + 12] = col.R; buf[o + 13] = col.G; buf[o + 14] = col.B; buf[o + 15] = col.A;
        }
        mm.Buffer = buf;
        parent.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    Node3D BuildYard(float R, Random rnd)
    {
        var y = new Node3D();
        float L = 25f * K, W = 6f * K;              // an orbital yard twenty-five kilometres long
        var mat = Works.HullMat();
        // four long rails and a few hoops make the cradle
        foreach (var (sx, sy) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
            y.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(W * 0.06f, W * 0.06f, L) }, MaterialOverride = mat, Position = new Vector3(sx * W * 0.5f, sy * W * 0.5f, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        for (int i = 0; i < 4; i++)
        {
            float z = -L * 0.5f + L * i / 3f;
            y.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(W, W * 0.05f, W * 0.05f) }, MaterialOverride = mat, Position = new Vector3(0, W * 0.5f, z), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            y.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(W, W * 0.05f, W * 0.05f) }, MaterialOverride = mat, Position = new Vector3(0, -W * 0.5f, z), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
        var hull = Spectacle.SeedLaunch.BuildSeedShip();
        hull.Scale = Vector3.One * L * 0.42f;
        y.AddChild(hull);
        for (int i = 0; i < 5; i++)
        {
            var s = Spectacle.Sprite(new Color(0.7f, 0.85f, 1f), W * 0.12f, 3f);
            s.Position = new Vector3(((float)rnd.NextDouble() - 0.5f) * W * 0.6f, ((float)rnd.NextDouble() - 0.5f) * W * 0.6f, ((float)rnd.NextDouble() - 0.5f) * L * 0.8f);
            y.AddChild(s);
            _yardSparks.Add(s);
        }
        y.SetMeta("r", R * (1.5f + (float)rnd.NextDouble() * 0.9f));
        y.SetMeta("ph", (float)rnd.NextDouble() * Mathf.Tau);
        y.SetMeta("inc", ((float)rnd.NextDouble() - 0.5f) * 0.6f);
        return y;
    }

    // ---------- every frame ----------
    void Animate(float dt, Camera3D cam, float k)
    {
        var b = _bv.B;
        float R = b.Radius;
        var spin = _bv.Spin.Basis.Orthonormalized();
        var axis = spin * Vector3.Up;
        var sunDir = (_sv.TrueStar - _bv.TrueView).Normalized();
        var starSeen = _bv.GlobalPosition + (_sv.TrueStar - _bv.TrueView) * _bv.Squash;      // as the world sees it (see BodyView)
        _towerMat.SetShaderParameter("star_pos", starSeen);
        _towerMat.SetShaderParameter("planet_pos", _bv.GlobalPosition);
        _towerMat.SetShaderParameter("floors", Floors / Mathf.Max(_bv.Squash, 1e-6f));
        _ringMat?.SetShaderParameter("star_pos", starSeen);
        float fade = Mathf.Clamp((Range - k) / 5f, 0, 1);
        if (_orbMat != null)
        {
            _orbMat.SetShaderParameter("time", _clock);
            _orbMat.SetShaderParameter("speed", 0.35f);
            _orbMat.SetShaderParameter("sun_dir", sunDir);
            _orbMat.SetShaderParameter("fade", fade);
        }
        _surf.Visible = k < Range * 0.6f;

        // shipyards ride slow orbits, sparks of welding flicker along the hull
        foreach (var y in _yards)
        {
            float r = (float)y.GetMeta("r"), ph = (float)y.GetMeta("ph") + _clock * 0.06f * Mathf.Pow(R / r, 1.5f), inc = (float)y.GetMeta("inc");
            var p = new Basis(Vector3.Right, inc) * new Vector3(Mathf.Cos(ph) * r, 0, Mathf.Sin(ph) * r);
            var fwd = new Basis(Vector3.Right, inc) * new Vector3(-Mathf.Sin(ph), 0, Mathf.Cos(ph));
            y.Transform = new Transform3D(Basis.LookingAt(fwd, Vector3.Up), p);
        }
        for (int i = 0; i < _yardSparks.Count; i++) _yardSparks[i].Visible = Mathf.Sin(_clock * (7f + i) + i * 3.1f) > 0.6f;

        // flights: how busy is the sky?
        var c = _c;
        float dev01 = Mathf.Clamp(Dev(c, b), 0, 1);
        int era = Era(c);
        bool gas = b.Kind is BodyKind.Gas or BodyKind.IceGiant;
        int want = _pads.Count == 0 && !gas ? 0 : (int)Mathf.Clamp((gas ? 6 * c.Hubs.GetValueOrDefault(b) + 20 * dev01 : 3 + (IsEarth(c, b) ? 14 : 0) + 26 * dev01 * (1 + era * 0.6f)) * (float)((0.5 + c.TrafficLevel) * c.TrafficFactor), 0, FlightMax);
        int guard = 0;
        while (_flights.Count < want && guard++ < 8)
        {
            var f = new Flight { T0 = _clock - (_flights.Count < want / 2 ? (float)_r.NextDouble() * 10f : 0) };
            if (gas)
            {
                // skimmers dip into the cloud tops to scoop hydrogen, then climb away
                f.K = FlightKind.Skim;
                f.Up = new Vector3((float)_r.NextDouble() - 0.5f, ((float)_r.NextDouble() - 0.5f) * 0.6f, (float)_r.NextDouble() - 0.5f).Normalized();
                f.Dur = 8f + (float)_r.NextDouble() * 6f; f.Alt = 1.6f + (float)_r.NextDouble(); f.Col = new Color(0.6f, 0.85f, 1f);
            }
            else
            {
                double roll = _r.NextDouble();
                f.K = roll < 0.45 ? FlightKind.Launch : roll < 0.85 ? FlightKind.Land : FlightKind.Depart;
                var pad = _pads[_r.Next(_pads.Count)];
                // a few kilometres of scatter so a busy port doesn't stack every ship on one pixel
                f.Up = (spin * pad + new Vector3((float)_r.NextDouble() - 0.5f, (float)_r.NextDouble() - 0.5f, (float)_r.NextDouble() - 0.5f) * 0.004f).Normalized();
                f.Dur = (f.K == FlightKind.Depart ? 14f : 7f) + (float)_r.NextDouble() * 5f;
                f.Alt = 1.06f + (float)_r.NextDouble() * 0.2f;
                f.Col = f.K == FlightKind.Depart ? new Color(0.65f, 0.8f, 1f) : new Color(1f, 0.72f, 0.4f);
            }
            var east = axis.Cross(f.Up);
            f.East = (east.LengthSquared() < 1e-4f ? f.Up.Cross(Vector3.Right) : east).Normalized();
            if (_r.NextDouble() < 0.15) f.East = f.East.Rotated(f.Up, ((float)_r.NextDouble() - 0.5f) * 1.6f);   // polar and odd inclinations
            f.Arc = 0.9f + (float)_r.NextDouble() * 0.9f;
            _flights.Add(f);
        }

        int ks = 0, kg = 0;
        var sb = _shipBuf; var gb = _glowBuf;
        float size = 0.03f * K;                     // shuttles sixty metres long (the hull mesh runs two units)
        for (int i = _flights.Count - 1; i >= 0; i--)
        {
            var f = _flights[i];
            float u = (_clock - f.T0) / f.Dur;
            if (u >= 1f) { _flights.RemoveAt(i); continue; }
            if (u < 0f) continue;
            var p = FlightPos(f, u, R);
            var fwd = (FlightPos(f, Mathf.Min(u + 0.004f, 1f), R) - p);
            if (f.K == FlightKind.Land) fwd = -fwd;
            if (fwd.LengthSquared() < 1e-12f) fwd = f.East; else fwd = fwd.Normalized();
            // engines burn hardest near the ground
            float burn = f.K switch { FlightKind.Launch => u < 0.55f ? 1f : 0.3f, FlightKind.Land => u > 0.75f ? 1f : 0.25f, FlightKind.Skim => Mathf.Abs(u - 0.5f) < 0.15f ? 1f : 0.4f, _ => 0.6f };
            float alpha = Mathf.Clamp(Mathf.Min(u, 1f - u) * 12f, 0, 1);
            if (ks < FlightMax)
            {
                var bs = Basis.LookingAt(-fwd, Mathf.Abs(fwd.Dot(f.Up)) > 0.98f ? f.East : f.Up).Scaled(Vector3.One * size);
                PutT(sb, ks * 12, bs, p);
                ks++;
            }
            // the exhaust: a glowing plume behind it, then a short trail of embers
            for (int j = 0; j <= TrailN && kg < GlowMax; j++)
            {
                float uj = f.K == FlightKind.Land ? u + j * 0.004f : u - j * 0.004f;
                if (uj < 0 || uj > 1) break;
                var pj = j == 0 ? p - fwd * size * 1.2f : FlightPos(f, uj, R);
                float a = (j == 0 ? 1f : 0.5f * (1f - j / (float)(TrailN + 1))) * burn * alpha;
                PutGlow(gb, kg++, pj, size * (j == 0 ? 0.9f + 1.6f * burn : 0.3f + j * 0.05f), f.Col, a * (j == 0 ? 1f : 0.6f), j == 0 ? 0.8f : 0f);
            }
        }
        // with fold drives, departing ships vanish in a flash at the end of their climb
        if (c.Civ.Techs.Contains("X6"))
            foreach (var f in _flights)
            {
                if (f.K != FlightKind.Depart) continue;
                float u = (_clock - f.T0) / f.Dur;
                if (u < 0.88f || u > 1f || kg >= GlowMax) continue;
                float w = 1f - Mathf.Abs(u - 0.94f) / 0.06f;
                PutGlow(gb, kg++, FlightPos(f, Mathf.Min(u, 0.94f), R), 30f * K * w, new Color(0.75f, 0.85f, 1f), w, 1f);
            }
        // mass drivers fling packets along the rail and off into space
        foreach (var (at, dir, len) in _drivers)
            for (int j = 0; j < 6 && kg < GlowMax; j++)
            {
                float ph = Mathf.PosMod(_clock * 0.35f + j / 6f, 1f);
                var a0 = spin * (at * R * 1.004f);
                var d0 = spin * dir;
                var pj = a0 + d0 * (ph < 0.3f ? len * ph / 0.3f : len + (ph - 0.3f) * R * 4f) + spin * at * Mathf.Max(0, ph - 0.3f) * R * 0.8f;
                PutGlow(gb, kg++, pj, 0.5f * K, new Color(0.7f, 0.9f, 1f), ph < 0.3f ? 1f : 1f - (ph - 0.3f) / 0.7f, 0.3f);
            }
        _ships.Multimesh.VisibleInstanceCount = 0;
        if (ks > 0) _ships.Multimesh.Buffer = sb;   // full-size buffer; the tail past the visible count is ignored
        _ships.Multimesh.VisibleInstanceCount = ks;
        _glows.Multimesh.VisibleInstanceCount = 0;
        if (kg > 0) _glows.Multimesh.Buffer = gb;
        _glows.Multimesh.VisibleInstanceCount = kg;
    }

    Vector3 FlightPos(Flight f, float u, float R)
    {
        switch (f.K)
        {
            case FlightKind.Skim:
            {
                // a dive from high orbit to the cloud tops and back out, swinging around the planet
                float th = (u - 0.5f) * 2.4f;
                float r = R * (1.02f + (f.Alt - 1.02f) * (2f * Mathf.Abs(u - 0.5f)) * (2f * Mathf.Abs(u - 0.5f)));
                return (f.Up * Mathf.Cos(th) + f.East * Mathf.Sin(th)) * r;
            }
            case FlightKind.Depart:
            {
                // from a parking orbit, spiral outward and away
                float th = u * 3.2f * f.Arc;
                float r = R * (f.Alt + u * u * 8f);
                return (f.Up * Mathf.Cos(th) + f.East * Mathf.Sin(th)) * r + f.Up.Cross(f.East) * u * u * R * 2f;
            }
            default:
            {
                // gravity turn: straight up, pitch over eastward, settle into orbit
                float v = f.K == FlightKind.Land ? 1f - u : u;
                float th = v * v * f.Arc;
                float h = 1f + (f.Alt - 1f) * Mathf.SmoothStep(0f, 0.7f, v);
                return (f.Up * Mathf.Cos(th) + f.East * Mathf.Sin(th)) * R * h;
            }
        }
    }

    static void PutT(float[] b, int o, Basis bs, Vector3 p)
    {
        b[o] = bs.X.X; b[o + 1] = bs.Y.X; b[o + 2] = bs.Z.X; b[o + 3] = p.X;
        b[o + 4] = bs.X.Y; b[o + 5] = bs.Y.Y; b[o + 6] = bs.Z.Y; b[o + 7] = p.Y;
        b[o + 8] = bs.X.Z; b[o + 9] = bs.Y.Z; b[o + 10] = bs.Z.Z; b[o + 11] = p.Z;
    }
    static void PutGlow(float[] b, int k, Vector3 p, float s, Color c, float a, float big)
    {
        int o = k * 20;
        PutT(b, o, Basis.Identity.Scaled(Vector3.One * s), p);
        b[o + 12] = c.R; b[o + 13] = c.G; b[o + 14] = c.B; b[o + 15] = a;
        b[o + 16] = big; b[o + 17] = 0; b[o + 18] = 0; b[o + 19] = 0;
    }
}
