using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// The war economy's works round a colony's seat, one model each (assets/ships/war/*.glb, built by the scripts beside
/// them): factories (Factory), shipyard slips (Slip, with the hull being built inside each busy one), barracks
/// (Barracks), special forces (Insertion) and armies on the ground (Army). Each kind keeps its placeholder colour
/// (data/war.json "placeholders") as a faint tint that strengthens as the model shrinks on screen, where a glow marker
/// in that colour takes over, so the kinds still read apart from across the system; each model's "Owner" band takes
/// the owner's empire colour. A kind whose .glb is
/// missing (a fresh clone) falls back to the old cube, its top face in the empire's colour. They sit in a shell 2.2
/// to 2.8 seat radii out. Fleets and the armies they carry are drawn with real hulls (WorksFleets).
/// </summary>
public partial class Works
{
    const int WarPropMax = 160;

    const string CubeShader = @"shader_type spatial;
varying vec3 v_empire; varying float v_face;
void vertex() { v_empire = INSTANCE_CUSTOM.rgb; v_face = step(0.9, NORMAL.y); }
void fragment() { ALBEDO = mix(COLOR.rgb, v_empire, v_face); EMISSION = ALBEDO * 0.12; ROUGHNESS = 0.7; }";

    /// <summary>One kind of war prop: a MultiMesh of its model (or the cube), filled afresh each frame.</summary>
    sealed class WarPool { public string Key; public MultiMeshInstance3D Mi; public int K; }
    readonly Dictionary<string, WarPool> _warPools = new();
    /// <summary>Where the first of each kind sits this frame and its drawn size, for --focus=works:KIND.</summary>
    readonly Dictionary<string, (D3 At, float Size)> _warAt = new();
    public bool WorksHere(string kind, out D3 at, out float size)
    {
        bool ok = _warAt.TryGetValue(kind, out var w); at = w.At; size = w.Size;
        return ok;
    }

    WarPool WarPoolOf(string key, Func<Mesh> model)
    {
        if (_warPools.TryGetValue(key, out var p)) return p;
        var mesh = model();
        bool cube = mesh == null;
        if (cube) mesh = new BoxMesh { Size = Vector3.One, Material = new ShaderMaterial { Shader = new Shader { Code = CubeShader } } };
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = mesh, InstanceCount = WarPropMax, VisibleInstanceCount = 0 };
        p = new WarPool
        {
            Key = key,
            Mi = new MultiMeshInstance3D
            {
                Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, CustomAabb = Everywhere,
                MaterialOverlay = cube ? null : WarTintMat(), Layers = cube ? 1u : FleetLayer,
            },
        };
        _near.AddChild(p.Mi);
        _warPools[key] = p;
        return p;
    }

    static readonly Dictionary<string, ArrayMesh> _warModels = new();
    static ShaderMaterial _ownerMat, _warTint;

    /// <summary>
    /// A war prop's model (assets/ships/war/&lt;name&gt;.glb, null if it has not been built), centred and scaled so its
    /// longest side is one unit, as the cube was. Its "Owner" surface is repainted per instance with the owner's colour.
    /// </summary>
    public static ArrayMesh WarModel(string name)
    {
        if (_warModels.TryGetValue(name, out var cached)) return cached;
        ArrayMesh mesh = null;
        string path = $"res://assets/ships/war/{name}.glb";
        if (ResourceLoader.Exists(path))
        {
            var root = GD.Load<PackedScene>(path).Instantiate<Node3D>();
            var src = root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().FirstOrDefault()?.Mesh as ArrayMesh;
            root.QueueFree();
            if (src != null)
            {
                var box = src.GetAabb();
                float k = 1f / Mathf.Max(Mathf.Max(box.Size.X, Mathf.Max(box.Size.Y, box.Size.Z)), 1e-3f);
                var o = box.GetCenter();
                mesh = new ArrayMesh();
                for (int i = 0; i < src.GetSurfaceCount(); i++)
                {
                    // positions only: scaling the normals with them (SurfaceTool.AppendFrom) leaves them too short to light
                    var arr = src.SurfaceGetArrays(i);
                    var v = arr[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                    for (int j = 0; j < v.Length; j++) v[j] = (v[j] - o) * k;
                    arr[(int)Mesh.ArrayType.Vertex] = v;
                    mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arr);
                    var m = src.SurfaceGetMaterial(i);
                    if (m?.ResourceName == "Owner") m = OwnerMat();
                    else if (m is BaseMaterial3D bm && bm.VertexColorUseAsAlbedo)
                    {
                        // the instance colour is the kind's tint for the overlay, not paint for the hull
                        var b = (BaseMaterial3D)bm.Duplicate(); b.VertexColorUseAsAlbedo = false; m = b;
                    }
                    mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, m);
                }
            }
        }
        return _warModels[name] = mesh;
    }

    static ShaderMaterial OwnerMat() => _ownerMat ??= new ShaderMaterial
    {
        Shader = new Shader
        {
            Code = @"shader_type spatial;
varying vec3 v_owner;
void vertex() { v_owner = INSTANCE_CUSTOM.rgb; }
void fragment() { ALBEDO = v_owner * 0.85; ROUGHNESS = 0.55; METALLIC = 0.15; EMISSION = v_owner * 0.05; }"
        }
    };

    /// <summary>The kind's colour washed over the model: faint up close, strong once the model is a few pixels
    /// across (INSTANCE_CUSTOM.a, from WarFar).</summary>
    static ShaderMaterial WarTintMat() => _warTint ??= new ShaderMaterial
    {
        Shader = new Shader
        {
            Code = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_back, shadows_disabled, fog_disabled;
varying vec3 v_kind; varying float v_far;
void vertex() { v_kind = COLOR.rgb; v_far = INSTANCE_CUSTOM.a; }
void fragment() { ALBEDO = v_kind * mix(0.025, 0.6, v_far); }"
        }
    };

    /// <summary>How far toward a map marker a model this big, drawn here, should look: 0 when it fills a good part of the
    /// screen, 1 once it is a few pixels across (distance over size, so it holds at every zoom).</summary>
    float WarFar(Vector3 at, float size)
    {
        var cam = GetViewport()?.GetCamera3D();
        if (cam == null || size <= 0) return 0;
        float ratio = (at - _near.ToLocal(cam.GlobalPosition)).Length() / size;       // the works live in _near's frame
        return Mathf.SmoothStep(40f, 400f, ratio);
    }

    // the map-scale markers: a glow in each kind's colour that takes over as its model shrinks to a few pixels
    MultiMeshInstance3D _warGlow;
    float[] _warGlowBuf;
    int _nWarGlow;

    void UpdateWarProps(Colony c, double year)
    {
        foreach (var p in _warPools.Values) p.K = 0;
        _warAt.Clear();
        if (_warGlow == null) { _warGlow = Glows(WarPropMax * 2); _warGlow.CustomAabb = Everywhere; _warGlowBuf = new float[WarPropMax * 2 * 20]; _near.AddChild(_warGlow); }
        _nWarGlow = 0;
        var seat = c.Seat;
        if (seat != null) FillWarProps(c, seat);
        foreach (var p in _warPools.Values) p.Mi.Multimesh.VisibleInstanceCount = p.K;
        if (_nWarGlow > 0) _warGlow.Multimesh.Buffer = _warGlowBuf;
        _warGlow.Multimesh.VisibleInstanceCount = _nWarGlow;
    }

    void FillWarProps(Colony c, Body seat)
    {
        var sim = c.Sim; var civ = c.Civ;
        var p0 = PosOfD(seat, _sv.VisYear);
        var empire = civ.Color;
        // each kind gets its own stretch of the ring, so a glance tells them apart
        Transform3D Put(WarPool pool, int slot, int of, float lane, float km, Color col, float spin = 0, Color? tint = null)
        {
            if (pool.K >= WarPropMax) return default;
            float a = lane + slot * (0.9f / Math.Max(of, 6)) + _clock * 0.002f;
            float r = seat.Radius * (2.2f + 0.6f * ((slot * 0.618f) % 1f));
            var trueAt = p0 + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(slot * 1.7f) * seat.Radius * 0.05f, Mathf.Sin(a) * r);
            var at = Drawn(trueAt, out float sq, out _);
            // true size is far below a pixel at system zoom: the works are drawn at least ~0.7% of the seat's radius per km
            float size = Math.Max(km * StarSystem.Km, seat.Radius * 0.007f * km);
            if (slot == 0) _warAt[pool.Key] = (trueAt, size);
            // the long axis (model +X) lies along the ring, the way the works drift round it
            var t = new Transform3D(new Basis(Vector3.Up, spin - a - Mathf.Pi / 2).Scaled(Vector3.One * size * sq), at);
            var mm = pool.Mi.Multimesh;
            mm.SetInstanceTransform(pool.K, t);
            mm.SetInstanceColor(pool.K, col);
            var e = tint ?? empire;
            float far = WarFar(at, size * sq);
            mm.SetInstanceCustomData(pool.K, new Color(e.R, e.G, e.B, far));
            pool.K++;
            Glow(_warGlowBuf, ref _nWarGlow, WarPropMax * 2, trueAt, size * 0.35f, col, 0.9f * far, 1.0f);
            return t;
        }

        var factories = WarPoolOf("factory", () => WarModel("Factory"));
        int fac = Math.Min(c.Foundries, 12);
        for (int i = 0; i < fac; i++) Put(factories, i, fac, 0f, 3f, WarData.Colour("factory"));

        var slips = WarPoolOf("slip", () => WarModel("Slip"));
        int style = Ships.Style(civ);
        int first = civ.Colonies.TakeWhile(x => x != c).Sum(sim.SlipsOf), mine = sim.SlipsOf(c);
        for (int i = 0; i < mine; i++)
        {
            var slip = Put(slips, i, mine, 1.2f, 5f, WarData.Colour("shipyard"));
            // the hull being built lies along its slip, growing from half the cradle's length to most of it
            if (first + i >= civ.Yard.Count || civ.Yard[first + i].Progress <= 0) continue;
            var build = civ.Yard[first + i];
            int cls = Math.Clamp(build.Hull, 0, Ships.Classes - 1);
            var hulls = WarPoolOf($"hull{cls}_{style}", () => Ships.HasModel(cls, style) ? Ships.Mesh(cls, style) : null);
            if (hulls.K >= WarPropMax) continue;
            float cradle = slip.Basis.X.Length();
            float len = cradle * 0.62f * (0.5f + 0.5f * (float)Math.Min(build.Progress, 1));
            // hulls run two units nose to tail along +Z: a quarter-turn lays them along the slip's +X
            var hb = (slip.Basis.Orthonormalized() * new Basis(Vector3.Up, Mathf.Pi / 2)).Scaled(Vector3.One * len * 0.5f);
            var hm = hulls.Mi.Multimesh;
            hm.SetInstanceTransform(hulls.K, new Transform3D(hb, slip.Origin));
            hm.SetInstanceColor(hulls.K, WarData.Colour("hull")); hm.SetInstanceCustomData(hulls.K, new Color(empire.R, empire.G, empire.B, WarFar(slip.Origin, len)));
            hulls.K++;
        }

        var barracks = WarPoolOf("barracks", () => WarModel("Barracks"));
        var here = c.Star.Id;
        var train = civ.Barracks.Where(t => t.At == here).ToList();
        for (int i = 0; i < train.Count; i++) Put(barracks, i, train.Count, 2.4f, 2f, WarData.Colour("barracks"));

        // special forces, purple: courses at the capital, and the player's own teams wherever they are (others' are secret)
        var specops = WarPoolOf("specops", () => WarModel("Insertion"));
        int courses = c.Home ? civ.Academy.Count : 0;
        var me = sim.Humanity;
        var teams = sim.Teams.Where(t => t.Civ == me.Id && t.To < 0 && t.At == here).ToList();
        for (int i = 0; i < courses + teams.Count; i++) Put(specops, i, courses + teams.Count, 5.6f, 1.5f, WarData.Colour("specops"), 0, i < courses ? null : me.Color);

        // armies on the ground (those aboard a fleet are drawn as its transports, WorksFleets)
        var armies = WarPoolOf("army", () => WarModel("Army"));
        var onGround = sim.Armies.Where(a => a.At == here && a.Fleet < 0).ToList();
        for (int i = 0; i < onGround.Count; i++) Put(armies, i, onGround.Count, 4.6f, 1.5f, WarData.Colour("transport"));
    }
}
