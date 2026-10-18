using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// The visible fingerprints of a civilisation in one system: the swarm, ship traffic, stations,
/// Earth's elevator and orbital ring, O'Neill habitats and mass-driver streams.
/// </summary>
public partial class Works : Node3D
{
    SystemView _sv;
    StarSystem Sys => _sv.Sys;
    Colony _c;
    Random _r = new(7);

    /// <summary>Everything that must hold its place to the metre beside a world: ships, stations, habitats, the fleet.
    /// TopLevel at the view origin, with every instance placed in double precision and squashed if far (see Frame).</summary>
    Node3D _near;
    static readonly Aabb Everywhere = new(-Vector3.One * 1e15f, Vector3.One * 2e15f);

    // swarm
    MultiMeshInstance3D _swarm;
    ShaderMaterial _swarmMat;
    const int SwarmMax = 16000;           // each quad stands for a great many collectors; 60,000 cost more than they showed
    MeshInstance3D _haze;
    ShaderMaterial _hazeMat;

    // traffic
    class Ship { public Body A, B; public double T0, Dur; public float Lift, Side; public int Kind, Cls; public float Size; public bool Out; }
    readonly List<Ship> _ships = new();
    MultiMeshInstance3D _shipGlow, _trails;
    readonly MultiMeshInstance3D[] _classMesh = new MultiMeshInstance3D[Ships.Classes];
    readonly float[][] _classBuf = new float[Ships.Classes][];
    readonly int[] _classN = new int[Ships.Classes];
    int _style;
    const int ShipMax = 3000;
    float[] _engBuf = new float[ShipMax * 20], _wakeBuf = new float[ShipMax * 16];

    // stations
    MultiMeshInstance3D _stations, _stationLights;
    const int StationMax = 400;

    // Earth works
    Node3D _elevator, _ring; ShaderMaterial _ringMat;
    // habitats
    MultiMeshInstance3D _habs, _habLights;
    readonly MultiMeshInstance3D[] _habKinds = new MultiMeshInstance3D[3];   // O'Neill cylinder, Stanford torus, Bernal sphere
    const int HabMax = 80;
    // mass streams
    MultiMeshInstance3D _streams;
    const int StreamMax = 3000;
    float _clock;
    GreatWorks _great;
    CloseUp _close;
    public Megastructures Mega;
    Node3D _fleet; Node3D _ark; readonly List<MeshInstance3D> _frigates = new();

    public void Setup(SystemView sv)
    {
        _sv = sv;
        _near = new Node3D { Name = "Near", TopLevel = true };
        AddChild(_near);
        BuildSwarm();
        BuildTraffic();
        BuildStations();
        BuildWarProps();
        BuildFleets();
        BuildHabitats();
        BuildStreams();
        _great = new GreatWorks();
        AddChild(_great);
        _great.Setup(sv);
        Mega = new Megastructures();
        AddChild(Mega);
        Mega.Setup(sv);
        _close = new CloseUp();
        AddChild(_close);
        _close.Setup(sv);
    }

    // ---------- swarm ----------
    void BuildSwarm()
    {
        var r = new Random(Sys.Star.Seed);
        var quad = new QuadMesh { Size = new Vector2(1, 0.62f) };
        _swarmMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/swarm.gdshader") };
        quad.Material = _swarmMat;
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = quad, InstanceCount = SwarmMax, VisibleInstanceCount = 0 };
        for (int i = 0; i < SwarmMax; i++)
        {
            float order = (float)i / SwarmMax;
            // early collectors sit near the ecliptic; later ones tilt until the shell closes
            float inc = ((float)r.NextDouble() - 0.5f) * (0.12f + 3.0f * order);
            var q = new Quaternion(Vector3.Up, (float)r.NextDouble() * Mathf.Tau) * new Quaternion(Vector3.Right, inc);
            mm.SetInstanceTransform(i, Transform3D.Identity);
            mm.SetInstanceColor(i, new Color(q.X * 0.5f + 0.5f, q.Y * 0.5f + 0.5f, q.Z * 0.5f + 0.5f, q.W * 0.5f + 0.5f));
            float size = 0.12f + (float)r.NextDouble() * 0.2f;
            mm.SetInstanceCustomData(i, new Color((float)Math.Pow(r.NextDouble(), 0.8), (float)r.NextDouble(), size, (float)r.NextDouble()));
        }
        _swarm = new MultiMeshInstance3D { Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _swarm.CustomAabb = new Aabb(-Vector3.One * 200, Vector3.One * 400);
        AddChild(_swarm);

        // infrared haze: the shell glows faintly red with the heat it must shed
        _hazeMat = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_back, shadows_disabled, fog_disabled;
uniform float strength = 0.0;
varying vec3 v_n; varying vec3 v_w;
void vertex(){ v_n = normalize((MODEL_MATRIX * vec4(NORMAL,0.0)).xyz); v_w = (MODEL_MATRIX * vec4(VERTEX,1.0)).xyz; }
void fragment(){
	float f = 1.0 - abs(dot(normalize(v_n), normalize(CAMERA_POSITION_WORLD - v_w)));
	ALBEDO = vec3(0.6, 0.1, 0.03) * pow(f, 6.0) * strength;
}"
            }
        };
        _haze = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 64, Rings = 32 }, MaterialOverride = _hazeMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_haze);
    }

    /// <summary>Half-width (radians of latitude) of the band a swarm leaves clear for the worlds it shares a star with.</summary>
    public const float EclipticGap = 0.11f;

    public (float In, float Out) SwarmShell()
    {
        float sr = Sys.StarRadius;
        float first = Sys.Bodies.Count > 0 ? Sys.Bodies.Min(b => b.Orbit - b.Radius * 3) : sr * 8;
        float rin = sr * 1.8f;
        float rout = Mathf.Clamp(first * 0.75f, rin + 4, sr * 6f);
        return (rin, rout);
    }

    // ---------- traffic ----------
    static ArrayMesh ShipHull()
    {
        // a slender hull: long spine, wider drive section at the back (nose toward +Z)
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        Vector3[] ring(float z, float w, float h) => new[] { new Vector3(-w, 0, z), new Vector3(0, h, z), new Vector3(w, 0, z), new Vector3(0, -h, z) };
        var rings = new[] { ring(1f, 0.02f, 0.02f), ring(0.6f, 0.12f, 0.1f), ring(-0.4f, 0.16f, 0.12f), ring(-0.7f, 0.28f, 0.08f), ring(-1f, 0.2f, 0.14f) };
        for (int k = 0; k < rings.Length - 1; k++)
            for (int i = 0; i < 4; i++)
            {
                var a = rings[k][i]; var b = rings[k][(i + 1) % 4]; var c = rings[k + 1][i]; var d = rings[k + 1][(i + 1) % 4];
                st.AddVertex(a); st.AddVertex(c); st.AddVertex(b);
                st.AddVertex(b); st.AddVertex(c); st.AddVertex(d);
            }
        var back = rings[^1];
        st.AddVertex(back[0]); st.AddVertex(back[1]); st.AddVertex(back[2]);
        st.AddVertex(back[0]); st.AddVertex(back[2]); st.AddVertex(back[3]);
        st.GenerateNormals();
        return st.Commit();
    }

    static ShaderMaterial _glowMat;
    /// <summary>Additive sprite that stays at least a few pixels wide: engine flares, station beacons, stream particles.</summary>
    public static ShaderMaterial GlowMat()
    {
        if (_glowMat != null) return _glowMat;
        _glowMat = new ShaderMaterial
        {
            RenderPriority = 4,
            Shader = new Shader
            {
                Code = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled, skip_vertex_transform;
uniform float min_px = 1.8;
varying vec4 v_col; varying float v_boost; varying float v_floor;
void vertex(){
	v_floor = max(INSTANCE_CUSTOM.y, 0.08);
	vec3 c = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
	float s = length((MODEL_MATRIX * vec4(1.0, 0.0, 0.0, 0.0)).xyz);
	vec4 vc = VIEW_MATRIX * vec4(c, 1.0);
	float d = max(-vc.z, 1e-4);
	float px = s / d * VIEWPORT_SIZE.y * 0.5 * abs(PROJECTION_MATRIX[1][1]);
	float mpx = min_px * (0.6 + INSTANCE_CUSTOM.x);
	float k = px < mpx ? mpx / max(px, 1e-4) : 1.0;
	v_boost = 1.0 / (k * k * 0.6 + 0.4);
	POSITION = PROJECTION_MATRIX * (vc + vec4(VERTEX.xy * s * k, 0.0, 0.0));
	v_col = COLOR;
}
void fragment(){
	vec2 c = UV * 2.0 - 1.0; float r = dot(c, c);
	float a = exp(-r * 6.0) + 0.4 * exp(-r * 40.0);
	ALBEDO = v_col.rgb * v_col.a * a * max(v_boost, v_floor) * 2.2;
}"
            }
        };
        return _glowMat;
    }

    static MultiMeshInstance3D Glows(int n)
    {
        var quad = new QuadMesh { Size = new Vector2(2, 2), Material = GlowMat() };
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = quad, InstanceCount = n, VisibleInstanceCount = 0 };
        var mi = new MultiMeshInstance3D { Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        mi.CustomAabb = new Aabb(-Vector3.One * 2000, Vector3.One * 4000);
        return mi;
    }

    static ArrayMesh _hullMesh;
    public static ArrayMesh ShipHullMesh() { if (_hullMesh == null) { _hullMesh = ShipHull(); _hullMesh.SurfaceSetMaterial(0, HullMat()); } return _hullMesh; }

    static ShaderMaterial _hullMat;
    public static ShaderMaterial HullMat() => _hullMat ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/hull.gdshader") };

    void BuildTraffic()
    {
        // one batch per hull class; each grows only as large as that class's traffic needs
        for (int c = 0; c < Ships.Classes; c++)
        {
            int n = c < 3 ? ShipMax : Math.Max(12, (int)(2400 / Math.Pow(Ships.ClassScale[c], 2.2)));      // capital ships are rare, and heavy
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = Ships.Mesh(c), InstanceCount = n, VisibleInstanceCount = 0 };
            _classMesh[c] = new MultiMeshInstance3D { Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, CustomAabb = Everywhere };
            _classBuf[c] = new float[n * 12];
            _near.AddChild(_classMesh[c]);
        }
        _shipGlow = Glows(ShipMax);
        _shipGlow.CustomAabb = Everywhere;
        _near.AddChild(_shipGlow);
        var trailMat = new ShaderMaterial
        {
            RenderPriority = 3,
            Shader = new Shader
            {
                Code = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
varying vec4 v_col;
void vertex(){ v_col = COLOR; }
void fragment(){
	vec2 c = UV * 2.0 - 1.0;
	float a = clamp(pow(clamp(UV.y, 0.0, 1.0), 1.6), 0.0, 1.0) * exp(-c.x * c.x * 3.0);
	ALBEDO = clamp(v_col.rgb, 0.0, 1.0) * clamp(v_col.a, 0.0, 1.0) * a;
}"
            }
        };
        var quad = new QuadMesh { Size = Vector2.One, Material = trailMat };
        var tm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = quad, InstanceCount = ShipMax, VisibleInstanceCount = 0 };
        _trails = new MultiMeshInstance3D { Multimesh = tm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, CustomAabb = Everywhere };
        _near.AddChild(_trails);
    }

    // ---------- stations ----------
    static ArrayMesh _stationMesh;
    public static ArrayMesh StationMesh() => _stationMesh ??= BuildStationMesh();
    static ArrayMesh BuildStationMesh()
    {
        var st = new SurfaceTool();
        var torus = new TorusMesh { InnerRadius = 0.8f, OuterRadius = 1f, Rings = 24, RingSegments = 6 };
        st.AppendFrom(torus, 0, Transform3D.Identity);
        var hub = new CylinderMesh { TopRadius = 0.18f, BottomRadius = 0.18f, Height = 1.6f, RadialSegments = 8 };
        st.AppendFrom(hub, 0, new Transform3D(new Basis(Vector3.Right, Mathf.Pi / 2), Vector3.Zero).Rotated(Vector3.Right, -Mathf.Pi / 2));
        foreach (float ang in new[] { 0f, Mathf.Pi / 2, Mathf.Pi, 3 * Mathf.Pi / 2 })
        {
            var spoke = new BoxMesh { Size = new Vector3(0.05f, 0.05f, 0.85f) };
            st.AppendFrom(spoke, 0, new Transform3D(new Basis(Vector3.Up, ang), new Vector3(Mathf.Sin(ang), 0, Mathf.Cos(ang)) * 0.42f));
        }
        var panel = new BoxMesh { Size = new Vector3(2.4f, 0.02f, 0.5f) };
        st.AppendFrom(panel, 0, new Transform3D(Basis.Identity, new Vector3(0, 0.75f, 0)));
        st.AppendFrom(panel, 0, new Transform3D(Basis.Identity, new Vector3(0, -0.75f, 0)));
        return st.Commit();
    }

    void BuildStations()
    {
        var m = StationMesh();
        m.SurfaceSetMaterial(0, HullMat());
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = m, InstanceCount = StationMax, VisibleInstanceCount = 0 };
        _stations = new MultiMeshInstance3D { Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _stations.CustomAabb = Everywhere;
        _near.AddChild(_stations);
        _stationLights = Glows(StationMax);
        _stationLights.CustomAabb = Everywhere;
        _near.AddChild(_stationLights);
    }

    // ---------- habitats ----------
    static ArrayMesh HabitatMesh()
    {
        var st = new SurfaceTool();
        var cyl = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.22f, Height = 1.6f, RadialSegments = 20 };
        st.AppendFrom(cyl, 0, Transform3D.Identity);
        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.Tau / 3;
            var mirror = new BoxMesh { Size = new Vector3(0.36f, 1.5f, 0.01f) };
            var basis = new Basis(Vector3.Up, a) * new Basis(Vector3.Right, -0.45f);
            st.AppendFrom(mirror, 0, new Transform3D(basis, new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 0.42f + new Vector3(0, 0.3f, 0)));
        }
        var cap = new SphereMesh { Radius = 0.22f, Height = 0.2f, RadialSegments = 16, Rings = 4, IsHemisphere = true };
        st.AppendFrom(cap, 0, new Transform3D(Basis.Identity, new Vector3(0, 0.8f, 0)));
        return st.Commit();
    }

    void BuildHabitats()
    {
        var m = HabitatMesh();
        var mat = new StandardMaterial3D { AlbedoColor = new Color(0.62f, 0.64f, 0.66f), Metallic = 0.4f, Roughness = 0.35f, EmissionEnabled = true, Emission = new Color(0.25f, 0.2f, 0.12f) };
        m.SurfaceSetMaterial(0, mat);
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = m, InstanceCount = HabMax, VisibleInstanceCount = 0 };
        _habs = new MultiMeshInstance3D { Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _habs.CustomAabb = Everywhere;
        _near.AddChild(_habs);
        _habKinds[0] = _habs;
        var torus = new SurfaceTool();
        torus.AppendFrom(new TorusMesh { InnerRadius = 0.62f, OuterRadius = 0.8f, Rings = 32, RingSegments = 10 }, 0, Transform3D.Identity);
        torus.AppendFrom(new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.1f, Height = 0.3f, RadialSegments = 10 }, 0, Transform3D.Identity);
        for (int i = 0; i < 6; i++) torus.AppendFrom(new BoxMesh { Size = new Vector3(0.03f, 0.03f, 0.62f) }, 0, new Transform3D(new Basis(Vector3.Up, i * Mathf.Pi / 3), new Basis(Vector3.Up, i * Mathf.Pi / 3) * new Vector3(0, 0, 0.36f)));
        torus.AppendFrom(new CylinderMesh { TopRadius = 0.9f, BottomRadius = 0.0f, Height = 0.4f, RadialSegments = 24 }, 0, new Transform3D(Basis.Identity, new Vector3(0, 0.55f, 0)));   // the sun-mirror above
        var bernal = new SurfaceTool();
        bernal.AppendFrom(new SphereMesh { Radius = 0.45f, Height = 0.9f, RadialSegments = 24, Rings = 12 }, 0, Transform3D.Identity);
        bernal.AppendFrom(new TorusMesh { InnerRadius = 0.6f, OuterRadius = 0.68f, Rings = 32, RingSegments = 6 }, 0, Transform3D.Identity);
        bernal.AppendFrom(new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.06f, Height = 1.5f, RadialSegments = 8 }, 0, Transform3D.Identity);
        for (int k = 1; k < 3; k++)
        {
            var hm = (k == 1 ? torus : bernal).Commit();
            hm.SurfaceSetMaterial(0, mat);
            var km = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = hm, InstanceCount = HabMax, VisibleInstanceCount = 0 };
            _habKinds[k] = new MultiMeshInstance3D { Multimesh = km, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, CustomAabb = Everywhere };
            _near.AddChild(_habKinds[k]);
        }
        // the Blender-built habitats (assets/stations/habitats.py), when they have been built
        for (int k = 0; k < 3; k++)
            if (HabitatAsset(HabSlug[k]) is ArrayMesh am) { _habKinds[k].Multimesh.Mesh = am; _habModel[k] = true; }
        _habLights = Glows(HabMax);
        _habLights.CustomAabb = Everywhere;
        _near.AddChild(_habLights);
    }

    static readonly string[] HabSlug = { "oneill", "torus", "bernal" };
    /// <summary>Kilometres per model unit: the O'Neill is modelled in hundreds of metres, the torus and sphere in tens.</summary>
    static readonly float[] HabUnitKm = { 0.1f, 0.01f, 0.01f };
    readonly bool[] _habModel = new bool[3];
    static readonly Dictionary<string, ArrayMesh> _habAssets = new();

    /// <summary>A habitat's glb folded into one mesh, a surface per material, its lit windows brightened to read from afar.</summary>
    static ArrayMesh HabitatAsset(string slug)
    {
        if (_habAssets.TryGetValue(slug, out var cached)) return cached;
        string path = $"res://assets/stations/{slug}/{slug}.glb";
        ArrayMesh mesh = null;
        if (ResourceLoader.Exists(path))
        {
            var inst = GD.Load<PackedScene>(path).Instantiate<Node3D>();
            var byMat = new Dictionary<Material, SurfaceTool>();
            foreach (var mi in inst.FindChildren("*", "MeshInstance3D", true, false).Cast<MeshInstance3D>())
            {
                var t = Transform3D.Identity;
                for (Node q = mi; q != inst && q is Node3D q3; q = q.GetParent()) t = q3.Transform * t;
                for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                {
                    var m = mi.GetActiveMaterial(i) ?? mi.Mesh.SurfaceGetMaterial(i);
                    if (m == null) continue;
                    if (!byMat.TryGetValue(m, out var st)) { st = new SurfaceTool(); st.Begin(Mesh.PrimitiveType.Triangles); byMat[m] = st; }
                    st.AppendFrom(mi.Mesh, i, t);
                }
            }
            mesh = new ArrayMesh();
            foreach (var (m, st) in byMat)
            {
                st.Commit(mesh);
                var mat = m;
                if (m is BaseMaterial3D bm && bm.EmissionEnabled) { var d = (BaseMaterial3D)bm.Duplicate(); d.EmissionEnergyMultiplier = Mathf.Max(d.EmissionEnergyMultiplier, 1f) * 3f; mat = d; }
                mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, mat);
            }
            inst.QueueFree();
        }
        return _habAssets[slug] = mesh;
    }

    // ---------- mass driver streams ----------
    void BuildStreams()
    {
        _streams = Glows(StreamMax);
        AddChild(_streams);
    }

    // ---------- Earth: elevator and orbital ring ----------
    void EnsureEarthWorks(BodyView earth)
    {
        if (_elevator != null || earth == null) return;
        float R = earth.B.Radius;
        var mat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.75f, 0.85f, 0.95f) };
        _elevator = new Node3D { Visible = false };
        var tether = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.03f * StarSystem.Km, BottomRadius = 0.03f * StarSystem.Km, Height = R * 2.2f, RadialSegments = 4 }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        tether.Position = new Vector3(R * 2.1f, 0, 0);
        tether.Rotation = new Vector3(0, 0, Mathf.Pi / 2);
        _elevator.AddChild(tether);
        var top = new MeshInstance3D { Mesh = StationMesh(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Scale = Vector3.One * 8f * StarSystem.Km, Position = new Vector3(R * 3.2f, 0, 0) };
        top.MaterialOverride = HullMat();
        _elevator.AddChild(top);
        var beacon = new OmniLight3D { LightColor = new Color(0.6f, 0.8f, 1f), OmniRange = 0.08f * R, LightEnergy = 0.6f, Position = new Vector3(R * 3.2f, 0, 0) };
        _elevator.AddChild(beacon);
        earth.Spin.AddChild(_elevator);

        _ringMat = new ShaderMaterial { Shader = RingShader() };
        _ring = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = R * 1.105f, OuterRadius = R * 1.105f + 8f * StarSystem.Km, Rings = 1024, RingSegments = 4 }, MaterialOverride = _ringMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
        earth.Tilted.AddChild(_ring);
    }

    static Shader _ringShader;
    public static Shader RingShader() => _ringShader ??= new Shader
    {
        Code = @"shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled, fog_disabled;
uniform float progress = 0.0;
uniform vec3 star_pos = vec3(0.0);
varying vec3 v_local; varying vec3 v_w;
void vertex(){ v_local = VERTEX; v_w = (MODEL_MATRIX * vec4(VERTEX,1.0)).xyz; }
void fragment(){
	float ang = (atan(v_local.z, v_local.x) / 6.2831853) + 0.5;
	if (ang > progress) discard;
	float lit = clamp(dot(normalize(star_pos - v_w), normalize(v_w - NODE_POSITION_WORLD)) * 2.0 + 0.6, 0.15, 1.0);
	float lights = step(0.85, fract(ang * 900.0)) * 2.5;
	ALBEDO = vec3(0.55, 0.6, 0.66) * lit + vec3(1.0, 0.8, 0.5) * lights * (1.2 - lit);
}"
    };

    // ---------- per frame ----------
    public void Sync(Colony c, Simulation sim) { _c = c; _sim = sim; }

    public static string Hide = "";
    public void Update(double year, float dt, Camera3D cam)
    {
        _clock += dt;
        _swarm.Visible = !Hide.Contains("swarm"); _haze.Visible = !Hide.Contains("haze");
        foreach (var cm in _classMesh) cm.Visible = !Hide.Contains("traffic");
        _shipGlow.Visible = _trails.Visible = !Hide.Contains("traffic");
        if (Hide.Contains("trails")) _trails.Visible = false;
        if (Hide.Contains("shipglow")) _shipGlow.Visible = false;
        _stations.Visible = _stationLights.Visible = !Hide.Contains("stations");
        foreach (var hk in _habKinds) hk.Visible = !Hide.Contains("habs");
        _habLights.Visible = !Hide.Contains("habs");
        _streams.Visible = !Hide.Contains("streams");
        var c = _c;
        // swarm
        double cap = c?.Capture ?? 0;
        if (Sys.Star.Owner >= 0 && c == null) cap = Sys.Star.Capture;
        int n = cap <= 0 ? 0 : (int)Mathf.Clamp(SwarmMax * Mathf.Pow((float)cap, 0.42f) * 1.05f, 30, SwarmMax);
        if (cap < 1e-9) n = 0;
        _swarm.Multimesh.VisibleInstanceCount = n;
        var (rin, rout) = SwarmShell();
        _swarmMat.SetShaderParameter("time", (float)year);
        _swarmMat.SetShaderParameter("r_in", rin);
        _swarmMat.SetShaderParameter("r_out", rout);
        var scol = Sys.StarColor;
        _swarmMat.SetShaderParameter("sun_color", new Vector3(scol.R, scol.G, scol.B));
        _swarmMat.SetShaderParameter("heat", Mathf.Clamp((float)cap * 1.4f, 0, 1));
        _swarmMat.SetShaderParameter("gap", c != null && c.EclipticOpen ? EclipticGap : 0f);
        _haze.Scale = Vector3.One * rout * 1.05f;
        _hazeMat.SetShaderParameter("strength", Mathf.Clamp((float)cap - 0.25f, 0, 1) * 1.2f);

        using (Prof.T("great")) _great.Update(c, dt, rout);
        using (Prof.T("closeup")) _close.Update(c, dt, cam);
        using (Prof.T("mega")) Mega.Apply();
        // fleets gather at any star, settled or not
        using (Prof.T("fleets")) UpdateFleets(c, year, dt, cam);
        if (c == null) { HideAll(); return; }
        using (Prof.T("traffic")) UpdateTraffic(c, year, dt, cam);
        using (Prof.T("stations")) UpdateStations(c, year);
        using (Prof.T("war")) UpdateWarProps(c, year);
        using (Prof.T("earth")) UpdateEarthWorks(c);
        using (Prof.T("habs")) UpdateHabitats(c, year);
        using (Prof.T("streams")) UpdateStreams(c, year, rin, rout);
        using (Prof.T("fleet")) UpdateFleet(c);
    }

    void HideAll()
    {
        if (_fleet != null) _fleet.Visible = false;
        foreach (var cm in _classMesh) cm.Multimesh.VisibleInstanceCount = 0;
        _shipGlow.Multimesh.VisibleInstanceCount = 0; _trails.Multimesh.VisibleInstanceCount = 0;
        _stations.Multimesh.VisibleInstanceCount = 0; _stationLights.Multimesh.VisibleInstanceCount = 0;
        _warProps.Multimesh.VisibleInstanceCount = 0;
        foreach (var hk in _habKinds) hk.Multimesh.VisibleInstanceCount = 0;
        _habLights.Multimesh.VisibleInstanceCount = 0;
        _streams.Multimesh.VisibleInstanceCount = 0;
        if (_elevator != null) _elevator.Visible = false;
        if (_ring != null) _ring.Visible = false;
    }

    Vector3 PosOf(Body b, double year) => PosOfD(b, year).F;
    static D3 PosOfD(Body b, double year)
    {
        if (b.Kind != BodyKind.Belt) return b.WorldPosD(year);
        double a = b.Phase + Math.Tau * year / b.Period;
        return new D3(Math.Cos(a) * b.Orbit, 0, -Math.Sin(a) * b.Orbit);
    }
    /// <summary>Where a system-coordinate point is drawn this frame, how much it shrinks there, and its true view position.</summary>
    Vector3 Drawn(D3 sys, out float squash, out Vector3 trueView)
    {
        trueView = Frame.View(_sv.Offset + sys);
        squash = Frame.Squash(trueView, out var d);
        return d;
    }

    void UpdateTraffic(Colony c, double year, float dt, Camera3D cam)
    {
        // routes: the seat to every settled world and every working mine, plus active convoys
        var routes = new List<(Body A, Body B, float W, int Kind)>();
        foreach (var (b, s) in c.Settled) if (b != c.Seat) routes.Add((c.Seat, b, 1f + s * 2f, 0));
        foreach (var (b, lvl) in c.Hubs) if (lvl > 0 && b.Mined < 1 && b != c.Seat) routes.Add((b, c.Seat, lvl * 0.8f, 1));
        foreach (var m in c.Missions) routes.Add((m.From ?? c.Seat, m.Target, 3f, 2));
        // the homeworld's own backyard is busy from the first year: crew rotations, tankers, tourists
        if (c.Home && c.Seat != null) foreach (var moon in c.Seat.Moons) routes.Add((c.Seat, moon, 3f, 0));
        if (c.Seat != null) routes.Add((c.Seat, c.Seat, 2f + c.Stations.Values.Sum() * 0.3f, 3));   // local orbital traffic
        // the starbase is the system's spaceport: shuttles climb to it from the seat, and long-haul ships call there
        if (Mega.Starbase != null && c.Seat != null)
        {
            routes.Add((c.Seat, c.Seat, 4f, 4));
            foreach (var b in c.Settled.Keys) if (b != c.Seat) routes.Add((b, c.Seat, 1.2f, 4));
        }
        // as spaceflight becomes ordinary, liners run between every pair of settled worlds
        var settled = c.Settled.Keys.Where(b => b != c.Seat).ToList();
        for (int i = 0; i < settled.Count; i++)
            for (int j = i + 1; j < settled.Count; j++)
                routes.Add((settled[i], settled[j], 0.4f * (c.Settled[settled[i]] + c.Settled[settled[j]]), 0));
        float mainstream = 1f + Math.Max(c.Era, 0) * 0.9f + (float)Math.Max(0, Math.Log10(Math.Max(c.Pop, 1) / 1e9)) * 0.6f;
        // ports closed, convoys holding: crises thin the lanes (missions still fly; they are the emergency)
        float open = (float)c.TrafficFactor;
        int want = (int)Mathf.Clamp((float)(routes.Sum(r => r.W * (r.Kind == 2 ? 1f : open)) * 9 * (0.4 + c.TrafficLevel) * mainstream), 0, ShipMax);
        if (OS.GetCmdlineUserArgs().Contains("--trace") && Engine.GetProcessFrames() % 60 == 0) GD.Print($"TRAFFIC open={open:0.00} want={want} flying={_ships.Count} crisis={c.Crisis?.Id}");
        // spawn
        float totalW = routes.Sum(r => r.W);
        int maxCls = Ships.MaxClass(c.Civ);
        int style = Ships.Style(c.Civ);
        if (style != _style)
        {
            // a different people's yards: swap every class's hull for theirs
            _style = style;
            for (int ci = 0; ci < Ships.Classes; ci++) _classMesh[ci].Multimesh.Mesh = Ships.Mesh(ci, style);
        }
        int guard = 0;
        while (_ships.Count < want && totalW > 0 && guard++ < 120)
        {
            float pick = (float)_r.NextDouble() * totalW;
            var rt = routes[0];
            foreach (var r in routes) { pick -= r.W; if (pick <= 0) { rt = r; break; } }
            bool back = _r.NextDouble() < 0.5 && rt.Kind != 2;
            if (rt.Kind == 4) back = _r.NextDouble() < 0.5;
            var s = new Ship { Out = rt.Kind == 4 && back, A = rt.Kind == 4 ? rt.A : back ? rt.B : rt.A, B = rt.Kind == 4 ? rt.B : back ? rt.A : rt.B, T0 = _clock - (_ships.Count < want / 2 ? _r.NextDouble() * 6 : 0), Kind = rt.Kind, Lift = (float)(_r.NextDouble() - 0.5) * 0.3f, Side = (float)(_r.NextDouble() - 0.5) * 0.25f };
            s.Cls = rt.Kind == 2 ? 0 : Ships.Pick(_r, maxCls, rt.Kind == 3);
            s.Size = Ships.Size(s.Cls) * (0.85f + 0.3f * (float)_r.NextDouble());
            s.Dur = rt.Kind == 3 ? 3 + _r.NextDouble() * 4 : 5 + _r.NextDouble() * 6;
            _ships.Add(s);
        }
        var gm = _shipGlow.Multimesh; var tm = _trails.Multimesh;
        Array.Clear(_classN);
        int k = 0;
        var camV = Frame.Cam;
        for (int i = _ships.Count - 1; i >= 0; i--)
        {
            var s = _ships[i];
            float u = (float)((_clock - s.T0) / s.Dur);
            if (u >= 1 || _ships.Count > want + 20 && u < 0.05f) { _ships.RemoveAt(i); continue; }
            if (u < 0) continue;
            D3 p; Vector3 fwd;
            float routeLen;
            if (s.Kind == 3)
            {
                // orbiting traffic round the seat, from low orbit out past geostationary
                var c0 = PosOfD(s.A, _sv.VisYear);
                float r = s.A.Radius * (1.04f + Mathf.Abs(s.Lift) * 36f);
                float a = u * Mathf.Tau * (s.Side > 0 ? 1 : -1) + s.Side * 20f;
                var axis = new Vector3(s.Lift, 1, s.Side).Normalized();
                var rad = new Basis(axis, a) * new Vector3(r, 0, 0).Rotated(Vector3.Right, s.Lift * 2);
                p = c0 + rad;
                fwd = axis.Cross(rad).Normalized() * (s.Side > 0 ? 1 : -1);
                routeLen = r * 6f;
            }
            else
            {
                D3 pa = PosOfD(s.A, _sv.VisYear), pb = PosOfD(s.B, _sv.VisYear);
                bool dock = s.Kind == 4 && Mega.Starbase != null;
                if (dock) { pb = Mega.Starbase.Value; if (s.Out) (pa, pb) = (pb, pa); }
                double e = u * u * (3 - 2 * u);
                var dir = (pb - pa).F;
                float len = dir.Length();
                var side = dir.Cross(Vector3.Up).Normalized();
                var arc = (pa + pb) * 0.5 + (side * len * s.Side + Vector3.Up * len * s.Lift * 0.3f);
                // quadratic bezier from just above one world to just above the other
                var start = dock && s.Out ? pa : pa + (arc - pa).F.Normalized() * s.A.Radius * 1.05f;
                var end = dock && !s.Out ? pb : pb + (arc - pb).F.Normalized() * s.B.Radius * 1.05f;
                p = start * ((1 - e) * (1 - e)) + arc * (2 * (1 - e) * e) + end * (e * e);
                fwd = ((arc - start) * (2 * (1 - e)) + (end - arc) * (2 * e)).F.Normalized();
                routeLen = len;
            }
            if (fwd.LengthSquared() < 1e-6f) fwd = Vector3.Forward;
            var at = Drawn(p, out float sq, out var trueV);
            float dcam = camV.DistanceTo(trueV);
            var basisS = Basis.LookingAt(-fwd, Mathf.Abs(fwd.Y) > 0.98f ? Vector3.Right : Vector3.Up).Scaled(Vector3.One * (s.Size * sq));
            if (_classN[s.Cls] * 12 < _classBuf[s.Cls].Length) Put(_classBuf[s.Cls], _classN[s.Cls]++ * 12, basisS, at);
            Color ec = s.Kind == 1 ? new Color(1f, 0.6f, 0.3f) : s.Kind == 2 ? new Color(0.7f, 0.85f, 1f) : new Color(0.55f, 0.75f, 1f);
            if (c.Civ.Alien) ec = c.Civ.Color.Lerp(Colors.White, s.Kind == 2 ? 0.4f : 0.1f);
            if (c.Civ.Exalted) ec = new Color(1f, 0.82f, 0.4f);              // an Exalted civ's ships burn gold
            float burn = s.Kind == 3 ? 0.35f : (u < 0.15f || u > 0.85f ? 1f : 0.45f);
            int go = k * 20;
            // the drive's glow: a point of light that never shrinks below a couple of pixels, so traffic reads from far off
            Put(_engBuf, go, Basis.Identity.Scaled(Vector3.One * s.Size * 0.6f * sq), at - fwd * s.Size * 1.05f * sq);
            _engBuf[go + 12] = ec.R; _engBuf[go + 13] = ec.G; _engBuf[go + 14] = ec.B; _engBuf[go + 15] = burn;
            _engBuf[go + 16] = s.Kind == 2 ? 1f : 0.35f + s.Cls * 0.06f; _engBuf[go + 17] = s.Kind == 3 ? 0.15f : 0.45f; _engBuf[go + 18] = _engBuf[go + 19] = 0;
            // a faint wake behind the ship, as wide on screen at any distance, and gone when the hull itself fills the view
            float tlen = Mathf.Min(dcam * 0.03f, routeLen * 0.06f);
            float ends = s.Kind == 3 ? 1f : Mathf.SmoothStep(0.04f, 0.18f, u) * Mathf.SmoothStep(0.96f, 0.82f, u);
            float tw = Mathf.Max(dcam * 0.0012f, s.Size * 0.5f);
            var toCam = (camV - trueV).Normalized();
            var wside = fwd.Cross(toCam).Normalized();
            if (wside.LengthSquared() < 0.01f) wside = Vector3.Up;
            int to = k * 16;
            Put(_wakeBuf, to, new Basis(wside * tw * sq, fwd * tlen * sq, wside.Cross(fwd) * sq), at - fwd * (tlen * 0.5f + s.Size) * sq);
            _wakeBuf[to + 12] = ec.R; _wakeBuf[to + 13] = ec.G; _wakeBuf[to + 14] = ec.B; _wakeBuf[to + 15] = burn * 0.22f * ends * Mathf.SmoothStep(s.Size * 40f, s.Size * 400f, dcam);
            if (++k >= ShipMax) break;
        }
        for (int ci = 0; ci < Ships.Classes; ci++)
        {
            var cm = _classMesh[ci].Multimesh;
            if (_classN[ci] > 0) cm.Buffer = _classBuf[ci];
            cm.VisibleInstanceCount = _classN[ci];
        }
        gm.Buffer = _engBuf; tm.Buffer = _wakeBuf;
        gm.VisibleInstanceCount = k;
        tm.VisibleInstanceCount = k;
    }

    static void Put(float[] b, int o, Basis bs, Vector3 p)
    {
        b[o] = bs.X.X; b[o + 1] = bs.Y.X; b[o + 2] = bs.Z.X; b[o + 3] = p.X;
        b[o + 4] = bs.X.Y; b[o + 5] = bs.Y.Y; b[o + 6] = bs.Z.Y; b[o + 7] = p.Y;
        b[o + 8] = bs.X.Z; b[o + 9] = bs.Y.Z; b[o + 10] = bs.Z.Z; b[o + 11] = p.Z;
    }

    void UpdateStations(Colony c, double year)
    {
        // orbital stations: half a kilometre to two across, from low orbit outward
        var mm = _stations.Multimesh; var lm = _stationLights.Multimesh;
        int k = 0;
        foreach (var (b, count) in c.Stations)
        {
            var p0 = PosOfD(b, _sv.VisYear);
            for (int i = 0; i < count && k < StationMax; i++)
            {
                float r = b.Radius * (1.06f + (i % 4) * 0.08f + (i / 4) * 0.5f);
                float speed = 2.2f / Mathf.Pow(r / b.Radius, 1.5f);
                float a = i * 2.39996f + _clock * 0.05f * speed;
                var incl = new Basis(new Vector3(1, 0, 0.3f * (i % 3 - 1)).Normalized(), 0.35f * ((i * 7) % 5 - 2) / 2f);
                var at = Drawn(p0 + incl * new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r), out float sq, out _);
                float sz = (0.5f + 0.4f * (i % 3)) * StarSystem.Km * sq;           // the mesh's ring has radius 1
                mm.SetInstanceTransform(k, new Transform3D(new Basis(Vector3.Up, _clock * 0.3f + i).Scaled(Vector3.One * sz), at));
                bool blink = Mathf.Sin(_clock * 3f + i * 1.7f) > 0.7f;
                lm.SetInstanceTransform(k, new Transform3D(Basis.Identity.Scaled(Vector3.One * sz * 0.35f), at));
                var lc = c.Civ.Alien ? c.Civ.Color : new Color(1f, 0.8f, 0.55f);
                lm.SetInstanceColor(k, blink ? new Color(lc.R, lc.G, lc.B, 1f) : new Color(lc.R, lc.G, lc.B, 0.35f));
                lm.SetInstanceCustomData(k, new Color(0.1f, 0, 0, 0));
                k++;
            }
        }
        mm.VisibleInstanceCount = k; lm.VisibleInstanceCount = k;
    }

    void UpdateEarthWorks(Colony c)
    {
        if (!c.Home) return;
        var earth = _sv.Bodies.FirstOrDefault(b => b.B == c.Seat);
        EnsureEarthWorks(earth);
        if (_elevator == null) return;
        _elevator.Visible = c.ElevatorP >= 1;
        _ring.Visible = c.RingP > 0;
        _ringMat.SetShaderParameter("progress", Mathf.Clamp(c.RingP, 0, 1));
        _ringMat.SetShaderParameter("star_pos", earth.GlobalPosition + (_sv.TrueStar - earth.TrueView) * earth.Squash);
    }

    void UpdateHabitats(Colony c, double year)
    {
        // true sizes: an O'Neill cylinder is 32 km long, a Stanford torus 1.8 km across, a Bernal sphere 500 m
        var lm = _habLights.Multimesh;
        Span<int> kn = stackalloc int[3];
        int n = Math.Min(c.Habitats, HabMax);
        var seat = c.Seat;
        for (int i = 0; i < n; i++)
        {
            // first at the seat world's L4/L5 points, later scattered through the inner system
            D3 p;
            int kind = i % 3;
            if (i < 24 && seat != null && seat.Parent == null)
            {
                double lead = (i % 2 == 0 ? 1 : -1) * Math.PI / 3;
                double a = seat.Phase + Math.Tau * _sv.VisYear / seat.Period + lead;
                var r = new Random(i * 31 + 7);
                var center = new D3(Math.Cos(a) * seat.Orbit, 0, Math.Sin(a) * seat.Orbit);
                p = center + new Vector3((float)r.NextDouble() - 0.5f, ((float)r.NextDouble() - 0.5f) * 0.4f, (float)r.NextDouble() - 0.5f) * (seat.Orbit * 0.004f);
            }
            else
            {
                var r = new Random(i * 17 + 3);
                float rad = SwarmShell().Out * (1.2f + (float)r.NextDouble() * 1.5f);
                double a = r.NextDouble() * Math.Tau + _sv.VisYear * 2 * Math.PI / Math.Pow(rad / StarSystem.AU, 1.5);
                p = new D3(Math.Cos(a) * rad, ((float)r.NextDouble() - 0.5f) * rad * 0.04f, Math.Sin(a) * rad);
            }
            var at = Drawn(p, out float sq, out _);
            float sz = (_habModel[kind] ? HabUnitKm[kind] : kind == 0 ? 32f / 1.6f : kind == 1 ? 0.9f / 0.8f : 0.25f / 0.45f) * StarSystem.Km * sq;
            var basis = new Basis(new Vector3(0, 0, 1), 0.4f) * new Basis(Vector3.Up, _clock * 0.5f + i);
            _habKinds[kind].Multimesh.SetInstanceTransform(kn[kind]++, new Transform3D(basis.Scaled(Vector3.One * sz), at));
            lm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * sz * 0.4f), at));
            lm.SetInstanceColor(i, new Color(1f, 0.8f, 0.55f, 0.6f));
            lm.SetInstanceCustomData(i, new Color(0.3f, 0, 0, 0));
        }
        for (int k = 0; k < 3; k++) _habKinds[k].Multimesh.VisibleInstanceCount = kn[k];
        lm.VisibleInstanceCount = n;
    }

    /// <summary>The colony's flotilla in orbit around its seat: the ark that brought them, and the frigates they have built.</summary>
    void UpdateFleet(Colony c)
    {
        if (c.Seat == null) return;
        if (_fleet == null)
        {
            _fleet = new Node3D { TopLevel = true };
            AddChild(_fleet);
            _ark = Spectacle.SeedLaunch.BuildSeedShip();
            _fleet.AddChild(_ark);
            var hull = ShipHull();
            hull.SurfaceSetMaterial(0, HullMat());
            for (int i = 0; i < 6; i++)
            {
                var f = new MeshInstance3D { Mesh = hull, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                _fleet.AddChild(f);
                _frigates.Add(f);
            }
        }
        _fleet.Visible = true;
        float R = c.Seat.Radius;
        float a = _clock * 0.04f;
        var orbit = new Vector3(Mathf.Cos(a), 0.12f, Mathf.Sin(a)) * R * 3.2f;
        var fwd = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
        _fleet.Basis = Basis.LookingAt(fwd, Vector3.Up);
        Frame.Place(_fleet, Frame.View(_sv.Offset + PosOfD(c.Seat, _sv.VisYear) + orbit));
        // the ark that brought them (about 20 km), and the frigates they have built (300 m), a few kilometres apart
        _ark.Visible = !c.Home;
        _ark.Scale = Vector3.One * 10f * StarSystem.Km;
        for (int i = 0; i < _frigates.Count; i++)
        {
            bool on = i < c.FrigateNames.Count;
            _frigates[i].Visible = on;
            if (!on) continue;
            float side = (i % 2 == 0 ? 1 : -1) * (6f + 4f * (i / 2));
            _frigates[i].Position = new Vector3(side, 0.8f * (i % 3), 6f + 5f * (i / 2)) * StarSystem.Km;
            _frigates[i].Scale = Vector3.One * Ships.Size(2);
        }
    }

    void UpdateStreams(Colony c, double year, float rin, float rout)
    {
        var mm = _streams.Multimesh;
        if (!c.Civ.M.Swarm || c.Capture >= 0.9) { mm.VisibleInstanceCount = 0; return; }
        var hubs = c.Hubs.Where(h => h.Value > 0 && h.Key.Mined < 1).ToList();
        int k = 0;
        int per = hubs.Count == 0 ? 0 : Math.Min(StreamMax / hubs.Count, 260 * hubs.Max(h => h.Value));
        foreach (var (b, lvl) in hubs)
        {
            var from = PosOf(b, _sv.VisYear);
            int count = Math.Min(per, 140 * lvl);
            for (int i = 0; i < count && k < StreamMax; i++)
            {
                // packets fall sunward on a gentle spiral into the swarm shell
                float ph = Mathf.PosMod(i * 0.6180339f + _clock * (0.05f + 0.02f * (i % 5)), 1f);
                float ang = i * 2.3999632f;
                float targetR = rin + (rout - rin) * ((i * 0.37f) % 1f);
                var dir = from.Normalized();
                var tgt = dir.Rotated(Vector3.Up, 0.9f + (i % 7) * 0.1f) * targetR + Vector3.Up * Mathf.Sin(ang) * (rout - rin) * 0.3f;
                var mid = (from + tgt) * 0.5f + from.Cross(Vector3.Up).Normalized() * from.Length() * 0.25f;
                float e = ph;
                var p = (1 - e) * (1 - e) * from + 2 * (1 - e) * e * mid + e * e * tgt;
                mm.SetInstanceTransform(k, new Transform3D(Basis.Identity.Scaled(Vector3.One * 0.012f), p));
                mm.SetInstanceColor(k, new Color(1f, 0.75f, 0.45f, 0.35f * (1 - Mathf.Abs(ph * 2 - 1) * 0.6f)));
                mm.SetInstanceCustomData(k, new Color(0f, 0, 0, 0));
                k++;
            }
        }
        mm.VisibleInstanceCount = k;
    }
}
