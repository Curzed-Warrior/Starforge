using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// Fleets at the viewed star, drawn with their real hulls (Ships.Mesh), and the battles they fight. Each civ's fleets
/// park in formation at its own station 2.5 seat radii out: a wedge per fleet, squadrons in ranks by class with the
/// biggest at the back, army transports behind them. When two sides at war share the star (or a battle here is
/// younger than interface.battleShowYears) the formations face each other across the gap and fire with their main
/// weapon family. Every hull is drawn at least camera distance x 0.004 long so a fleet reads at any range; effects
/// are stateless functions of an effect clock that advances with the game (frozen when paused).
/// </summary>
public partial class Works
{
    /// <summary>One draw batch: every hull of one class and style (or the army transport), in one MultiMesh.</summary>
    sealed class HullPool { public MultiMeshInstance3D Mi; public float[] Buf; public int N, Stride; }
    readonly Dictionary<int, HullPool> _hullPools = new();
    const int TransportKey = -1;

    /// <summary>A hull to draw this frame: where (true system position), which way, how big (half-length, drawn).</summary>
    struct HullAt { public int Key; public D3 At; public Vector3 Fwd; public float Half; }
    readonly List<HullAt> _hulls = new();
    readonly List<int>[] _sideHulls = { new(), new() };

    /// <summary>A squadron (or a fleet's transports) to lay out: count to draw, mesh key, length.</summary>
    struct Entry { public int Civ, Fleet, Key, N; public float LenKm; }
    readonly List<Entry> _entries = new();
    readonly List<(int Key, Vector3 Local, float Half)> _local = new();
    readonly List<int> _civsHere = new();
    readonly List<Fleet> _fleetsHere = new();
    readonly List<(int From, int To, float W)> _spans = new();

    // effects
    const int BeamMax = 64, BurstMax = 32, SparkMax = 256;
    MultiMeshInstance3D _beams, _bursts, _sparks, _drives;
    float[] _beamBuf, _burstBuf, _sparkBuf, _driveBuf;
    int _nBeam, _nBurst, _nSpark, _nDrive;
    float _fxT;
    Simulation _sim;
    /// <summary>Test: --battlefam=A,B draws the battle with these weapon families (energy, kinetic, missile, craft, exotic).</summary>
    static string[] _famOverride;

    // the battle the camera can fly to
    bool _battleOn; D3 _battleC; float _battleR;
    /// <summary>Where to watch the battle from: horizontal direction from its centre out past the player's side
    /// (side A when the player isn't in it), a little off the line of fire so one formation doesn't hide the other.</summary>
    public Vector3 BattleView { get; private set; } = Vector3.Back;

    /// <summary>Is a battle being drawn here, and where (true system position) and how big at true scale.</summary>
    public bool BattleHere(out D3 centre, out float radius)
    {
        centre = _battleC; radius = _battleR;
        return _battleOn;
    }

    const string FxShader = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
float sqr(float x){ return x * x; }
varying vec4 v_col; varying float v_shape;
void vertex(){ v_col = COLOR; v_shape = INSTANCE_CUSTOM.x; }
void fragment(){
	vec2 c = UV * 2.0 - 1.0;
	float a;
	if (v_shape < 2.5) a = sqr(clamp(1.0 - UV.y, 0.0, 1.0)) * exp(-c.x * c.x * 4.84);
	else a = exp(-c.x * c.x * 6.0) * (0.6 + 0.4 * exp(-c.x * c.x * 60.0)) * smoothstep(0.0, 0.03, UV.y) * (1.0 - smoothstep(0.85, 1.0, UV.y));
	ALBEDO = v_col.rgb * v_col.a * a * 2.0;
}";

    void BuildFleets()
    {
        if (Array.Find(OS.GetCmdlineUserArgs(), x => x.StartsWith("--battlefam=")) is string bf) _famOverride = bf[12..].Split(',');
        // beams and tracers: Spectacle.Additive's tail and beam shapes, with the colour per instance
        var beamMat = new ShaderMaterial { Shader = new Shader { Code = FxShader }, RenderPriority = 8 };
        var bmm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = new QuadMesh { Size = Vector2.One, Material = beamMat }, InstanceCount = BeamMax, VisibleInstanceCount = 0 };
        _beams = new MultiMeshInstance3D { Multimesh = bmm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, CustomAabb = Everywhere };
        _beamBuf = new float[BeamMax * 20];
        _near.AddChild(_beams);
        // explosions, missiles, point-defence flashes, strike craft: the glow sprite that never shrinks below a few pixels
        _bursts = Glows(BurstMax); _bursts.CustomAabb = Everywhere; _burstBuf = new float[BurstMax * 20]; _near.AddChild(_bursts);
        _sparks = Glows(SparkMax); _sparks.CustomAabb = Everywhere; _sparkBuf = new float[SparkMax * 20]; _near.AddChild(_sparks);
        // each hull's drive, in its empire's colour: what shows of a fleet from across the system
        int cap = Math.Max(1, (int)WarData.D("interface", "fleetDrawMax"));
        _drives = Glows(cap); _drives.CustomAabb = Everywhere; _driveBuf = new float[cap * 20]; _near.AddChild(_drives);
    }

    HullPool Pool(int key)
    {
        if (_hullPools.TryGetValue(key, out var p)) return p;
        int cap = Math.Max(1, (int)WarData.D("interface", "fleetDrawMax"));
        Mesh mesh; bool cube = false;
        if (key == TransportKey) { mesh = Ships.Named("Transport"); cube = mesh == null; }
        else
        {
            int cls = key % Ships.Classes, style = key / Ships.Classes;
            cube = !Ships.HasModel(cls, style);
            mesh = cube ? null : Ships.Mesh(cls, style);
        }
        if (cube)
        {
            // no .glb at all for this class (a fresh clone): a grey hull-long box, the empire's colour on top
            var mat = new ShaderMaterial { Shader = new Shader { Code = CubeShader } };
            mesh = new BoxMesh { Size = new Vector3(0.5f, 0.35f, 2f), Material = mat };
        }
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = cube, UseCustomData = cube, Mesh = mesh, InstanceCount = cap, VisibleInstanceCount = 0 };
        p = new HullPool { Mi = new MultiMeshInstance3D { Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, CustomAabb = Everywhere }, Stride = cube ? 20 : 12 };
        p.Buf = new float[cap * p.Stride];
        _near.AddChild(p.Mi);
        _hullPools[key] = p;
        return p;
    }

    static float TransportLenKm() => Ships.TrueLengthM.TryGetValue("Transport", out var m) ? m / 1000f : 0.3f;

    /// <summary>Where the fleets at this star gather: the colony's seat, else the biggest world, else the star.</summary>
    (D3 At, float R) Anchor(Colony c)
    {
        var seat = c?.Seat ?? Sys.Bodies.Where(b => b.Kind != BodyKind.Belt).OrderByDescending(b => b.Radius).FirstOrDefault();
        return seat != null ? (PosOfD(seat, _sv.VisYear), seat.Radius) : (D3.Zero, Sys.StarRadius);
    }

    static uint Hash(int a, int b, int c = 0)
    {
        uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791);
        h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
        return h;
    }

    void UpdateFleets(Colony c, double year, float vdt)
    {
        _fxT += vdt;
        _battleOn = false;
        foreach (var p in _hullPools.Values) p.N = 0;
        _nBeam = _nBurst = _nSpark = _nDrive = 0;
        _hulls.Clear(); _sideHulls[0].Clear(); _sideHulls[1].Clear();
        var sim = _sim;
        if (sim != null && !Hide.Contains("fleets")) LayOut(sim, c);
        foreach (var p in _hullPools.Values)
        {
            if (p.N > 0) p.Mi.Multimesh.Buffer = p.Buf;
            p.Mi.Multimesh.VisibleInstanceCount = p.N;
        }
        if (_nBeam > 0) _beams.Multimesh.Buffer = _beamBuf;
        if (_nBurst > 0) _bursts.Multimesh.Buffer = _burstBuf;
        if (_nSpark > 0) _sparks.Multimesh.Buffer = _sparkBuf;
        if (_nDrive > 0) _drives.Multimesh.Buffer = _driveBuf;
        _drives.Multimesh.VisibleInstanceCount = _nDrive;
        _beams.Multimesh.VisibleInstanceCount = _nBeam;
        _bursts.Multimesh.VisibleInstanceCount = _nBurst;
        _sparks.Multimesh.VisibleInstanceCount = _nSpark;
    }

    void LayOut(Simulation sim, Colony c)
    {
        int star = Sys.Star.Id;
        _fleetsHere.Clear(); _civsHere.Clear();
        foreach (var f in sim.Fleets)
            if (f.At == star && !f.Moving && f.Civ < sim.Civs.Count)
            {
                _fleetsHere.Add(f);
                if (!_civsHere.Contains(f.Civ)) _civsHere.Add(f.Civ);
            }

        // a battle: the newest one fought here within the window, or two sides at war sharing the star
        double show = WarData.D("interface", "battleShowYears");
        BattleRecord rec = null;
        foreach (var b in sim.BattlesAt(star, sim.Year - show)) rec = b;
        int ca = -1, cb = -1;
        if (rec != null) { ca = rec.CivA; cb = rec.CivB; }
        else
            for (int i = 0; i < _civsHere.Count && ca < 0; i++)
                for (int j = i + 1; j < _civsHere.Count; j++)
                    if (sim.Civs[_civsHere[i]].AtWar.Contains(_civsHere[j])) { ca = _civsHere[i]; cb = _civsHere[j]; break; }
        bool battle = ca >= 0 && cb >= 0 && ca < sim.Civs.Count && cb < sim.Civs.Count;
        if (battle) { if (!_civsHere.Contains(ca)) _civsHere.Add(ca); if (!_civsHere.Contains(cb)) _civsHere.Add(cb); }
        if (_civsHere.Count == 0) return;

        // losses still to be seen: the hulls the battle destroyed are drawn at first and go one by one over the window
        float u = rec == null ? 0f : (float)Math.Clamp((sim.Year - rec.Year) / Math.Max(show, 1e-9), 0, 1);
        double ghostA = rec == null ? 0 : rec.LostA * (1 - u), ghostB = rec == null ? 0 : rec.LostB * (1 - u);

        // what to draw, squadron by squadron, inside the per-frame budget
        _entries.Clear();
        int left = (int)WarData.D("interface", "fleetDrawMax");
        foreach (int civId in _civsHere)
        {
            var civ = sim.Civs[civId];
            int style = Ships.Style(civ);
            double ghosts = battle ? civId == ca ? ghostA : civId == cb ? ghostB : 0 : 0;
            double total = 0;
            foreach (var f in _fleetsHere) if (f.Civ == civId) total += f.Ships;
            bool any = false;
            foreach (var f in _fleetsHere)
            {
                if (f.Civ != civId) continue;
                foreach (var sq in f.Squadrons.OrderBy(s => s.Hull))
                {
                    double extra = total > 0 ? ghosts * sq.Ships / total : 0;
                    int n = Ships.DrawCount(sq.Ships + extra, left);
                    if (n <= 0) continue;
                    left -= n; any = true;
                    _entries.Add(new Entry { Civ = civId, Fleet = f.Id, Key = style * Ships.Classes + sq.Hull, N = n, LenKm = Ships.LengthKm(sq.Hull) });
                }
                // armies aboard: one transport per 50,000 troops (troops are counted in billions)
                double troops = 0;
                foreach (var a in sim.Armies) if (a.Fleet == f.Id) troops += a.Troops;
                int nt = Ships.DrawCount(troops / 5e-5, left);
                if (nt > 0) { left -= nt; any = true; _entries.Add(new Entry { Civ = civId, Fleet = f.Id, Key = TransportKey, N = nt, LenKm = TransportLenKm() }); }
            }
            // a side wiped out in the fight still shows the ships it lost, of its best hull, until the window closes
            if (!any && ghosts > 0)
            {
                int n = Ships.DrawCount(ghosts, left);
                if (n > 0) { left -= n; _entries.Add(new Entry { Civ = civId, Fleet = -1, Key = style * Ships.Classes + civ.Hull, N = n, LenKm = Ships.LengthKm(civ.Hull) }); }
            }
        }
        if (_entries.Count == 0) return;
        float maxLen = 0;
        foreach (var e in _entries) maxLen = Math.Max(maxLen, e.LenKm);

        var (anchor, R) = Anchor(c);
        float Angle(int civId) => civId * 2.39996f + 0.7f;
        Vector3 Dir(int civId) { float a = Angle(civId); return new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); }

        if (battle)
        {
            // the two sides meet between their stations, out from the world, lined up along its orbit
            var da = Dir(ca); var db = Dir(cb);
            var m = da + db;
            var dc = m.LengthSquared() < 0.04f ? da : m.Normalized();
            var centre = anchor + dc * (2.5f * R);
            var axis = Vector3.Up.Cross(dc).Normalized();
            if (axis.Dot(da - db) > 0) axis = -axis;
            Drawn(centre, out _, out var tv);
            float dcam = Frame.Cam.DistanceTo(tv);
            float bigHalf = Half(maxLen, dcam);
            float gap = 40f * 2f * bigHalf;
            Formation(ca, centre - axis * (gap * 0.5f), axis, dcam, false, 0);
            Formation(cb, centre + axis * (gap * 0.5f), -axis, dcam, false, 1);
            // the battle's reach is half the gap at true scale: drawn sizes grow with the camera's distance, so they
            // can't set the range the camera flies to
            float trueHalf = maxLen * StarSystem.Km * 0.5f;
            _battleOn = true; _battleC = centre;
            _battleR = gap * 0.5f * trueHalf / Math.Max(bigHalf, 1e-12f);
            var mineSide = cb == sim.Humanity.Id ? axis : -axis;
            BattleView = mineSide.Rotated(Vector3.Up, 0.35f);
            bool aLoses = rec != null ? !rec.AWins : Strength(sim, ca) < Strength(sim, cb);
            float shareA = rec != null && rec.LostA + rec.LostB > 0 ? (float)(rec.LostA / (rec.LostA + rec.LostB)) : aLoses ? 0.75f : 0.25f;
            string famA = rec?.FamA ?? Starforge.Sim.Battles.BestFamily(sim.Civs[ca]);
            string famB = rec?.FamB ?? Starforge.Sim.Battles.BestFamily(sim.Civs[cb]);
            if (_famOverride?.Length == 2) { famA = _famOverride[0]; famB = _famOverride[1]; }
            Fire(0, famA, sim.Civs[ca].Color, axis);
            Fire(1, famB, sim.Civs[cb].Color, -axis);
            Explosions(shareA);
        }
        foreach (int civId in _civsHere)
        {
            if (battle && (civId == ca || civId == cb)) continue;
            var d = Dir(civId);
            var station = anchor + d * (2.5f * R);
            Drawn(station, out _, out var tv);
            Formation(civId, station, Vector3.Up.Cross(d).Normalized(), Frame.Cam.DistanceTo(tv), true, -1);
        }
    }

    double Strength(Simulation sim, int civId)
    {
        double s = 0;
        foreach (var f in _fleetsHere) if (f.Civ == civId) foreach (var q in f.Squadrons) s += q.Ships * Simulation.HullKg(q.Hull);
        return s;
    }

    /// <summary>Drawn half-length (the transform scale, as Ships.Size): true size, or at least 0.4% of the camera distance.</summary>
    static float Half(float lenKm, float dcam) => Math.Max(lenKm * StarSystem.Km * 0.5f, dcam * 0.004f);

    /// <summary>
    /// One civ's fleets in formation: a wedge per fleet, side by side, each squadron starting a new rank (ranks of
    /// 1, 3, 5 ... hulls), 3 hull lengths apart. front is the true position of the formation's leading edge (or its
    /// middle when centred), fwd the way it faces. Returns the formation's depth.
    /// </summary>
    float Formation(int civId, D3 front, Vector3 fwd, float dcam, bool centred, int side)
    {
        _local.Clear();
        float width = 0, depth = 0;
        int start = 0;
        int lastFleet = int.MinValue;
        float fz = 0, fwidth = 0; int row = 0;
        // first pass: each fleet's wedge in its own frame, x relative to the wedge's spine
        var spans = _spans; spans.Clear();
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if (e.Civ != civId) continue;
            if (e.Fleet != lastFleet)
            {
                if (lastFleet != int.MinValue) spans.Add((start, _local.Count, fwidth));
                lastFleet = e.Fleet; start = _local.Count; fz = 0; fwidth = 0; row = 0;
            }
            float half = Half(e.LenKm, dcam);
            float sp = 3f * 2f * half;
            int placed = 0;
            while (placed < e.N)
            {
                int inRow = Math.Min(2 * row + 1, e.N - placed);
                fz -= row == 0 && placed == 0 && _local.Count == start ? 0 : sp;
                for (int k = 0; k < inRow; k++)
                {
                    float x = (k - (inRow - 1) * 0.5f) * sp;
                    float y = (row % 2 == 0 ? 0.15f : -0.15f) * sp;
                    _local.Add((e.Key, new Vector3(x, y, fz), half));
                }
                fwidth = Math.Max(fwidth, inRow * sp);
                placed += inRow; row++;
            }
            depth = Math.Max(depth, -fz);
        }
        if (lastFleet != int.MinValue) spans.Add((start, _local.Count, fwidth));
        if (_local.Count == 0) return 0;
        // second pass: wedges side by side, the whole line centred on the station
        foreach (var s in spans) width += s.W;
        float between = 0.3f * width / spans.Count;
        width += (spans.Count - 1) * between;
        float x0 = -width * 0.5f;
        var right = fwd.Cross(Vector3.Up).Normalized();
        var up = right.Cross(fwd);
        float shift = centred ? depth * 0.5f : 0;
        var empire = _sim.Civs[civId].Color;
        foreach (var (from, to, w) in spans)
        {
            float cx = x0 + w * 0.5f;
            for (int i = from; i < to; i++)
            {
                var (key, l, half) = _local[i];
                // a slow drift in place, so a parked fleet is alive
                float bob = Mathf.Sin(_clock * 0.4f + i * 1.3f) * 0.08f * half;
                var at = front + right * (cx + l.X) + up * (l.Y + bob) + fwd * (l.Z + shift);
                if (PutHull(key, at, fwd, half, empire) && side >= 0) _sideHulls[side].Add(_hulls.Count - 1);
            }
            x0 += w + between;
        }
        return depth;
    }

    bool PutHull(int key, D3 at, Vector3 fwd, float half, Color empire)
    {
        var p = Pool(key);
        if (p.N * p.Stride >= p.Buf.Length) return false;
        var d = Drawn(at, out float sq, out _);
        var b = Basis.LookingAt(-fwd, Mathf.Abs(fwd.Y) > 0.98f ? Vector3.Right : Vector3.Up).Scaled(Vector3.One * (half * sq));
        int o = p.N * p.Stride;
        Put(p.Buf, o, b, d);
        if (p.Stride == 20)
        {
            var g = WarData.Colour("hull");
            p.Buf[o + 12] = g.R; p.Buf[o + 13] = g.G; p.Buf[o + 14] = g.B; p.Buf[o + 15] = 1;
            p.Buf[o + 16] = empire.R; p.Buf[o + 17] = empire.G; p.Buf[o + 18] = empire.B; p.Buf[o + 19] = 1;
        }
        p.N++;
        _hulls.Add(new HullAt { Key = key, At = at, Fwd = fwd, Half = half });
        Glow(_driveBuf, ref _nDrive, _driveBuf.Length / 20, at - fwd * (half * 1.05f), half * 0.3f, empire.Lerp(Colors.White, 0.45f), 0.55f, 0.2f);
        return true;
    }

    // ---------- effects ----------
    /// <summary>A beam or tracer quad from a to b (true positions), as wide on screen at any range.</summary>
    void Beam(D3 a, D3 b, Color col, float alpha, bool tail, float minW)
    {
        if (_nBeam >= BeamMax || alpha <= 0.01f) return;
        var da = Drawn(a, out float sq, out var ta);
        var db = Drawn(b, out _, out _);
        var dir = db - da; float len = dir.Length();
        if (len < 1e-9f) return;
        dir /= len;
        var toCam = (Frame.Cam - ta).Normalized();
        var sideV = dir.Cross(toCam);
        if (sideV.LengthSquared() < 1e-6f) sideV = dir.Cross(Vector3.Up);
        sideV = sideV.Normalized();
        float w = Math.Max(Frame.Cam.DistanceTo(ta) * 0.002f, minW) * sq;
        int o = _nBeam++ * 20;
        Put(_beamBuf, o, new Basis(sideV * w, dir * len, sideV.Cross(dir)), da + dir * len * 0.5f);
        _beamBuf[o + 12] = col.R; _beamBuf[o + 13] = col.G; _beamBuf[o + 14] = col.B; _beamBuf[o + 15] = alpha;
        _beamBuf[o + 16] = tail ? 2f : 3f; _beamBuf[o + 17] = _beamBuf[o + 18] = _beamBuf[o + 19] = 0;
    }

    void Glow(float[] buf, ref int n, int max, D3 at, float size, Color col, float alpha, float px = 0.6f)
    {
        if (n >= max || alpha <= 0.01f) return;
        var d = Drawn(at, out float sq, out _);
        int o = n++ * 20;
        Put(buf, o, Basis.Identity.Scaled(Vector3.One * size * sq), d);
        buf[o + 12] = col.R; buf[o + 13] = col.G; buf[o + 14] = col.B; buf[o + 15] = alpha;
        buf[o + 16] = px; buf[o + 17] = 0.5f; buf[o + 18] = buf[o + 19] = 0;
    }
    void Spark(D3 at, float size, Color col, float alpha, float px = 0.6f) => Glow(_sparkBuf, ref _nSpark, SparkMax, at, size, col, alpha, px);

    static float Frac(float x) => x - Mathf.Floor(x);

    /// <summary>One side's fire at the other, by its main weapon family. fwd points at the enemy.</summary>
    void Fire(int side, string fam, Color col, Vector3 fwd)
    {
        var mine = _sideHulls[side]; var theirs = _sideHulls[1 - side];
        if (mine.Count == 0 || theirs.Count == 0) return;
        int slots = Math.Clamp(mine.Count, 4, BeamMax / 2);
        var bright = col.Lerp(Colors.White, 0.35f);
        for (int i = 0; i < slots; i++)
        {
            float period = fam switch { "energy" => 1.4f, "kinetic" => 0.9f, "missile" => 3.2f, "craft" => 4f, "exotic" => 1.1f, _ => 1.4f };
            float t = _fxT / period + i * 0.618034f + side * 0.31f;
            int cyc = (int)Mathf.Floor(t); float ph = Frac(t);
            var sh = _hulls[mine[(int)(Hash(i, cyc, side) % (uint)mine.Count)]];
            var tg = _hulls[theirs[(int)(Hash(i, cyc, side + 7) % (uint)theirs.Count)]];
            D3 from = sh.At + sh.Fwd * sh.Half;
            var j = new Vector3((Hash(i, cyc, 1) % 1000) / 1000f - 0.5f, (Hash(i, cyc, 2) % 1000) / 1000f - 0.5f, (Hash(i, cyc, 3) % 1000) / 1000f - 0.5f);
            D3 to = tg.At + j * tg.Half;
            var path = (to - from).F; float dist = path.Length();
            switch (fam)
            {
                case "energy":
                    // lances of light, on for a moment, side colour
                    if (ph < 0.4f) { float a = Mathf.Sin(Mathf.Pi * ph / 0.4f); Beam(from, to, bright, a * 1.5f, false, sh.Half * 0.15f); Spark(to, tg.Half * 0.5f, bright, a * 0.8f); }
                    break;
                case "kinetic":
                {
                    // tracer streaks crossing the gap, a muzzle flash as they leave and a spark where they land
                    var head = from.Lerp(to, ph);
                    float len = dist * 0.12f;
                    Beam(head - path / dist * Math.Min(len, dist * ph + 1e-6f), head, new Color(1f, 0.82f, 0.45f), 0.9f, true, sh.Half * 0.08f);
                    if (ph < 0.1f) Spark(from, sh.Half * 0.35f, new Color(1f, 0.8f, 0.5f), 1f - ph * 10f);
                    if (ph > 0.9f) Spark(to, tg.Half * 0.4f, new Color(1f, 0.7f, 0.4f), (ph - 0.9f) * 10f);
                    break;
                }
                case "missile":
                {
                    // missiles arc across; point defence picks a third of them off on the way in
                    bool shot = Hash(i, cyc, 9) % 3 == 0;
                    float end = shot ? 0.72f : 1f;
                    for (int m = 0; m < 3; m++)
                    {
                        float p = ph - m * 0.04f;
                        if (p < 0 || p > end) continue;
                        float arc = Mathf.Sin(Mathf.Pi * p);
                        var lift = Vector3.Up.Cross(fwd).Normalized() * ((m - 1) * 0.06f * dist * arc) + Vector3.Up * (arc * dist * 0.12f);
                        Spark(from.Lerp(to, p) + lift, sh.Half * 0.25f, new Color(1f, 0.95f, 0.85f), 1f, 0.9f);
                    }
                    if (shot && ph > end && ph < end + 0.08f) Spark(from.Lerp(to, end) + Vector3.Up * (Mathf.Sin(Mathf.Pi * end) * dist * 0.12f), tg.Half * 0.6f, new Color(1f, 1f, 0.9f), 1f - (ph - end) / 0.08f);
                    if (!shot && ph > 0.96f) Spark(to, tg.Half * 0.8f, new Color(1f, 0.6f, 0.3f), (ph - 0.96f) * 25f);
                    break;
                }
                case "craft":
                {
                    // swarms of strike craft: out across the gap, a pass over the enemy, and back
                    if (i % 2 == 1) break;
                    float e = 0.5f - 0.5f * Mathf.Cos(Mathf.Tau * ph);
                    var c0 = from.Lerp(to, e * 0.95f);
                    for (int k = 0; k < 7; k++)
                    {
                        float a = _fxT * 1.7f + k * Mathf.Tau / 7f + i;
                        var off = new Vector3(Mathf.Cos(a), Mathf.Sin(a * 1.3f) * 0.5f, Mathf.Sin(a)) * (dist * 0.025f + sh.Half);
                        Spark(c0 + off, sh.Half * 0.15f, bright, 1f, 0.7f);
                    }
                    break;
                }
                default:
                {
                    // exotic: violet arcs, crooked and flickering
                    if (ph > 0.55f) break;
                    int fl = (int)(_fxT * 9f) + i;
                    var n = path.Cross(Vector3.Up).Normalized();
                    var mid = from.Lerp(to, 0.5) + n * (((Hash(i, fl, 4) % 1000) / 1000f - 0.5f) * dist * 0.12f) + Vector3.Up * (((Hash(i, fl, 5) % 1000) / 1000f - 0.5f) * dist * 0.08f);
                    var violet = new Color(0.75f, 0.35f, 1f);
                    float a2 = Mathf.Sin(Mathf.Pi * ph / 0.55f);
                    Beam(from, mid, violet, a2, false, sh.Half * 0.12f);
                    Beam(mid, to, violet, a2, false, sh.Half * 0.12f);
                    Spark(to, tg.Half * 0.6f, violet, a2);
                    break;
                }
            }
        }
    }

    /// <summary>Bursts on random ships of each side, as many as its share of the losses.</summary>
    void Explosions(float shareA)
    {
        int splitA = Mathf.RoundToInt(BurstMax * shareA);
        for (int i = 0; i < BurstMax; i++)
        {
            int side = i < splitA ? 0 : 1;
            var hs = _sideHulls[side];
            // never more bursts on a side than half its hulls, so the ships still show through the fire
            int k = side == 0 ? i : i - splitA;
            if (hs.Count == 0 || k >= Math.Max(1, hs.Count / 2)) continue;
            float t = _fxT / 2.2f + i * 0.618034f;
            int cyc = (int)Mathf.Floor(t); float ph = Frac(t);
            if (ph > 0.5f) continue;
            float q = ph / 0.5f;
            var h = _hulls[hs[(int)(Hash(i, cyc, 11) % (uint)hs.Count)]];
            var col = new Color(1f, 0.9f, 0.7f).Lerp(new Color(1f, 0.4f, 0.15f), q);
            Glow(_burstBuf, ref _nBurst, BurstMax, h.At + h.Fwd * (h.Half * 0.3f), h.Half * (0.35f + 1.2f * q), col, Mathf.Pow(1f - q, 1.5f) * 1.3f, 0.7f);
        }
    }
}
