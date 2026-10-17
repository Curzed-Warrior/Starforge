using System;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// The whole galaxy as light: ~300k aggregate star points, unresolved glow, emission nebulae and dust lanes.
/// The node is scaled by World.G and shifted so the focused star sits at the world origin,
/// which makes it the sky when the camera is inside a system.
/// </summary>
public partial class GalaxyView : Node3D
{
    Galaxy _gal;
    ShaderMaterial _points, _glow, _neb, _dust, _simPts;
    public const int PointCount = 320_000;

    public void Setup(Galaxy gal)
    {
        _gal = gal;
        var r = new Random(gal.Seed);

        _points = Mat("res://shaders/galaxy_points.gdshader", 2);
        _glow = Mat("res://shaders/galaxy_glow.gdshader", 0);
        _neb = Mat("res://shaders/galaxy_glow.gdshader", 3);
        _neb.SetShaderParameter("nebula", 1.0f);
        _dust = Mat("res://shaders/galaxy_dust.gdshader", 1);

        AddChild(BuildPoints(r));
        AddChild(BuildGlow(r));
        AddChild(BuildDust(r));
        AddChild(BuildNebulae(r));
        BuildLocalGroup();
        BuildCore();
        BuildLandmarks();
    }

    public readonly System.Collections.Generic.List<(Galaxy.Landmark L, MeshInstance3D Node, ShaderMaterial Mat, MeshInstance3D Pulsar)> Marks = new();

    void BuildLandmarks()
    {
        var sh = GD.Load<Shader>("res://shaders/nebula_volume.gdshader");
        foreach (var lm in _gal.Landmarks)
        {
            var m = new ShaderMaterial { Shader = sh, RenderPriority = 9 };
            m.SetShaderParameter("kind", (float)lm.Kind);
            m.SetShaderParameter("seed", (float)(lm.Name.Length * 3.7));
            m.SetShaderParameter("col_a", new Vector3(lm.A.R, lm.A.G, lm.A.B));
            m.SetShaderParameter("col_b", new Vector3(lm.B.R, lm.B.G, lm.B.B));
            var node = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 32, Rings = 16 }, MaterialOverride = m,
                Position = lm.Pos, Scale = Vector3.One * lm.Radius, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 16384,
                Rotation = new Vector3(0.3f * lm.Kind, lm.Name.Length * 0.4f, 0.2f)
            };
            AddChild(node);
            MeshInstance3D pulsar = null;
            if (lm.Kind == 1)
            {
                pulsar = View.Spectacle.Sprite(new Color(0.7f, 0.85f, 1f), lm.Radius * 0.02f, 3f);
                pulsar.Position = lm.Pos;
                AddChild(pulsar);
            }
            Marks.Add((lm, node, m, pulsar));
        }
    }

    public const float CoreRs = 0.02f, CoreBound = 0.6f;      // galaxy units
    ShaderMaterial _holeMat;
    float _holeT;
    public float CoreTap;

    void BuildCore()
    {
        _holeMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/blackhole.gdshader"), RenderPriority = 10 };
        var hole = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = CoreBound, Height = CoreBound * 2, RadialSegments = 48, Rings = 24 },
            MaterialOverride = _holeMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 16384,
            Rotation = new Vector3(Mathf.DegToRad(24f), 0, Mathf.DegToRad(-12f)),
        };
        AddChild(hole);
    }

    readonly System.Collections.Generic.List<ShaderMaterial> _far = new();
    public Vector3 AndromedaPos { get; private set; }

    /// <summary>Andromeda and the Magellanic Clouds, at their real distances (1 galaxy unit = 50 light-years).</summary>
    void BuildLocalGroup()
    {
        void Add(Vector3 pos, float radius, float kind, float seed, float bright, Vector3 normal)
        {
            var m = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/far_galaxy.gdshader"), RenderPriority = -1 };
            m.SetShaderParameter("kind", kind); m.SetShaderParameter("seed", seed); m.SetShaderParameter("bright", bright);
            var q = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One * 2 }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 16384 };
            q.Position = pos;
            q.Scale = Vector3.One * radius;
            q.Basis = Basis.LookingAt(normal, Mathf.Abs(normal.Y) > 0.9f ? Vector3.Right : Vector3.Up).Scaled(Vector3.One * radius);
            q.CustomAabb = new Aabb(-Vector3.One * 1e9f, Vector3.One * 2e9f);
            AddChild(q);
            _far.Add(m);
        }
        AndromedaPos = new Vector3(-26000f, 21000f, 37000f);
        Add(AndromedaPos, 1100f, 0, 1.7f, 0.9f, new Vector3(0.45f, 0.75f, -0.48f).Normalized());
        Add(new Vector3(800f, -2200f, -2300f), 180f, 1, 3.3f, 0.22f, new Vector3(0.2f, 1f, 0.3f).Normalized());
        Add(new Vector3(1400f, -2900f, -2600f), 110f, 1, 7.1f, 0.16f, new Vector3(-0.3f, 1f, 0.1f).Normalized());
    }

    static ShaderMaterial Mat(string path, int priority)
    {
        var m = new ShaderMaterial { Shader = GD.Load<Shader>(path) };
        m.RenderPriority = priority;
        return m;
    }

    Color StarColor(Random r, Galaxy.Pop pop, float armDist, out float lum)
    {
        float t;
        switch (pop)
        {
            case Galaxy.Pop.Arm:
                bool young = armDist < 0.6f && r.NextDouble() < 0.55;
                t = young ? 7000f + (float)Math.Pow(r.NextDouble(), 2) * 20000f : 3600f + (float)r.NextDouble() * 3500f;
                lum = young ? 0.5f + (float)r.NextDouble() * 0.5f : 0.25f + (float)r.NextDouble() * 0.35f;
                break;
            case Galaxy.Pop.Bulge: case Galaxy.Pop.Bar:
                t = 3300f + (float)r.NextDouble() * 2200f; lum = 0.3f + (float)r.NextDouble() * 0.35f; break;
            case Galaxy.Pop.Halo:
                t = 3500f + (float)r.NextDouble() * 1500f; lum = 0.15f + (float)r.NextDouble() * 0.2f; break;
            default:
                t = 3500f + (float)r.NextDouble() * 4500f; lum = 0.2f + (float)r.NextDouble() * 0.35f; break;
        }
        if (r.NextDouble() < 0.01) lum = 1f;          // a few supergiants
        return Galaxy.Blackbody(t);
    }

    MeshInstance3D BuildPoints(Random r)
    {
        var verts = new Vector3[PointCount];
        var cols = new Color[PointCount];
        for (int i = 0; i < PointCount; i++)
        {
            var p = _gal.Sample(r, out var pop, out float ad);
            var c = StarColor(r, pop, ad, out float lum);
            verts[i] = p;
            cols[i] = new Color(c.R, c.G, c.B, lum);
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.Color] = cols;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Points, arrays);
        mesh.SurfaceSetMaterial(0, _points);
        var mi = new MeshInstance3D { Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        mi.CustomAabb = new Aabb(new Vector3(-1, -1, -1) * Galaxy.Radius * 1.5f, Vector3.One * Galaxy.Radius * 3f);
        return mi;
    }

    MultiMeshInstance3D Sprites(int n, ShaderMaterial mat)
    {
        var quad = new QuadMesh { Size = new Vector2(2, 2) };
        quad.Material = mat;
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = quad, InstanceCount = n };
        var mi = new MultiMeshInstance3D { Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        mi.CustomAabb = new Aabb(new Vector3(-1, -1, -1) * Galaxy.Radius * 1.5f, Vector3.One * Galaxy.Radius * 3f);
        return mi;
    }

    MultiMeshInstance3D BuildGlow(Random r)
    {
        const int n = 16000;
        var mi = Sprites(n, _glow);
        for (int i = 0; i < n; i++)
        {
            var p = _gal.Sample(r, out var pop, out float ad);
            float rad = new Vector2(p.X, p.Z).Length();
            float size; Color c; float a;
            if (pop == Galaxy.Pop.Bulge || pop == Galaxy.Pop.Bar)
            {
                size = 18f + (float)r.NextDouble() * 34f;
                c = new Color(1f, 0.76f, 0.45f); a = 0.045f;
            }
            else if (pop == Galaxy.Pop.Halo) { size = 90f; c = new Color(1f, 0.85f, 0.7f); a = 0.003f; }
            else if (pop == Galaxy.Pop.Arm)
            {
                size = 9f + (float)r.NextDouble() * 18f;
                c = new Color(0.55f, 0.7f, 1f).Lerp(new Color(1f, 0.85f, 0.65f), Mathf.Clamp(1f - rad / 450f, 0, 1) * 0.8f);
                a = 0.05f;
            }
            else
            {
                size = 25f + (float)r.NextDouble() * 40f;
                c = new Color(1f, 0.9f, 0.78f); a = 0.012f;
            }
            p.Y *= 0.5f;
            mi.Multimesh.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * size), p));
            mi.Multimesh.SetInstanceColor(i, new Color(c, a));
        }
        return mi;
    }

    MultiMeshInstance3D BuildDust(Random r)
    {
        const int n = 9000;
        var mi = Sprites(n, _dust);
        int i = 0;
        while (i < n)
        {
            // dust hugs the inner (trailing) edge of each arm and the bar's leading edges
            float rad = -Mathf.Log(1f - (float)r.NextDouble() * 0.99f) * Galaxy.Radius * 0.36f;
            if (rad > Galaxy.Radius * 1.05f || rad < _gal.BarLength * 0.5f) continue;
            int k = r.Next(_gal.Arms);
            float th = _gal.ArmAngle(k, rad) - 0.07f + Galaxy.Gauss(r) * 0.06f;
            if (r.NextDouble() < 0.25) th = (float)r.NextDouble() * Mathf.Tau;   // diffuse flocculent dust
            var p = new Vector3(Mathf.Cos(th) * rad, Galaxy.Gauss(r) * 2.5f, Mathf.Sin(th) * rad);
            float size = 7f + (float)r.NextDouble() * 14f;
            mi.Multimesh.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * size), p));
            float a = 0.5f + (float)r.NextDouble() * 0.45f;
            mi.Multimesh.SetInstanceColor(i, new Color(0.16f, 0.1f, 0.07f, a));
            i++;
        }
        return mi;
    }

    MultiMeshInstance3D BuildNebulae(Random r)
    {
        const int n = 2600;
        var mi = Sprites(n, _neb);
        int i = 0;
        while (i < n)
        {
            var p = _gal.Sample(r, out var pop, out float ad);
            if (pop != Galaxy.Pop.Arm || ad > 0.45f) continue;
            p.Y *= 0.4f;
            float size = 1.5f + (float)Math.Pow(r.NextDouble(), 3) * 9f;
            Color c = r.NextDouble() < 0.8 ? new Color(1f, 0.32f, 0.5f) : new Color(0.45f, 0.7f, 1f);   // H-alpha pink, reflection blue
            mi.Multimesh.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * size), p));
            mi.Multimesh.SetInstanceColor(i, new Color(c, 0.35f + (float)r.NextDouble() * 0.4f));
            i++;
        }
        return mi;
    }

    public void SetTerritory(Texture2D cap)
    {
        foreach (var m in new[] { _points, _glow, _neb }) m.SetShaderParameter("cap_tex", cap);
    }

    public static float[] SkyOverride;   // inside exposure, glow inside, nebula inside factor, dust strength
    public float InsideExposure = 3500f, OutsideExposure = 2.5e5f, GlowInside = 0.06f, GlowOutside = 0.55f, NebInside = 0.18f;

    /// <summary>
    /// zoomGal: camera distance from the focused star in galaxy units. Like an eye adapting, exposure rises
    /// as we pull out: from inside a system the galaxy is a faint band, from outside it is a blazing disk.
    /// </summary>
    /// <summary>
    /// 0 = inside the galaxy's starfield (a dark sky, the Milky Way as a band), 1 = outside looking at the whole disk.
    /// Needs both: far from the current star, and far from the galaxy's body (so the core is "inside").
    /// </summary>
    public float Outside(float zoomGal, Vector3 galOrigin, float worldScale)
    {
        float tz = Mathf.Clamp((Mathf.Log(Mathf.Max(zoomGal, 1e-6f)) - Mathf.Log(2f)) / (Mathf.Log(1500f) - Mathf.Log(2f)), 0f, 1f);
        var cam = GetViewport().GetCamera3D();
        var local = (cam.GlobalPosition - galOrigin) / worldScale;
        float rPlane = new Vector2(local.X, local.Z).Length();
        float out1 = Mathf.Max(rPlane - Galaxy.Radius * 0.9f, 0f), out2 = Mathf.Max(Mathf.Abs(local.Y) - 60f, 0f);
        float dOut = Mathf.Sqrt(out1 * out1 + out2 * out2);
        float tc = Mathf.Clamp(dOut / 900f, 0f, 1f);
        float t = Mathf.Min(tz, tc);
        return t * t * (3f - 2f * t);
    }
    public float LastOutside;

    public void Tune(float zoomGal, float worldScale, float skyK)
    {
        _holeT += (float)GetProcessDeltaTime() * Main.Anim;
        _holeMat.SetShaderParameter("rs", CoreRs * worldScale);
        _holeMat.SetShaderParameter("bound", CoreBound * worldScale);
        _holeMat.SetShaderParameter("time", _holeT);
        _holeMat.SetShaderParameter("tap", CoreTap);
        var camW = GetViewport().GetCamera3D();
        foreach (var (lm, node, m, pulsar) in Marks)
        {
            float d = camW.GlobalPosition.DistanceTo(node.GlobalPosition);
            float rW = lm.Radius * worldScale;
            node.Visible = d - rW < camW.Far * 0.9f;
            m.SetShaderParameter("radius", rW);
            m.SetShaderParameter("time", _holeT);
            m.SetShaderParameter("bright", Mathf.Clamp(1.4f - (d - rW) / (camW.Far * 0.9f), 0f, 1f) * 1.4f);
            if (pulsar != null)
            {
                pulsar.Visible = node.Visible;
                float beat = Mathf.Pow(Mathf.Max(Mathf.Sin(_holeT * 9f), 0f), 12f);
                pulsar.Scale = Vector3.One * lm.Radius * (0.015f + 0.05f * beat);
            }
        }
        if (SkyOverride != null)
        {
            InsideExposure = SkyOverride[0]; GlowInside = SkyOverride[1]; NebInside = SkyOverride[2];
            _dust.SetShaderParameter("strength", SkyOverride[3]);
            SkyOverride = null;
        }
        float t = Outside(zoomGal, Position, worldScale);
        LastOutside = t;
        float exp = Mathf.Exp(Mathf.Lerp(Mathf.Log(InsideExposure), Mathf.Log(OutsideExposure), t));
        _points.SetShaderParameter("exposure", exp);
        _points.SetShaderParameter("gscale", worldScale);
        _points.SetShaderParameter("hide_near", 0.25f);
        foreach (var m in new[] { _points, _glow, _neb, _dust }) m.SetShaderParameter("sky_k", skyK);
        foreach (var m in _far) { m.SetShaderParameter("sky_k", skyK); m.SetShaderParameter("exposure", Mathf.Lerp(0.35f, 1.0f, t)); }
        foreach (var m in new[] { _glow, _neb }) { m.SetShaderParameter("gal_origin", Position); m.SetShaderParameter("gscale", worldScale); }
        float hideWorld = 0.4f * worldScale;
        float g = Mathf.Lerp(GlowInside, GlowOutside, t);
        _glow.SetShaderParameter("exposure", g);
        _glow.SetShaderParameter("near_fade", hideWorld);
        _neb.SetShaderParameter("exposure", Mathf.Lerp(GlowInside * NebInside, 1.6f, t));
        _neb.SetShaderParameter("near_fade", hideWorld);
        _dust.SetShaderParameter("near_fade", hideWorld);
    }
}
