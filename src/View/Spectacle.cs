using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>One-off set pieces in the viewed system: comets, flares, seed ships leaving, the visitor, battles.</summary>
public partial class Spectacle : Node3D
{
    SystemView _sv;
    readonly List<Act> _acts = new();
    readonly Random _r = new(5);
    Node3D _visitor;
    public Node3D Visitor => _visitor != null && _visitor.Visible ? _visitor : null;
    MeshInstance3D _visitorHalo;
    float _clock;

    public abstract class Act { public float T, Life; public Node3D Node; public abstract void Tick(Spectacle s, float dt); }

    /// <summary>Anchored at the star's true position, unsquashed (see Frame): what happens near the camera keeps its place.</summary>
    public void Setup(SystemView sv) { _sv = sv; TopLevel = true; }
    /// <summary>A seed ark's length: twenty kilometres. The model runs half a unit.</summary>
    public const float ArkScale = 20f * StarSystem.Km / 0.5f;

    public void Play(string kind, Colony c, Body b)
    {
        switch (kind)
        {
            case "comet": Spawn(new Comet(this, c)); break;
            case "flare": Spawn(new Flare(this)); break;
            case "seedlaunch": { var sl = new SeedLaunch(this, c, b ?? c?.Seat); Spawn(sl); Followed = sl._ship; break; }
            case "breakthrough": Spawn(new Pulse(this, c?.Seat)); break;
            case "battle": Spawn(new Battle(this, c)); break;
            case "liftoff": case "landing": if (b != null && _sv.ByBody.TryGetValue(b, out var bvL)) Spawn(new Liftoff(this, bvL, kind == "landing")); break;
            case "arrival": { var ar = new Arrival(this, c, b ?? c?.Seat); Spawn(ar); Followed = ar.Ship; break; }
            // cosmic powers
            case "seed":
                Spawn(new Shockwave(this, b, new Color(0.45f, 1f, 0.55f), 4f, 6f, 2.2f));
                Spawn(new Shockwave(this, b, new Color(0.6f, 0.85f, 1f), 3f, 3f, 1.6f, 0.6f));
                break;
            case "shatter":
                Spawn(new Shockwave(this, b, new Color(1f, 0.95f, 0.85f), 1.2f, 3f, 5f, flash: true));
                Spawn(new Shockwave(this, b, new Color(1f, 0.5f, 0.2f), 5f, 14f, 2.2f));
                Spawn(new Shards(this, b));
                break;
            case "ignite":
                Spawn(new Shockwave(this, b, new Color(1f, 0.85f, 0.6f), 1.5f, 2.5f, 5f, flash: true));
                Spawn(new Shockwave(this, b, new Color(1f, 0.55f, 0.2f), 6f, 10f, 2.5f));
                break;
            case "orbit":
                Spawn(new Shockwave(this, b, new Color(0.6f, 0.8f, 1f), 3f, 8f, 2.2f));
                Spawn(new Shockwave(this, b, new Color(0.9f, 0.95f, 1f), 1.2f, 2f, 3f, flash: true));
                Spawn(new Shockwave(this, null, new Color(0.5f, 0.7f, 1f), 5f, 40f, 0.8f, 0.4f));
                break;
            case "visitor":
                Spawn(new Shockwave(this, null, new Color(0.75f, 0.6f, 1f), 6f, 300f, 1.5f));
                break;
            case "nova":
                Spawn(new Flare(this)); Spawn(new Flare(this)); Spawn(new Flare(this));
                Spawn(new Shockwave(this, null, new Color(1f, 0.95f, 0.8f), 2f, 8f, 6f, flash: true));
                Spawn(new Shockwave(this, null, new Color(1f, 0.6f, 0.3f), 9f, 120f, 3f));
                Spawn(new Shockwave(this, null, new Color(0.6f, 0.75f, 1f), 12f, 200f, 1.6f, 1.5f));
                break;
        }
    }

    void Spawn(Act a) { _acts.Add(a); if (a.Node != null && a.Node.GetParent() == null) AddChild(a.Node); }

    float _launchT = 3f;
    public void Tick(float dt, Colony c)
    {
        _clock += dt;
        // a busy homeworld launches all the time; you notice when you are close
        if (c != null && c.Seat != null && c.Pop > 1 && _sv.ByBody.TryGetValue(c.Seat, out var seatV))
        {
            var cam = GetViewport().GetCamera3D();
            float d = cam.GlobalPosition.DistanceTo(seatV.TrueView) / c.Seat.Radius;
            _launchT -= dt;
            if (d < 25f && _launchT <= 0)
            {
                if (_r.NextDouble() < c.TrafficFactor) Play("liftoff", c, c.Seat);       // closed ports: hardly anything flies
                _launchT = (float)(2.5 + _r.NextDouble() * 6.0) / (float)Math.Max(0.4, c.TrafficLevel + (c.Home ? 0.6 : 0));
            }
        }
        for (int i = _acts.Count - 1; i >= 0; i--)
        {
            var a = _acts[i];
            a.T += dt;
            a.Tick(this, dt);
            if (a.T >= a.Life) { a.Node?.QueueFree(); _acts.RemoveAt(i); }
        }
        UpdateVisitor(c, dt);
    }

    // ---------- shared pieces ----------
    public static ShaderMaterial Additive(Color col, float strength = 1f) => new ShaderMaterial()
    {
        Shader = _addShader ??= new Shader
        {
            Code = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
float sqr(float x){ return x * x; }
uniform vec3 col = vec3(1.0);
uniform float strength = 1.0;
uniform float shape = 0.0;   // 0 soft sprite, 1 ring, 2 tail (UV.y along), 3 beam (even along UV.y)
void vertex(){
	if (shape < 0.5) {
		float s = length(MODEL_MATRIX[0].xyz);
		MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
		MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(vec4(s,0,0,0), vec4(0,s,0,0), vec4(0,0,s,0), vec4(0,0,0,1));
	}
}
void fragment(){
	vec2 c = UV * 2.0 - 1.0;
	float a;
	if (shape < 0.5) a = (exp(-dot(c,c) * 5.0) + 0.5 * exp(-dot(c,c) * 40.0)) * clamp(1.0 - dot(c,c), 0.0, 1.0);   // fade to nothing at the quad's edge
	else if (shape < 1.5) { float r = length(c); a = exp(-sqr((r - 0.9) * 40.0)) * step(r, 1.0) + 0.15 * exp(-sqr((r - 0.8) * 8.0)); }
	else if (shape < 2.5) a = sqr(clamp(1.0 - UV.y, 0.0, 1.0)) * exp(-c.x * c.x * 4.84);
	else a = exp(-c.x * c.x * 6.0) * (0.6 + 0.4 * exp(-c.x * c.x * 60.0)) * smoothstep(0.0, 0.03, UV.y) * (1.0 - smoothstep(0.85, 1.0, UV.y));
	ALBEDO = col * a * strength;
}"
        },
        RenderPriority = 8
    }.With(m => { m.SetShaderParameter("col", new Vector3(col.R, col.G, col.B)); m.SetShaderParameter("strength", strength); });
    static Shader _addShader;
    public static Shader AddShader => _addShader;

    public static MeshInstance3D Sprite(Color col, float size, float strength = 1f)
    {
        var m = Additive(col, strength);
        return new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One * 2 }, MaterialOverride = m, Scale = Vector3.One * size, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 1000 };
    }

    // ---------- comet ----------
    class Comet : Act
    {
        Vector3 _from, _to; MeshInstance3D _head, _tail; Colony _c;
        public Comet(Spectacle s, Colony c)
        {
            _c = c;
            Life = 9f;
            Node = new Node3D();
            var target = c?.Seat?.WorldPos(s._sv.VisYear) ?? Vector3.Zero;
            var cam = s.GetViewport().GetCamera3D();
            var right = cam.GlobalBasis.X;
            var up = cam.GlobalBasis.Y;
            float view = Mathf.Max(cam.GlobalPosition.DistanceTo(s.GlobalPosition + target), 10f);
            _from = target + (right * (s._r.NextDouble() < 0.5 ? -1 : 1) * 1.1f + up * 0.5f) * view;
            _to = target + (target.Normalized() * 3f + up * 0.3f);
            _head = Sprite(new Color(0.75f, 0.9f, 1f), 0.6f, 3f);
            Node.AddChild(_head);
            var tailMat = Additive(new Color(0.55f, 0.75f, 1f), 2.4f);
            tailMat.SetShaderParameter("shape", 2f);
            _tail = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(1, 1) }, MaterialOverride = tailMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 1000 };
            Node.AddChild(_tail);
        }
        public override void Tick(Spectacle s, float dt)
        {
            float u = Mathf.Clamp(T / Life, 0, 1);
            float e = 1 - Mathf.Pow(1 - u, 1.6f);
            var p = _from.Lerp(_to, e);
            _head.Position = p;
            // the tail always points away from the star
            var away = p.Normalized();
            float len = (6f + 24f / Mathf.Max(p.Length() / (0.57f * StarSystem.AU), 0.5f)) * Mathf.Max(_from.DistanceTo(_to) / 60f, 0.3f);
            var cam = s.GetViewport().GetCamera3D();
            var toCam = (cam.GlobalPosition - (s.GlobalPosition + p)).Normalized();
            var side = away.Cross(toCam).Normalized();
            float w = len * 0.12f;
            var basis = new Basis(side * w, -away * len, side.Cross(away));
            _tail.Transform = new Transform3D(basis, p + away * len * 0.5f);
            float fade = Mathf.Clamp((1 - u) * 6f, 0, 1);
            _head.Scale = Vector3.One * 0.6f * fade * Mathf.Max(_from.DistanceTo(_to) / 60f, 0.3f);
            _tail.Visible = fade > 0.02f;
        }
    }

    // ---------- flare ----------
    class Flare : Act
    {
        MeshInstance3D _burst, _ring;
        Vector3 _dir;
        public Flare(Spectacle s)
        {
            Life = 6f;
            Node = new Node3D();
            float r = s._sv.Sys.StarRadius;
            _dir = new Vector3((float)s._r.NextDouble() - 0.5f, ((float)s._r.NextDouble() - 0.5f) * 0.6f, (float)s._r.NextDouble() - 0.5f).Normalized();
            _burst = Sprite(new Color(1f, 0.7f, 0.35f), r * 0.8f, 4f);
            _burst.Position = _dir * r * 1.05f;
            Node.AddChild(_burst);
            var ringMat = Additive(new Color(1f, 0.55f, 0.3f), 1.4f);
            ringMat.SetShaderParameter("shape", 1f);
            _ring = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One * 2 }, MaterialOverride = ringMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 1000 };
            _ring.Rotation = new Vector3(-Mathf.Pi / 2, 0, 0);
            Node.AddChild(_ring);
        }
        public override void Tick(Spectacle s, float dt)
        {
            float u = T / Life;
            float r = s._sv.Sys.StarRadius;
            _burst.Scale = Vector3.One * r * (0.6f + 2.5f * u) * Mathf.Clamp(1.3f - u, 0, 1);
            _burst.Position = _dir * r * (1.05f + u * 2f);
            _ring.Scale = Vector3.One * r * (1.5f + u * 9f);
            ((ShaderMaterial)_ring.MaterialOverride).SetShaderParameter("strength", 1.4f * (1 - u));
        }
    }

    // ---------- breakthrough pulse ----------
    class Pulse : Act
    {
        MeshInstance3D _ring; Body _b;
        public Pulse(Spectacle s, Body b)
        {
            _b = b; Life = 3f; Node = new Node3D();
            var m = Additive(new Color(0.55f, 0.9f, 1f), 2f);
            m.SetShaderParameter("shape", 1f);
            _ring = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One * 2 }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 1000 };
            Node.AddChild(_ring);
        }
        public override void Tick(Spectacle s, float dt)
        {
            if (_b == null) return;
            float u = T / Life;
            var cam = s.GetViewport().GetCamera3D();
            var p = _b.WorldPos(s._sv.VisYear);
            _ring.Position = p;
            var toCam = (cam.GlobalPosition - _ring.GlobalPosition).Normalized();
            if (toCam.LengthSquared() > 0.5f) _ring.Basis = Basis.LookingAt(toCam, Mathf.Abs(toCam.Y) > 0.98f ? Vector3.Right : Vector3.Up);
            _ring.Scale = Vector3.One * _b.Radius * (1.2f + u * 4f);
            ((ShaderMaterial)_ring.MaterialOverride).SetShaderParameter("strength", 2f * (1 - u));
        }
    }

    // ---------- cosmic powers ----------
    /// <summary>A ring of light (or, with flash, a blinding burst) swelling out from a world, or from the star when b is null.
    /// Sizes are in radii of that world or star; delay holds it back a moment.</summary>
    class Shockwave : Act
    {
        MeshInstance3D _q; Body _b; float _r0, _r1, _k, _delay; bool _flash;
        public Shockwave(Spectacle s, Body b, Color col, float life, float growTo, float strength, float delay = 0f, bool flash = false)
        {
            _b = b; Life = life + delay; _delay = delay; _flash = flash; _k = strength;
            float R = b?.Radius ?? s._sv.Sys.StarRadius;
            _r0 = R * (flash ? 0.8f : 1.1f); _r1 = R * growTo;
            Node = new Node3D();
            var m = Additive(col, strength);
            m.SetShaderParameter("shape", flash ? 0f : 1f);
            _q = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One * 2 }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 1e5f, Visible = false };
            Node.AddChild(_q);
        }
        public override void Tick(Spectacle s, float dt)
        {
            float u = Mathf.Clamp((T - _delay) / (Life - _delay), 0, 1);
            _q.Visible = T >= _delay;
            var p = _b?.WorldPos(s._sv.VisYear) ?? Vector3.Zero;
            _q.Position = p;
            var cam = s.GetViewport().GetCamera3D();
            if (!_flash)
            {
                var toCam = (cam.GlobalPosition - _q.GlobalPosition).Normalized();
                if (toCam.LengthSquared() > 0.5f) _q.Basis = Basis.LookingAt(toCam, Mathf.Abs(toCam.Y) > 0.98f ? Vector3.Right : Vector3.Up);
            }
            float e = 1 - (1 - u) * (1 - u);
            _q.Scale = Vector3.One * Mathf.Lerp(_r0, _r1, e);
            ((ShaderMaterial)_q.MaterialOverride).SetShaderParameter("strength", _k * (_flash ? Mathf.Clamp(1.5f - u * 1.5f, 0, 1) : 1 - u));
        }
    }

    /// <summary>A world breaking apart: glowing fragments flung out along and across its orbit, cooling as they go.</summary>
    class Shards : Act
    {
        readonly List<(MeshInstance3D M, Vector3 V, float Spin)> _bits = new();
        Body _b; Vector3 _at;
        public Shards(Spectacle s, Body b)
        {
            _b = b; Life = 14f; Node = new Node3D();
            _at = b.WorldPos(s._sv.VisYear);
            var rock = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.26f, 0.22f), EmissionEnabled = true, Emission = new Color(1f, 0.4f, 0.1f), EmissionEnergyMultiplier = 2.5f, Roughness = 0.9f };
            for (int i = 0; i < 70; i++)
            {
                var dir = new Vector3((float)s._r.NextDouble() - 0.5f, ((float)s._r.NextDouble() - 0.5f) * 0.5f, (float)s._r.NextDouble() - 0.5f).Normalized();
                float sz = b.Radius * (0.05f + 0.22f * (float)Math.Pow(s._r.NextDouble(), 2));
                var m = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 6, Rings = 3 }, MaterialOverride = rock, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    Scale = new Vector3(sz, sz * 0.7f, sz * 0.85f), Position = _at };
                Node.AddChild(m);
                _bits.Add((m, dir * b.Radius * (0.6f + 2.4f * (float)s._r.NextDouble()), (float)s._r.NextDouble() * 3f));
                if (i % 3 == 0) { var g = Sprite(new Color(1f, 0.55f, 0.2f), sz * 1.6f, 2f); m.AddChild(g); }
            }
        }
        public override void Tick(Spectacle s, float dt)
        {
            float u = T / Life;
            foreach (var (m, v, spin) in _bits)
            {
                m.Position = _at + v * (1 - Mathf.Exp(-T * 0.35f)) * 6f;
                m.Rotation = new Vector3(T * spin, T * spin * 0.7f, 0);
                m.Visible = u < 0.98f;
            }
            if (u > 0.6f) foreach (var (m, _, _) in _bits) m.Scale *= 1f - dt * 0.4f;
        }
    }

    // ---------- a seed ship leaves the system ----------
    public Node3D Followed;

    public class SeedLaunch : Act
    {
        public Node3D _ship; MeshInstance3D _plume, _flare;
        Vector3 _start, _dir; Body _from;
        public SeedLaunch(Spectacle s, Colony c, Body from)
        {
            _from = from; Life = 26f; Node = new Node3D();
            _start = from?.WorldPos(s._sv.VisYear) ?? Vector3.Zero;
            _dir = (_start.Normalized() + new Vector3(0, 0.25f, 0)).Normalized();
            if (_dir.LengthSquared() < 0.1f) _dir = Vector3.Forward;
            _ship = BuildSeedShip();
            _ship.Scale = Vector3.One * ArkScale;
            Node.AddChild(_ship);
            var pm = Additive(new Color(0.6f, 0.75f, 1f), 2f);
            pm.SetShaderParameter("shape", 2f);
            _plume = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One }, MaterialOverride = pm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 1000 };
            Node.AddChild(_plume);
            _flare = Sprite(new Color(0.75f, 0.85f, 1f), 0.05f, 2.5f);
            Node.AddChild(_flare);
        }
        public override void Tick(Spectacle s, float dt)
        {
            float u = T;
            // drift clear of orbit, light the torch, then accelerate hard toward the target star
            float k = ArkScale;
            float dist = u < 4 ? u * 0.15f * k : 0.6f * k + Mathf.Pow(u - 4, 2.4f) * 0.35f;
            var p = _start + _dir * (_from?.Radius * 1.6f ?? 1f) + _dir * dist;
            _ship.Position = p;
            _ship.Basis = Basis.LookingAt(_dir, Mathf.Abs(_dir.Y) > 0.98f ? Vector3.Right : Vector3.Up).Scaled(_ship.Scale);
            Spin(_ship, dt);
            float burn = u < 3.5f ? 0 : Mathf.Clamp((u - 3.5f) * 0.8f, 0, 1);
            var cam = s.GetViewport().GetCamera3D();
            var tc = (cam.GlobalPosition - (s.GlobalPosition + p)).Normalized();
            var side = _dir.Cross(tc).Normalized();
            float len = (0.6f + burn * 2f) * k + burn * dist * 0.25f;
            _plume.Transform = new Transform3D(new Basis(side * 0.035f * k * (0.4f + burn), _dir * len, side.Cross(_dir)), p - _dir * (0.27f * k + len * 0.5f));
            _plume.Visible = burn > 0.01f;
            _flare.Position = p - _dir * 0.1f * k;
            _flare.Scale = Vector3.One * (0.01f + burn * 0.06f) * k;
        }

        /// <summary>
        /// The seed ark from the Blender kit (assets/ships/hum/SeedArk.glb: metres, nose +X), turned so its nose points
        /// down -Z and shrunk to half a unit long like the old model. Its two habitat drums are separate parts (SpinA,
        /// SpinB) that Spin() turns in opposite directions. Falls back to the procedural model if the asset is missing.
        /// </summary>
        public static Node3D BuildSeedShip()
        {
            const string path = "res://assets/ships/hum/SeedArk.glb";
            if (ResourceLoader.Exists(path))
            {
                var model = GD.Load<PackedScene>(path).Instantiate<Node3D>();
                float len = 20350f;
                foreach (var mi in model.FindChildren("*", "MeshInstance3D", true, false).Cast<MeshInstance3D>())
                {
                    mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                    for (int i = 0; i < (mi.Mesh?.GetSurfaceCount() ?? 0); i++)
                        if (mi.Mesh.SurfaceGetMaterial(i) is BaseMaterial3D m && m.EmissionEnabled)
                        {
                            var b = (BaseMaterial3D)m.Duplicate();
                            b.EmissionEnergyMultiplier = Mathf.Max(b.EmissionEnergyMultiplier, 1f) * 3f;     // windows read from afar
                            mi.SetSurfaceOverrideMaterial(i, b);
                        }
                }
                model.Transform = new Transform3D(new Basis(Vector3.Up, Mathf.Pi / 2).Scaled(Vector3.One * (0.5f / len)), Vector3.Zero);
                var root0 = new Node3D();
                root0.AddChild(model);
                root0.SetMeta("spinA", model.FindChild("SpinA", true, false));
                root0.SetMeta("spinB", model.FindChild("SpinB", true, false));
                return root0;
            }
            return BuildProcedural();
        }

        /// <summary>The habitat drums turn (about one turn a minute for 1 g at a kilometre's radius, sped up to read on screen).</summary>
        public static void Spin(Node3D ship, float dt)
        {
            if (ship == null || dt <= 0) return;
            if (ship.GetMeta("spinA", default).AsGodotObject() is Node3D a) a.RotateObjectLocal(Vector3.Right, dt * 0.35f);
            if (ship.GetMeta("spinB", default).AsGodotObject() is Node3D b) b.RotateObjectLocal(Vector3.Right, -dt * 0.35f);
        }

        static Node3D BuildProcedural()
        {
            // a long spine: shield dish at the bow, habitat rings, tanks, radiators, drive at the stern (nose -Z after LookAt)
            var root = new Node3D();
            var hull = Works.HullMat();
            void Add(Mesh m, Vector3 pos, Vector3 rot, Material mat = null) { root.AddChild(new MeshInstance3D { Mesh = m, Position = pos, RotationDegrees = rot, MaterialOverride = mat ?? hull, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off }); }
            float L = 0.5f;
            Add(new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.008f, Height = L, RadialSegments = 8 }, Vector3.Zero, new Vector3(90, 0, 0));
            Add(new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.02f, Height = 0.01f, RadialSegments = 24 }, new Vector3(0, 0, -L * 0.5f), new Vector3(90, 0, 0));
            for (int i = 0; i < 3; i++) Add(new TorusMesh { InnerRadius = 0.028f, OuterRadius = 0.034f, Rings = 24, RingSegments = 6 }, new Vector3(0, 0, -0.12f + i * 0.05f), new Vector3(90, 0, 0));
            for (int i = 0; i < 6; i++) Add(new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.012f, Height = 0.12f, RadialSegments = 8 }, new Vector3(Mathf.Cos(i) * 0.022f, Mathf.Sin(i) * 0.022f, 0.08f), new Vector3(90, 0, 0));
            var rad = new StandardMaterial3D { AlbedoColor = new Color(0.07f, 0.07f, 0.075f), Metallic = 0.5f, Roughness = 0.5f, EmissionEnabled = true, Emission = new Color(0.45f, 0.1f, 0.03f), EmissionEnergyMultiplier = 0.08f };
            Add(new BoxMesh { Size = new Vector3(0.11f, 0.0015f, 0.05f) }, new Vector3(0, 0, 0.16f), Vector3.Zero, rad);
            Add(new BoxMesh { Size = new Vector3(0.0015f, 0.11f, 0.05f) }, new Vector3(0, 0, 0.16f), Vector3.Zero, rad);
            Add(new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.03f, Height = 0.04f, RadialSegments = 16 }, new Vector3(0, 0, L * 0.5f), new Vector3(90, 0, 0));
            return root;
        }
    }

    // ---------- a rocket climbs from the surface into orbit ----------
    class Liftoff : Act
    {
        BodyView _bv; Vector3 _up, _east; MeshInstance3D _head, _trail; float _R; bool _land;
        public Liftoff(Spectacle s, BodyView bv, bool land = false)
        {
            _land = land;
            _bv = bv; Life = 14f; Node = new Node3D();
            _R = bv.B.Radius;
            // a launch site on the side facing the camera, within the tropics
            var cam = s.GetViewport().GetCamera3D();
            var toCam = (cam.GlobalPosition - bv.TrueView).Normalized();
            var local = (bv.Spin.GlobalBasis.Inverse() * toCam).Normalized();
            float lon = Mathf.Atan2(local.Z, local.X) + ((float)s._r.NextDouble() - 0.5f) * 1.2f;
            float lat = ((float)s._r.NextDouble() - 0.5f) * 1.1f;
            _up = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
            _east = new Vector3(-Mathf.Sin(lon), 0, Mathf.Cos(lon));
            _head = Sprite(new Color(1f, 0.85f, 0.6f), 2f * StarSystem.Km, 4f);
            Node.AddChild(_head);
            var tm = Additive(new Color(1f, 0.75f, 0.5f), 1.2f);
            tm.SetShaderParameter("shape", 2f);
            _trail = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One }, MaterialOverride = tm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 1000 };
            Node.AddChild(_trail);
            bv.Spin.AddChild(Node);
        }
        Vector3 At(float u)
        {
            // straight up, then pitching over eastward into a low orbit
            float h = _R * (0.004f + 0.32f * u * u);
            float down = _R * 1.1f * Mathf.Pow(Mathf.Max(u - 0.2f, 0f), 1.5f);
            return (_up * (_R + h) + _east * down).Normalized() * (_R + h);
        }
        public override void Tick(Spectacle s, float dt)
        {
            float u = Mathf.Clamp(T / (Life * 0.75f), 0, 1);
            if (_land) u = 1f - u;   // a landing is a launch run backwards
            var p = At(u);
            _head.Position = p;
            float camD = s.GetViewport().GetCamera3D().GlobalPosition.DistanceTo(_head.GlobalPosition);
            _head.Scale = Vector3.One * Mathf.Max(1.5f * StarSystem.Km, camD * 0.004f);
            if (OS.GetCmdlineUserArgs().Contains("--debugfx") && Mathf.PosMod(T, 2f) < 0.02f) { var cam0 = s.GetViewport().GetCamera3D(); GD.Print($"LIFT T={T:0.0} head={_head.GlobalPosition} earth={_bv.GlobalPosition} cam={cam0.GlobalPosition} vis={_head.IsVisibleInTree()} behind={cam0.IsPositionBehind(_head.GlobalPosition)} sp={cam0.UnprojectPosition(_head.GlobalPosition)}"); }
            float fade = Mathf.Clamp((Life - T) / 3f, 0, 1);
            ((ShaderMaterial)_head.MaterialOverride).SetShaderParameter("strength", 4f * fade * (u < 0.98f ? 1f : 0.3f));
            // trail from a little way back along the path to the head
            var back = At(Mathf.Max(u - 0.35f, 0));
            var dir = p - back; float len = dir.Length();
            if (len < 1e-5f) { _trail.Visible = false; return; }
            _trail.Visible = true;
            dir /= len;
            var cam = s.GetViewport().GetCamera3D();
            var tc = (cam.GlobalPosition - _head.GlobalPosition).Normalized();
            var tcl = (_bv.Spin.GlobalBasis.Inverse() * tc).Normalized();
            var side = dir.Cross(tcl).Normalized();
            // UV.y 0 at the head (bright), 1 at the tail
            _trail.Transform = new Transform3D(new Basis(side * Mathf.Max(0.5f * StarSystem.Km, camD * 0.0028f), -dir * len, side.Cross(dir)), p - dir * len * 0.5f);
            ((ShaderMaterial)_trail.MaterialOverride).SetShaderParameter("strength", 1.0f * fade);
        }
    }

    // ---------- a seed ship arrives, braking hard on its torch ----------
    public class Arrival : Act
    {
        public Node3D Ship; MeshInstance3D _plume, _flare;
        Vector3 _from, _to, _dir; Body _seat;
        public Arrival(Spectacle s, Colony c, Body seat)
        {
            _seat = seat; Life = 22f; Node = new Node3D();
            var target = seat?.WorldPos(s._sv.VisYear) ?? Vector3.Zero;
            float outer = s._sv.Sys.Bodies.Count > 0 ? s._sv.Sys.Bodies.Max(b => b.Orbit) : 100f;
            var inDir = (target.Normalized() + new Vector3(0.3f, 0.35f, -0.2f)).Normalized();
            _from = target + inDir * outer * 1.6f;
            _to = target + inDir * (seat?.Radius ?? 1f) * 4f;
            _dir = (_to - _from).Normalized();
            Ship = SeedLaunch.BuildSeedShip();
            Ship.Scale = Vector3.One * ArkScale;
            Node.AddChild(Ship);
            var pm = Additive(new Color(0.6f, 0.75f, 1f), 2f);
            pm.SetShaderParameter("shape", 2f);
            _plume = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One }, MaterialOverride = pm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 1000 };
            Node.AddChild(_plume);
            _flare = Sprite(new Color(0.75f, 0.85f, 1f), 0.05f, 2.5f);
            Node.AddChild(_flare);
        }
        public override void Tick(Spectacle s, float dt)
        {
            float u = Mathf.Clamp(T / (Life - 4f), 0, 1);
            // fast in, hard braking: position eases out
            float e = 1 - Mathf.Pow(1 - u, 3f);
            var p = _from.Lerp(_to, e);
            Ship.Position = p;
            // the ship flips and fires its drive toward its destination
            Ship.Basis = Basis.LookingAt(-_dir, Mathf.Abs(_dir.Y) > 0.98f ? Vector3.Right : Vector3.Up).Scaled(Ship.Scale);
            SeedLaunch.Spin(Ship, dt);
            float burn = u < 0.08f ? 0 : Mathf.Clamp(1.2f - u, 0, 1);
            float len = (0.4f + burn * 3f) * Ship.Scale.X;
            var cam = s.GetViewport().GetCamera3D();
            var tc = (cam.GlobalPosition - (s.GlobalPosition + p)).Normalized();
            var side = _dir.Cross(tc).Normalized();
            _plume.Transform = new Transform3D(new Basis(side * 0.04f * Ship.Scale.X * (0.4f + burn), _dir * len, side.Cross(_dir)), p + _dir * (0.28f * Ship.Scale.X + len * 0.5f));
            _plume.Visible = burn > 0.02f;
            _flare.Position = p + _dir * 0.27f * Ship.Scale.X;
            _flare.Scale = Vector3.One * (0.01f + burn * 0.06f) * Ship.Scale.X;
        }
    }

    // ---------- battle over the system ----------
    class Battle : Act
    {
        readonly List<MeshInstance3D> _beams = new();
        Vector3 _a, _b;
        public Battle(Spectacle s, Colony c)
        {
            Life = 12f; Node = new Node3D();
            var seat = c?.Seat?.WorldPos(s._sv.VisYear) ?? Vector3.Zero;
            float outer = s._sv.Sys.Bodies.Max(b => b.Orbit);
            _a = seat + new Vector3(3, 1, 2);
            _b = seat.Normalized() * outer * 0.6f + new Vector3(0, 6, 0);
            for (int i = 0; i < 8; i++)
            {
                var m = Additive(i % 2 == 0 ? new Color(1f, 0.4f, 0.25f) : new Color(0.4f, 0.8f, 1f), 2f);
                m.SetShaderParameter("shape", 2f);
                var beam = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 1000 };
                Node.AddChild(beam);
                _beams.Add(beam);
            }
        }
        public override void Tick(Spectacle s, float dt)
        {
            var cam = s.GetViewport().GetCamera3D();
            for (int i = 0; i < _beams.Count; i++)
            {
                var b = _beams[i];
                bool on = Mathf.Sin(T * 7f + i * 2.1f) > 0.4f && T < Life - 1;
                b.Visible = on;
                if (!on) continue;
                var from = (i % 2 == 0 ? _a : _b) + new Vector3(Mathf.Sin(i * 3.1f), Mathf.Cos(i * 1.7f), Mathf.Sin(i * 0.7f)) * 1.5f;
                var to = (i % 2 == 0 ? _b : _a) + new Vector3(Mathf.Cos(i * 2.3f), Mathf.Sin(i * 1.1f), Mathf.Cos(i * 0.9f)) * 1.5f;
                var dir = to - from; float len = dir.Length(); dir /= len;
                var tc = (cam.GlobalPosition - (s.GlobalPosition + from)).Normalized();
                var side = dir.Cross(tc).Normalized();
                float w = Mathf.Max(cam.GlobalPosition.DistanceTo(s.GlobalPosition + from) * 0.0015f, 2f * StarSystem.Km);     // as wide on screen at any range
                b.Transform = new Transform3D(new Basis(side * w, -dir * len, side.Cross(dir)), from + dir * len * 0.5f);
            }
        }
    }

    // ---------- the visitor ----------
    void UpdateVisitor(Colony c, float dt)
    {
        bool want = c != null && c.Home && c.VisitorT >= 0 && c.VisitorStage < 4;
        if (!want) { if (_visitor != null) _visitor.Visible = false; return; }
        if (_visitor == null)
        {
            _visitor = new Node3D();
            var body = new MeshInstance3D
            {
                Mesh = Octahedron(), Scale = new Vector3(0.7f, 2f, 0.7f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.02f, 0.02f, 0.025f), Metallic = 1f, Roughness = 0.08f },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            _visitor.AddChild(body);
            var halo = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 1.38f, OuterRadius = 1.42f, Rings = 128, RingSegments = 6 }, MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.66f, 0.6f, 0.82f) * 2.4f }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _visitor.AddChild(halo);
            _visitorHalo = halo;
            var glow = Sprite(new Color(0.55f, 0.45f, 0.85f), 3.5f, 0.8f);
            _visitor.AddChild(glow);
            _visitor.Scale = Vector3.One * 0.15f;          // a thousand kilometres from tip to tip
            AddChild(_visitor);
        }
        _visitor.Visible = true;
        float outer = _sv.Sys.Bodies.Where(b => b.Name == "Mars" || b.Name == "Jupiter").Select(b => b.Orbit).DefaultIfEmpty(100).Max();
        var home = new Vector3(0, outer * 0.25f, outer * 0.85f);
        double age = _sv.Colony == null ? 0 : _sv.VisYear - c.VisitorT;
        float arrive = Mathf.Clamp((float)age / 3f, 0, 1);
        float leave = c.VisitorStage >= 3 ? Mathf.Clamp((float)(age - (c.VisitorOutcome == "fire" ? 22 : 32)) / 4f, 0, 1) : 0;
        var far = home * 5f + new Vector3(0, outer, 0);
        _visitor.Position = far.Lerp(home, arrive * arrive * (3 - 2 * arrive)).Lerp(far, leave);
        _visitor.RotateY(dt * 0.05f);
        _visitorHalo.RotateX(dt * 0.2f);
    }

    static ArrayMesh Octahedron()
    {
        var v = new[] { Vector3.Up, Vector3.Down, Vector3.Left, Vector3.Right, Vector3.Forward, Vector3.Back };
        int[] f = { 0, 3, 4, 0, 4, 2, 0, 2, 5, 0, 5, 3, 1, 4, 3, 1, 2, 4, 1, 5, 2, 1, 3, 5 };
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        foreach (int i in f) st.AddVertex(v[i]);
        st.GenerateNormals();
        return st.Commit();
    }
}

static class MatExt
{
    public static T With<T>(this T m, Action<T> a) { a(m); return m; }
}
