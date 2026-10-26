using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>Renders one star system around the world origin: star, worlds, atmospheres, rings, moons, belts, orbits.</summary>
public partial class SystemView : Node3D
{
    public StarSystem Sys;
    /// <summary>Where this system's star is, in the viewed system's coordinates (zero unless it is the one being left behind).</summary>
    public Sim.D3 Offset;
    public readonly List<BodyView> Bodies = new();
    public readonly Dictionary<Body, BodyView> ByBody = new();
    MeshInstance3D _star, _corona, _glare;
    ShaderMaterial _starMat, _coronaMat, _glareMat;
    DirectionalLight3D _light;
    /// <summary>The star's true view-space position this frame, and how much the system node is squashed (see Frame).</summary>
    public Vector3 TrueStar; public float StarScale = 1f;
    /// <summary>The star's light on this system (not on fleet hulls, which Works lights with a softer copy).</summary>
    public DirectionalLight3D SunLight => _light;
    public float StarDim = 1f;
    /// <summary>The swarm leaves the planets' plane open (inhabited worlds): they stay lit, and the star shines for anyone in that plane.</summary>
    public bool LaneOpen;
    /// <summary>How bright the star looks from where the camera is: dimmed by the swarm, except through the open band.</summary>
    public float ViewDim = 1f;
    public float SurfaceBright = 6f;
    MeshInstance3D _second, _secondGlare; ShaderMaterial _secondMat, _secondGlareMat;
    float _time;
    public double VisYear;
    public Works Works;
    public Spectacle Spectacle;

    static readonly Dictionary<string, Texture2D> TexCache = new();
    public static Texture2D Tex(string key)
    {
        if (key == null) return null;
        if (TexCache.TryGetValue(key, out var t)) return t;
        foreach (var ext in new[] { ".jpg", ".png" })
        {
            string path = "res://textures/" + key + ext;
            if (ResourceLoader.Exists(path)) { t = GD.Load<Texture2D>(path); break; }
        }
        TexCache[key] = t;
        return t;
    }

    static Shader _planetShader, _atmoShader, _cloudShader, _ringShader, _orbitShader;
    static Shader S(ref Shader s, string path) => s ??= GD.Load<Shader>(path);

    public void Build(StarSystem sys)
    {
        Sys = sys;
        var sc = sys.StarColor;
        // the eye sees cool stars as orange and red, not the pale peach of a raw black-body curve
        float tk0 = sys.Star.Temp;
        if (tk0 < 5200f && sys.StarTex == null)
        {
            float k = Mathf.Clamp((5200f - tk0) / 2000f, 0f, 1f);
            sc = new Color(1f, Mathf.Pow(sc.G, 1f + 1.2f * k), Mathf.Pow(sc.B, 1f + 2.2f * k));
        }
        var starCol = new Vector3(sc.R, sc.G, sc.B);
        SurfaceBright = Mathf.Clamp(6f * Mathf.Pow(tk0 / 5772f, 2.5f), 0.9f, 14f);
        if (sys.Star.Dead) SurfaceBright = 0.18f;           // a cinder: put out by the voice, or the dormant Shard

        _starMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/sun.gdshader") };
        var stex = Tex(sys.StarTex);
        _starMat.SetShaderParameter("textured", stex != null);
        if (stex != null) _starMat.SetShaderParameter("tex", stex);
        _starMat.SetShaderParameter("color", starCol);
        // cool stars are dimmer per square metre and visibly coloured; hot ones blaze blue-white
        float tK = sys.Star.Temp;
        _starMat.SetShaderParameter("brightness", SurfaceBright);
        _starMat.SetShaderParameter("cell_scale", sys.Giant ? 2.5f : 9f);
        _star = new MeshInstance3D { Mesh = new SphereMesh { Radius = sys.StarRadius, Height = sys.StarRadius * 2, RadialSegments = 128, Rings = 64 }, MaterialOverride = _starMat };
        _star.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(_star);

        _coronaMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/corona.gdshader"), RenderPriority = 5 };
        _coronaMat.SetShaderParameter("color", starCol);
        _coronaMat.SetShaderParameter("star_r", sys.StarRadius);
        _corona = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(2, 2) }, MaterialOverride = _coronaMat, Scale = Vector3.One * sys.StarRadius * 7f };
        _corona.ExtraCullMargin = 16384;
        AddChild(_corona);

        _glareMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/glare.gdshader"), RenderPriority = 6 };
        _glareMat.SetShaderParameter("color", starCol);
        _glare = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(1, 1) }, MaterialOverride = _glareMat };
        _glare.ExtraCullMargin = 16384;
        AddChild(_glare);

        if (sys.Second != null)
        {
            var c2 = sys.Second;
            var cc = c2.Color;
            if (c2.Temp < 5200f) { float k = Mathf.Clamp((5200f - c2.Temp) / 2000f, 0, 1); cc = new Color(1f, Mathf.Pow(cc.G, 1f + 1.2f * k), Mathf.Pow(cc.B, 1f + 2.2f * k)); }
            _secondMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/sun.gdshader") };
            _secondMat.SetShaderParameter("color", new Vector3(cc.R, cc.G, cc.B));
            _secondMat.SetShaderParameter("brightness", c2.Bright);
            _secondMat.SetShaderParameter("cell_scale", 7f);
            _second = new MeshInstance3D { Mesh = new SphereMesh { Radius = c2.Radius, Height = c2.Radius * 2, RadialSegments = 64, Rings = 32 }, MaterialOverride = _secondMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(_second);
            _secondGlareMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/glare.gdshader"), RenderPriority = 6 };
            _secondGlareMat.SetShaderParameter("color", new Vector3(cc.R, cc.G, cc.B));
            _secondGlare = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(1, 1) }, MaterialOverride = _secondGlareMat, ExtraCullMargin = 16384 };
            AddChild(_secondGlare);
        }

        // the star lights what the camera is near: a directional light from the star toward the view's focus
        // (worlds light themselves in their shaders; this is for ships, stations and megastructures)
        // fleet hulls have their own copy of this light (Works.FleetLayer), set softer so their paint keeps its shading
        _light = new DirectionalLight3D { LightColor = new Color(sc.R, sc.G, sc.B), LightEnergy = 1.4f, ShadowEnabled = false, LightCullMask = 0xFFFFFu & ~Works.FleetLayer };
        AddChild(_light);

        Works = new Works();
        AddChild(Works);
        Works.Setup(this);
        Spectacle = new Spectacle();
        AddChild(Spectacle);
        Spectacle.Setup(this);

        foreach (var b in sys.Bodies)
        {
            var bv = new BodyView();
            AddChild(bv);
            bv.Build(this, b, starCol);
            Bodies.Add(bv);
            ByBody[b] = bv;
            foreach (var m in b.Moons)
            {
                var mv = new BodyView();
                AddChild(mv);
                mv.Build(this, m, starCol);
                Bodies.Add(mv);
                ByBody[m] = mv;
            }
        }
    }

    /// <summary>Before the camera moves: things the camera may be following settle where they are this frame.</summary>
    public void Prepare(float dt)
    {
        var c = Colony;
        double cap = c?.Capture ?? 0;
        using (Prof.T("mega.update")) Works.Mega.Update(c, dt, Works.SwarmShell().Out);
        using (Prof.T("spectacle")) Spectacle.Tick(dt, Colony);         // ships the camera may follow move before it does
    }

    public void Update(double year, float dt, Camera3D cam)
    {
        VisYear = year;
        // the system hangs off its star, squashed into the depth range if it is far; worlds place themselves (BodyView)
        TrueStar = Frame.View(Offset);
        StarScale = Frame.Squash(TrueStar, out var drawn);
        Position = drawn;
        Scale = Vector3.One * StarScale;
        var toFocus = -TrueStar;
        if (toFocus.LengthSquared() > 1e-6f) _light.GlobalBasis = Basis.LookingAt(toFocus.Normalized(), Mathf.Abs(toFocus.Normalized().Y) > 0.99f ? Vector3.Right : Vector3.Up);
        _light.Visible = Offset.X == 0 && Offset.Y == 0 && Offset.Z == 0;     // a system being left behind doesn't light the new one
        _coronaMat.SetShaderParameter("star_r", Sys.StarRadius * StarScale);
        Spectacle.Position = TrueStar;
        _time += dt;
        _starMat.SetShaderParameter("time", _time);
        // seen through the open band (if there is one) the star is bare; above or below it, the swarm hides it
        var fromStar = cam.GlobalPosition - TrueStar;
        float lat = Mathf.Abs(Mathf.Asin(Mathf.Clamp(fromStar.Y / Mathf.Max(fromStar.Length(), 1e-6f), -1f, 1f)));
        float through = LaneOpen ? Mathf.SmoothStep(Works.EclipticGap * 1.3f, Works.EclipticGap * 0.7f, lat) : 0f;
        ViewDim = Mathf.Lerp(StarDim, 1f, through);
        _starMat.SetShaderParameter("dim", Mathf.Max(ViewDim, 0.07f));
        _coronaMat.SetShaderParameter("time", _time);
        _coronaMat.SetShaderParameter("dim", ViewDim * 0.45f * Mathf.Clamp(SurfaceBright / 6f, 0.4f, 1.4f));
        float d = cam.GlobalPosition.DistanceTo(TrueStar);
        // glare: dominant from afar, gone when the disk fills the view
        float diskPx = Sys.StarRadius / Mathf.Max(d, 0.01f) * 900f;
        // from interstellar range a single star should be a modest point, not a flare
        float far = Mathf.Clamp(Mathf.Log(Mathf.Max(d, 1f) / 3000f) / Mathf.Log(10f) / 4f, 0f, 1f);
        _glareMat.SetShaderParameter("px", Mathf.Lerp(Mathf.Clamp(260f - diskPx * 0.8f, 40f, 260f), 26f, far));
        _glareMat.SetShaderParameter("strength", Mathf.Clamp(1.0f - diskPx / 60f, 0.0f, 1.0f) * ViewDim * Mathf.Lerp(1f, 0.35f, far) * Mathf.Clamp(SurfaceBright / 6f, 0.35f, 1.6f) * (Sys.Star.Dead ? 0.05f : 1f));
        _glareMat.SetShaderParameter("sky_k", cam.Far * 0.5f);
        _light.LightEnergy = 1.4f * Mathf.Lerp(0.3f, 1f, LaneOpen ? 1f : StarDim) * (Sys.Star.Dead ? 0.05f : 1f);
        if (_second != null)
        {
            var p2 = Sys.Second.Pos(year);
            _second.Position = p2;
            _second.Rotation = new Vector3(0, _time * 0.01f, 0);
            _secondMat.SetShaderParameter("time", _time);
            _secondGlare.Position = p2;
            float d2 = cam.GlobalPosition.DistanceTo(TrueStar + p2);
            float disk2 = Sys.Second.Radius / Mathf.Max(d2, 0.01f) * 900f;
            _secondGlareMat.SetShaderParameter("px", Mathf.Clamp(140f - disk2 * 0.8f, 24f, 140f));
            _secondGlareMat.SetShaderParameter("strength", Mathf.Clamp(1f - disk2 / 60f, 0f, 1f) * Mathf.Clamp(Sys.Second.Bright / 6f, 0.3f, 1.4f));
            _secondGlareMat.SetShaderParameter("sky_k", cam.Far * 0.5f);
        }
        using (Prof.T("bodies")) foreach (var bv in Bodies) bv.Update(year, dt, cam);
        Works.Update(year, dt, cam);
    }

    /// <summary>Copy the simulation's state for this system onto its visuals.</summary>
    public void Sync(Colony c, Simulation sim)
    {
        Colony = c;
        Works.Sync(c, sim);
        StarDim = c == null ? 1f : (float)Math.Clamp(1 - c.Capture * 0.92, 0.06, 1);
        LaneOpen = c != null && c.EclipticOpen;
        foreach (var bv in Bodies)
        {
            if (c == null) { if (bv.B.Name != "Earth") bv.City = 0; bv.Terraform = bv.B.Greening; continue; }
            bv.Terraform = Mathf.Max(bv.B.Greening, bv.B.Name == "Mars" && c.Home ? Mathf.Max(0, c.TerraformP) : 0);
            bv.CityColor = c.Civ.Alien ? c.Civ.Color.Lerp(Colors.White, 0.25f) : new Color(1f, 0.7f, 0.38f);
            float s = c.Settled.TryGetValue(bv.B, out var v) ? v * Mathf.Clamp((float)(c.PopCap > 0 ? 1 : 0) * WorldScale(bv.B), 0.12f, 1f) : 0;
            if (c.Natives.TryGetValue(bv.B, out var n) && n.Contact != "cleared" && n.Level >= 2) s = Math.Max(s, 0.25f * (n.Level - 1));
            bv.City = bv.B.Name == "Earth" && c.Home ? 1f + (float)Math.Min(1.5, c.Pop / 20) : s;
        }
    }
    public Colony Colony;
    static float WorldScale(Body b) => b.Name switch { "Moon" => 0.15f, "Mars" => 0.55f, "Venus" => 0.35f, "Main Belt" => 0.2f, _ => b.Kind is BodyKind.Moon ? 0.18f : b.Life ? 1f : 0.45f };

    public float LightAt(float dist) => Mathf.Clamp(Mathf.Pow(StarSystem.AU * Mathf.Sqrt(Mathf.Max(Sys.Star.Lum, 1e-4f)) / Mathf.Max(dist, 1f), 0.5f), 0.3f, 2.2f) * Mathf.Lerp(0.3f, 1f, LaneOpen ? 1f : StarDim);

    // ---------- shared mesh helpers ----------
    public static ArrayMesh Annulus(float rin, float rout, int seg)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < seg; i++)
        {
            float a0 = Mathf.Tau * i / seg, a1 = Mathf.Tau * (i + 1) / seg;
            Vector3 i0 = new(Mathf.Cos(a0) * rin, 0, Mathf.Sin(a0) * rin), o0 = new(Mathf.Cos(a0) * rout, 0, Mathf.Sin(a0) * rout);
            Vector3 i1 = new(Mathf.Cos(a1) * rin, 0, Mathf.Sin(a1) * rin), o1 = new(Mathf.Cos(a1) * rout, 0, Mathf.Sin(a1) * rout);
            void V(Vector3 p, float u) { st.SetNormal(Vector3.Up); st.SetUV(new Vector2(u, 0.5f)); st.AddVertex(p); }
            V(i0, 0); V(o0, 1); V(o1, 1);
            V(i0, 0); V(o1, 1); V(i1, 0);
        }
        return st.Commit();
    }

    public static ArrayMesh OrbitLoop(float radius, int seg)
    {
        var verts = new Vector3[seg + 1];
        for (int i = 0; i <= seg; i++) { float a = Mathf.Tau * i / seg; verts[i] = new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius); }
        var arr = new Godot.Collections.Array();
        arr.Resize((int)Mesh.ArrayType.Max);
        arr[(int)Mesh.ArrayType.Vertex] = verts;
        var m = new ArrayMesh();
        m.AddSurfaceFromArrays(Mesh.PrimitiveType.LineStrip, arr);
        return m;
    }

    static ShaderMaterial _orbitMat;
    public static ShaderMaterial OrbitMat()
    {
        if (_orbitMat != null) return _orbitMat;
        var sh = new Shader
        {
            Code = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
uniform vec3 color = vec3(0.45, 0.62, 0.8);
uniform float alpha = 0.07;
varying float v_d;
void vertex(){ v_d = length((MODEL_MATRIX * vec4(VERTEX,1.0)).xyz - CAMERA_POSITION_WORLD); }
void fragment(){ ALBEDO = color * alpha * clamp(v_d * 0.02, 0.0, 1.0); }"
        };
        _orbitMat = new ShaderMaterial { Shader = sh };
        return _orbitMat;
    }
    public static ShaderMaterial PlanetShader() => new() { Shader = S(ref _planetShader, "res://shaders/planet.gdshader") };
    public static ShaderMaterial AtmoShader() => new() { Shader = S(ref _atmoShader, "res://shaders/atmosphere.gdshader"), RenderPriority = 2 };
    public static ShaderMaterial CloudShader() => new() { Shader = S(ref _cloudShader, "res://shaders/clouds.gdshader"), RenderPriority = 1 };
    public static ShaderMaterial RingShader() => new() { Shader = S(ref _ringShader, "res://shaders/ring.gdshader"), RenderPriority = 3 };
}

/// <summary>One world: surface, clouds, atmosphere, ring, orbit line.</summary>
public partial class BodyView : Node3D
{
    public Body B;
    public Node3D Spin;              // tilted, rotating
    public Node3D Tilted;            // tilted, not rotating (rings)
    /// <summary>The world's true view-space position this frame, and how much it is squashed to fit the depth range (see Frame).</summary>
    public Vector3 TrueView; public float Squash = 1f;
    ShaderMaterial _surf, _clouds, _atmo, _ring;
    MeshInstance3D _orbit;
    SystemView _sv;
    float _cloudOff;
    MultiMeshInstance3D _belt, _beltDust;
    public float City;               // night lights intensity, driven by the sim
    public Color CityColor = new(1f, 0.7f, 0.38f);
    public float Terraform;
    float _shownTerra, _shownIgnite, _orbitBuilt = -1;
    OmniLight3D _fireLight; MeshInstance3D _fireGlow;

    /// <summary>An ignited gas giant lights its moons and glows from across the system.</summary>
    void EnsureFire(float k)
    {
        if (_fireLight == null)
        {
            _fireLight = new OmniLight3D { LightColor = new Color(1f, 0.6f, 0.3f), OmniRange = B.Radius * 40f, OmniAttenuation = 1.2f, ShadowEnabled = false };
            AddChild(_fireLight);
            _fireGlow = Spectacle.Sprite(new Color(1f, 0.4f, 0.12f), B.Radius * 3f, 0.6f);
            AddChild(_fireGlow);
        }
        _fireLight.LightEnergy = 3f * k;
        _fireGlow.Scale = Vector3.One * B.Radius * 3f * k;
    }
    double _lastYear = double.NaN, _spinPh = double.NaN;
    ShaderMaterial[] _mats = System.Array.Empty<ShaderMaterial>();
    MeshInstance3D _surfMesh, _atmoMesh, _cloudMesh;
    MultiMeshInstance3D _debris;

    public void Build(SystemView sv, Body b, Vector3 starCol)
    {
        _sv = sv;
        B = b;
        Name = b.Name;
        TopLevel = true;                 // placed in double precision about the view's origin, not under the system node
        var tiltBasis = new Basis(Vector3.Right, b.Tilt);
        Tilted = new Node3D { Basis = tiltBasis };
        AddChild(Tilted);
        Spin = new Node3D { Basis = tiltBasis };
        AddChild(Spin);

        // orbit line (moons parent to their planet via Update)
        _orbit = new MeshInstance3D { Mesh = SystemView.OrbitLoop(b.Orbit, b.Parent == null ? 2048 : 256), MaterialOverride = SystemView.OrbitMat() };
        _orbit.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        sv.AddChild(_orbit);

        if (b.Kind == BodyKind.Belt) { BuildBelt(b); return; }

        _surf = SystemView.PlanetShader();
        var tex = SystemView.Tex(b.Tex);
        int mode = tex != null ? 0 : b.Kind switch
        {
            BodyKind.Gas or BodyKind.IceGiant => 2, BodyKind.Ocean => 3, BodyKind.Lava => 4, BodyKind.Ice => 5,
            BodyKind.Terran => 6, BodyKind.Desert => 7, _ => 1
        };
        _surf.SetShaderParameter("mode", mode);
        if (tex != null) _surf.SetShaderParameter("day_tex", tex);
        _surf.SetShaderParameter("col_a", new Vector3(b.A.R, b.A.G, b.A.B));
        _surf.SetShaderParameter("col_b", new Vector3(b.B.R, b.B.G, b.B.B));
        _surf.SetShaderParameter("seed", (b.ProcSeed % 1000) * 0.137f);
        _surf.SetShaderParameter("sun_color", starCol);
        _surf.SetShaderParameter("atmo_tint", new Vector3(b.Atmo.R, b.Atmo.G, b.Atmo.B));
        if (b.Name == "Earth")
        {
            _surf.SetShaderParameter("has_night", true); _surf.SetShaderParameter("night_tex", SystemView.Tex("8k_earth_nightmap"));
            _surf.SetShaderParameter("has_spec", true); _surf.SetShaderParameter("spec_tex", SystemView.Tex("8k_earth_specular_map"));
            _surf.SetShaderParameter("has_normal", true); _surf.SetShaderParameter("normal_tex", SystemView.Tex("8k_earth_normal_map"));
            _surf.SetShaderParameter("has_clouds", true); _surf.SetShaderParameter("cloud_tex", SystemView.Tex("8k_earth_clouds"));
            City = 1f;
        }
        int seg = b.Radius > 1.5f ? 192 : b.Radius > 0.3f ? 128 : 48;
        var mesh = new SphereMesh { Radius = b.Radius, Height = b.Radius * 2, RadialSegments = seg, Rings = seg / 2 };
        var surf = new MeshInstance3D { Mesh = mesh, MaterialOverride = _surf, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        Spin.AddChild(surf);
        _surfMesh = surf;

        if (b.Clouds && tex != null && b.Name == "Earth")
        {
            _clouds = SystemView.CloudShader();
            _clouds.SetShaderParameter("cloud_tex", SystemView.Tex("8k_earth_clouds"));
            _clouds.SetShaderParameter("sun_color", starCol);
            var cm = new SphereMesh { Radius = b.Radius * 1.0015f, Height = b.Radius * 2.003f, RadialSegments = seg, Rings = seg / 2 };      // cloud tops at ten kilometres
            _cloudMesh = new MeshInstance3D { Mesh = cm, MaterialOverride = _clouds, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            Spin.AddChild(_cloudMesh);
        }
        if (b.Atmo.R + b.Atmo.G + b.Atmo.B > 0.01f)
        {
            _atmo = SystemView.AtmoShader();
            float ar = b.Radius * (1f + b.AtmoHeight);
            _atmo.SetShaderParameter("planet_r", b.Radius * 0.999f);
            _atmo.SetShaderParameter("atmo_r", ar);
            _atmo.SetShaderParameter("tint", new Vector3(b.Atmo.R, b.Atmo.G, b.Atmo.B));
            _atmo.SetShaderParameter("density", b.AtmoDensity);
            _atmo.SetShaderParameter("sun_color", starCol);
            var am = new SphereMesh { Radius = ar, Height = ar * 2, RadialSegments = 64, Rings = 32 };
            _atmoMesh = new MeshInstance3D { Mesh = am, MaterialOverride = _atmo, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(_atmoMesh);
        }
        if (b.Ring)
        {
            _ring = SystemView.RingShader();
            var rtex = SystemView.Tex(b.RingTex);
            _ring.SetShaderParameter("textured", rtex != null);
            if (rtex != null) _ring.SetShaderParameter("ring_tex", rtex);
            _ring.SetShaderParameter("col", new Vector3(b.B.R, b.B.G, b.B.B));
            _ring.SetShaderParameter("planet_r", b.Radius);
            _ring.SetShaderParameter("sun_color", starCol);
            _ring.SetShaderParameter("seed", (b.ProcSeed % 100) * 0.3f);
            Tilted.AddChild(new MeshInstance3D { Mesh = SystemView.Annulus(b.Radius * b.RingIn, b.Radius * b.RingOut, 256), MaterialOverride = _ring, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            _surf.SetShaderParameter("has_ring", true);
            _surf.SetShaderParameter("ring_textured", rtex != null);
            if (rtex != null) _surf.SetShaderParameter("ring_tex", rtex);
            _surf.SetShaderParameter("ring_in", b.Radius * b.RingIn);
            _surf.SetShaderParameter("ring_out", b.Radius * b.RingOut);
        }
        _mats = new[] { _surf, _clouds, _atmo, _ring };
    }

    void BuildBelt(Body b)
    {
        var r = new Random(b.ProcSeed + 7);
        var rocks = new ArrayMesh[3];
        for (int k = 0; k < 3; k++) rocks[k] = RockMesh(r);
        const int n = 14000;
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = rocks[0], InstanceCount = n };
        for (int i = 0; i < n; i++)
        {
            float a = (float)r.NextDouble() * Mathf.Tau;
            float rr = b.Orbit + Galaxy.Gauss(r) * b.BeltWidth * 0.45f;
            var pos = new Vector3(Mathf.Cos(a) * rr, Galaxy.Gauss(r) * b.BeltWidth * 0.06f, Mathf.Sin(a) * rr);
            float s = 0.0003f + Mathf.Pow((float)r.NextDouble(), 6f) * 0.07f;      // two kilometres to Ceres-sized
            var basis = new Basis(new Quaternion(new Vector3((float)r.NextDouble() - 0.5f, (float)r.NextDouble() - 0.5f, (float)r.NextDouble() - 0.5f).Normalized(), (float)r.NextDouble() * 6f));
            basis = basis.Scaled(new Vector3(s, s * (0.6f + (float)r.NextDouble() * 0.5f), s * (0.7f + (float)r.NextDouble() * 0.4f)));
            mm.SetInstanceTransform(i, new Transform3D(basis, pos));
            float g = 0.6f + (float)r.NextDouble() * 0.5f;
            mm.SetInstanceColor(i, new Color(g, g * 0.93f, g * 0.85f));
        }
        var mat = new StandardMaterial3D { VertexColorUseAsAlbedo = true, AlbedoColor = new Color(0.32f, 0.3f, 0.28f), Roughness = 0.95f };
        rocks[0].SurfaceSetMaterial(0, mat);
        _belt = new MultiMeshInstance3D { Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _sv.AddChild(_belt);
        // at true scale the rocks are lost in the gap between them: a faint dust of points, never smaller than a pixel,
        // lets the belt read from across the system
        const int dn = 9000;
        var quad = new QuadMesh { Size = new Vector2(2, 2), Material = Works.GlowMat() };
        var dm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = quad, InstanceCount = dn };
        for (int i = 0; i < dn; i++)
        {
            float a = (float)r.NextDouble() * Mathf.Tau;
            float rr = b.Orbit + Galaxy.Gauss(r) * b.BeltWidth * 0.4f;
            var pos = new Vector3(Mathf.Cos(a) * rr, Galaxy.Gauss(r) * b.BeltWidth * 0.05f, Mathf.Sin(a) * rr);
            dm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * 0.02f), pos));
            float g = 0.5f + 0.5f * (float)r.NextDouble();
            dm.SetInstanceColor(i, new Color(0.62f * g, 0.56f * g, 0.5f * g, 0.32f));
            dm.SetInstanceCustomData(i, new Color(-0.25f, 0.6f, 0, 0));
        }
        _beltDust = new MultiMeshInstance3D { Multimesh = dm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, CustomAabb = new Aabb(-Vector3.One * b.Orbit * 2f, Vector3.One * b.Orbit * 4f) };
        _sv.AddChild(_beltDust);
    }

    void EnsureDebris()
    {
        if (_debris != null) return;
        var r = new Random(B.ProcSeed + 99);
        var rock = RockMesh(r);
        rock.SurfaceSetMaterial(0, new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.27f, 0.24f), Roughness = 0.9f, EmissionEnabled = true, Emission = new Color(0.25f, 0.08f, 0.02f) });
        const int n = 3000;
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = rock, InstanceCount = n, VisibleInstanceCount = 0 };
        for (int i = 0; i < n; i++)
        {
            // concentrated around where the world was, spreading along the whole orbit
            float a = (float)(Galaxy.Gauss(r) * 0.6 + (r.NextDouble() < 0.5 ? r.NextDouble() * Mathf.Tau : 0));
            float rr = B.Orbit + Galaxy.Gauss(r) * B.Radius * 1.5f;
            var pos = new Vector3(Mathf.Cos(a) * rr, Galaxy.Gauss(r) * B.Radius * 0.3f, Mathf.Sin(a) * rr);
            float sz = B.Radius * (0.01f + Mathf.Pow((float)r.NextDouble(), 4f) * 0.08f);
            mm.SetInstanceTransform(i, new Transform3D(new Basis(new Quaternion(new Vector3((float)r.NextDouble(), (float)r.NextDouble(), (float)r.NextDouble()).Normalized(), (float)r.NextDouble() * 6f)).Scaled(Vector3.One * sz), pos));
        }
        _debris = new MultiMeshInstance3D { Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _sv.AddChild(_debris);
    }

    public static ArrayMesh RockMesh(Random r)
    {
        var sphere = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 10, Rings = 6 };
        var arrays = sphere.GetMeshArrays();
        var v = (Vector3[])arrays[(int)Mesh.ArrayType.Vertex];
        float o1 = (float)r.NextDouble() * 10, o2 = (float)r.NextDouble() * 10;
        for (int i = 0; i < v.Length; i++)
        {
            var d = v[i].Normalized();
            float n = 1f + 0.25f * Mathf.Sin(d.X * 3.1f + o1) * Mathf.Cos(d.Y * 2.7f + o2) + 0.15f * Mathf.Sin(d.Z * 5.3f + o1 * 2);
            v[i] = d * n;
        }
        arrays[(int)Mesh.ArrayType.Vertex] = v;
        var st = new SurfaceTool();
        var m = new ArrayMesh();
        m.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        st.CreateFrom(m, 0);
        st.GenerateNormals();
        return st.Commit();
    }

    /// <summary>Paint settlements and mines onto the surface (set by the close-up view while it is near).</summary>
    public void SetSites(Vector4[] sites, int n, float garden)
    {
        if (_surf == null) return;
        if (n > 0) _surf.SetShaderParameter("sites", sites);
        _surf.SetShaderParameter("site_n", n);
        _surf.SetShaderParameter("garden", garden);
    }

    public void Update(double year, float dt, Camera3D cam)
    {
        var pos = B.WorldPos(year);
        TrueView = Frame.View(_sv.Offset + B.WorldPosD(year));
        Squash = Frame.Place(this, TrueView);
        // orbit line follows the parent
        _orbit.Position = B.Parent == null ? Vector3.Zero : B.Parent.WorldPos(year);
        if (_orbitBuilt < 0) _orbitBuilt = B.Orbit;
        _orbit.Scale = Vector3.One * (B.Orbit / Mathf.Max(_orbitBuilt, 1e-4f));     // a world the voice has moved keeps its line
        _orbit.Basis = B.Incl != 0 ? new Basis(Vector3.Right, B.Incl) : Basis.Identity;
        if (_belt != null)
        {
            _belt.Basis = new Basis(Vector3.Up, -(float)(Mathf.Tau * year / B.Period));
            _beltDust.Basis = _belt.Basis;
            return;
        }
        // a world being taken apart shrinks, then is gone; its mass becomes a ring of debris along the orbit
        float left = Mathf.Clamp(1f - B.Mined * 0.97f, 0.03f, 1f);
        float shrink = Mathf.Pow(left, 1f / 3f);
        bool gone = B.Mined >= 0.999f;
        Spin.Scale = Vector3.One * shrink;
        Spin.Visible = !gone;
        Tilted.Visible = !gone;
        if (_atmoMesh != null) _atmoMesh.Visible = !gone && B.Mined < 0.5f && _shownIgnite < 0.5f;
        if (B.Mined > 0.25f && B.Parent == null) EnsureDebris();
        if (_debris != null)
        {
            _debris.Visible = true;
            _debris.Multimesh.VisibleInstanceCount = (int)(_debris.Multimesh.InstanceCount * Mathf.Clamp((B.Mined - 0.2f) * 1.3f, 0, 1));
            _debris.Basis = new Basis(Vector3.Up, -(float)(Mathf.Tau * year / B.Period) - B.Phase);
        }
        // spin is shown at a watchable pace: at a month per second the real Earth would turn 30 times a second
        double turns = double.IsNaN(_lastYear) ? year / B.Day % 1.0 : (year - _lastYear) / B.Day;
        _lastYear = year;
        double cap = dt / 400.0;
        if (!double.IsNaN(_spinPh) && Math.Abs(turns) > cap) turns = Math.Sign(turns) * cap;
        _spinPh = ((double.IsNaN(_spinPh) ? 0 : _spinPh) + turns) % 1.0;
        double rot = Mathf.Tau * _spinPh;
        Spin.Basis = new Basis(Vector3.Right, B.Tilt) * new Basis(Vector3.Up, (float)rot);
        float light = _sv.LightAt((B.Parent == null ? pos : B.Parent.WorldPos(year)).Length());
        // the star as this world sees it: true direction, at the distance its own squash puts it
        var sp = GlobalPosition + (_sv.TrueStar - TrueView) * Squash;
        foreach (var m in _mats) m?.SetShaderParameter("star_pos", sp);
        if (_atmo != null) { _atmo.SetShaderParameter("planet_r", B.Radius * 0.999f * Squash); _atmo.SetShaderParameter("atmo_r", B.Radius * (1f + B.AtmoHeight) * Squash); }
        if (_ring != null)
        {
            _ring.SetShaderParameter("planet_r", B.Radius * Squash);
            _surf.SetShaderParameter("ring_in", B.Radius * B.RingIn * Squash);
            _surf.SetShaderParameter("ring_out", B.Radius * B.RingOut * Squash);
        }
        _surf.SetShaderParameter("light", light);
        _surf.SetShaderParameter("city", City);
        _surf.SetShaderParameter("city_col", new Vector3(CityColor.R, CityColor.G, CityColor.B));
        _surf.SetShaderParameter("mined", B.Mined);
        // seeded life and ignition arrive over a few seconds rather than in a frame
        _shownTerra = Mathf.MoveToward(_shownTerra, Terraform, dt * 0.12f);
        _shownIgnite = Mathf.MoveToward(_shownIgnite, B.Ignited ? 1f : 0f, dt * 0.25f);
        _surf.SetShaderParameter("terraform", _shownTerra);
        _surf.SetShaderParameter("ignite", _shownIgnite);
        if (_shownIgnite > 0.01f) EnsureFire(_shownIgnite);
        if (_atmo != null && Terraform > 0)
        {
            var t0 = new Vector3(B.Atmo.R, B.Atmo.G, B.Atmo.B);
            _atmo.SetShaderParameter("tint", t0.Lerp(new Vector3(0.2f, 0.42f, 1f), Terraform));
            _atmo.SetShaderParameter("density", Mathf.Lerp(B.AtmoDensity, 1.1f, Terraform));
        }
        _surf.SetShaderParameter("time", (float)(year * 50 % 1000));
        if (_ring != null)
        {
            _ring.SetShaderParameter("light", light);
            _surf.SetShaderParameter("ring_normal", (Tilted.GlobalBasis * Vector3.Up).Normalized());
        }
        if (_atmo != null) _atmo.SetShaderParameter("light", light);
        if (_clouds != null)
        {
            _cloudOff += dt * 0.0006f;
            _clouds.SetShaderParameter("cloud_off", _cloudOff);
            _clouds.SetShaderParameter("light", light);
            _surf.SetShaderParameter("cloud_off", _cloudOff);
        }
    }
}
