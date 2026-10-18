using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// Procedural hull designs, from the first orbital shuttles to world-ships. Every hull runs nose to +Z,
/// about two units long, so one transform scale serves them all. Surface 0 is plating, surface 1 glows
/// (drive plumes, window strips, weapon emitters).
/// </summary>
public static class Ships
{
    /// <summary>Hull classes from progression.json (Shuttle .. Seraph); the order is the build order.</summary>
    public static string[] ClassNames => Defs.Hulls;
    /// <summary>How much bigger than a shuttle each class is.</summary>
    public static float[] ClassScale => Defs.HullScale;
    public static int Classes => Defs.Hulls.Length;
    /// <summary>Real hull lengths, nose to tail (the simulation owns the table).</summary>
    public static float LengthKm(int cls) => Sim.Simulation.LengthKm(cls);
    /// <summary>Transform scale for a class's hull in system units (the meshes run two units nose to tail).</summary>
    public static float Size(int cls) => LengthKm(cls) * StarSystem.Km * 0.5f;
    /// <summary>The nine hulls modelled before the tech bible, by their old index (Worldship was the old "Colossus").</summary>
    static readonly string[] Legacy = { "Shuttle", "Corvette", "Frigate", "Destroyer", "Cruiser", "Battleship", "Carrier", "Titan", "Worldship" };
    /// <summary>A class's stand-in until it has a model of its own: its legacy index, following the fallback chain.</summary>
    static int LegacyIndex(int cls)
    {
        string n = ClassNames[cls];
        for (int guard = 0; guard < 6; guard++)
        {
            int i = Array.IndexOf(Legacy, n);
            if (i >= 0) return i;
            var fb = TechData.P("hulls", "fallbackUntilModelled");
            if (!fb.TryGetProperty(n, out var to)) break;
            n = to.GetString() == "Colossus" ? "Worldship" : to.GetString();
        }
        return 0;
    }

    /// <summary>How many hulls to draw for a squadron of this many ships, with this many draws left in the budget.</summary>
    public static int DrawCount(double ships, int left)
    {
        if (ships <= 0 || left <= 0) return 0;
        return Math.Max(1, (int)Math.Min(Math.Min(Math.Ceiling(ships), (int)WarData.D("interface", "squadronDrawMax")), left));
    }

    static readonly Dictionary<string, ArrayMesh> _named = new();
    /// <summary>A kit-v2 hull by name (assets/ships/hum/&lt;Name&gt;.glb), cached; null if there is no such file.</summary>
    public static ArrayMesh Named(string name)
    {
        if (!_named.TryGetValue(name, out var m)) _named[name] = m = TrueScale(name);
        return m;
    }

    /// <summary>Whether any .glb resolves for this class and style: its own, its named one, or its stand-in's.</summary>
    public static bool HasModel(int cls, int style)
    {
        string n = ClassNames[cls]; int li = LegacyIndex(cls);
        if (style == 0) return ResourceLoader.Exists($"res://assets/ships/hum/{n}.glb") || ResourceLoader.Exists($"res://assets/ships/ship{n}.glb") || ResourceLoader.Exists($"res://assets/ships/ship{li}.glb");
        return ResourceLoader.Exists($"res://assets/ships/ship{style}_{n}.glb") || ResourceLoader.Exists($"res://assets/ships/ship{style}_{li}.glb");
    }

    public static int MaxClass(Civ civ) => civ.Hull;

    /// <summary>Most traffic is a couple of classes below the best the yards can build; the flagships are rare.</summary>
    public static int Pick(Random r, int max, bool local)
    {
        if (local) max = Math.Min(max, 3);
        double total = 0;
        Span<double> w = stackalloc double[Classes];
        for (int k = 0; k <= max; k++) { double d = max - 2 - k; w[k] = Math.Exp(-d * d / 5) + (k == max ? 0.12 : 0); total += w[k]; }
        double p = r.NextDouble() * total;
        for (int k = 0; k <= max; k++) { p -= w[k]; if (p <= 0) return k; }
        return max;
    }

    static ArrayMesh[] _meshes;
    public static ArrayMesh Mesh(int cls) { _meshes ??= new ArrayMesh[Classes]; return _meshes[cls] ??= Asset(cls) ?? Build(LegacyIndex(cls)); }

    /// <summary>The Blender-built hull for a class (assets/ships/shipN.glb), if it has been built.</summary>
    static ArrayMesh Asset(int cls, int style = 0)
    {
        // a model named for the class (shipTitan.glb, ship1_Titan.glb) wins; otherwise the legacy-numbered model of its stand-in
        string name = ClassNames[cls];
        if (style == 0 && TrueScale(name) is ArrayMesh real) return real;
        string path = style == 0 ? $"res://assets/ships/ship{name}.glb" : $"res://assets/ships/ship{style}_{name}.glb";
        if (!ResourceLoader.Exists(path))
        {
            int li = LegacyIndex(cls);
            path = style == 0 ? $"res://assets/ships/ship{li}.glb" : $"res://assets/ships/ship{style}_{li}.glb";
        }
        if (!ResourceLoader.Exists(path)) return null;
        var root = GD.Load<PackedScene>(path).Instantiate<Node3D>();
        var mi = root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().FirstOrDefault();
        var mesh = mi?.Mesh as ArrayMesh;
        if (mesh != null)
            for (int i = 0; i < mesh.GetSurfaceCount(); i++)
                if (mesh.SurfaceGetMaterial(i) is BaseMaterial3D m && m.EmissionEnabled)
                {
                    // windows and drives must read from across a system, like the stations' lights
                    var b = (BaseMaterial3D)m.Duplicate();
                    b.EmissionEnergyMultiplier = Mathf.Max(b.EmissionEnergyMultiplier, 1f) * (style == 0 ? 3f : 1f);
                    mesh.SurfaceSetMaterial(i, b);
                }
        root.QueueFree();
        return mesh;
    }

    /// <summary>
    /// Ship kit v2 hulls (assets/ships/hum/&lt;Class&gt;.glb): modelled in metres, nose along +X, so they can be walked
    /// through one day. The traffic and fleet code still wants the old convention (two units nose to tail along +Z),
    /// so the hull is turned and shrunk once here; the true length is kept in TrueLengthM for whatever needs it.
    /// </summary>
    static ArrayMesh TrueScale(string name)
    {
        string path = $"res://assets/ships/hum/{name}.glb";
        if (!ResourceLoader.Exists(path)) return null;
        var root = GD.Load<PackedScene>(path).Instantiate<Node3D>();
        var src = root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().FirstOrDefault()?.Mesh as ArrayMesh;
        root.QueueFree();
        if (src == null) return null;
        var box = src.GetAabb();
        float len = Mathf.Max(box.Size.X, 1e-3f);
        TrueLengthM[name] = len;
        var cx = box.GetCenter().X;
        // +X (nose) -> +Z, then centre on the length and scale to two units
        var t = new Transform3D(new Basis(Vector3.Up, -Mathf.Pi / 2).Scaled(Vector3.One * (2f / len)), Vector3.Zero) * new Transform3D(Basis.Identity, new Vector3(-cx, 0, 0));
        var mesh = new ArrayMesh();
        for (int i = 0; i < src.GetSurfaceCount(); i++)
        {
            var st = new SurfaceTool();
            st.AppendFrom(src, i, t);
            st.Commit(mesh);
            var m = src.SurfaceGetMaterial(i);
            if (m is BaseMaterial3D bm && bm.EmissionEnabled)
            {
                var b = (BaseMaterial3D)bm.Duplicate();
                b.EmissionEnergyMultiplier = Mathf.Max(b.EmissionEnergyMultiplier, 1f) * 3f;     // read from across a system
                m = b;
            }
            mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, m);
        }
        return mesh;
    }
    /// <summary>True modelled length in metres of each kit-v2 hull, by class name.</summary>
    public static readonly Dictionary<string, float> TrueLengthM = new();

    /// <summary>Hull families: 0 human, 1 organic (ribbed, crescent-winged), 2 crystalline (clustered shards).</summary>
    public static int Style(Civ civ) => civ.M.Has("GrownHulls") ? 1 : civ.Alien ? 1 + civ.Id % 2 : 0;
    static ArrayMesh[,] _alien;
    public static ArrayMesh Mesh(int cls, int style)
    {
        if (style == 0) return Mesh(cls);
        _alien ??= new ArrayMesh[3, Classes];
        return _alien[style, cls] ??= Asset(cls, style) ?? (style == 1 ? Organic(LegacyIndex(cls)) : Crystal(LegacyIndex(cls)));
    }

    static StandardMaterial3D _organicHull, _crystalHull, _organicGlow, _crystalGlow;
    static ArrayMesh Organic(int cls)
    {
        var k = new Kit();
        int ribs = 3 + cls;
        // the body: a long ellipsoid, ribbed like a seed pod
        k.Shape(new SphereMesh { Radius = 1, Height = 2, RadialSegments = 18, Rings = 10 }, new Transform3D(Basis.Identity.Scaled(new Vector3(0.28f, 0.22f, 1f)), Vector3.Zero));
        for (int i = 0; i < ribs; i++)
        {
            float z = -0.8f + 1.6f * (i + 0.5f) / ribs;
            float rr = Mathf.Sqrt(Mathf.Max(0.02f, 1 - z * z)) * 0.29f;
            k.Shape(new TorusMesh { InnerRadius = rr * 0.92f, OuterRadius = rr * 1.06f, Rings = 18, RingSegments = 6 }, new Transform3D(new Basis(Vector3.Right, Mathf.Pi / 2).Scaled(new Vector3(1, 1, 0.78f)), new Vector3(0, 0, z)));
        }
        // crescent wings sweeping back, and for the big ones a dorsal sail
        for (int s = -1; s <= 1; s += 2)
            k.Shape(new SphereMesh { Radius = 1, Height = 2, RadialSegments = 14, Rings = 6 },
                new Transform3D(new Basis(Vector3.Up, s * 0.5f).Scaled(new Vector3(0.55f + cls * 0.04f, 0.035f, 0.28f)), new Vector3(s * 0.38f, -0.02f, -0.35f)));
        if (cls >= 4) k.Shape(new SphereMesh { Radius = 1, Height = 2, RadialSegments = 12, Rings = 6 }, new Transform3D(Basis.Identity.Scaled(new Vector3(0.03f, 0.35f, 0.5f)), new Vector3(0, 0.25f, -0.2f)));
        for (int i = 0; i < cls / 2; i++)
            for (int s = -1; s <= 1; s += 2) k.Ball(0.07f, new Vector3(s * 0.3f, 0, 0.4f - i * 0.25f));
        // a glowing vein along the spine and a heart-light at the stern
        k.Shape(new SphereMesh { Radius = 1, Height = 2, RadialSegments = 8, Rings = 4 }, new Transform3D(Basis.Identity.Scaled(new Vector3(0.02f, 0.02f, 0.85f)), new Vector3(0, 0.215f, 0)), true);
        k.Ball(0.14f, new Vector3(0, 0, -0.95f), true);
        return k.Done(_organicHull ??= new StandardMaterial3D { AlbedoColor = new Color(0.32f, 0.26f, 0.2f), Metallic = 0.35f, Roughness = 0.45f },
                      _organicGlow ??= new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.5f, 1.3f, 0.9f) });
    }

    static ArrayMesh Crystal(int cls)
    {
        var k = new Kit();
        // a long central shard: two four-sided cones base to base
        k.Cyl(0.0f, 0.24f, 1.3f, new Vector3(0, 0, 0.35f), 4);
        k.Cyl(0.24f, 0.05f, 0.7f, new Vector3(0, 0, -0.65f), 4);
        var r = new Random(cls * 31 + 7);
        int n = 2 + cls;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.Tau / n + (float)r.NextDouble() * 0.4f;
            float len = 0.4f + 0.5f * (float)r.NextDouble();
            var b = new Basis(Vector3.Forward, a) * new Basis(Vector3.Right, -0.25f - 0.2f * (float)r.NextDouble());
            var at = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.2f + new Vector3(0, 0, -0.1f + 0.3f * (float)r.NextDouble());
            k.Shape(new CylinderMesh { TopRadius = 0, BottomRadius = 0.09f, Height = len, RadialSegments = 4, Rings = 1 }, new Transform3D(b * new Basis(Vector3.Right, Mathf.Pi / 2), at));
            k.Shape(new CylinderMesh { TopRadius = 0, BottomRadius = 0.03f, Height = len * 0.25f, RadialSegments = 4, Rings = 1 }, new Transform3D(b * new Basis(Vector3.Right, Mathf.Pi / 2), at + b * new Vector3(0, 0, len * 0.4f)), true);
        }
        k.Shape(new SphereMesh { Radius = 0.11f, Height = 0.22f, RadialSegments = 6, Rings = 3 }, new Transform3D(Basis.Identity, new Vector3(0, 0, -0.2f)), true);
        k.Ball(0.08f, new Vector3(0, 0, -1.0f), true);
        return k.Done(_crystalHull ??= new StandardMaterial3D { AlbedoColor = new Color(0.16f, 0.12f, 0.28f), Metallic = 0.9f, Roughness = 0.12f },
                      _crystalGlow ??= new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(1.1f, 0.6f, 1.6f) });
    }

    static StandardMaterial3D _glow, _dark;
    public static StandardMaterial3D GlowMat() => _glow ??= new StandardMaterial3D
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.5f, 0.7f, 1.25f)
    };
    static StandardMaterial3D DarkMat() => _dark ??= new StandardMaterial3D { AlbedoColor = new Color(0.22f, 0.23f, 0.26f), Metallic = 0.7f, Roughness = 0.35f };

    // ---- primitive helpers: everything is appended into one of two surface tools ----
    sealed class Kit
    {
        public readonly SurfaceTool Hull = new(), Glow = new();
        public Kit() { Hull.Begin(Godot.Mesh.PrimitiveType.Triangles); Glow.Begin(Godot.Mesh.PrimitiveType.Triangles); }
        void Add(SurfaceTool st, Mesh m, Transform3D t) => st.AppendFrom(m, 0, t);
        public void Shape(Mesh m, Transform3D t, bool glow = false) => Add(glow ? Glow : Hull, m, t);
        public void Box(Vector3 size, Vector3 at, bool glow = false, Basis? b = null) => Add(glow ? Glow : Hull, new BoxMesh { Size = size }, new Transform3D(b ?? Basis.Identity, at));
        /// <summary>A cylinder along Z.</summary>
        public void Cyl(float r0, float r1, float len, Vector3 at, int seg = 10, bool glow = false) =>
            Add(glow ? Glow : Hull, new CylinderMesh { TopRadius = r1, BottomRadius = r0, Height = len, RadialSegments = seg, Rings = 1 }, new Transform3D(new Basis(Vector3.Right, Mathf.Pi / 2), at));
        public void Ring(float r, float thick, Vector3 at, bool glow = false, int seg = 32) =>
            Add(glow ? Glow : Hull, new TorusMesh { InnerRadius = r - thick, OuterRadius = r + thick, Rings = seg, RingSegments = 6 }, new Transform3D(new Basis(Vector3.Right, Mathf.Pi / 2), at));
        public void Ball(float r, Vector3 at, bool glow = false) => Add(glow ? Glow : Hull, new SphereMesh { Radius = r, Height = r * 2, RadialSegments = 16, Rings = 8 }, new Transform3D(Basis.Identity, at));
        /// <summary>Drive nozzle at the stern with its plume.</summary>
        public void Engine(float r, Vector3 at)
        {
            Cyl(r * 1.1f, r * 0.8f, r * 1.2f, at);
            Cyl(r * 0.75f, r * 0.75f, r * 0.15f, at - new Vector3(0, 0, r * 0.6f), 10, true);
        }
        /// <summary>A line of lit windows along the hull.</summary>
        public void Windows(float z0, float z1, float x, float y, float h = 0.01f) => Box(new Vector3(0.006f, h, Mathf.Abs(z1 - z0)), new Vector3(x, y, (z0 + z1) * 0.5f), true);
        public ArrayMesh Done(Material hull = null, Material glow = null)
        {
            Hull.GenerateNormals();
            var m = Hull.Commit();
            m.SurfaceSetMaterial(0, hull ?? Works.HullMat());
            Glow.Commit(m);
            m.SurfaceSetMaterial(1, glow ?? GlowMat());
            return m;
        }
    }

    static ArrayMesh Build(int cls)
    {
        var k = new Kit();
        switch (cls)
        {
            case 0: // shuttle: a lifting body with a cargo bay and two engines
                k.Box(new Vector3(0.36f, 0.16f, 1.3f), new Vector3(0, 0, 0.1f));
                k.Cyl(0.02f, 0.18f, 0.5f, new Vector3(0, 0, 0.95f), 8);
                k.Box(new Vector3(0.9f, 0.03f, 0.5f), new Vector3(0, -0.04f, -0.35f));
                k.Engine(0.09f, new Vector3(-0.1f, 0, -0.6f)); k.Engine(0.09f, new Vector3(0.1f, 0, -0.6f));
                k.Windows(0.7f, 0.95f, 0.15f, 0.06f);
                break;
            case 1: // corvette: a dart with a big drive
                k.Cyl(0.22f, 0.02f, 1.6f, new Vector3(0, 0, 0.2f), 6);
                k.Box(new Vector3(0.8f, 0.04f, 0.4f), new Vector3(0, 0, -0.4f));
                k.Box(new Vector3(0.04f, 0.5f, 0.35f), new Vector3(0, 0.2f, -0.45f));
                k.Engine(0.2f, new Vector3(0, 0, -0.7f));
                k.Windows(0.1f, 0.5f, 0.0f, 0.12f);
                break;
            case 2: // frigate: spine, crew section, twin nacelles
                k.Box(new Vector3(0.25f, 0.2f, 1.7f), Vector3.Zero);
                k.Cyl(0.12f, 0.3f, 0.5f, new Vector3(0, 0, 0.9f), 8);
                k.Box(new Vector3(0.12f, 0.12f, 0.9f), new Vector3(-0.42f, -0.05f, -0.35f)); k.Box(new Vector3(0.12f, 0.12f, 0.9f), new Vector3(0.42f, -0.05f, -0.35f));
                k.Box(new Vector3(0.75f, 0.03f, 0.2f), new Vector3(0, -0.05f, -0.3f));
                k.Engine(0.08f, new Vector3(-0.42f, -0.05f, -0.82f)); k.Engine(0.08f, new Vector3(0.42f, -0.05f, -0.82f)); k.Engine(0.11f, new Vector3(0, 0, -0.9f));
                k.Windows(-0.6f, 0.6f, 0.126f, 0.03f); k.Windows(-0.6f, 0.6f, -0.126f, 0.03f);
                break;
            case 3: // destroyer: wedge hull with turrets along the dorsal line
                k.Cyl(0.35f, 0.05f, 1.9f, Vector3.Zero, 4);
                k.Box(new Vector3(0.3f, 0.18f, 0.8f), new Vector3(0, 0.15f, -0.35f));
                for (int i = 0; i < 4; i++) { float z = -0.5f + i * 0.28f; k.Box(new Vector3(0.1f, 0.06f, 0.1f), new Vector3(0, 0.27f, z)); k.Box(new Vector3(0.02f, 0.02f, 0.18f), new Vector3(0, 0.29f, z + 0.12f)); }
                for (int i = -1; i <= 1; i++) k.Engine(0.1f, new Vector3(i * 0.18f, 0, -0.98f));
                k.Windows(-0.7f, 0.3f, 0.16f, 0.12f); k.Windows(-0.7f, 0.3f, -0.16f, 0.12f);
                break;
            case 4: // cruiser: long hull, a spin-gravity ring amidships, sensor mast
                k.Cyl(0.18f, 0.12f, 1.9f, Vector3.Zero, 8);
                k.Cyl(0.05f, 0.24f, 0.4f, new Vector3(0, 0, 1.0f), 8);
                k.Ring(0.45f, 0.04f, new Vector3(0, 0, 0.1f));
                for (int i = 0; i < 4; i++) k.Box(new Vector3(0.02f, 0.45f, 0.03f), new Vector3(0, 0, 0.1f), false, new Basis(Vector3.Forward, i * Mathf.Pi / 4));
                k.Box(new Vector3(0.6f, 0.03f, 0.3f), new Vector3(0, 0, -0.6f));
                k.Box(new Vector3(0.03f, 0.5f, 0.06f), new Vector3(0, 0.3f, 0.6f));
                k.Ring(0.45f, 0.012f, new Vector3(0, 0, 0.1f), true);
                k.Engine(0.14f, new Vector3(0, 0, -0.98f)); k.Engine(0.08f, new Vector3(0.22f, 0, -0.9f)); k.Engine(0.08f, new Vector3(-0.22f, 0, -0.9f));
                break;
            case 5: // battleship: armoured slab, spinal mount, banks of turrets, engine block
                k.Box(new Vector3(0.5f, 0.3f, 1.8f), Vector3.Zero);
                k.Cyl(0.08f, 0.25f, 0.35f, new Vector3(0, 0, 1.05f), 6);
                k.Cyl(0.05f, 0.05f, 0.2f, new Vector3(0, 0, 1.25f), 8, true);   // spinal gun muzzle
                k.Box(new Vector3(0.7f, 0.2f, 0.6f), new Vector3(0, 0, -0.7f));
                k.Box(new Vector3(0.25f, 0.2f, 0.4f), new Vector3(0, 0.25f, -0.2f));
                for (int i = 0; i < 6; i++) { float z = -0.2f + i * 0.17f; foreach (float x in new[] { -0.2f, 0.2f }) { k.Box(new Vector3(0.09f, 0.06f, 0.09f), new Vector3(x, 0.18f, z)); k.Box(new Vector3(0.015f, 0.015f, 0.16f), new Vector3(x, 0.2f, z + 0.1f)); } }
                for (int i = 0; i < 4; i++) k.Engine(0.11f, new Vector3(-0.27f + i * 0.18f, 0, -1.02f));
                k.Windows(-0.8f, 0.8f, 0.251f, 0.05f); k.Windows(-0.8f, 0.8f, -0.251f, 0.05f);
                k.Box(new Vector3(0.012f, 0.012f, 1.4f), new Vector3(0, 0.151f, 0.1f), true);
                break;
            case 6: // carrier: a flat deck with open hangar mouths and a command island
                k.Box(new Vector3(0.9f, 0.15f, 1.9f), Vector3.Zero);
                k.Box(new Vector3(0.25f, 0.3f, 0.5f), new Vector3(0.3f, 0.2f, -0.3f));
                k.Box(new Vector3(0.6f, 0.08f, 0.04f), new Vector3(0, 0, 0.95f), true);    // hangar glow
                k.Box(new Vector3(0.6f, 0.08f, 0.04f), new Vector3(0, 0, -0.95f), true);
                for (int i = 0; i < 6; i++) k.Box(new Vector3(0.5f, 0.004f, 0.02f), new Vector3(-0.1f, 0.077f, -0.7f + i * 0.28f), true);   // deck lights
                k.Box(new Vector3(1.3f, 0.04f, 0.5f), new Vector3(0, -0.05f, -0.4f));
                for (int i = 0; i < 5; i++) k.Engine(0.1f, new Vector3(-0.36f + i * 0.18f, 0, -1.0f));
                break;
            case 7: // titan: a kilometre-scale hull with a lance down its spine and cathedral superstructure
                k.Cyl(0.35f, 0.15f, 1.9f, Vector3.Zero, 8);
                k.Box(new Vector3(0.95f, 0.12f, 1.2f), new Vector3(0, -0.05f, -0.25f));
                k.Box(new Vector3(0.18f, 0.55f, 0.7f), new Vector3(0, 0.35f, -0.35f));
                k.Box(new Vector3(0.4f, 0.12f, 0.25f), new Vector3(0, 0.62f, -0.45f));
                k.Cyl(0.06f, 0.06f, 2.0f, new Vector3(0, 0, 0.05f), 8, true);        // the lance's core shows through
                k.Ring(0.42f, 0.03f, new Vector3(0, 0, 0.55f)); k.Ring(0.36f, 0.03f, new Vector3(0, 0, 0.75f));
                k.Ring(0.42f, 0.008f, new Vector3(0, 0, 0.55f), true);
                for (int i = 0; i < 8; i++) { float z = -0.7f + i * 0.17f; foreach (float x in new[] { -0.4f, 0.4f }) k.Box(new Vector3(0.1f, 0.07f, 0.1f), new Vector3(x, 0.05f, z)); }
                for (int i = 0; i < 3; i++) for (int j = 0; j < 2; j++) k.Engine(0.12f, new Vector3(-0.3f + i * 0.3f, -0.12f + j * 0.24f, -0.98f));
                k.Windows(-0.8f, 0.4f, 0.48f, -0.05f, 0.02f); k.Windows(-0.8f, 0.4f, -0.48f, -0.05f, 0.02f);
                k.Windows(-0.6f, -0.1f, 0.091f, 0.3f, 0.3f);
                break;
            default: // colossus: a world-ship, a moon-sized core with rings of city, ringed by drives
                k.Ball(0.65f, Vector3.Zero);
                k.Ring(0.9f, 0.07f, Vector3.Zero); k.Ring(0.9f, 0.015f, Vector3.Zero, true);
                k.Box(new Vector3(1.8f, 0.05f, 0.12f), Vector3.Zero); k.Box(new Vector3(0.05f, 1.8f, 0.12f), Vector3.Zero);
                k.Cyl(0.15f, 0.5f, 0.6f, new Vector3(0, 0, 0.75f), 12);
                k.Cyl(0.12f, 0.12f, 0.15f, new Vector3(0, 0, 1.1f), 12, true);     // the planet-cracker at the bow
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.Tau / 6;
                    var at = new Vector3(Mathf.Cos(a) * 0.55f, Mathf.Sin(a) * 0.55f, -0.75f);
                    k.Cyl(0.14f, 0.1f, 0.5f, at + new Vector3(0, 0, 0.2f), 8);
                    k.Engine(0.13f, at - new Vector3(0, 0, 0.1f));
                }
                for (int i = 0; i < 5; i++) k.Ring(0.6f - i * 0.03f, 0.004f, new Vector3(0, 0, -0.4f + i * 0.2f), true);
                break;
        }
        return k.Done();
    }

    /// <summary>Where on the hull the drive glow sits, for the engine sprites.</summary>
    public static float SternZ(int cls) => cls == 8 ? -1.0f : -1.05f;
}
