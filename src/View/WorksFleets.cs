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
    sealed class HullPool { public MultiMeshInstance3D Mi; public float[] Buf; public int N, Stride; public Aabb Box; public bool Lit; }
    readonly Dictionary<int, HullPool> _hullPools = new(), _farPools = new();
    const int TransportKey = -1;
    /// <summary>The gap between two battle lines, in lengths of the biggest hull there: close enough that from where the
    /// camera frames the fight the big hulls read as ships, not specks.</summary>
    const float GapHulls = 12f;
    /// <summary>The battle camera's pitch: high enough that the two lines don't collapse into one.</summary>
    public const float BattlePitch = -0.6f;
    /// <summary>How far the battle camera sits from the centre, in battle radii: just outside the lines, so the nearer
    /// line's hulls are large and the far one is still in frame.</summary>
    public const float BattleFrame = 0.95f;
    int _beamLimit = BeamMax;

    /// <summary>A hull to draw this frame: where (true system position), which way, how big (half-length, drawn).
    /// Culled: too close to the lens to draw. Dying: blowing up now (holds its fire, not a target). Dead: gone.
    /// Ghost: for one of the hulls a battle destroyed, drawn back in for its window, its slot's stable key (0 otherwise).</summary>
    struct HullAt { public int Key; public D3 At; public Vector3 Fwd; public float Half; public Color Empire; public bool Culled, Dying, Dead; public long Ghost; }
    readonly List<HullAt> _hulls = new();
    readonly List<int>[] _sideHulls = { new(), new() };

    /// <summary>A squadron (or a fleet's transports) to lay out: count to draw, mesh key, length.
    /// Ghost: the squadron's key for its ghost hulls (see GhostKey), 0 if it has none; its slots from Alive on are ghosts.</summary>
    struct Entry { public int Civ, Fleet, Key, N, Alive; public float LenKm; public long Ghost; }
    readonly List<Entry> _entries = new();
    readonly List<(int Key, Vector3 Local, float Half, long Ghost)> _local = new();
    readonly List<int> _civsHere = new();
    readonly List<Fleet> _fleetsHere = new();
    readonly List<(int From, int To, float W)> _spans = new();
    readonly Dictionary<int, double> _troops = new();

    // effects
    const int BeamMax = 256, BillMax = 96, SparkMax = 384, BeamsPerSide = 96;
    MultiMeshInstance3D _beams, _sparks, _drives, _lights, _rings;
    float[] _beamBuf, _sparkBuf, _driveBuf, _lightBuf, _ringBuf;
    int _nBeam, _nSpark, _nDrive, _nLight, _nRing;
    float _fxT;
    /// <summary>Fleet hulls sit on their own render layer, lit by their own lights: the star's light, softer than on the
    /// worlds (the human paint is pale and goes flat white under the full sun), and a cool fill from the camera's side.</summary>
    public const uint FleetLayer = 1u << 19;
    DirectionalLight3D _fleetKey, _fleetFill;
    const float KeyShare = 0.5f;
    Simulation _sim;
    /// <summary>Test: --battlefam=A,B draws the battle with these weapon families (energy, kinetic, missile, craft, exotic).</summary>
    static string[] _famOverride;

    // the battle the camera can fly to
    bool _battleOn, _battleWas; D3 _battleOff; Body _battleSeat; float _battleR;

    // losses: the battle record they belong to, each side's drawn ghost hulls (the hulls it lost), how many of those
    // have blown up so far and when (effect clock; far in the past for one that went before the view saw it)
    const float KillLife = 1.8f;
    BattleRecord _killRec;
    bool _killFresh;
    /// <summary>For the battle record shown: how many ghost hulls each squadron draws (latched at first sight, so the
    /// set doesn't move as ships are built, laid up or arrive), and when each ghost slot blew up (effect clock).</summary>
    readonly Dictionary<long, int> _ghostLatch = new();
    /// <summary>The latch has been filled for the record shown: a squadron first seen after that (a fleet arriving mid-battle)
    /// draws no ghosts, so the explosions stay the battle's real losses.</summary>
    bool _ghostLatched;
    readonly Dictionary<long, float> _deathAt = new();
    /// <summary>A squadron's key for its ghosts: fleet (-1 / -2 for side A / B wiped out) and hull class. A ghost slot's key is
    /// this shifted left 12 bits plus its index in the run.</summary>
    static long GhostKey(int fleet, int hull) => ((long)(fleet + 3) << 8) | (long)(hull & 0xFF);
    // war.json numbers read every frame, looked up once (the data is loaded once)
    static double? _showYears; static int? _drawMax; static Color? _hullCol;
    static double ShowYears => _showYears ??= WarData.D("interface", "battleShowYears");
    static int DrawMax => _drawMax ??= (int)WarData.D("interface", "fleetDrawMax");
    static Color HullCol => _hullCol ??= WarData.Colour("hull");
    readonly List<Squadron> _sqs = new();
    static readonly Comparison<Squadron> ByHull = (a, b) => a.Hull.CompareTo(b.Hull);
    /// <summary>Where to watch the battle from: horizontal direction from its centre out past the player's side
    /// (side A when the player isn't in it), a little off the line of fire so one formation doesn't hide the other.</summary>
    public Vector3 BattleView { get; private set; } = Vector3.Back;

    /// <summary>Is a battle being drawn here, and where (true system position) and how big at true scale. The centre is
    /// worked out for the view's current year, not last frame's: the camera asks before the fleets are laid out, and at
    /// speed the world they gather at moves many battle radii a frame.</summary>
    public bool BattleHere(out D3 centre, out float radius)
    {
        centre = (_battleSeat != null ? PosOfD(_battleSeat, _sv.VisYear) : D3.Zero) + _battleOff; radius = _battleR;
        return _battleOn;
    }

    const string FxShader = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
float sqr(float x){ return x * x; }
// (a sin-free hash: fract(sin(big)) loses precision on the GPU and tears the noise into flat patches)
float hash(vec2 p){ vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p){
	vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p){ float v = 0.0, a = 0.5; for (int i = 0; i < 4; i++) { v += a * noise(p); p = p * 2.07 + 13.1; a *= 0.5; } return v; }
varying vec4 v_col; varying float v_shape; varying float v_open; varying float v_seed; varying float v_prog;
void vertex(){ v_col = COLOR; v_shape = INSTANCE_CUSTOM.x; v_open = INSTANCE_CUSTOM.y; v_seed = INSTANCE_CUSTOM.z; v_prog = INSTANCE_CUSTOM.w; }
void fragment(){
	vec2 c = UV * 2.0 - 1.0;
	vec3 col = v_col.rgb;
	if (v_shape < 0.5) {
		// fireball: billowing and ragged-edged, a white-hot heart cooling through yellow and orange to a deep red rim
		float r = length(c), q = v_prog;
		vec2 sd = vec2(v_seed * 17.0, v_seed * 5.3);
		// noise turned by the seed and warped by itself, so no two fireballs share the lattice's square grain
		float ang = v_seed * 6.2832; mat2 rot = mat2(vec2(cos(ang), sin(ang)), vec2(-sin(ang), cos(ang)));
		vec2 p = rot * c * (3.2 - 1.2 * q) + sd;
		float n = fbm(p + 1.6 * vec2(fbm(p * 0.7 + 5.2), fbm(p * 0.7 + 9.7)) - 0.8);
		float edge = 0.5 + 0.34 * n;
		float body = (1.0 - smoothstep(edge * 0.6, edge, r)) * (1.0 - smoothstep(0.62, 0.95, r));
		float heat = clamp((1.0 - r / edge) * (0.6 + 1.0 * n) * (1.5 - q), 0.0, 1.5);
		vec3 ramp = mix(vec3(0.5, 0.05, 0.015), vec3(1.0, 0.4, 0.07), smoothstep(0.05, 0.4, heat));
		ramp = mix(ramp, vec3(1.0, 0.82, 0.45), smoothstep(0.45, 0.8, heat));
		ramp = mix(ramp, vec3(1.0, 0.98, 0.93), smoothstep(0.85, 1.2, heat));
		ALBEDO = ramp * col * body * (0.6 + 2.6 * heat) * v_col.a * 2.6;
	} else if (v_shape < 1.5) {
		// shock ring: a thin bright edge, broken into arcs, nothing inside
		float r = length(c);
		float ang = atan(c.y, c.x);
		float n = noise(vec2(cos(ang), sin(ang)) * 3.2 + v_seed * 31.0);
		float a = exp(-sqr((r - 0.92) * 45.0)) * smoothstep(0.25, 0.75, n) * 1.6;
		a *= 1.0 - smoothstep(0.97, 1.0, r);
		ALBEDO = col * v_col.a * a * 1.7;
	} else if (v_shape < 2.5) {
		// tail: bright at the head (+Y), fading behind, with soft edges all round (a hard edge shows over a fireball)
		ALBEDO = col * v_col.a * sqr(clamp(1.0 - UV.y, 0.0, 1.0)) * smoothstep(0.0, 0.08, UV.y) * exp(-c.x * c.x * 4.84) * (1.0 - smoothstep(0.7, 1.0, abs(c.x))) * 2.0;
	} else {
		// lance: a soft coloured glow round a white-hot core; open ends join the next piece without a gap
		float ends = v_open > 0.5 ? 1.0 : smoothstep(0.0, 0.03, UV.y) * (1.0 - smoothstep(0.92, 1.0, UV.y));
		float glow = exp(-c.x * c.x * 5.0), core = exp(-c.x * c.x * 28.0);
		ALBEDO = (col * glow * 0.55 + mix(col, vec3(1.0), 0.7) * core) * v_col.a * ends * 1.6;
	}
}";
    const float ShapeFire = 0f, ShapeRing = 1f, ShapeTail = 2f, ShapeLance = 3f;

    void BuildFleets()
    {
        if (Array.Find(OS.GetCmdlineUserArgs(), x => x.StartsWith("--battlefam=")) is string bf) _famOverride = bf[12..].Split(',');
        // beams and tracers: Spectacle.Additive's tail and beam shapes, with the colour per instance
        var beamMat = new ShaderMaterial { Shader = new Shader { Code = FxShader }, RenderPriority = 8 };
        var bmm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = new QuadMesh { Size = Vector2.One, Material = beamMat }, InstanceCount = BeamMax, VisibleInstanceCount = 0 };
        _beams = new MultiMeshInstance3D { Multimesh = bmm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, CustomAabb = Everywhere };
        _beamBuf = new float[BeamMax * 20];
        _near.AddChild(_beams);
        // explosions' fireballs and shock rings, quads in the same shader
        var rmm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = new QuadMesh { Size = new Vector2(2, 2), Material = beamMat }, InstanceCount = BillMax, VisibleInstanceCount = 0 };
        _rings = new MultiMeshInstance3D { Multimesh = rmm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, CustomAabb = Everywhere };
        _ringBuf = new float[BillMax * 20];
        _near.AddChild(_rings);
        // explosions, missiles, point-defence flashes, strike craft: the glow sprite that never shrinks below a few pixels
        _sparks = Glows(SparkMax); _sparks.CustomAabb = Everywhere; _sparkBuf = new float[SparkMax * 20]; _near.AddChild(_sparks);
        // each hull's drive in its empire's colour (what shows of a fleet from across the system), and up close the
        // running lights of the human hulls
        int cap = Math.Max(1, (int)WarData.D("interface", "fleetDrawMax"));
        _drives = Glows(cap); _drives.CustomAabb = Everywhere; _driveBuf = new float[cap * 20]; _near.AddChild(_drives);
        // running lights have their own budget, so they can never crowd out a later hull's drive
        _lights = Glows(cap * 4); _lights.CustomAabb = Everywhere; _lightBuf = new float[cap * 4 * 20]; _near.AddChild(_lights);
        // the fleet layer's lights: the star's, softer, and a cool fill from above the camera so the side of a hull
        // facing the camera shows its shape even when the star lights the other side
        _fleetKey = new DirectionalLight3D { LightCullMask = FleetLayer, ShadowEnabled = false };
        _fleetFill = new DirectionalLight3D { LightCullMask = FleetLayer, LightEnergy = 0.9f, LightColor = new Color(0.78f, 0.86f, 1f), ShadowEnabled = false, LightSpecular = 0.4f };
        _near.AddChild(_fleetKey); _near.AddChild(_fleetFill);
    }

    /// <summary>The pool a hull draws into; a far one (Ships.FarRatio) gets its class's lean mesh.</summary>
    HullPool Pool(int key, bool far = false)
    {
        var pools = far ? _farPools : _hullPools;
        if (pools.TryGetValue(key, out var p)) return p;
        int cap = Math.Max(1, (int)WarData.D("interface", "fleetDrawMax"));
        Mesh mesh; bool cube = false;
        if (key == TransportKey) { mesh = Ships.Named("Transport"); cube = mesh == null; }
        else
        {
            int cls = key % Ships.Classes, style = key / Ships.Classes;
            cube = !Ships.HasModel(cls, style);
            mesh = cube ? null : Ships.Mesh(cls, style);
        }
        if (far && !cube) mesh = Ships.Far(mesh);
        if (cube)
        {
            // no .glb at all for this class (a fresh clone): a grey hull-long box, the empire's colour on top
            var mat = new ShaderMaterial { Shader = new Shader { Code = CubeShader } };
            mesh = new BoxMesh { Size = new Vector3(0.5f, 0.35f, 2f), Material = mat };
        }
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = cube, UseCustomData = cube, Mesh = mesh, InstanceCount = cap, VisibleInstanceCount = 0 };
        p = new HullPool
        {
            Mi = new MultiMeshInstance3D { Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, CustomAabb = Everywhere, MaterialOverlay = RimMat(), Layers = FleetLayer },
            Stride = cube ? 20 : 12, Box = mesh.GetAabb(),
            // the human hulls' windows and lamps are too small to see at battle range: they get running lights
            Lit = !cube && (key == TransportKey || key / Ships.Classes == 0),
        };
        p.Buf = new float[cap * p.Stride];
        _near.AddChild(p.Mi);
        pools[key] = p;
        return p;
    }

    static ShaderMaterial _rimMat;
    /// <summary>A faint cool rim light on every fleet hull, so a hull on the unlit side reads as a ship and not as its
    /// window strips alone.</summary>
    static ShaderMaterial RimMat() => _rimMat ??= new ShaderMaterial
    {
        Shader = new Shader
        {
            Code = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_back, shadows_disabled, fog_disabled;
void fragment(){
	float f = 1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	ALBEDO = vec3(0.32, 0.4, 0.55) * pow(f, 3.0) * 0.5;
}"
        }
    };

    static float TransportLenKm() => Ships.TrueLengthM.TryGetValue("Transport", out var m) ? m / 1000f : 0.3f;

    /// <summary>Where the fleets at this star gather: the colony's seat, else the biggest world, else the star.</summary>
    (D3 At, float R, Body Seat) Anchor(Colony c)
    {
        var seat = c?.Seat;
        if (seat == null) foreach (var b in Sys.Bodies) if (b.Kind != BodyKind.Belt && (seat == null || b.Radius > seat.Radius)) seat = b;
        return seat != null ? (PosOfD(seat, _sv.VisYear), seat.Radius, seat) : (D3.Zero, Sys.StarRadius, null);
    }

    static uint Hash(int a, int b, int c = 0)
    {
        uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791);
        h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
        return h;
    }

    void UpdateFleets(Colony c, double year, float vdt, Camera3D cam)
    {
        _fxT += vdt;
        _battleWas = _battleOn;
        _battleOn = false;
        if (cam != null)
        {
            // the fill light grazes in from the camera's upper right, so it models the hulls rather than flattening them
            var cb = cam.GlobalBasis;
            var from = (cb.X * 0.85f + cb.Y * 0.5f - cb.Z * 0.15f).Normalized();
            _fleetFill.GlobalBasis = Basis.LookingAt(-from, Mathf.Abs(from.Y) > 0.98f ? Vector3.Right : Vector3.Up);
        }
        var sun = _sv.SunLight;
        if (sun != null)
        {
            _fleetKey.GlobalBasis = sun.GlobalBasis; _fleetKey.Visible = sun.Visible;
            _fleetKey.LightColor = sun.LightColor; _fleetKey.LightEnergy = sun.LightEnergy * KeyShare;
        }
        foreach (var p in _hullPools.Values) p.N = 0;
        foreach (var p in _farPools.Values) p.N = 0;
        _nBeam = _nSpark = _nDrive = _nLight = _nRing = 0;
        _hulls.Clear(); _sideHulls[0].Clear(); _sideHulls[1].Clear();
        var sim = _sim;
        if (sim != null && !HiddenParts.Contains("fleets")) LayOut(sim, c);
        foreach (var p in _hullPools.Values.Concat(_farPools.Values))
        {
            if (p.N > 0) p.Mi.Multimesh.Buffer = p.Buf;
            p.Mi.Multimesh.VisibleInstanceCount = p.N;
        }
        if (_nBeam > 0) _beams.Multimesh.Buffer = _beamBuf;
        if (_nRing > 0) _rings.Multimesh.Buffer = _ringBuf;
        _rings.Multimesh.VisibleInstanceCount = _nRing;
        if (_nSpark > 0) _sparks.Multimesh.Buffer = _sparkBuf;
        if (_nDrive > 0) _drives.Multimesh.Buffer = _driveBuf;
        _drives.Multimesh.VisibleInstanceCount = _nDrive;
        if (_nLight > 0) _lights.Multimesh.Buffer = _lightBuf;
        _lights.Multimesh.VisibleInstanceCount = _nLight;
        _beams.Multimesh.VisibleInstanceCount = _nBeam;
        _sparks.Multimesh.VisibleInstanceCount = _nSpark;
    }

    void LayOut(Simulation sim, Colony c)
    {
        int star = Sys.Star.Id;
        _cmdOn = false;
        if (sim.Command is { } cmd && cmd.Star == star) { LayOutCommand(sim, c, cmd); return; }
        _fleetsHere.Clear(); _civsHere.Clear();
        foreach (var f in sim.Fleets)
            if (f.At == star && !f.Moving && f.Civ < sim.Civs.Count)
            {
                _fleetsHere.Add(f);
                if (!_civsHere.Contains(f.Civ)) _civsHere.Add(f.Civ);
            }

        // a battle: the newest one fought here within the window, or two sides at war sharing the star
        // armies aboard, by fleet, in one pass
        _troops.Clear();
        foreach (var a in sim.Armies) if (a.Fleet >= 0) _troops[a.Fleet] = _troops.GetValueOrDefault(a.Fleet) + a.Troops;

        double show = ShowYears;
        BattleRecord rec = null;
        // the newest battle here within the window (Simulation.BattlesAt, without the per-frame allocation)
        for (int i = sim.Battles.Count - 1; i >= 0 && rec == null; i--)
        {
            var b = sim.Battles[i];
            if (b.Star == star && b.Year >= sim.Year - show && b.Year <= sim.Year) rec = b;
        }
        int ca = -1, cb = -1;
        if (rec != null) { ca = rec.CivA; cb = rec.CivB; }
        else
            for (int i = 0; i < _civsHere.Count && ca < 0; i++)
                for (int j = i + 1; j < _civsHere.Count; j++)
                    if (sim.Civs[_civsHere[i]].AtWar.Contains(_civsHere[j])) { ca = _civsHere[i]; cb = _civsHere[j]; break; }
        bool battle = ca >= 0 && cb >= 0 && ca < sim.Civs.Count && cb < sim.Civs.Count;
        if (battle) { if (!_civsHere.Contains(ca)) _civsHere.Add(ca); if (!_civsHere.Contains(cb)) _civsHere.Add(cb); }
        if (_civsHere.Count == 0) return;

        // losses still to be seen: the hulls the battle destroyed (the sim has already removed them) are drawn back in
        // as ghosts for the whole window, in place, and blow up one by one as it passes (ScheduleBursts)
        float u = rec == null ? 0f : (float)Math.Clamp((sim.Year - rec.Year) / Math.Max(show, 1e-9), 0, 1);
        double ghostA = rec == null ? 0 : rec.LostA, ghostB = rec == null ? 0 : rec.LostB;
        // a new record (or a battle the view has only now come upon): the ghosts are counted afresh, and kills already
        // past are simply gone
        _killFresh = !_battleWas || !ReferenceEquals(rec, _killRec);
        if (!ReferenceEquals(rec, _killRec)) { _killRec = rec; _ghostLatch.Clear(); _deathAt.Clear(); _ghostLatched = false; }

        // what to draw, squadron by squadron, inside the per-frame budget
        _entries.Clear();
        int left = DrawMax;
        foreach (int civId in _civsHere)
        {
            var civ = sim.Civs[civId];
            int style = Ships.Style(civ);
            int sideOf = battle ? civId == ca ? 0 : civId == cb ? 1 : -1 : -1;
            double ghosts = sideOf == 0 ? ghostA : sideOf == 1 ? ghostB : 0;
            double total = 0;
            foreach (var f in _fleetsHere) if (f.Civ == civId) foreach (var q in f.Squadrons) total += q.Ships;
            bool any = false;
            foreach (var f in _fleetsHere)
            {
                if (f.Civ != civId) continue;
                _sqs.Clear(); _sqs.AddRange(f.Squadrons); _sqs.Sort(ByHull);
                foreach (var sq in _sqs)
                {
                    int key = style * Ships.Classes + sq.Hull;
                    // the squadron's ghosts: what it draws with its share of the losses beyond what it draws without them.
                    // They come first out of the budget: they are the battle's losses, and each must get to blow up
                    int g = 0;
                    if (ghosts > 0)
                    {
                        long gk = GhostKey(f.Id, sq.Hull);
                        if (!_ghostLatch.TryGetValue(gk, out g) && !_ghostLatched)
                        {
                            int with = Ships.DrawCount(sq.Ships + (total > 0 ? ghosts * sq.Ships / total : 0), left);
                            _ghostLatch[gk] = g = Math.Max(0, with - Ships.DrawCount(sq.Ships, left));
                        }
                        g = Math.Min(g, Math.Max(0, left));
                    }
                    int alive = Ships.DrawCount(sq.Ships, left - g);
                    if (alive + g > 0) { any = true; _entries.Add(new Entry { Civ = civId, Fleet = f.Id, Key = key, N = alive + g, Alive = alive, LenKm = Ships.LengthKm(sq.Hull), Ghost = g > 0 ? GhostKey(f.Id, sq.Hull) : 0 }); }
                    left -= alive + g;
                }
                // armies aboard: one transport per 50,000 troops (troops are counted in billions)
                double troops = _troops.GetValueOrDefault(f.Id);
                int nt = Ships.DrawCount(troops / 5e-5, left);
                if (nt > 0) { left -= nt; any = true; _entries.Add(new Entry { Civ = civId, Fleet = f.Id, Key = TransportKey, N = nt, LenKm = TransportLenKm() }); }
            }
            // a side wiped out in the fight still shows the ships it lost, of its best hull, until the window closes,
            // even once a fleet of its own arrives
            long wk = GhostKey(-1 - sideOf, civ.Hull);
            if (ghosts > 0 && (_ghostLatched ? _ghostLatch.ContainsKey(wk) : !any))
            {
                long gk = wk;
                if (!_ghostLatch.TryGetValue(gk, out int n)) _ghostLatch[gk] = n = Ships.DrawCount(ghosts, left);
                n = Math.Min(n, left);
                if (n > 0) { left -= n; _entries.Add(new Entry { Civ = civId, Fleet = -1, Key = style * Ships.Classes + civ.Hull, N = n, LenKm = Ships.LengthKm(civ.Hull), Ghost = gk }); }
            }
        }
        if (rec != null) _ghostLatched = true;
        if (_entries.Count == 0) return;
        float maxLen = 0;
        foreach (var e in _entries) maxLen = Math.Max(maxLen, e.LenKm);

        var (anchor, R, seatBody) = Anchor(c);
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
            float gap = GapHulls * 2f * bigHalf;
            float depthA = Formation(ca, centre - axis * (gap * 0.5f), axis, dcam, false, 0);
            float depthB = Formation(cb, centre + axis * (gap * 0.5f), -axis, dcam, false, 1);
            // the battle's reach (half the gap and the deeper line behind it) at true scale: drawn sizes grow with the
            // camera's distance, so they can't set the range the camera flies to
            float trueHalf = maxLen * StarSystem.Km * 0.5f;
            _battleOn = true; _battleSeat = seatBody; _battleOff = centre - anchor;
            _battleR = (gap * 0.5f + Math.Max(depthA, depthB)) * trueHalf / Math.Max(bigHalf, 1e-12f);
            var mineSide = cb == sim.Humanity.Id ? axis : -axis;
            BattleView = mineSide.Rotated(Vector3.Up, 1.3f);
            bool aLoses = rec != null ? !rec.AWins : Strength(sim, ca) < Strength(sim, cb);
            float shareA = rec != null && rec.LostA + rec.LostB > 0 ? (float)(rec.LostA / (rec.LostA + rec.LostB)) : aLoses ? 0.75f : 0.25f;
            string famA = rec?.FamA ?? Starforge.Sim.Battles.BestFamily(sim.Civs[ca]);
            string famB = rec?.FamB ?? Starforge.Sim.Battles.BestFamily(sim.Civs[cb]);
            if (_famOverride?.Length == 2) { famA = _famOverride[0]; famB = _famOverride[1]; }
            ScheduleBursts(rec, u, shareA);
            Fire(0, famA, sim.Civs[ca].Color);
            Fire(1, famB, sim.Civs[cb].Color);
            _beamLimit = BeamMax;
            Explosions();
        }
        foreach (int civId in _civsHere)
        {
            if (battle && (civId == ca || civId == cb)) continue;
            var d = Dir(civId);
            var station = anchor + d * (2.5f * R);
            Drawn(station, out _, out var tv);
            Formation(civId, station, Vector3.Up.Cross(d).Normalized(), Frame.Cam.DistanceTo(tv), true, -1);
        }
        FlushHulls();
    }

    double Strength(Simulation sim, int civId)
    {
        double s = 0;
        foreach (var f in _fleetsHere) if (f.Civ == civId) foreach (var q in f.Squadrons) s += q.Ships * Simulation.HullKg(q.Hull);
        return s;
    }

    /// <summary>Drawn half-length (the transform scale, as Ships.Size): true size, or at least 0.4% of the camera distance.</summary>
    static float Half(float lenKm, float dcam) => Math.Max(lenKm * StarSystem.Km * 0.5f, dcam * 0.004f);

    /// <summary>A hull's biggest extent over its length (1 for a hull longer than it is wide or tall), from its mesh.</summary>
    float Spread(int key) { var sz = Pool(key).Box.Size; return Math.Max(1f, Math.Max(sz.X, sz.Y) / Math.Max(sz.Z, 1e-3f)); }

    /// <summary>
    /// One civ's fleets in formation: a wedge per fleet, side by side, each squadron starting a new rank (ranks of
    /// 1, 3, 5 ... hulls), 3 hull lengths apart. front is the true position of the formation's leading edge (or its
    /// middle when centred), fwd the way it faces. Returns the formation's depth.
    /// </summary>
    float Formation(int civId, D3 front, Vector3 fwd, float dcam, bool centred, int side)
    {
        _local.Clear(); _spanFleet.Clear();
        float width = 0, depth = 0;
        int start = 0;
        int lastFleet = int.MinValue;
        float fz = 0, fwidth = 0, lastSp = 0; int row = 0;
        // first pass: each fleet's wedge in its own frame, x relative to the wedge's spine
        var spans = _spans; spans.Clear();
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if (e.Civ != civId) continue;
            if (e.Fleet != lastFleet)
            {
                if (lastFleet != int.MinValue) { spans.Add((start, _local.Count, fwidth)); _spanFleet.Add(lastFleet); }
                lastFleet = e.Fleet; start = _local.Count; fz = 0; fwidth = 0; row = 0; lastSp = 0;
            }
            float half = Half(e.LenKm, dcam);
            // 3 hull lengths apart, or 3 spans for a hull wider or taller than it is long
            float sp = 3f * 2f * half * Spread(e.Key);
            int placed = 0;
            while (placed < e.N)
            {
                int inRow = Math.Min(2 * row + 1, e.N - placed);
                // the first rank of a squadron behind another's is spaced for both classes, so small hulls behind big
                // ones don't sit inside them
                float pitch = placed == 0 && lastSp > 0 ? Math.Max(sp, 0.5f * (sp + lastSp)) : sp;
                fz -= row == 0 && placed == 0 && _local.Count == start ? 0 : pitch;
                for (int k = 0; k < inRow; k++)
                {
                    float x = (k - (inRow - 1) * 0.5f) * sp;
                    float y = (row % 2 == 0 ? 0.15f : -0.15f) * sp;
                    // the squadron's ghosts are its last slots, after the hulls still flying
                    int slot = placed + k;
                    _local.Add((e.Key, new Vector3(x, y, fz), half, e.Ghost != 0 && slot >= e.Alive ? (e.Ghost << 12) | (long)(slot - e.Alive) : 0));
                }
                fwidth = Math.Max(fwidth, inRow * sp);
                placed += inRow; row++;
            }
            lastSp = sp;
            depth = Math.Max(depth, -fz);
        }
        if (lastFleet != int.MinValue) { spans.Add((start, _local.Count, fwidth)); _spanFleet.Add(lastFleet); }
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
        int spanNo = -1;
        foreach (var (from, to, w) in spans)
        {
            spanNo++;
            float cx = x0 + w * 0.5f;
            // a commanded unit sits where its orders put it: sideways across the line, and back from it
            Vector3 uoff = Vector3.Zero; int unitId = _spanFleet[spanNo];
            if (_cmdOn && _unitOff.TryGetValue(unitId, out var uo)) uoff = right * uo.Lat - fwd * uo.Dep;
            for (int i = from; i < to; i++)
            {
                var (key, l, half, ghost) = _local[i];
                // a slow drift in place, so a parked fleet is alive
                float bob = Mathf.Sin(_clock * 0.4f + i * 1.3f) * 0.08f * half;
                // each hull a little off its slot and off the line's heading, as crews hold station, not a stamped grid
                float J(int k) => (Hash(civId, i, k) % 1000) / 1000f - 0.5f;
                // (at most a twelfth of the spacing each way, so neighbours never touch)
                float js = 0.33f * 3f * half * Spread(key);
                var jit = right * (J(41) * js) + up * (J(42) * js * 0.7f) + fwd * (J(43) * js);
                var hfwd = fwd.Rotated(up, J(44) * 0.22f).Rotated(right, J(45) * 0.1f);
                var at = front + right * (cx + l.X) + up * (l.Y + bob) + fwd * (l.Z + shift) + jit + uoff;
                AddHull(key, at, hfwd, half, empire, ghost);
                if (side >= 0) _sideHulls[side].Add(_hulls.Count - 1);
                if (_cmdOn) { if (!_unitHulls.TryGetValue(unitId, out var uh)) _unitHulls[unitId] = uh = new List<int>(); uh.Add(_hulls.Count - 1); }
            }
            x0 += w + between;
        }
        return depth;
    }

    /// <summary>Adds a hull to this frame's list. One the lens is all but inside would fill the screen as a smear, so it
    /// is kept (the battle's bookkeeping counts it) but marked culled: not drawn, not firing, not a target. Only that
    /// close (CullRatio): a big hull merely near the camera stays, rather than popping out of view.</summary>
    void AddHull(int key, D3 at, Vector3 fwd, float half, Color empire, long ghost)
    {
        Drawn(at, out _, out var tv);
        bool culled = half > Frame.Cam.DistanceTo(tv) * CullRatio;
        _hulls.Add(new HullAt { Key = key, At = at, Fwd = fwd, Half = half, Empire = empire, Culled = culled, Ghost = ghost });
    }

    const float CullRatio = 0.6f;
    /// <summary>A missile's arc height, as a share of the distance it flies.</summary>
    const float MissileArc = 0.06f;
    static readonly Color NavRed = new(1f, 0.12f, 0.06f), NavGreen = new(0.15f, 1f, 0.3f), Lamp = new(1f, 0.8f, 0.5f);

    /// <summary>Every hull still flying into its pool, with its drive glow and (human hulls, near enough to show them)
    /// its running lights: red to port, green to starboard, a white strobe and a few lit windows.</summary>
    void FlushHulls()
    {
        for (int i = 0; i < _hulls.Count; i++)
        {
            var h = _hulls[i];
            if (h.Dead || h.Culled) continue;
            var d = Drawn(h.At, out float sq, out var tv);
            var p = Pool(h.Key, Frame.Cam.DistanceTo(tv) > h.Half * 2f * Ships.FarRatio);
            if (p.N * p.Stride >= p.Buf.Length) continue;
            var basis = Basis.LookingAt(-h.Fwd, Mathf.Abs(h.Fwd.Y) > 0.98f ? Vector3.Right : Vector3.Up);
            int o = p.N * p.Stride;
            Put(p.Buf, o, basis.Scaled(Vector3.One * (h.Half * sq)), d);
            if (p.Stride == 20)
            {
                var g = HullCol;
                p.Buf[o + 12] = g.R; p.Buf[o + 13] = g.G; p.Buf[o + 14] = g.B; p.Buf[o + 15] = 1;
                p.Buf[o + 16] = h.Empire.R; p.Buf[o + 17] = h.Empire.G; p.Buf[o + 18] = h.Empire.B; p.Buf[o + 19] = 1;
            }
            p.N++;
            Glow(_driveBuf, ref _nDrive, _driveBuf.Length / 20, h.At - h.Fwd * (h.Half * 1.05f), h.Half * 0.3f, h.Empire.Lerp(Colors.White, 0.45f), 0.55f, 0.2f);
            // running lights only where the hull is a dozen pixels or more: on a speck they are noise
            if (!p.Lit || h.Half < Frame.Cam.DistanceTo(tv) * 0.006f) continue;
            var bx = p.Box; var lo = bx.Position; var hi = bx.End; float cz = (lo.Z + hi.Z) * 0.5f;
            D3 L(float x, float y, float z) => h.At + (basis.X * x + basis.Y * y + basis.Z * z) * h.Half;
            float lamp = h.Half * 0.05f; int max = _lightBuf.Length / 20;
            Glow(_lightBuf, ref _nLight, max, L(hi.X * 0.95f, 0, cz), lamp, NavRed, 2.2f, 0.8f);
            Glow(_lightBuf, ref _nLight, max, L(lo.X * 0.95f, 0, cz), lamp, NavGreen, 2.2f, 0.8f);
            if (Frac(_clock * 0.7f + i * 0.37f) < 0.1f) Glow(_lightBuf, ref _nLight, max, L(0, hi.Y * 1.05f, lo.Z * 0.5f), lamp * 1.3f, Colors.White, 2.6f, 0.9f);
            // windows along the top of the spine
            for (int w = 0; w < 4; w++) Glow(_lightBuf, ref _nLight, max, L(0, hi.Y * 1.02f, Mathf.Lerp(lo.Z * 0.55f, hi.Z * 0.6f, w / 3f)), lamp * 0.7f, Lamp, 1.4f, 0.55f);
        }
    }

    // ---------- effects ----------
    /// <summary>A beam or tracer quad from a to b (true positions), as wide on screen at any range.</summary>
    void Beam(D3 a, D3 b, Color col, float alpha, float shape, float minW, float widthK = 0.002f, bool open = false)
    {
        if (_nBeam >= _beamLimit || alpha <= 0.01f) return;
        var da = Drawn(a, out float sq, out var ta);
        var db = Drawn(b, out _, out var tb);
        var dir = db - da; float len = dir.Length();
        if (len < 1e-9f) return;
        dir /= len;
        // the camera's nearest approach to the beam: a beam passing by the lens would sweep across the screen, so it fades
        var seg = tb - ta; float sl = Math.Max(seg.Length(), 1e-12f);
        float u = Mathf.Clamp((Frame.Cam - ta).Dot(seg) / (sl * sl), 0, 1);
        var near = ta + seg * u;
        float dClose = Frame.Cam.DistanceTo(near);
        alpha *= Mathf.SmoothStep(sl * 0.08f, sl * 0.3f, dClose);
        if (alpha <= 0.01f) return;
        var toCam = (Frame.Cam - near).Normalized();
        var sideV = dir.Cross(toCam);
        if (sideV.LengthSquared() < 1e-6f) sideV = dir.Cross(Vector3.Up);
        sideV = sideV.Normalized();
        // never under about 1.6 px wide: a thinner quad breaks up into dots on screen
        float w = Math.Max(dClose * Math.Max(widthK, shape == ShapeTail ? 0.0026f : 0.0018f), Math.Min(minW, dClose * 0.01f)) * sq;
        int o = _nBeam++ * 20;
        Put(_beamBuf, o, new Basis(sideV * w, dir * len, sideV.Cross(dir)), da + dir * len * 0.5f);
        _beamBuf[o + 12] = col.R; _beamBuf[o + 13] = col.G; _beamBuf[o + 14] = col.B; _beamBuf[o + 15] = alpha;
        _beamBuf[o + 16] = shape; _beamBuf[o + 17] = open ? 1f : 0f; _beamBuf[o + 18] = _beamBuf[o + 19] = 0;
    }

    void Glow(float[] buf, ref int n, int max, D3 at, float size, Color col, float alpha, float px = 0.6f)
    {
        if (n >= max || alpha <= 0.01f) return;
        var d = Drawn(at, out float sq, out var tv);
        // a sprite right by the lens would fill the screen: fade it out as it gets that close
        alpha *= 1f - Mathf.SmoothStep(0.04f, 0.1f, size / Math.Max(Frame.Cam.DistanceTo(tv), 1e-12f));
        if (alpha <= 0.01f) return;
        int o = n++ * 20;
        Put(buf, o, Basis.Identity.Scaled(Vector3.One * size * sq), d);
        buf[o + 12] = col.R; buf[o + 13] = col.G; buf[o + 14] = col.B; buf[o + 15] = alpha;
        buf[o + 16] = px; buf[o + 17] = 0.5f; buf[o + 18] = buf[o + 19] = 0;
    }
    void Spark(D3 at, float size, Color col, float alpha, float px = 0.6f) => Glow(_sparkBuf, ref _nSpark, SparkMax, at, size, col, alpha, px);

    static float Frac(float x) => x - Mathf.Floor(x);

    /// <summary>A hull of this side able to fight (not culled, blowing up or gone), found from a hashed start; -1 if none.</summary>
    int PickLive(List<int> hs, uint h)
    {
        int n = hs.Count;
        if (n == 0) return -1;
        int s = (int)(h % (uint)n);
        for (int k = 0; k < n; k++)
        {
            int idx = hs[(s + k) % n];
            var t = _hulls[idx];
            if (!t.Culled && !t.Dying && !t.Dead) return idx;
        }
        return -1;
    }

    static float Unit(uint h) => (h % 1000) / 1000f;

    /// <summary>One side's fire at the other, by its main weapon family: every shot runs from a live hull of this side
    /// to a live hull of the other.</summary>
    void Fire(int side, string fam, Color col) => FireAt(_sideHulls[side], _sideHulls[1 - side], side, fam, col, 16);

    /// <summary>Fire from the hulls in `mine` at the hulls in `theirs` (indices into _hulls). `side` also seeds the shot pattern,
    /// so give a different one to each stream; at most `maxSlots` shots are in the air at once.</summary>
    void FireAt(List<int> mine, List<int> theirs, int side, string fam, Color col, int maxSlots)
    {
        if (mine.Count == 0 || theirs.Count == 0) return;
        int slots = Math.Clamp(mine.Count, 4, 16);
        if (fam == "kinetic") slots = Math.Clamp(mine.Count / 2, 3, 7);     // a few streams, so the hulls and hits stay the picture
        slots = Math.Min(slots, maxSlots);
        // each side gets its share of the beam pool, so one family's many pieces can't starve the other side or the explosions
        _beamLimit = Math.Min(BeamMax, _nBeam + BeamsPerSide);
        var bright = col.Lerp(Colors.White, 0.35f);
        float period = fam switch { "energy" => 1.4f, "kinetic" => 1.5f, "missile" => 3.2f, "craft" => 4f, "exotic" => 1.1f, _ => 1.4f };
        for (int i = 0; i < slots; i++)
        {
            float t = _fxT / period + i * 0.618034f + side * 0.31f;
            int cyc = (int)Mathf.Floor(t); float ph = Frac(t);
            int si = PickLive(mine, Hash(i, cyc, side)); if (si < 0) continue;
            int ti = PickLive(theirs, Hash(i, cyc, side + 7)); if (ti < 0) continue;
            var sh = _hulls[si]; var tg = _hulls[ti];
            // the gun: a mount on the hull's forward half, off its spine
            var sideways = sh.Fwd.Cross(Vector3.Up); if (sideways.LengthSquared() < 1e-6f) sideways = Vector3.Right;
            sideways = sideways.Normalized();
            D3 from = sh.At + sh.Fwd * (sh.Half * (0.2f + 0.5f * Unit(Hash(i, cyc, 4)))) + sideways * (sh.Half * (Unit(Hash(i, cyc, 5)) - 0.5f) * 0.4f);
            var j = new Vector3(Unit(Hash(i, cyc, 1)) - 0.5f, Unit(Hash(i, cyc, 2)) - 0.5f, Unit(Hash(i, cyc, 3)) - 0.5f);
            D3 to = tg.At + j * (tg.Half * 0.6f);
            var path = (to - from).F; float dist = path.Length();
            if (dist < 1e-12f) continue;
            switch (fam)
            {
                case "energy":
                    // lances of light, on for a moment, side colour
                    if (ph < 0.4f) { float a = Mathf.Sin(Mathf.Pi * ph / 0.4f); Beam(from, to, col, a * 0.8f, ShapeLance, sh.Half * 0.1f, 0.005f); Spark(to, tg.Half * 0.5f, bright, a * 0.8f); Spark(from, sh.Half * 0.2f, bright, a * 0.6f); }
                    break;
                case "kinetic":
                {
                    // a burst of rounds walking from the gun to the target: a dashed line of tracers in flight along it,
                    // the muzzle flashing while it fires and sparks where the rounds strike
                    const int rounds = 4; const float gapPh = 0.08f, flight = 0.42f;
                    float len = Math.Min(dist * 0.05f, sh.Half * 3f);
                    // each side's rounds in its own colour, warmed toward white-hot, so the two streams read as fire and return fire
                    var gold = col.Lerp(new Color(1f, 0.9f, 0.7f), 0.45f);
                    for (int r = 0; r < rounds; r++)
                    {
                        float p = (ph - r * gapPh) / flight;
                        if (p < 0f || p > 1.08f) continue;
                        if (p <= 1f)
                        {
                            var head = from.Lerp(to, p);
                            Beam(head - path / dist * Math.Min(len, dist * p + 1e-6f), head, gold, 0.5f, ShapeTail, sh.Half * 0.03f);
                        }
                        if (p > 0.93f) Spark(to + j * (tg.Half * 0.15f * r), tg.Half * 0.22f, new Color(1f, 0.75f, 0.4f), 1.2f * (1.08f - p) / 0.15f, 0.5f);
                    }
                    if (ph < rounds * gapPh) Spark(from, sh.Half * 0.2f, new Color(1f, 0.85f, 0.55f), 0.6f + 0.4f * Mathf.Sin(ph * 90f), 0.45f);
                    break;
                }
                case "missile":
                {
                    // missiles arc across, half over the lines and half under, low enough to stay between them; point
                    // defence picks a third of them off on the way in
                    bool shot = Hash(i, cyc, 9) % 3 == 0;
                    float end = shot ? 0.72f : 1f;
                    float arc = (Hash(i, cyc, 11) & 1) == 0 ? MissileArc : -MissileArc;
                    if (ph <= end)
                    {
                        // a bright head on a short exhaust trail, along its arc
                        D3 At(float p) => from.Lerp(to, p) + Vector3.Up * (Mathf.Sin(Mathf.Pi * p) * dist * arc);
                        var head = At(ph);
                        var back = At(Math.Max(ph - 0.06f, 0f));
                        Beam(back, head, new Color(1f, 0.75f, 0.45f), 0.8f, ShapeTail, sh.Half * 0.05f, 0.0025f);
                        Spark(head, sh.Half * 0.18f, new Color(1f, 0.97f, 0.9f), 1f, 0.6f);
                    }
                    if (shot && ph > end && ph < end + 0.08f) Spark(from.Lerp(to, end) + Vector3.Up * (Mathf.Sin(Mathf.Pi * end) * dist * arc), tg.Half * 0.6f, new Color(1f, 1f, 0.9f), 1f - (ph - end) / 0.08f);
                    if (!shot && ph > 0.96f) Spark(to, tg.Half * 0.8f, new Color(1f, 0.6f, 0.3f), (ph - 0.96f) * 25f);
                    break;
                }
                case "craft":
                {
                    // a few tight swarms of strike craft fly out together to the enemy line and back, each craft a
                    // point of light on a tiny trail
                    if (i >= 5) break;
                    float e = 0.5f - 0.5f * Mathf.Cos(Mathf.Tau * ph);
                    float sgn = Mathf.Sin(Mathf.Tau * ph) >= 0 ? 1f : -1f;
                    var c0 = from.Lerp(to, e * 0.9f);
                    var pd = path / dist * sgn;
                    // across the path (any other axis when the path runs straight up or down)
                    var lat = pd.Cross(Vector3.Up);
                    if (lat.LengthSquared() < 1e-6f) lat = pd.Cross(Vector3.Right);
                    lat = lat.Normalized();
                    float spread = dist * 0.012f + sh.Half * 0.5f;
                    for (int k = 0; k < 6; k++)
                    {
                        // a loose arrowhead, jostling a little
                        int rank = (k + 1) / 2; float sideK = k == 0 ? 0 : k % 2 == 0 ? 1 : -1;
                        var off = lat * (sideK * rank * spread) - pd * (rank * spread * 0.8f) + Vector3.Up * (Mathf.Sin(_fxT * 3f + k * 2.1f + i) * spread * 0.3f);
                        var at = c0 + off;
                        Beam(at - pd * (spread * 1.6f), at, bright, 0.5f, ShapeTail, sh.Half * 0.02f);
                        Spark(at, sh.Half * 0.1f, bright, 1f, 0.5f);
                    }
                    break;
                }
                default:
                {
                    // exotic: violet arcs, crooked and flickering
                    if (ph > 0.55f) break;
                    int fl = (int)(_fxT * 9f) + i;
                    // the kink stays small and across the line of fire, so the arc runs from shooter to target
                    var n = path.Cross(Vector3.Up);
                    if (n.LengthSquared() < 1e-6f * dist * dist) n = path.Cross(Vector3.Right);
                    n = n.Normalized();
                    var mid = from.Lerp(to, 0.4 + 0.2 * Unit(Hash(i, fl, 5))) + n * ((Unit(Hash(i, fl, 4)) - 0.5f) * dist * 0.06f);
                    var violet = new Color(0.75f, 0.35f, 1f);
                    float a2 = Mathf.Sin(Mathf.Pi * ph / 0.55f);
                    Beam(from, mid, violet, (0.35f + 0.65f * a2) * 0.7f, ShapeLance, sh.Half * 0.08f, 0.004f, true);
                    Beam(mid, to, violet, (0.35f + 0.65f * a2) * 0.7f, ShapeLance, sh.Half * 0.08f, 0.004f, true);
                    Spark(to, tg.Half * 0.6f, violet, a2);
                    break;
                }
            }
        }
    }

    /// <summary>A fireball or shock ring of this radius (true units), at least a few pixels across. It faces the camera,
    /// or lies across normal (a shock ring seen at an angle) when one is given. seed and prog feed the shader's noise.</summary>
    void Billboard(D3 at, float radius, Color col, float alpha, float shape, float seed, float prog, Vector3 normal = default)
    {
        if (_nRing >= BillMax || alpha <= 0.01f) return;
        var d = Drawn(at, out float sq, out var tv);
        float dist = Math.Max(Frame.Cam.DistanceTo(tv), 1e-12f);
        radius = Math.Max(radius, dist * 0.006f);
        alpha *= 1f - Mathf.SmoothStep(0.15f, 0.4f, radius / dist);
        if (alpha <= 0.01f) return;
        var toCam = (Frame.Cam - tv) / dist;
        var n = normal == default ? toCam : normal;
        var right = n.Cross(Mathf.Abs(n.Y) > 0.98f ? Vector3.Right : Vector3.Up).Normalized();
        var up = right.Cross(n);
        int o = _nRing++ * 20;
        Put(_ringBuf, o, new Basis(right * radius * sq, up * radius * sq, n * radius * sq), d);
        _ringBuf[o + 12] = col.R; _ringBuf[o + 13] = col.G; _ringBuf[o + 14] = col.B; _ringBuf[o + 15] = alpha;
        _ringBuf[o + 16] = shape; _ringBuf[o + 17] = 0; _ringBuf[o + 18] = seed; _ringBuf[o + 19] = prog;
    }

    /// <summary>One explosion this frame: where, how big (true units), how far through (0..1), its seed, and whether it
    /// is a kill (the hull goes) or a hit (the hull burns and flies on).</summary>
    readonly List<(D3 At, float R, float Q, int Seed, bool Kill)> _blasts = new();

    /// <summary>
    /// The battle's explosions this frame, before anyone fires.
    /// Kills: only the ghost hulls blow up (the hulls the record says each side lost, drawn back in as the last slots of their
    /// squadrons), each at its own moment in the battle's window, fixed by its slot's key; a hull stops firing as it goes
    /// and is gone for good once its flash has peaked. Kills that happened before the view saw the battle are simply
    /// gone. When the window closes it is exactly these hulls that leave the layout. Hits: hulls that fly on take bursts
    /// of fire on their armour (flash, fireball, burning debris; no shock ring), more on the side that lost more, so even
    /// a paused frame shows the fight.
    /// </summary>
    void ScheduleBursts(BattleRecord rec, float u, float shareA)
    {
        _blasts.Clear();
        int recSeed = rec == null ? 0 : (int)(rec.Year * 1000) ^ rec.CivA * 31 ^ rec.CivB;
        if (rec != null)
            for (int side = 0; side < 2; side++)
                foreach (int idx in _sideHulls[side])
                {
                    var h = _hulls[idx];
                    if (h.Ghost == 0) continue;
                    uint hk = Hash((int)h.Ghost, (int)(h.Ghost >> 32), recSeed);
                    float uk = 0.03f + 0.9f * Unit(hk);
                    if (u < uk) continue;
                    if (!_deathAt.TryGetValue(h.Ghost, out float t0)) _deathAt[h.Ghost] = t0 = _killFresh ? -1e9f : _fxT;
                    float q = (_fxT - t0) / KillLife;
                    if (q > 0.14f) h.Dead = true; else h.Dying = true;
                    _hulls[idx] = h;
                    if (q <= 1f && !h.Culled) _blasts.Add((h.At, h.Half * (1.4f + 0.6f * Unit(hk >> 10)), q, (int)Hash((int)hk, 4, recSeed), true));
                }
        // hits, staggered so a frame holds them at every stage: flash, fireball, ring
        const int hits = 14;
        int hitsA = Math.Clamp(Mathf.RoundToInt(hits * shareA), 3, hits - 3);
        for (int s = 0; s < hits; s++)
        {
            int side = s < hitsA ? 0 : 1;
            float period = 1.7f + Unit(Hash(s, 0, 61));
            float t = _fxT / period + s * 0.618034f + 0.13f;
            int cyc = (int)Mathf.Floor(t);
            float q = Frac(t) / 0.6f;
            if (q > 1f) continue;
            int idx = PickLive(_sideHulls[side], Hash(s, cyc, 62));
            if (idx < 0) continue;
            var h = _hulls[idx];
            // on the armour, on the side the camera sees, so the hull doesn't hide it
            Drawn(h.At, out _, out var tv);
            var dir = new Vector3(Unit(Hash(s, cyc, 63)) - 0.5f, Unit(Hash(s, cyc, 64)) - 0.5f, Unit(Hash(s, cyc, 65)) - 0.5f) * 0.6f + (Frame.Cam - tv).Normalized() * 0.8f;
            _blasts.Add((h.At + dir * (h.Half * 0.7f), h.Half * (0.4f + 0.3f * Unit(Hash(s, cyc, 66))), q, (int)Hash(s, cyc, 67), false));
        }
    }

    static readonly Color Flash = new(1f, 1f, 0.97f), Debris = new(1f, 0.62f, 0.3f), Shock = new(1f, 0.78f, 0.52f);

    void Explosions()
    {
        foreach (var (at, r, q, seed, kill) in _blasts)
        {
            float sd = (seed & 1023) / 1023f;
            // a white-hot flash first, peaking fast
            if (q < 0.3f) Spark(at, r * (kill ? 0.8f : 0.5f) * (0.5f + q * 3f), Flash, (kill ? 4f : 2.8f) * Mathf.Sin(Mathf.Pi * q / 0.3f), 1.6f);
            // and a white-hot heart that lingers inside the fireball
            if (q < 0.6f) Spark(at, r * (kill ? 0.3f : 0.2f), Flash, (kill ? 2.2f : 1.5f) * (1f - q / 0.6f), 1.1f);
            // the fireball billows out, white at the heart through orange to a red rim, and burns down
            float fq = Mathf.Clamp((q - 0.02f) / 0.85f, 0, 1);
            if (q > 0.02f && fq < 1f) Billboard(at, r * (kill ? 0.9f + 2.4f * Mathf.Sqrt(fq) : 0.8f + 1.2f * Mathf.Sqrt(fq)), Colors.White, Mathf.Pow(1f - fq, 1.4f), ShapeFire, sd, fq);
            // a kill's thin shock ring racing out across a tilted plane, fading as it grows
            if (kill && q > 0.08f)
            {
                float rq = (q - 0.08f) / 0.92f;
                // the ring's plane is always seen 50-75 degrees off face-on, so it reads as a flat wave, never a bubble
                Drawn(at, out _, out var tv);
                var toCam = (Frame.Cam - tv).Normalized();
                var perp = toCam.Cross(Vector3.Up); if (perp.LengthSquared() < 1e-6f) perp = Vector3.Right;
                perp = perp.Normalized().Rotated(toCam, Unit(Hash(seed, 0, 7)) * Mathf.Tau);
                float tilt = 0.87f + 0.44f * Unit(Hash(seed, 0, 8));
                var nrm = toCam * Mathf.Cos(tilt) + perp * Mathf.Sin(tilt);
                Billboard(at, r * (1.1f + 5.5f * Mathf.Sqrt(rq)), Shock, Mathf.Pow(1f - rq, 2.2f) * 1.1f, ShapeRing, sd, rq, nrm);
            }
            // pieces of the hull flung out burning, each on a short trail
            if (q > 0.05f)
            {
                int pieces = kill ? 8 : 3;
                float dq = Mathf.Sqrt((q - 0.05f) / 0.95f);
                for (int d = 0; d < pieces; d++)
                {
                    var dir = new Vector3(Unit(Hash(seed, d, 1)) * 2f - 1f, Unit(Hash(seed, d, 2)) * 2f - 1f, Unit(Hash(seed, d, 3)) * 2f - 1f);
                    float reach = r * (kill ? 4.5f : 2.2f) * (0.6f + 0.4f * Unit(Hash(seed, d, 4)));
                    var head = at + dir * (reach * dq);
                    var tail = at + dir * (reach * Math.Max(dq - 0.3f, 0f));
                    float a = Mathf.Pow(1f - q, 1.1f);
                    Beam(tail, head, Debris, a * 1.2f, ShapeTail, r * 0.06f);
                    Spark(head, r * 0.12f, Debris.Lerp(Flash, 0.6f), a * 2.4f, 0.9f);
                }
            }
        }
    }
}
