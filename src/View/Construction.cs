using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Starforge.View;

/// <summary>
/// Megastructures under construction. While one is going up its hull is split into slices in the order it would be
/// built (from the core outward, sweeping round), and fills in slice by slice over the years it takes. A faint
/// blueprint of the whole hangs where it will stand, welding sparks crawl along the build front, moving parts swing
/// into place near the end, and its lights, beams and shields come on only when it is finished.
/// </summary>
public partial class Megastructures
{
    const int Slices = 24;
    /// <summary>Test hook: show forced structures at this stage of construction.</summary>
    public static float ForceProg = 1f;
    /// <summary>Structures still being built this frame, by shown name, and how far along they are.</summary>
    public readonly Dictionary<string, float> Building = new();

    /// <summary>One rigid piece of the structure (its body, or a ring that turns) cut into slices in its own space.</summary>
    sealed class Piece
    {
        public MeshInstance3D[] Slice = new MeshInstance3D[Slices];
        /// <summary>The blueprint of each slice, shown only where nothing is built yet.</summary>
        public MeshInstance3D[] Ghost = new MeshInstance3D[Slices];
        public Vector3[][] Front = new Vector3[Slices][];
        public MultiMeshInstance3D Sparks, Convoys;
        public Vector3[] Sources;
        public float Reach;
        public Vector3 Centre;
        public Transform3D At;
        public MultiMeshInstance3D Gantries, Drones;
    }
    sealed class Site
    {
        public readonly List<GeometryInstance3D> Held = new();         // the finished look, hidden until it is done
        public readonly List<(MultiMeshInstance3D M, int Count)> Fleets = new();   // instanced parts appear in proportion
        public readonly List<Piece> Pieces = new();
        public readonly List<Light3D> Lights = new();
        public ShaderMaterial GhostMat, FrameMat;
    }
    readonly Dictionary<Node3D, Site> _sites = new();

    /// <summary>Show a structure at a stage of construction (1 = finished). Runs after Place has animated it.</summary>
    void Construct(Node3D n, float p)
    {
        if (p >= 0.999f) { if (_sites.Remove(n, out var old)) Finish(old); return; }
        if (!_sites.TryGetValue(n, out var s)) _sites[n] = s = Begin(n);
        s.Held.RemoveAll(g => !IsInstanceValid(g));
        foreach (var g in s.Held) g.Visible = false;
        foreach (var l in s.Lights) if (IsInstanceValid(l)) l.Visible = false;     // the lights come on when it is finished
        // three states per slice: built (its own look), going up (a bare frame, plated cell by cell as the slice
        // progresses, so the work moves continuously rather than in 24 jumps), and not yet begun (blueprint only)
        float at = p * Slices;
        int upTo = Mathf.Clamp((int)at, 0, Slices - 1);
        float fill = at - (int)at;
        foreach (var pc in s.Pieces)
        {
            for (int k = 0; k < Slices; k++)
            {
                if (pc.Slice[k] != null)
                {
                    pc.Slice[k].Visible = k <= upTo;
                    pc.Slice[k].MaterialOverride = k == upTo ? s.FrameMat : null;
                }
                if (pc.Ghost[k] != null) pc.Ghost[k].Visible = k > upTo;
            }
            Weld(pc, upTo);
            Work(pc, at);
            Supply(pc, upTo);
        }
        foreach (var (m, count) in s.Fleets) m.Multimesh.VisibleInstanceCount = (int)(count * p);
        s.GhostMat.SetShaderParameter("strength", Mathf.Lerp(0.16f, 0.09f, p));
        s.GhostMat.SetShaderParameter("time", _t);
        s.FrameMat.SetShaderParameter("fill", fill);
        s.FrameMat.SetShaderParameter("time", _t);
    }

    /// <summary>Finished: the hull goes back to its merged mesh, the lights come on, the scaffolding goes.</summary>
    static void Finish(Site s)
    {
        foreach (var g in s.Held) if (IsInstanceValid(g)) g.Visible = true;
        foreach (var l in s.Lights) if (IsInstanceValid(l)) l.Visible = true;
        foreach (var (m, _) in s.Fleets) if (IsInstanceValid(m)) m.Multimesh.VisibleInstanceCount = -1;
        foreach (var pc in s.Pieces)
        {
            foreach (var x in pc.Slice) if (x != null && IsInstanceValid(x)) x.QueueFree();
            foreach (var x in pc.Ghost) if (x != null && IsInstanceValid(x)) x.QueueFree();
            if (IsInstanceValid(pc.Sparks)) pc.Sparks.QueueFree();
            if (IsInstanceValid(pc.Convoys)) pc.Convoys.QueueFree();
            if (IsInstanceValid(pc.Gantries)) pc.Gantries.QueueFree();
            if (IsInstanceValid(pc.Drones)) pc.Drones.QueueFree();
        }
    }

    Site Begin(Node3D n)
    {
        var s = new Site();
        // each mesh belongs to the nearest part that moves on its own, or to the structure itself
        Node3D Anchor(Node x) { for (x = x.GetParent(); x != null && x != n; x = x.GetParent()) if (Animated.Contains(x.Name.ToString())) return (Node3D)x; return n; }
        var hulls = new Dictionary<Node3D, List<MeshInstance3D>>();
        foreach (var g in n.FindChildren("*", "GeometryInstance3D", true, false).Cast<GeometryInstance3D>())
        {
            if (g.IsQueuedForDeletion()) continue;
            if (g is MultiMeshInstance3D mm)
            {
                if (g.Name == "Sparkle" || mm.Multimesh == null || mm.Multimesh.VisibleInstanceCount >= 0) s.Held.Add(g);   // lights, or counts driven elsewhere
                else s.Fleets.Add((mm, mm.Multimesh.InstanceCount));
                continue;
            }
            s.Held.Add(g);
            if (g is MeshInstance3D mi && mi.Mesh != null && !IsGlow(mi) && !KeepProc(mi) && mi.Visible)
            {
                var a = Anchor(mi);
                if (!hulls.TryGetValue(a, out var l)) hulls[a] = l = new();
                l.Add(mi);
            }
        }
        foreach (var l in n.FindChildren("*", "Light3D", true, false).Cast<Light3D>()) if (l.Visible) s.Lights.Add(l);
        s.GhostMat = new ShaderMaterial { Shader = GhostShader() };
        s.FrameMat = new ShaderMaterial { Shader = FrameShader() };
        float reach = 0;
        foreach (var (a, hull) in hulls)
        {
            var pc = new Piece();
            Slice(a, hull, pc);
            reach = Mathf.Max(reach, pc.Reach * a.Scale.X);
            for (int k = 0; k < Slices; k++)
            {
                if (pc.Slice[k] == null) continue;
                pc.Ghost[k] = new MeshInstance3D { Name = "Blueprint", Mesh = pc.Slice[k].Mesh, Transform = pc.At, MaterialOverride = s.GhostMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                a.AddChild(pc.Ghost[k]);
            }
            var quad = new QuadMesh { Size = new Vector2(2, 2), Material = Works.GlowMat() };
            var spark = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = quad, InstanceCount = Welds };
            spark.VisibleInstanceCount = 0;
            pc.Sparks = new MultiMeshInstance3D { Name = "Welding", Multimesh = spark, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 1e5f };
            a.AddChild(pc.Sparks);
            var cq = new QuadMesh { Size = new Vector2(2, 2), Material = Works.GlowMat() };
            pc.Convoys = new MultiMeshInstance3D { Name = "Convoys", CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 1e5f,
                Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = cq, InstanceCount = Convoy, VisibleInstanceCount = 0 } };
            a.AddChild(pc.Convoys);
            // freight arrives from two or three directions (the system's mines and yards, the levy from other stars)
            var sr = new Random(a.Name.ToString().GetHashCode());
            pc.Sources = Enumerable.Range(0, 2 + sr.Next(2)).Select(_ => pc.Centre + new Vector3((float)sr.NextDouble() * 2 - 1, (float)sr.NextDouble() * 0.6f - 0.3f, (float)sr.NextDouble() * 2 - 1).Normalized() * pc.Reach * 4f).ToArray();
            pc.Gantries = Crew("scaffold", Gantries); a.AddChild(pc.Gantries);
            pc.Drones = Crew("constructor", Drones); a.AddChild(pc.Drones);
            s.Pieces.Add(pc);
            if (OS.HasEnvironment("SF_TRACE")) GD.Print($"[build] {n.Name}/{a.Name}: {hull.Count} meshes, reach {pc.Reach}");
        }
        s.GhostMat.SetShaderParameter("scan", 14f / Mathf.Max(reach, 1e-6f));
        float r0 = s.Pieces.Count > 0 ? s.Pieces.Max(x => x.Reach) : 1f;
        s.FrameMat.SetShaderParameter("cell", Mathf.Max(r0 * 0.045f, 1e-6f));
        return s;
    }

    static readonly Dictionary<Mesh, (ArrayMesh Full, ArrayMesh[] Slices, Vector3[][] Front, float Reach, Vector3 Centre)> _cut = new();

    /// <summary>Cut a piece into slices in build order (cached per mesh when it is one merged mesh). Returns the whole, for the blueprint.</summary>
    static ArrayMesh Slice(Node3D n, List<MeshInstance3D> hull, Piece s)
    {
        Mesh key = hull.Count == 1 ? hull[0].Mesh : null;
        if (key == null || !_cut.TryGetValue(key, out var cut))
        {
            cut = CutUp(n, hull);
            if (key != null) _cut[key] = cut;
        }
        // slices live in the piece's own space, alongside its parts
        var at = hull.Count == 1 ? Rel(n, hull[0]) : Transform3D.Identity;
        for (int k = 0; k < Slices; k++)
        {
            s.Front[k] = cut.Front[k].Select(p => at * p).ToArray();
            if (cut.Slices[k].GetSurfaceCount() == 0) continue;
            s.Slice[k] = new MeshInstance3D { Mesh = cut.Slices[k], Transform = at, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            n.AddChild(s.Slice[k]);
        }
        s.Reach = cut.Reach * at.Basis.Scale.X;
        s.Centre = at * cut.Centre;
        s.At = at;
        return cut.Full;
    }

    static Transform3D Rel(Node3D root, Node3D x)
    {
        var t = Transform3D.Identity;
        for (Node q = x; q != root && q is Node3D q3; q = q.GetParent()) t = q3.Transform * t;
        return t;
    }

    static (ArrayMesh, ArrayMesh[], Vector3[][], float, Vector3) CutUp(Node3D n, List<MeshInstance3D> hull)
    {
        bool single = hull.Count == 1;
        var parts = hull.Select(mi => (Mi: mi, T: single ? Transform3D.Identity : Rel(n, mi))).ToList();
        var lo = Vector3.One * 1e30f; var hi = -lo;
        foreach (var (mi, t) in parts) { var b = mi.GetAabb(); for (int i = 0; i < 8; i++) { var p = t * b.GetEndpoint(i); lo = lo.Min(p); hi = hi.Max(p); } }
        var centre = (lo + hi) * 0.5f;
        float reach = Mathf.Max((hi - lo).Length() * 0.5f, 1e-6f);

        // first pass: every triangle's place in the build order, so each slice gets an even share of the work
        // (a ring is all rim and no core: the order is relative to the piece's own spread)
        var mids = new List<Vector3>();
        foreach (var (mi, t) in parts)
            for (int si = 0; si < mi.Mesh.GetSurfaceCount(); si++)
            {
                if (mi.Mesh is ArrayMesh am0 && am0.SurfaceGetPrimitiveType(si) != Mesh.PrimitiveType.Triangles) continue;
                if ((mi.GetActiveMaterial(si) ?? mi.Mesh.SurfaceGetMaterial(si) ?? Fallback) == null) continue;
                var arr = mi.Mesh.SurfaceGetArrays(si);
                var v = (Vector3[])arr[(int)Mesh.ArrayType.Vertex];
                int[] idx = arr[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil ? null : (int[])arr[(int)Mesh.ArrayType.Index];
                int count = idx?.Length ?? v.Length;
                for (int i = 0; i + 2 < count; i += 3)
                    mids.Add((t * v[idx?[i] ?? i] + t * v[idx?[i + 1] ?? i + 1] + t * v[idx?[i + 2] ?? i + 2]) / 3f);
            }
        float dLo = 1e30f, dHi = 0;
        foreach (var m0 in mids) { float l = (m0 - centre).Length(); dLo = Mathf.Min(dLo, l); dHi = Mathf.Max(dHi, l); }
        float Order(Vector3 mid)
        {
            // the core first, spreading outward, sweeping round as it grows
            var d = mid - centre;
            float sweep = (Mathf.Atan2(d.Z, d.X) + Mathf.Pi) / Mathf.Tau;
            return 0.6f * Mathf.Clamp((d.Length() - dLo) / Mathf.Max(dHi - dLo, 1e-9f), 0, 1) + 0.4f * sweep;
        }
        var sorted = mids.Select(Order).OrderBy(x => x).ToArray();
        var edge = new float[Slices];
        for (int k = 0; k < Slices; k++) edge[k] = sorted.Length == 0 ? 0 : sorted[Math.Min(sorted.Length - 1, (k + 1) * sorted.Length / Slices)];

        var full = new Dictionary<Material, SurfaceTool>();
        var slices = Enumerable.Range(0, Slices).Select(_ => new Dictionary<Material, SurfaceTool>()).ToArray();
        var front = Enumerable.Range(0, Slices).Select(_ => new List<Vector3>()).ToArray();
        var rnd = new Random(7);
        static SurfaceTool Tool(Dictionary<Material, SurfaceTool> d, Material m)
        {
            if (!d.TryGetValue(m, out var st)) { st = new SurfaceTool(); st.Begin(Mesh.PrimitiveType.Triangles); d[m] = st; }
            return st;
        }
        foreach (var (mi, t) in parts)
            for (int si = 0; si < mi.Mesh.GetSurfaceCount(); si++)
            {
                if (mi.Mesh is ArrayMesh am && am.SurfaceGetPrimitiveType(si) != Mesh.PrimitiveType.Triangles) continue;
                var m = mi.GetActiveMaterial(si) ?? mi.Mesh.SurfaceGetMaterial(si) ?? Fallback;
                Tool(full, m).AppendFrom(mi.Mesh, si, t);
                var arr = mi.Mesh.SurfaceGetArrays(si);
                var v = (Vector3[])arr[(int)Mesh.ArrayType.Vertex];
                Vector3[] nr = arr[(int)Mesh.ArrayType.Normal].VariantType == Variant.Type.Nil ? null : (Vector3[])arr[(int)Mesh.ArrayType.Normal];
                Vector2[] uv = arr[(int)Mesh.ArrayType.TexUV].VariantType == Variant.Type.Nil ? null : (Vector2[])arr[(int)Mesh.ArrayType.TexUV];
                float[] tg = arr[(int)Mesh.ArrayType.Tangent].VariantType == Variant.Type.Nil ? null : (float[])arr[(int)Mesh.ArrayType.Tangent];
                int[] idx = arr[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil ? null : (int[])arr[(int)Mesh.ArrayType.Index];
                if (nr != null && nr.Length != v.Length) nr = null;
                if (uv != null && uv.Length != v.Length) uv = null;
                if (tg != null && tg.Length != v.Length * 4) tg = null;
                int count = idx?.Length ?? v.Length;
                var nb = t.Basis.Inverse().Transposed();
                for (int i = 0; i + 2 < count; i += 3)
                {
                    int a = idx?[i] ?? i, b = idx?[i + 1] ?? i + 1, c = idx?[i + 2] ?? i + 2;
                    var mid = (t * v[a] + t * v[b] + t * v[c]) / 3f;
                    float order = Order(mid);
                    int k = 0;
                    while (k < Slices - 1 && order > edge[k]) k++;
                    var st = Tool(slices[k], m);
                    foreach (int j in new[] { a, b, c })
                    {
                        if (nr != null) st.SetNormal((nb * nr[j]).Normalized());
                        if (uv != null) st.SetUV(uv[j]);
                        if (tg != null) st.SetTangent(new Plane((t.Basis * new Vector3(tg[j * 4], tg[j * 4 + 1], tg[j * 4 + 2])).Normalized(), tg[j * 4 + 3]));
                        st.AddVertex(t * v[j]);
                    }
                    if (front[k].Count < 300 && rnd.NextDouble() < 0.05) front[k].Add(mid);
                }
            }
        var whole = new ArrayMesh();
        foreach (var (m, st) in full) { st.Commit(whole); whole.SurfaceSetMaterial(whole.GetSurfaceCount() - 1, m); }
        var cuts = new ArrayMesh[Slices];
        for (int k = 0; k < Slices; k++)
        {
            cuts[k] = new ArrayMesh();
            foreach (var (m, st) in slices[k]) { st.Commit(cuts[k]); cuts[k].SurfaceSetMaterial(cuts[k].GetSurfaceCount() - 1, m); }
        }
        return (whole, cuts, front.Select(f => f.ToArray()).ToArray(), reach, centre);
    }

    static readonly Material Fallback = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.52f, 0.56f), Metallic = 0.7f, Roughness = 0.4f };

    const int Welds = 40;
    readonly float[] _weld = new float[Welds * 20];

    /// <summary>Welding sparks flicker along the build front, where the next slice is going up.</summary>
    void Weld(Piece s, int k)
    {
        var pts = s.Front[k];
        if (pts == null || pts.Length == 0) for (int j = k; j >= 0 && (pts == null || pts.Length == 0); j--) pts = s.Front[j];
        int nk = 0;
        if (pts != null && pts.Length > 0 && Main.Anim > 0)
        {
            int tick = (int)(_t * 8f);
            for (int i = 0; i < Welds; i++)
            {
                var r = new Random(i * 7919 + tick * 31 + k);
                if (r.NextDouble() < 0.4) continue;
                var at = pts[r.Next(pts.Length)];
                float sz = s.Reach * (0.004f + 0.006f * (float)r.NextDouble());
                bool hot = r.NextDouble() < 0.5;
                int o = nk++ * 20;
                Array.Clear(_weld, o, 20);
                _weld[o] = sz; _weld[o + 3] = at.X; _weld[o + 5] = sz; _weld[o + 7] = at.Y; _weld[o + 10] = sz; _weld[o + 11] = at.Z;
                _weld[o + 12] = 1f; _weld[o + 13] = hot ? 0.8f : 0.7f; _weld[o + 14] = hot ? 0.45f : 1f; _weld[o + 15] = 0.6f + 0.4f * (float)r.NextDouble();
                _weld[o + 16] = 0.6f; _weld[o + 17] = 0.5f;
            }
        }
        var mm = s.Sparks.Multimesh;
        mm.VisibleInstanceCount = 0;
        if (nk > 0) mm.Buffer = _weld;
        mm.VisibleInstanceCount = nk;
    }

    const int Convoy = 90;
    readonly float[] _convoy = new float[Convoy * 20];

    /// <summary>
    /// Freight streams in to the build front: strings of lit cargo pods from each source, each pod a little apart,
    /// slowing as it arrives. The site's appetite is visible from across the system.
    /// </summary>
    void Supply(Piece pc, int k)
    {
        var mm = pc.Convoys.Multimesh;
        var front = pc.Front[k];
        if (front == null || front.Length == 0 || pc.Sources == null || Works.Hide.Contains("crew")) { mm.VisibleInstanceCount = 0; return; }
        int n = 0, per = Convoy / pc.Sources.Length;
        float size = pc.Reach * 0.006f;
        for (int si = 0; si < pc.Sources.Length; si++)
            for (int i = 0; i < per && n < Convoy; i++)
            {
                // each pod runs source -> a point on the front over ~12 s, staggered along the lane
                float u = (_t * 0.08f + i / (float)per) % 1f;
                var dst = front[(i * 13 + si * 7) % front.Length];
                var src = pc.Sources[si] + (dst - pc.Centre).Normalized() * pc.Reach * 0.3f;
                float e = 1f - (1f - u) * (1f - u);                       // fast out, slowing into the site
                var pos = src.Lerp(dst, e);
                float fade = Mathf.Min(u * 8f, 1f) * Mathf.Min((1f - u) * 12f, 1f);
                int o = n++ * 20;
                Array.Clear(_convoy, o, 20);
                _convoy[o] = size; _convoy[o + 3] = pos.X; _convoy[o + 5] = size; _convoy[o + 7] = pos.Y; _convoy[o + 10] = size; _convoy[o + 11] = pos.Z;
                _convoy[o + 12] = 1f; _convoy[o + 13] = 0.78f; _convoy[o + 14] = 0.5f; _convoy[o + 15] = 0.9f * fade;
                _convoy[o + 16] = 0.5f; _convoy[o + 17] = 0.4f;
            }
        mm.VisibleInstanceCount = 0;
        mm.Buffer = _convoy;
        mm.VisibleInstanceCount = n;
    }

    const int Gantries = 10, Drones = 14;
    static readonly Dictionary<string, Mesh> _crewMesh = new();

    /// <summary>The construction crew's hardware, from the Blender kit (assets/stations/construction.py).</summary>
    static MultiMeshInstance3D Crew(string slug, int count)
    {
        if (!_crewMesh.TryGetValue(slug, out var mesh))
        {
            string path = $"res://assets/stations/{slug}/{slug}.glb";
            var inst = ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path).Instantiate<Node3D>() : null;
            mesh = inst?.FindChildren("*", "MeshInstance3D", true, false).Cast<MeshInstance3D>().FirstOrDefault()?.Mesh
                ?? new BoxMesh { Size = new Vector3(40, 2, 12), Material = Fallback };
            inst?.Free();
            _crewMesh[slug] = mesh;
        }
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = count, VisibleInstanceCount = 0 };
        return new MultiMeshInstance3D { Name = slug, Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 1e5f };
    }

    /// <summary>
    /// Gantries stand on the build front, facing out from the structure, and move on as each slice is finished;
    /// constructor drones shuttle between them. Both are scaled to the work (a gantry spans a tenth of the piece).
    /// </summary>
    void Work(Piece pc, float at)
    {
        int k = Mathf.Clamp((int)at, 0, Slices - 1);
        var here = pc.Front[k];
        var next = pc.Front[Mathf.Min(k + 1, Slices - 1)];
        int ng = 0, nd = 0;
        if (here.Length + next.Length > 0 && !Works.Hide.Contains("crew"))
        {
            float g = pc.Reach * 0.1f / 40f, d = pc.Reach * 0.03f / 18f;     // kit units are metres: gantry 40 m, drone 18 m
            Vector3 Pick(int i) => i % 2 == 0 && here.Length > 0 || next.Length == 0 ? here[i * 7 % here.Length] : next[i * 5 % next.Length];
            Basis Facing(Vector3 up, Vector3 fwd)
            {
                up = up.LengthSquared() > 1e-12f ? up.Normalized() : Vector3.Up;
                var x = (fwd - up * fwd.Dot(up)); x = x.LengthSquared() > 1e-12f ? x.Normalized() : up.Cross(Vector3.Forward).Normalized();
                if (!x.IsFinite() || x.LengthSquared() < 0.5f) x = up.Cross(Vector3.Right).Normalized();
                return new Basis(x, up, x.Cross(up));
            }
            var gm = pc.Gantries.Multimesh;
            for (int i = 0; i < Gantries; i++)
            {
                var p = Pick(i);
                var up = p - pc.Centre;
                gm.SetInstanceTransform(ng++, new Transform3D(Facing(up, new Vector3(Mathf.Sin(i * 2.4f), 0, Mathf.Cos(i * 2.4f))).Scaled(Vector3.One * g), p + up.Normalized() * pc.Reach * 0.004f));
            }
            var dm = pc.Drones.Multimesh;
            for (int i = 0; i < Drones; i++)
            {
                // each drone flits from one site to another and hovers there a while
                float cyc = _t * 0.08f + i * 0.37f;
                int leg = (int)cyc; float u = Mathf.SmoothStep(0.3f, 0.7f, cyc - leg);
                var a = Pick(i + leg * 3); var b = Pick(i + leg * 3 + 1);
                var p = a.Lerp(b, u);
                var up = p - pc.Centre;
                var lift = up.Normalized() * pc.Reach * (0.02f + 0.05f * Mathf.Sin(u * Mathf.Pi));
                dm.SetInstanceTransform(nd++, new Transform3D(Facing(up, b - a).Scaled(Vector3.One * d), p + lift));
            }
        }
        pc.Gantries.Multimesh.VisibleInstanceCount = ng;
        pc.Drones.Multimesh.VisibleInstanceCount = nd;
    }

    static Shader _frame;
    /// <summary>
    /// The slice going up: a bare lattice of girders, plated cell by cell as `fill` rises (cells chosen by hash, so
    /// plating spreads in patches), the newest plates still glowing from the welders.
    /// </summary>
    static Shader FrameShader() => _frame ??= new Shader
    {
        Code = @"shader_type spatial;
render_mode cull_disabled;
uniform float fill = 0.0;
uniform float time = 0.0;
uniform float cell = 1.0;
varying vec3 v_l;
float hash(vec3 p){ p = fract(p * 0.3183099 + 0.1); p *= 17.0; return fract(p.x * p.y * p.z * (p.x + p.y + p.z)); }
void vertex(){ v_l = VERTEX; }
void fragment(){
	vec3 q = v_l / cell;
	vec3 g = abs(fract(q) - 0.5);
	float girder = step(0.44, max(g.x, max(g.y, g.z)));
	float h = hash(floor(q));
	float plated = step(h, fill);
	if (girder < 0.5 && plated < 0.5) discard;
	float fresh = plated * (1.0 - smoothstep(0.0, 0.12, fill - h));     // plates laid in the last moments still glow
	ALBEDO = mix(vec3(0.16, 0.17, 0.19), vec3(0.42, 0.44, 0.47), plated);
	METALLIC = 0.8; ROUGHNESS = 0.45;
	EMISSION = vec3(1.0, 0.55, 0.2) * (fresh * 3.0 + girder * (1.0 - plated) * 0.05 * (0.6 + 0.4 * sin(time * 3.0 + h * 20.0)));
}"
    };

    static Shader _ghost;
    static Shader GhostShader() => _ghost ??= new Shader
    {
        Code = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
uniform float strength = 1.0;
uniform float time = 0.0;
uniform float scan = 1.0;     // scan lines per model unit
varying vec3 v_n; varying vec3 v_w; varying vec3 v_l;
void vertex(){ v_l = VERTEX; v_n = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz); v_w = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment(){
	// the blueprint: edges glow where the surface turns away, faint lines sweep across it
	float f = 1.0 - abs(dot(normalize(v_n), normalize(CAMERA_POSITION_WORLD - v_w)));
	float line = smoothstep(0.9, 1.0, fract(v_l.y * scan - time * 0.25));
	ALBEDO = vec3(0.3, 0.62, 1.0) * (pow(f, 2.5) * 0.45 + 0.025 + line * 0.15) * strength;
}"
    };
}
