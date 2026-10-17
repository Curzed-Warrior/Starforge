using System;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// The three great projects, built visibly as their progress climbs:
/// a Shkadov stellar engine, a Matrioshka archive of nested shells, and a fold gate at the system's edge.
/// </summary>
public partial class GreatWorks : Node3D
{
    SystemView _sv;
    Node3D _engine, _archive, _gate;
    ShaderMaterial _mirrorMat, _shellMat, _portalMat;
    MeshInstance3D[] _jets = new MeshInstance3D[2];
    MeshInstance3D[] _shells = new MeshInstance3D[3];
    MeshInstance3D _portal;
    float _t;

    public void Setup(SystemView sv) { _sv = sv; }

    float _dt;
    public void Update(Colony c, float dt, float shellOut)
    {
        _t += dt; _dt = dt;
        string proj = c?.Project;
        float p = c == null ? 0 : (c.ProjectDone ? 1f : c.ProjectP);
        if (proj == "Industrious") { EnsureEngine(shellOut); UpdateEngine(p, c.ProjectDone); }
        if (proj == "Contemplative") { EnsureArchive(shellOut); UpdateArchive(p, c.ProjectDone, c.EclipticOpen); }
        if (proj == "Expansionist") { EnsureGate(); UpdateGate(p, c.ProjectDone); }
        if (_engine != null) _engine.Visible = proj == "Industrious";
        if (_archive != null) _archive.Visible = proj == "Contemplative";
        if (_gate != null) _gate.Visible = proj == "Expansionist";
        if (proj == "Ringworld") EnsureRing();
        if (_ringworld != null) { _ringworld.Visible = proj == "Ringworld"; if (proj == "Ringworld") UpdateRing(); }
    }

    // ---------- Ringworld ----------
    Node3D _ringworld; ShaderMaterial _ringSurf; MeshInstance3D _shadowSquares;

    static ArrayMesh Band(float r, float w, int seg)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < seg; i++)
        {
            float a0 = Mathf.Tau * i / seg, a1 = Mathf.Tau * (i + 1) / seg;
            Vector3 P(float a, float y) => new(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            void V(float a, float y, float u, float v) { st.SetNormal(-new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a))); st.SetUV(new Vector2(u, v)); st.AddVertex(P(a, y)); }
            float u0 = (float)i / seg, u1 = (float)(i + 1) / seg;
            V(a0, -w / 2, u0, 0); V(a1, -w / 2, u1, 0); V(a1, w / 2, u1, 1);
            V(a0, -w / 2, u0, 0); V(a1, w / 2, u1, 1); V(a0, w / 2, u0, 1);
        }
        return st.Commit();
    }

    void EnsureRing()
    {
        if (_ringworld != null) return;
        _ringworld = new Node3D();
        // at the distance where the light is Earth-like
        float lum = Mathf.Max(_sv.Sys.Star.Lum, 0.05f);
        float R = 70f * Mathf.Sqrt(lum) * 0.9f;
        float W = R * 0.06f;
        _ringSurf = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = @"shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled, fog_disabled;
float sstep(float a, float b, float x){ float t = clamp((x - a) / (b - a), 0.0, 1.0); return t * t * (3.0 - 2.0 * t); }
uniform vec3 star_pos = vec3(0.0);
uniform vec3 star_col = vec3(1.0);
uniform float time = 0.0;
varying vec3 v_w; varying vec3 v_n;
float h(vec2 p){ return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float n(vec2 p){ vec2 i = floor(p), f = fract(p); f = f*f*(3.0-2.0*f); return mix(mix(h(i), h(i+vec2(1,0)), f.x), mix(h(i+vec2(0,1)), h(i+vec2(1,1)), f.x), f.y); }
float fb(vec2 p){ float v = 0.0, a = 0.5; for (int i = 0; i < 6; i++) { v += a * n(p); p *= 2.07; a *= 0.5; } return v; }
void vertex(){ v_w = (MODEL_MATRIX * vec4(VERTEX,1.0)).xyz; v_n = normalize((MODEL_MATRIX * vec4(NORMAL,0.0)).xyz); }
void fragment(){
	vec3 V = normalize(CAMERA_POSITION_WORLD - v_w);
	vec3 L = normalize(star_pos - v_w);
	bool inner = dot(V, v_n) > 0.0;
	vec2 p = vec2(UV.x * 260.0, UV.y * 4.0);
	float e = fb(p * 0.6);
	float wall = sstep(0.04, 0.0, UV.y) + sstep(0.96, 1.0, UV.y);
	vec3 sea = vec3(0.03, 0.1, 0.2);
	vec3 land = mix(vec3(0.14, 0.24, 0.09), vec3(0.45, 0.38, 0.26), sstep(0.55, 0.75, e));
	vec3 col = mix(sea, land, sstep(0.48, 0.5, e));
	float cloud = sstep(0.55, 0.8, fb(p * 1.3 + vec2(time * 0.2, 0.0)));
	col = mix(col, vec3(0.9), cloud * 0.7);
	col = mix(col, vec3(0.35, 0.35, 0.38), wall);
	// the shadow squares give each place a night; the night side glows with cities
	float day = sstep(-0.2, 0.2, sin(UV.x * 6.2831 * 20.0 - time * 0.05));
	vec3 lit = col * star_col * 2.4 * (0.08 + 0.92 * day) * clamp(dot(v_n, L) * 2.0, 0.2, 1.0);
	float cities = step(0.985, h(floor(p * 3.0))) * sstep(0.5, 0.52, e) * (1.0 - day);
	ALBEDO = inner ? lit + vec3(1.0, 0.7, 0.4) * cities * 1.5 : vec3(0.05, 0.05, 0.055) + vec3(0.6, 0.15, 0.04) * 0.04;
}"
            }
        };
        _ringworld.AddChild(new MeshInstance3D { Mesh = Band(R, W, 720), MaterialOverride = _ringSurf, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        // shadow squares: a ring of dark panels close to the star
        var sq = Band(R * 0.32f, W * 3f, 40);
        var dark = new StandardMaterial3D { AlbedoColor = new Color(0.03f, 0.03f, 0.035f), Metallic = 0.9f, Roughness = 0.3f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        _shadowSquares = new MeshInstance3D { Mesh = sq, MaterialOverride = dark, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _ringworld.AddChild(_shadowSquares);
        _ringworld.Rotation = new Vector3(0.12f, 0, 0.05f);
        AddChild(_ringworld);
    }

    void UpdateRing()
    {
        var sc = _sv.Sys.StarColor;
        _ringSurf.SetShaderParameter("star_pos", _sv.GlobalPosition);      // the ring is hung from the system node, squashed with it
        _ringSurf.SetShaderParameter("star_col", new Vector3(sc.R, sc.G, sc.B));
        _ringSurf.SetShaderParameter("time", _t);
        _ringworld.RotateY(0.0015f * _dt);
        _shadowSquares.RotateY(-0.02f * _dt);
    }

    // ---------- Stellar engine ----------
    static ArrayMesh Cap(float r, float maxAngle, int rings, int segs)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < rings; i++)
            for (int j = 0; j < segs; j++)
            {
                float a0 = maxAngle * i / rings, a1 = maxAngle * (i + 1) / rings;
                float b0 = Mathf.Tau * j / segs, b1 = Mathf.Tau * (j + 1) / segs;
                Vector3 P(float a, float b) => new(Mathf.Sin(a) * Mathf.Cos(b) * r, Mathf.Cos(a) * r, Mathf.Sin(a) * Mathf.Sin(b) * r);
                void V(float a, float b) { var v = P(a, b); st.SetNormal(-v.Normalized()); st.SetUV(new Vector2(b / Mathf.Tau, a / maxAngle)); st.AddVertex(v); }
                V(a0, b0); V(a1, b0); V(a1, b1);
                V(a0, b0); V(a1, b1); V(a0, b1);
            }
        return st.Commit();
    }

    void EnsureEngine(float shellOut)
    {
        if (_engine != null) return;
        _engine = new Node3D();
        _mirrorMat = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = @"shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled, fog_disabled;
float sstep(float a, float b, float x){ float t = clamp((x - a) / (b - a), 0.0, 1.0); return t * t * (3.0 - 2.0 * t); }
float sqr(float x){ return x * x; }
uniform float progress = 0.0;
uniform vec3 star_col = vec3(1.0);
varying vec3 v_w; varying vec3 v_n;
void vertex(){ v_w = (MODEL_MATRIX * vec4(VERTEX,1.0)).xyz; v_n = normalize((MODEL_MATRIX * vec4(NORMAL,0.0)).xyz); }
void fragment(){
	// built panel by panel from the pole outward
	float panel = fract(sin(dot(floor(UV * vec2(120.0, 40.0)), vec2(12.9898, 78.233))) * 43758.5453);
	if (UV.y > progress * 1.05 - panel * 0.05) discard;
	vec3 toStar = normalize(NODE_POSITION_WORLD - v_w);
	vec3 V = normalize(CAMERA_POSITION_WORLD - v_w);
	bool inner = dot(V, toStar) > 0.0;
	float grid = step(0.94, fract(UV.x * 120.0)) + step(0.92, fract(UV.y * 40.0));
	vec3 mirror = star_col * (0.55 + 0.45 * pow(max(dot(reflect(-V, v_n), toStar), 0.0), 6.0)) * 1.2;
	vec3 back = vec3(0.02) + vec3(0.5, 0.12, 0.04) * 0.25;
	ALBEDO = (inner ? mirror : back) * (1.0 - 0.35 * min(grid, 1.0));
}"
            }
        };
        float R = shellOut * 1.7f;
        var cap = new MeshInstance3D { Mesh = Cap(R, Mathf.Pi * 0.38f, 40, 120), MaterialOverride = _mirrorMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _engine.AddChild(cap);
        for (int i = 0; i < 2; i++)
        {
            var m = new ShaderMaterial
            {
                Shader = new Shader
                {
                    Code = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
float sstep(float a, float b, float x){ float t = clamp((x - a) / (b - a), 0.0, 1.0); return t * t * (3.0 - 2.0 * t); }
float sqr(float x){ return x * x; }
uniform float strength = 0.0;
uniform float time = 0.0;
void fragment(){
	vec2 c = UV * 2.0 - 1.0;
	float along = UV.y;
	float w = exp(-sqr(c.x / (0.15 + along * 0.5)));
	float flicker = 0.85 + 0.15 * sin(along * 40.0 - time * 8.0);
	ALBEDO = vec3(1.0, 0.65, 0.35) * w * pow(clamp(1.0 - along, 0.0, 1.0), 1.5) * strength * flicker * 2.0;
}"
                }
            };
            var jet = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 1000 };
            _jets[i] = jet;
            _engine.AddChild(jet);
        }
        AddChild(_engine);
    }

    void UpdateEngine(float p, bool done)
    {
        _mirrorMat.SetShaderParameter("progress", p);
        var sc = _sv.Sys.StarColor;
        _mirrorMat.SetShaderParameter("star_col", new Vector3(sc.R, sc.G, sc.B));
        var cam = GetViewport().GetCamera3D();
        float sr = _sv.Sys.StarRadius;
        for (int i = 0; i < 2; i++)
        {
            // the thrust jet leaves through the open side of the mirror; a weaker counter-jet balances it
            var dir = i == 0 ? Vector3.Down : Vector3.Up;
            float len = sr * (i == 0 ? 14f : 5f) * (done ? 1f : Mathf.Clamp(p - 0.6f, 0, 0.4f) * 1.5f);
            var start = dir * sr * 1.05f;
            var tc = (cam.GlobalPosition - (_sv.GlobalPosition + start * _sv.StarScale)).Normalized();
            var side = dir.Cross(tc).Normalized();
            if (side.LengthSquared() < 0.01f) side = Vector3.Right;
            _jets[i].Transform = new Transform3D(new Basis(side * sr * 1.6f, -dir * len, side.Cross(dir)), start + dir * len * 0.5f);
            var m = (ShaderMaterial)_jets[i].MaterialOverride;
            m.SetShaderParameter("strength", len > 0.01f ? (i == 0 ? 1f : 0.5f) : 0f);
            m.SetShaderParameter("time", _t);
        }
    }

    // ---------- Archive of Everything (Matrioshka shells) ----------
    void EnsureArchive(float shellOut)
    {
        if (_archive != null) return;
        _archive = new Node3D();
        _shellMat = new ShaderMaterial
        {
            RenderPriority = 3,
            Shader = new Shader
            {
                Code = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
float sstep(float a, float b, float x){ float t = clamp((x - a) / (b - a), 0.0, 1.0); return t * t * (3.0 - 2.0 * t); }
float sqr(float x){ return x * x; }
uniform float progress = 0.0;
uniform float time = 0.0;
uniform float layer = 0.0;
uniform float gap = 0.0;       // the open band left for inhabited worlds (radians of latitude)
varying vec3 v_o; varying vec3 v_w; varying vec3 v_n;
void vertex(){ v_o = VERTEX; v_w = (MODEL_MATRIX * vec4(VERTEX,1.0)).xyz; v_n = normalize((MODEL_MATRIX * vec4(NORMAL,0.0)).xyz); }
float hexd(vec2 p){ p = abs(p); return max(dot(p, normalize(vec2(1.0, 1.732))), p.x); }
void fragment(){
	vec3 d = normalize(v_o);
	if (abs(asin(d.y)) < gap) discard;
	vec2 uv = vec2(atan(d.z, d.x) * 6.0, asin(d.y) * 6.0) * (3.0 + layer);
	vec2 a = mod(uv, vec2(1.0, 1.732)) - vec2(0.5, 0.866);
	vec2 b = mod(uv - vec2(0.5, 0.866), vec2(1.0, 1.732)) - vec2(0.5, 0.866);
	vec2 g = dot(a, a) < dot(b, b) ? a : b;
	float edge = sstep(0.42, 0.5, hexd(g));
	float fres = pow(clamp(1.0 - abs(dot(normalize(v_n), normalize(CAMERA_POSITION_WORLD - v_w))), 0.0, 1.0), 3.0);
	// thoughts: pulses racing across the lattice
	float pulse = pow(max(sin(dot(uv, vec2(0.7, 0.3)) * 3.0 - time * (1.0 + layer)), 0.0), 40.0) * step(0.7, fract(sin(dot(floor(uv * 0.5), vec2(3.1, 7.7))) * 437.0));
	float built = step(fract(sin(dot(floor(uv), vec2(17.1, 31.7))) * 9137.0), progress);
	vec3 col = mix(vec3(1.0, 0.45, 0.15), vec3(0.6, 0.75, 1.0), layer * 0.4);
	ALBEDO = col * built * (edge * 0.3 + fres * 0.22 + pulse * 0.5) * (0.6 - layer * 0.12);
}"
            }
        };
        for (int i = 0; i < 3; i++)
        {
            float r = shellOut * (1.15f + i * 0.45f);
            var m = (ShaderMaterial)_shellMat.Duplicate();
            m.SetShaderParameter("layer", (float)i);
            _shells[i] = new MeshInstance3D { Mesh = new SphereMesh { Radius = r, Height = r * 2, RadialSegments = 96, Rings = 48 }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _archive.AddChild(_shells[i]);
        }
        AddChild(_archive);
    }

    void UpdateArchive(float p, bool done, bool open)
    {
        for (int i = 0; i < 3; i++)
        {
            var m = (ShaderMaterial)_shells[i].MaterialOverride;
            m.SetShaderParameter("gap", open ? Works.EclipticGap : 0f);
            m.SetShaderParameter("progress", Mathf.Clamp(p * 3f - i, 0, 1));
            m.SetShaderParameter("time", _t);
            _shells[i].RotateY(0.01f * (i + 1) * _dt);
        }
    }

    // ---------- Fold gate ----------
    void EnsureGate()
    {
        if (_gate != null) return;
        _gate = new Node3D();
        float outer = 0;
        foreach (var b in _sv.Sys.Bodies) outer = Mathf.Max(outer, b.Orbit);
        _gate.Position = new Vector3(outer * 0.75f, outer * 0.12f, -outer * 0.5f);
        _gate.LookAtFromPosition(_gate.Position, Vector3.Zero, Vector3.Up);
        float R = 32f;
        var ring = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = R * 0.93f, OuterRadius = R, Rings = 160, RingSegments = 12 }, MaterialOverride = Works.HullMat(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        ring.RotationDegrees = new Vector3(90, 0, 0);
        _gate.AddChild(ring);
        for (int i = 0; i < 16; i++)
        {
            float a = i * Mathf.Tau / 16;
            var node = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.6f, 1.6f, 2.4f) }, MaterialOverride = Works.HullMat(), Position = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * R * 0.965f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            node.Rotation = new Vector3(0, 0, a);
            _gate.AddChild(node);
        }
        _portalMat = new ShaderMaterial
        {
            RenderPriority = 4,
            Shader = new Shader
            {
                Code = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
float sstep(float a, float b, float x){ float t = clamp((x - a) / (b - a), 0.0, 1.0); return t * t * (3.0 - 2.0 * t); }
float sqr(float x){ return x * x; }
uniform float open = 0.0;
uniform float time = 0.0;
void fragment(){
	vec2 c = UV * 2.0 - 1.0;
	float r = length(c);
	if (r > 1.0) discard;
	float ang = atan(c.y, c.x);
	float swirl = sin(ang * 6.0 + log(r + 0.02) * 9.0 - time * 3.0) * 0.5 + 0.5;
	float core = exp(-r * r * 4.0);
	vec3 col = mix(vec3(0.5, 0.35, 1.0), vec3(0.85, 0.9, 1.0), core);
	ALBEDO = col * (swirl * 0.5 + core) * open * (1.0 - sstep(0.85, 1.0, r)) * 1.6;
}"
            }
        };
        _portal = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One * R * 1.86f }, MaterialOverride = _portalMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _gate.AddChild(_portal);
        AddChild(_gate);
    }

    void UpdateGate(float p, bool done)
    {
        _gate.Scale = Vector3.One * Mathf.Clamp(p * 1.4f, 0.05f, 1f);
        _portalMat.SetShaderParameter("open", done ? 1f : Mathf.Clamp((p - 0.9f) * 10f, 0, 1) * 0.4f);
        _portalMat.SetShaderParameter("time", _t);
    }
}
