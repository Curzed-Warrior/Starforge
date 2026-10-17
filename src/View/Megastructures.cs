using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// The engineering of a civilisation at the scale of a solar system, each piece unlocked by what it knows:
/// starbases that grow from outpost to citadel, orbital foundries, mega shipyards laying down titans, laser-sail
/// arrays, antimatter factories, star lifters, planet crackers, science nexuses, Bishop rings, a Nicoll-Dyson beam,
/// Kugelblitz reactors, black hole forges, gateways and fold-jump flashes. Built once, then animated.
/// </summary>
public partial class Megastructures : Node3D
{
    SystemView _sv;
    float _t;
    readonly Dictionary<string, Node3D> _built = new();
    /// <summary>What stands in this system now, and where (system coordinates): read by the HUD.</summary>
    public readonly List<(string Name, D3 Pos, float Size)> Active = new();
    /// <summary>Where ships dock at the starbase this frame (system coordinates), if there is one.</summary>
    public D3? Starbase;

    /// <summary>
    /// True sizes, as radii in kilometres. Massive for the era and still small beside the worlds and stars they work:
    /// a citadel twenty kilometres across, a Bishop ring a thousand in radius, a sentry dish wider than the Moon.
    /// Structures that wrap a world or a star (the shield, the arc furnace, the lifter, the corona forge, the
    /// ringworld, the Alderson disk, the aegis) are sized to what they wrap instead.
    /// </summary>
    public static float RealKm(string id, int level, int maxCls) => id switch
    {
        "starbase" => new[] { 0.045f, 4f, 6f, 10f, 20f }[Mathf.Clamp(level, 0, 4)],     // the outpost is its walkable interior, to the metre
        "yard" => Mathf.Max(30f, Ships.LengthKm(maxCls) * 0.7f),
        "foundry" => 25f, "laser" => 60f, "antimatter" => 150f, "sentry" => 1800f, "nexus" => 300f, "coord" => 200f,
        "kugel" => 80f, "memory" => 1500f, "beam" => 6000f, "bishop" => 1000f, "gateway" => 400f, "relay" => 40f,
        "wormhole" => 600f, "forge" => 25000f,
        "launchloop" => 1000f, "decompressor" => 4000f, "lattice" => 2500f, "spire" => 2000f, "umbralfort" => 500f,
        "gatebastion" => 700f, "aegis" => 150f, "birch" => 60000f, "penrose" => 40000f, "sanctum" => 400f,
        "suncitadel" => 20000f, "starforge" => 700000f, "throne" => 600f,
        _ => 100f,
    };
    /// <summary>Wrapped round a world or star, or spread across the system: sized by Place, not by RealKm.</summary>
    static readonly HashSet<string> Relative = new() { "lifter", "shield", "cracker", "relay", "jumps", "ringworld", "alderson", "coronaforge", "aegis" };
    /// <summary>Where the node itself goes this frame, when that isn't the point the camera and label use.</summary>
    D3? _at;
    float _base = 1f;
    MultiMeshInstance3D _beacons;
    const int BeaconMax = 96;
    readonly float[] _bb = new float[BeaconMax * 20];
    int _nb;
    readonly Random _r = new(5);

    /// <summary>The catalogue, in the order a civilisation tends to reach them.</summary>
    public static readonly (string Id, string Name, string Tech, string Blurb)[] Catalogue =
    {
        ("starbase", "Starbase", null, "Outpost, starport, starhold, star fortress, citadel: the system's anchor grows with every era"),
        ("laser", "Laser Sail Array", "X3", "A phased array of mirrors that throws lightsails at other stars"),
        ("foundry", "Orbital Foundry", "I3", "A crucible the size of a city, fed by mass drivers, pouring hull plate"),
        ("shield", "Planetary Shield", "SHD-4", "A hexagonal field around the homeworld that turns flares and debris aside"),
        ("antimatter", "Antimatter Factory", "X5", "A sun-powered collider ring making antimatter by the kilogram"),
        ("yard", "Mega Shipyard", "I2", "A cradle many kilometres long where titans and world-ships are laid down"),
        ("sentry", "Sentry Array", "S5", "A dish wider than a moon, listening to every star within a thousand light-years"),
        ("cracker", "Arc Furnace", "I5", "A ring of harvester claws clamped round a world, drinking its molten heart"),
        ("nexus", "Science Nexus", "S6", "Counter-rotating rings of uploaded minds, thinking together"),
        ("lifter", "Star Lifter", "I6", "Magnetic funnels at the poles drawing the star's own mass up into collectors"),
        ("jumps", "Fold Jump Flashes", "X6", "Ships folding in and out at the system's edge"),
        ("beam", "Nicoll-Dyson Beam", "X7", "The swarm's light gathered into one lens: a weapon that reaches across light-years"),
        ("bishop", "Bishop Ring", "S7", "A habitat ring a thousand kilometres across, open to the sky, its air held in by walls"),
        ("coord", "Coordination Center", "S8", "Where a hive-mind's consensus is kept"),
        ("relay", "Hyper Relay", "FTL-6", "A chain of beacons that pins a fold lane open across the system"),
        ("wormhole", "Stable Wormhole", "FTL-9", "A held-open throat to the far side of the galaxy; the stars behind it are the wrong stars"),
        ("gateway", "Gateway", "X8", "A permanent fold to every other gate in the network"),
        ("kugel", "Kugelblitz Reactor", "E8", "A black hole made from pure light, caged and fed"),
        ("forge", "Black Hole Forge", "I8", "A stellar-mass black hole fed planets and paying back in energy"),
        ("memory", "Memory Spire", "S9", "Every mind that ever lived here, held in a helix of light"),
        // the tech bible's great works
        ("launchloop", "Launch Loop", "MEG-2", "A magnetic loop a thousand kilometres long that throws cargo into orbit"),
        ("decompressor", "Singularity Decompressor", "MIN-9", "A drill sunk into a black hole's ergosphere, pulling matter back out"),
        ("lattice", "Fleet Command Lattice", "MEG-9a", "A web of command stations where every fleet in the system is thought of at once"),
        ("spire", "Cognition Spire", "MEG-9b", "A needle of thinking matter taller than a moon is wide"),
        ("ringworld", "Ringworld", "COL-10", "A ring around the star itself, a million Earths of floor"),
        ("umbralfort", "Umbral Fortress", "STB-7", "A dark-matter superbase that bends the light around it"),
        ("gatebastion", "Gate Bastion", "STB-8", "A guardian station built on the Wardens' plans"),
        ("aegis", "System Aegis", "SHD-10", "A shield around a whole star system"),
        ("birch", "Birch Shellworld", "MEG-13", "Nested habitable shells around a black hole"),
        ("penrose", "Penrose Sphere", "MEG-15", "A sphere around a black hole, drinking its spin"),
        ("alderson", "Alderson Disk", "MEG-16", "A disk world with the star at its hub"),
        ("sanctum", "Glyph Sanctum", "STB-9", "A rune-fortress that rebuilds itself"),
        ("coronaforge", "Corona Forge", "IND-15", "Foundries swimming in the star's corona"),
        ("suncitadel", "Sun Citadel", "STB-10", "A citadel drawing its strength from the star"),
        ("starforge", "Starforge", "MEG-18", "A machine that builds stars"),
        ("throne", "Lucent Throne", "STB-11", "A seat of light, made for meeting the divine"),
    };
    /// <summary>Which great works have a model (or a procedural stand-in) yet.</summary>
    public static readonly HashSet<string> Modelled = new() { "starbase", "laser", "foundry", "shield", "antimatter", "yard", "sentry", "cracker", "nexus", "lifter", "jumps", "beam", "bishop", "coord", "relay", "wormhole", "gateway", "kugel", "forge", "memory",
        "launchloop", "decompressor", "lattice", "spire", "ringworld", "umbralfort", "gatebastion", "aegis", "birch", "penrose", "alderson", "sanctum", "coronaforge", "suncitadel", "starforge", "throne" };
    /// <summary>The tech bible's great works: Blender assets placed and sized directly (no procedural stand-in to dress).</summary>
    static readonly HashSet<string> NewWorks = new() { "launchloop", "decompressor", "lattice", "spire", "ringworld", "umbralfort", "gatebastion", "aegis", "birch", "penrose", "alderson", "sanctum", "coronaforge", "suncitadel", "starforge", "throne" };

    public void Setup(SystemView sv)
    {
        _sv = sv;
        var quad = new QuadMesh { Size = new Vector2(2, 2), Material = Works.GlowMat() };
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = quad, InstanceCount = BeaconMax, VisibleInstanceCount = 0 };
        _beacons = new MultiMeshInstance3D { Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, CustomAabb = new Aabb(-Vector3.One * 4000, Vector3.One * 8000) };
        AddChild(_beacons);
    }

    public static string Hide = "";

    bool Has(Colony c, string id) => id == "starbase" || c.Civ.M.Megas.Contains(id) || Force.Contains(id);
    /// <summary>Test hook: pretend these great works are known.</summary>
    public static HashSet<string> Force = new();

    readonly List<(Node3D Node, D3 At, float Scale)> _placed = new();

    /// <summary>
    /// Where everything stands this frame. Runs before the camera moves, so a camera following a structure looks at
    /// where it is now, not where it was a frame ago (at true scale a frame of orbital motion is many kilometres).
    /// The nodes themselves are placed about the new origin afterwards, in Apply().
    /// </summary>
    public void Update(Colony c, float dt, float shellOut)
    {
        _t += dt;
        _placed.Clear();
        Active.Clear();
        Building.Clear();
        Starbase = null;
        var cam = GetViewport().GetCamera3D();
        if (cam != null) _camLocal = (Frame.Sys(cam.GlobalPosition) - _sv.Offset).F;      // the camera in this system's coordinates
        _nb = 0;
        var on = new HashSet<string>();
        if (c != null && c.Seat != null && !Hide.Contains("mega"))
        {
            var seat = c.Seat;
            float S = Mathf.Clamp(OrbitOf(seat) / 70f, 0.5f, 3f);           // the system's own scale, Sol = 1
            float outer = Mathf.Max(_sv.Sys.Bodies.Count > 0 ? _sv.Sys.Bodies.Max(b => b.Orbit) : 200f, shellOut * 3f);
            var sp = SeatPos(seat);
            int level = Mathf.Clamp(Math.Max(c.Era, 0), 0, 4);
            foreach (var (id, name, tech, _) in Catalogue)
            {
                bool raised = c.Raised.Contains(id);
                if (!Has(c, id) && !raised || !Modelled.Contains(id)) continue;
                float prog = Force.Contains(id) ? ForceProg : c.BuildProgress(id);
                if (prog <= 0f) continue;                                           // not begun
                // the starbase grows with the era; the yard is refitted for each new hull class
                string key = id == "starbase" ? $"starbase{level}" : id == "yard" ? $"yard{Ships.MaxClass(c.Civ)}" : id;
                if (id == "beam" && c.Capture < 0.3 && !raised) continue;
                if (id == "gateway" && c.Project == "Expansionist") continue;   // the great fold gate stands in for it
                Body mined = null;
                if (id == "cracker")
                {
                    mined = c.Hubs.Keys.Where(b => b.Mined > 0.02f && b.Mined < 0.999f && b.Kind != BodyKind.Belt).OrderByDescending(b => b.Mined).FirstOrDefault();
                    if (mined == null) continue;
                }
                if (!_built.TryGetValue(key, out var node))
                {
                    if (id is "starbase" or "yard") foreach (var old in _built.Keys.Where(k => k.StartsWith(id) && k != key).ToList()) { _sites.Remove(_built[old]); _built[old].QueueFree(); _built.Remove(old); }
                    node = Dress(id, Build(id, level, c, seat.Radius, S));
                    node.TopLevel = true;
                    // the procedural model's own radius, once: it is scaled to its true size every frame
                    node.SetMeta("r0n", Radius(node, true));
                    AddChild(node);
                    _built[key] = node;
                }
                on.Add(key);
                node.Visible = true;
                float real = RealKm(id, level, Ships.MaxClass(c.Civ)) * StarSystem.Km;
                _at = null;
                _base = NewWorks.Contains(id) || Relative.Contains(id) ? 1f : real / Mathf.Max((float)node.GetMeta("r0n"), 1e-6f);
                var pos = Place(id, node, c, seat, sp, S, outer, shellOut, mined, dt, real);
                _placed.Add((node, _at ?? pos, _base));
                Construct(node, prog);
                float so0 = OrbitOf(seat);
                float size = id switch
                {
                    "shield" => seat.Radius * 1.25f, "ringworld" => so0 * 1.25f, "alderson" => outer * 3.8f, "aegis" => outer * 1.25f,
                    "coronaforge" => _sv.Sys.StarRadius * 2f, "lifter" => _sv.Sys.StarRadius * 5f, "relay" => real * 3f,
                    "cracker" => (mined?.Radius ?? 1f) * 1.6f, "jumps" => real,
                    _ => real
                };
                string shown = id == "starbase" ? StarbaseName(level) : id == "yard" && Ships.MaxClass(c.Civ) < 6 ? "Orbital Shipyard" : name;
                Active.Add((shown, pos, size));
                if (prog < 0.999f) Building[shown] = prog;
            }
        }
        foreach (var (k, n) in _built) if (!on.Contains(k)) n.Visible = false;
    }

    /// <summary>Place the structures about this frame's origin (after the camera has moved).</summary>
    public void Apply()
    {
        if (Works.Hide.Contains("megaglow")) foreach (var (node, _, _) in _placed) foreach (var mi in node.FindChildren("*", "MeshInstance3D", true, false).Cast<MeshInstance3D>()) if (IsGlow(mi)) mi.Visible = false;
        foreach (var (node, at, scale) in _placed) Frame.Place(node, Frame.View(_sv.Offset + at), scale);
        _beacons.Multimesh.VisibleInstanceCount = 0;
        if (_nb > 0) _beacons.Multimesh.Buffer = _bb;
        _beacons.Multimesh.VisibleInstanceCount = _nb;
    }

    public static string StarbaseName(int level) => new[] { "Outpost", "Starport", "Starhold", "Star Fortress", "Citadel" }[Mathf.Clamp(level, 0, 4)];

    static float OrbitOf(Body b) => b.Parent == null ? b.Orbit : b.Parent.Orbit;
    D3 SeatPos(Body b) => b.Kind == BodyKind.Belt ? new D3(b.Orbit, 0, 0) : b.WorldPosD(_sv.VisYear);
    Vector3 OnOrbit(float r, float ang, float y = 0) => new(Mathf.Cos(ang) * r, y, Mathf.Sin(ang) * r);
    float SeatAngle(Body seat)
    {
        var p = SeatPos(seat.Parent ?? seat);
        return (float)Math.Atan2(p.Z, p.X);
    }

    Vector3 _camLocal;
    void Beacon(D3 pd, Color col, float size, float floor = 0.5f)
    {
        var p = pd.F;
        if (_nb >= BeaconMax) return;
        // a marker for the far view only: up close the structure speaks for itself
        float k = Mathf.Clamp((_camLocal.DistanceTo(p) / size - 12f) / 30f, 0f, 1f);
        if (k <= 0.01f) return;
        col.A *= k;
        int o = _nb++ * 20;
        _bb[o] = size; _bb[o + 1] = 0; _bb[o + 2] = 0; _bb[o + 3] = p.X;
        _bb[o + 4] = 0; _bb[o + 5] = size; _bb[o + 6] = 0; _bb[o + 7] = p.Y;
        _bb[o + 8] = 0; _bb[o + 9] = 0; _bb[o + 10] = size; _bb[o + 11] = p.Z;
        _bb[o + 12] = col.R; _bb[o + 13] = col.G; _bb[o + 14] = col.B; _bb[o + 15] = col.A;
        _bb[o + 16] = 1.2f; _bb[o + 17] = floor; _bb[o + 18] = 0; _bb[o + 19] = 0;
    }

    // ---------- placement and animation, every frame ----------
    D3 Place(string id, Node3D n, Colony c, Body seat, D3 sp, float S, float outer, float shellOut, Body mined, float dt, float real)
    {
        float R = seat.Radius, a0 = SeatAngle(seat), so = OrbitOf(seat);
        D3 p;
        switch (id)
        {
            case "starbase":
                p = sp + OnOrbit(R * 3.4f, _t * 0.05f, R * 0.4f);
                n.Rotation = new Vector3(0, _t * 0.1f, 0.25f);
                Starbase = p;
                // a beacon of progress: from across the system the starbase is a bright, slowly pulsing star
                Beacon(p, new Color(0.8f, 0.9f, 1f, 0.8f + 0.2f * Mathf.Sin(_t * 1.5f)), R * 0.35f, 1f);
                break;
            case "foundry":
            {
                // at the trailing Lagrange point, upstream of the mines
                p = OnOrbit(so, a0 - Mathf.Pi / 3, 3f * S);
                n.Rotation = new Vector3(0, _t * 0.02f, 0);
                Beacon(p, new Color(1f, 0.55f, 0.2f), 0.4f * S);
                break;
            }
            case "yard":
                p = OnOrbit(so, a0 + Mathf.Pi / 3, 3.5f * S);
                n.Basis = Basis.LookingAt(OnOrbit(1, a0 + Mathf.Pi / 3 + Mathf.Pi / 2), Vector3.Up);
                AnimateYard(n, c);
                Beacon(p, new Color(0.75f, 0.85f, 1f), 0.4f * S);
                break;
            case "laser":
                p = OnOrbit(so * 0.62f, a0 + 2.2f, 0);
                n.Basis = Basis.LookingAt(-p.F.Normalized(), Vector3.Up);               // the mirrors face outward; the beam leaves along +Z
                FireBeam(n, 9f, 2.5f);
                Beacon(p, new Color(0.6f, 1f, 0.8f), 0.3f * S);
                break;
            case "antimatter":
                p = OnOrbit(Mathf.Max(so * 0.42f, shellOut * 1.15f), a0 - 2.4f + _t * 0.01f, 0);
                n.Rotation = new Vector3(0.6f, _t * 0.03f, 0);
                Pulse(n, "ring", 0.5f + 0.5f * Mathf.Sin(_t * 3f));
                Beacon(p, new Color(0.5f, 0.6f, 1f), 0.35f * S);
                break;
            case "sentry":
                p = OnOrbit(outer * 1.05f, a0 + 1.0f, outer * 0.08f);
                n.Basis = Basis.LookingAt(-p.F.Normalized(), Vector3.Up);
                Beacon(p, new Color(0.9f, 0.9f, 0.7f), 0.5f * S);
                break;
            case "cracker":
            {
                var bp = SeatPosOf(mined);
                float mr = mined.Radius * Mathf.Pow(Mathf.Max(1f - mined.Mined * 0.97f, 0.03f), 1f / 3f);
                var dir = (OnOrbit(1, _t * 0.03f, 0.3f)).Normalized();
                p = bp + dir * mr * 2.6f;
                n.Basis = Basis.LookingAt(dir, Vector3.Up);                             // +Z, and the beam, toward the world
                if (n.HasMeta("asset"))
                {
                    // the arc furnace clamps round the world itself (unit planet radius 100 in the asset), turning slowly
                    var arc = (Node3D)n.GetMeta("asset");
                    _at = bp; n.Basis = new Basis(Vector3.Up, _t * 0.03f);
                    arc.Scale = Vector3.One * mr / 100f;
                    Find(n, "beam").Visible = false;
                    p = bp + Vector3.Up * mr * 1.4f;
                }
                var beam = Find(n, "beam");
                beam.Scale = new Vector3(1, 1, mr * 1.6f);
                beam.Visible = Mathf.Sin(_t * 0.7f) > -0.6f;
                Beacon(bp + dir * mr, new Color(1f, 0.5f, 0.15f), mr * 0.3f);
                break;
            }
            case "nexus":
                p = sp + OnOrbit(R * 7f, -_t * 0.03f, -R);
                for (int i = 0; i < 3; i++) Find(n, $"r{i}").Rotation = new Vector3(_t * (0.3f + i * 0.17f), _t * (0.2f - i * 0.11f), i * 1.1f);
                Beacon(p, new Color(0.4f, 0.95f, 1f), R * 0.2f);
                break;
            case "lifter":
            {
                p = Vector3.Zero;
                float sr = _sv.Sys.StarRadius;
                _base = sr;
                foreach (var jet in new[] { "j0", "j1" }) Find(n, jet).Scale = new Vector3(1f + 0.2f * Mathf.Sin(_t * 5f), 1, 1f + 0.2f * Mathf.Sin(_t * 5f));
                Beacon(new Vector3(0, sr * 4.2f, 0), new Color(1f, 0.8f, 0.5f), sr * 0.3f);
                Beacon(new Vector3(0, -sr * 4.2f, 0), new Color(1f, 0.8f, 0.5f), sr * 0.3f);
                break;
            }
            case "jumps":
            {
                // ships folding in from nowhere: a white flash and a ring of light where they arrive
                p = Vector3.Zero;
                float cycle = 6f;
                int idx = (int)(_t / cycle);
                float u = (_t % cycle) / cycle;
                var rr = new Random(idx * 7919 + 3);
                var at = OnOrbit(outer * (0.75f + 0.35f * (float)rr.NextDouble()), (float)(rr.NextDouble() * Mathf.Tau), (float)(rr.NextDouble() - 0.5) * outer * 0.1f);
                var flash = Find(n, "flash"); var ring = Find(n, "ring");
                flash.Scale = Vector3.One * S * 3f * Mathf.Max(0, 1f - u * 6f);
                ring.Scale = Vector3.One * S * (0.5f + u * 12f);
                ((ShaderMaterial)((MeshInstance3D)ring).MaterialOverride).SetShaderParameter("strength", 2.5f * Mathf.Max(0, 1f - u * 1.5f));
                var ship = Find(n, "ship");
                ship.Visible = u > 0.05f;
                ship.Position = -at.Normalized() * u * S * 6f;
                ship.Basis = Basis.LookingAt(-at.Normalized(), Vector3.Up).Scaled(Vector3.One * Ships.Size(Math.Max(Ships.MaxClass(c.Civ), 5)));
                if (u < 0.2f) Beacon(at, new Color(0.85f, 0.9f, 1f), S * 2f, 1f);
                p = at;
                break;
            }
            case "beam":
            {
                float ang = a0 + 3.6f;
                p = OnOrbit(shellOut * 1.35f, ang, shellOut * 0.15f);
                n.Basis = Basis.LookingAt(-p.F.Normalized(), Vector3.Up);
                FireBeam(n, 25f, 5f);
                Beacon(p, new Color(1f, 0.95f, 0.8f), 0.6f * S);
                break;
            }
            case "bishop":
            {
                float ang = a0 + 1.3f + _t * 0.004f;
                p = OnOrbit(so * 1.22f, ang, 0);
                // the ring's axis lies along its orbit, so its open inner face catches the sun
                n.Basis = Basis.LookingAt(OnOrbit(1, ang + Mathf.Pi / 2), Vector3.Up) * new Basis(Vector3.Forward, _t * 0.02f);
                Beacon(p + n.Basis * new Vector3(0, 2.6f * S, 0), new Color(0.6f, 1f, 0.7f), 0.3f * S);
                break;
            }
            case "coord":
                p = sp + OnOrbit(R * 9f, _t * 0.02f + 2f, R * 1.5f);
                n.Rotation = new Vector3(0, _t * 0.08f, 0);
                Pulse(n, "halo", 0.6f + 0.4f * Mathf.Sin(_t * 1.3f));
                Beacon(p, new Color(1f, 0.8f, 0.4f), R * 0.2f);
                break;
            case "gateway":
                p = OnOrbit(outer * 1.15f, a0 - 0.8f, 0);
                n.Basis = Basis.LookingAt(-p.F.Normalized(), Vector3.Up);
                Find(n, "disc").Rotation = new Vector3(0, 0, _t * 0.6f);
                Beacon(p, new Color(0.7f, 0.5f, 1f), S);
                break;
            case "relay":
            {
                // three beacons strung along a straight line across the system, pulsing in sequence
                p = OnOrbit(outer * 0.95f, a0 + 2.9f, 0);
                _at = D3.Zero;
                for (int i = 0; i < 3; i++)
                {
                    var b = Find(n, $"b{i}");
                    b.Scale = Vector3.One * (real / 0.85f);
                    b.Position = OnOrbit(outer * (0.55f + 0.2f * i), a0 + 2.9f, 0);
                    b.Rotation = new Vector3(0, _t * 0.4f, 0);
                    float on = Mathf.Max(0, Mathf.Sin(_t * 2f - i * 1.2f));
                    Find(b, "pulse").Scale = Vector3.One * (0.5f + on) * S * 0.5f;
                }
                Beacon(p, new Color(0.5f, 0.8f, 1f), 0.4f * S);
                break;
            }
            case "wormhole":
            {
                p = OnOrbit(outer * 1.3f, a0 + 2.2f, outer * 0.05f);
                var cam = GetViewport().GetCamera3D();
                if (cam != null) n.Basis = Basis.LookingAt((Frame.Sys(cam.GlobalPosition) - p).F.Normalized(), Vector3.Up);
                Find(n, "swirl").Rotation = new Vector3(0, 0, _t * 0.8f);
                Beacon(p, new Color(0.5f, 0.7f, 1f), S * 1.2f);
                break;
            }
            case "kugel":
                p = sp + OnOrbit(R * 6f, _t * 0.04f + Mathf.Pi, 0);
                for (int i = 0; i < 3; i++) Find(n, $"c{i}").Rotation = new Vector3(_t * (0.5f + i * 0.2f), i * 1.05f, _t * 0.3f * (i - 1));
                Beacon(p, new Color(0.8f, 0.9f, 1f), R * 0.15f, 0.8f);
                break;
            case "forge":
                p = OnOrbit(outer * 0.72f, a0 + 4.4f, 0);
                Find(n, "swirl").Rotation = new Vector3(0, -_t * 0.5f, 0);
                Beacon(p, new Color(1f, 0.6f, 0.3f), S * 1.2f);
                break;
            case "memory":
                p = sp + OnOrbit(R * 12f, -_t * 0.015f + 1f, R * 2f);
                n.Rotation = new Vector3(0, _t * 0.25f, 0);
                Beacon(p, new Color(1f, 0.85f, 1f), R * 0.25f);
                break;
            // ---------- the tech bible's great works ----------
            case "launchloop":
                p = sp + OnOrbit(R * 2.6f, a0 + 0.9f, R * 0.25f);
                n.Basis = Basis.LookingAt((sp - p).F.Normalized(), Vector3.Up);
                Fit(n, real);
                Beacon(p, new Color(0.6f, 0.85f, 1f), R * 0.15f);
                break;
            case "decompressor":
                p = OnOrbit(outer * 0.85f, a0 + 1.7f, outer * 0.05f);
                Fit(n, real);
                Spin(n, "ring", 0, _t * 0.5f, 0);
                Beacon(p, new Color(1f, 0.55f, 0.2f), 0.4f * S);
                break;
            case "lattice":
                p = sp + OnOrbit(R * 11f, _t * 0.015f + 1f, R * 2f);
                Fit(n, real);
                Spin(n, "r0", _t * 0.1f, _t * 0.07f, 0);
                Beacon(p, new Color(0.6f, 0.8f, 1f), R * 0.2f);
                break;
            case "spire":
                p = sp + OnOrbit(R * 8f, -_t * 0.01f + 3f, -R * 0.6f);
                Fit(n, real);
                Spin(n, "r0", 0, _t * 0.25f, 0);
                Beacon(p, new Color(0.5f, 0.9f, 1f), R * 0.2f);
                break;
            case "ringworld":
            {
                p = Vector3.Zero;
                _at = D3.Zero; n.Basis = new Basis(Vector3.Right, 0.06f);
                Fit(n, so * 1.77f);                     // (the asset's radius is measured to its bounding box corners: the ring itself sits at 0.71 of that)
                Spin(n, "ring", 0, _t * 0.004f, 0);
                Spin(n, "r0", 0, -_t * 0.01f, 0);
                p = OnOrbit(so * 1.25f, a0 + 2.5f, 0);
                Beacon(p, new Color(0.7f, 1f, 0.7f), 0.5f * S);
                break;
            }
            case "umbralfort":
                p = sp + OnOrbit(R * 6f, _t * 0.02f + 4f, -R * 0.8f);
                Fit(n, real);
                Spin(n, "halo", 0.25f, _t * 0.2f, 0);
                Beacon(p, new Color(0.7f, 0.4f, 1f), R * 0.2f);
                break;
            case "gatebastion":
                p = OnOrbit(outer * 1.15f, a0 - 0.95f, outer * 0.02f);
                n.Basis = Basis.LookingAt(-p.F.Normalized(), Vector3.Up);
                Fit(n, real);
                Spin(n, "disc", 0, 0, _t * 0.3f);
                Beacon(p, new Color(1f, 0.8f, 0.4f), 0.5f * S);
                break;
            case "aegis":
            {
                p = Vector3.Zero;
                float rr = outer * 1.25f;
                var dome = Find(n, "dome");
                dome.Scale = Vector3.One * rr;
                _at = D3.Zero;
                ((ShaderMaterial)((MeshInstance3D)dome).MaterialOverride).SetShaderParameter("time", _t * 0.3f);
                var dirs = new[] { Vector3.Right, Vector3.Left, Vector3.Up, Vector3.Down, Vector3.Forward, Vector3.Back };
                for (int i = 0; i < 6; i++)
                {
                    var py = Find(n, $"p{i}");
                    if (py == null) continue;
                    py.Position = dirs[i] * rr;
                    py.Scale = Vector3.One * (real / Mathf.Max((float)py.GetMeta("r0"), 1e-3f));
                    Beacon(py.Position, new Color(0.5f, 0.8f, 1f), 0.3f * S);
                }
                p = dirs[0] * rr;
                break;
            }
            case "birch":
                p = OnOrbit(outer * 0.6f, a0 + 4.2f, outer * 0.04f);
                Fit(n, real);
                for (int i = 0; i < 3; i++) Spin(n, $"r{i}", i * 0.4f, _t * (0.05f + 0.03f * i) * (i % 2 == 0 ? 1 : -1), 0);
                Beacon(p, new Color(0.6f, 0.8f, 1f), 0.4f * S);
                break;
            case "penrose":
                p = OnOrbit(outer * 0.7f, a0 + 5f, -outer * 0.04f);
                Fit(n, real);
                Spin(n, "ring", 0, _t * 0.8f, 0);
                Beacon(p, new Color(1f, 0.6f, 0.3f), 0.4f * S);
                break;
            case "alderson":
                p = Vector3.Zero;
                // the disk begins beyond the last world (its hole is 0.3 of its radius, 0.21 of the measured one) and runs far out
                _at = D3.Zero; n.Basis = new Basis(Vector3.Right, 0.12f);
                Fit(n, outer * 1.15f / 0.21f);
                Spin(n, "disc", 0, _t * 0.002f, 0);
                p = n.Basis * OnOrbit(outer * 2.5f, a0 + 1f, 0);
                Beacon(p, new Color(0.7f, 1f, 0.8f), 0.5f * S);
                break;
            case "sanctum":
                p = sp + OnOrbit(R * 7f, _t * 0.02f + 5.5f, R);
                Fit(n, real);
                for (int i = 0; i < 3; i++) Spin(n, $"r{i}", 0.3f * i, _t * (0.2f + 0.1f * i) * (i == 1 ? -1 : 1), 0);
                Beacon(p, new Color(1f, 0.8f, 0.4f), R * 0.2f);
                break;
            case "coronaforge":
            {
                float sr = _sv.Sys.StarRadius;
                p = new Vector3(sr * 1.75f, 0, 0);
                _at = D3.Zero; Fit(n, sr * 2f);
                Spin(n, "ring", 0, _t * 0.05f, 0);
                Beacon(p, new Color(1f, 0.6f, 0.3f), sr * 0.2f);
                break;
            }
            case "suncitadel":
            {
                float sr = _sv.Sys.StarRadius;
                p = OnOrbit(sr * 3.2f, a0 + 0.5f + _t * 0.01f, 0);
                n.Basis = Basis.LookingAt(-p.F.Normalized(), Vector3.Up) * new Basis(Vector3.Up, -Mathf.Pi / 2);      // the mirror crown (-X) faces the star
                Fit(n, real);
                Beacon(p, new Color(1f, 0.85f, 0.5f), sr * 0.2f);
                break;
            }
            case "starforge":
                p = OnOrbit(outer * 0.9f, a0 + 3.3f, outer * 0.06f);
                Fit(n, real);
                Spin(n, "r0", _t * 0.03f, _t * 0.05f, 0);
                if (Find(n, "core") is Node3D core) core.Scale = Vector3.One * (1f + 0.08f * Mathf.Sin(_t * 2.5f));
                Beacon(p, new Color(1f, 0.7f, 0.3f), 0.5f * S);
                break;
            case "throne":
                p = sp + OnOrbit(R * 5f, 1.2f + _t * 0.01f, R * 1.2f);
                Fit(n, real);
                Spin(n, "halo", 0, 0, _t * 0.2f);
                Beacon(p, new Color(0.9f, 0.95f, 1f), R * 0.25f, 1f);
                break;
            case "shield":
                p = sp;
                _base = R;
                ((ShaderMaterial)((MeshInstance3D)n.GetChild(0)).MaterialOverride).SetShaderParameter("time", _t);
                break;
            default: p = Vector3.Zero; break;
        }
        return p;
    }

    static Node3D Find(Node3D n, string name) => (Node3D)n.FindChild(name, true, false);
    /// <summary>Size a placed asset so its solid radius is `want` (its own radius was measured once, at build).</summary>
    static void Fit(Node3D n, float want)
    {
        if (n.FindChild("work", false, false) is Node3D w && w.HasMeta("r0")) w.Scale = Vector3.One * (want / Mathf.Max((float)w.GetMeta("r0"), 1e-3f));
    }
    static void Spin(Node3D n, string part, float x, float y, float z) { if (n.FindChild(part, true, false) is Node3D q) q.Rotation = new Vector3(x, y, z); }
    D3 SeatPosOf(Body b) => b.WorldPosD(_sv.VisYear);

    void FireBeam(Node3D n, float period, float on)
    {
        var beam = Find(n, "beam");
        float ph = _t % period;
        beam.Visible = ph < on;
        float k = Mathf.Clamp(Mathf.Min(ph, on - ph) * 3f, 0, 1);
        beam.Scale = new Vector3(k, 1, k);
    }

    void Pulse(Node3D n, string child, float k)
    {
        if ((n.FindChild(child, true, false) as MeshInstance3D)?.MaterialOverride is StandardMaterial3D m && m.HasMeta("c")) m.AlbedoColor = (Color)m.GetMeta("c") * (0.5f + k);
    }

    void AnimateYard(Node3D n, Colony c)
    {
        // a hull grows out of the cradle, is finished, and slides out under its own power; then the next
        float cycle = 80f, u = (_t % cycle) / cycle;
        var hull = Find(n, "hull");
        float L = (float)n.GetMeta("L");
        float build = Mathf.Clamp(u / 0.75f, 0.02f, 1f);
        float launch = Mathf.Clamp((u - 0.8f) / 0.2f, 0, 1);
        hull.Scale = new Vector3(L * 0.22f, L * 0.22f, L * 0.22f * build);
        hull.Position = new Vector3(0, 0, -L * 0.5f + L * 0.5f * build + launch * launch * L * 3f);
        hull.Visible = launch < 0.95f;
        int i = 0;
        foreach (var g in n.GetChildren().OfType<Node3D>().Where(x => x.Name.ToString().StartsWith("gantry")))
            g.Position = new Vector3(g.Position.X, g.Position.Y, -L * 0.5f + L * build * (0.3f + 0.35f * (i++ % 3)) + Mathf.Sin(_t * 0.4f + i) * L * 0.05f);
        foreach (var s in n.GetChildren().OfType<Node3D>().Where(x => x.Name.ToString().StartsWith("spark")))
            s.Visible = launch <= 0 && Mathf.Sin(_t * (6f + s.GetIndex()) + s.GetIndex()) > 0.55f;
    }

    // ---------- construction ----------
    /// <summary>
    /// Swap a procedural structure's hull for its Blender-built asset (assets/stations/&lt;id&gt;), if there is one.
    /// The asset is fitted to the procedural one's size; the procedural glows and beams stay; nodes the animation
    /// drives (rings, discs, swirls) are taken from the asset by name.
    /// </summary>
    static Node3D Dress(string id, Node3D n)
    {
        if (id == "starbase" || NewWorks.Contains(id)) return n;
        if (id == "relay")
        {
            // three beacons that move independently: each gets its own copy of the asset
            for (int i = 0; i < 3; i++) if (Find(n, $"b{i}") is Node3D b) DressOne("relay", b);
            return n;
        }
        return DressOne(id, n);
    }

    static Node3D DressOne(string id, Node3D n)
    {
        var asset = Asset(id);
        if (asset == null) return n;
        float r0 = Radius(n, true);
        var names = asset.FindChildren("*", "Node3D", true, false).Select(x => x.Name.ToString()).ToHashSet();
        foreach (var mi in n.FindChildren("*", "MeshInstance3D", true, false).Cast<MeshInstance3D>())
            if (!IsGlow(mi) && !names.Contains(mi.Name.ToString()) && !KeepProc(mi)) mi.QueueFree();
        foreach (var x in n.FindChildren("*", "Node3D", true, false))
            if (names.Contains(x.Name.ToString())) x.Name = x.Name + "_proc";
        asset.Scale = Vector3.One * (r0 / Mathf.Max(Radius(asset, true), 1e-3f));
        n.AddChild(asset);
        n.SetMeta("asset", asset);
        return n;
    }

    /// <summary>Additive glows, beams and the shield: the procedural effects an asset keeps.</summary>
    static bool IsGlow(MeshInstance3D mi) => mi.MaterialOverride is ShaderMaterial sm && sm.Shader == Spectacle.AddShader;
    /// <summary>Procedural parts that animate and have no counterpart in the asset (the hull growing in a yard).</summary>
    static bool KeepProc(MeshInstance3D mi) => mi.Name == "hull";

    static Node3D Asset(string slug)
    {
        if (!_stationScenes.TryGetValue(slug, out var ps))
        {
            string path = $"res://assets/stations/{slug}/{slug}.glb";
            _stationScenes[slug] = ps = ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
        }
        if (ps == null) return null;
        var inst = ps.Instantiate<Node3D>();
        foreach (var l in inst.FindChildren("*", "Light3D", true, false)) l.QueueFree();
        foreach (var b in inst.FindChildren("*", "StaticBody3D", true, false)) b.QueueFree();
        Merge(inst, slug);
        AddSparkle(inst, slug);
        return inst;
    }

    static readonly HashSet<string> Animated = new() { "r0", "r1", "r2", "disc", "swirl", "ring", "c0", "c1", "c2", "core", "halo" };
    static readonly Dictionary<string, ArrayMesh> _merged = new();

    /// <summary>
    /// Fold every static mesh of a station into one mesh with one surface per material: a few draw calls instead of
    /// hundreds. Parts the game animates (named in Animated) stay separate. The merged mesh is cached per design.
    /// </summary>
    static void Merge(Node3D inst, string slug)
    {
        bool Keep(Node n) { for (; n != null && n != inst; n = n.GetParent()) if (Animated.Contains(n.Name.ToString())) return true; return false; }
        var parts = inst.FindChildren("*", "MeshInstance3D", true, false).Cast<MeshInstance3D>().Where(m => !Keep(m)).ToList();
        if (parts.Count < 2) return;
        if (!_merged.TryGetValue(slug, out var mesh))
        {
            var byMat = new Dictionary<Material, SurfaceTool>();
            foreach (var mi in parts)
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
            foreach (var (m, st) in byMat) { st.Commit(mesh); mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, m); }
            _merged[slug] = mesh;
        }
        foreach (var mi in parts) { mi.GetParent()?.RemoveChild(mi); mi.QueueFree(); }
        inst.AddChild(new MeshInstance3D { Name = "Merged", Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }
    static readonly string[] StationTier = { "outpost", "starport", "starport", "fortress", "citadel" };
    static readonly Dictionary<string, PackedScene> _stationScenes = new();

    /// <summary>The Blender-built station for a starbase tier, without its interior lamps (they only matter up close).</summary>
    static Node3D StationAsset(int level) => Asset(StationTier[Mathf.Clamp(level, 0, StationTier.Length - 1)]);

    static readonly Dictionary<string, float[]> _sparkle = new();
    static readonly Dictionary<Material, Material> _bright = new();

    /// <summary>
    /// Make a station shine from across the system: its lit windows and lamps burn brighter, and a few hundred of them
    /// are doubled by glow points that never shrink below a couple of pixels, so the whole structure reads as a
    /// constellation of lights when it is only a speck.
    /// </summary>
    static void AddSparkle(Node3D inst, string slug)
    {
        var meshes = inst.FindChildren("*", "MeshInstance3D", true, false).Cast<MeshInstance3D>().ToList();
        foreach (var mi in meshes)
            for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
            {
                if (mi.Mesh.SurfaceGetMaterial(i) is not BaseMaterial3D m || !m.EmissionEnabled) continue;
                if (!_bright.TryGetValue(m, out var b))
                {
                    var d = (BaseMaterial3D)m.Duplicate();
                    d.EmissionEnergyMultiplier = Mathf.Max(d.EmissionEnergyMultiplier, 1f) * 4f;
                    _bright[m] = b = d;
                }
                mi.SetSurfaceOverrideMaterial(i, b);
            }
        if (!_sparkle.TryGetValue(slug, out var buf))
        {
            // sample points on emissive surfaces once per station design
            var pts = new List<(Vector3 P, Color C)>();
            var rnd = new Random(Galaxy.StableHash(slug));
            foreach (var mi in meshes)
            {
                var t = Transform3D.Identity;
                for (Node q = mi; q != inst && q is Node3D q3; q = q.GetParent()) t = q3.Transform * t;
                for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                {
                    if (mi.Mesh.SurfaceGetMaterial(i) is not BaseMaterial3D m || !m.EmissionEnabled) continue;
                    var verts = (Vector3[])mi.Mesh.SurfaceGetArrays(i)[(int)Mesh.ArrayType.Vertex];
                    int take = Math.Min(verts.Length / 8 + 1, 60);
                    for (int k = 0; k < take; k++) pts.Add((t * verts[rnd.Next(verts.Length)], m.Emission.Lightened(0.2f)));
                }
            }
            while (pts.Count > 600) pts.RemoveAt(rnd.Next(pts.Count));
            float span = Radius(inst);
            buf = new float[pts.Count * 20];
            for (int k = 0; k < pts.Count; k++)
            {
                var (p, col) = pts[k];
                float s = span * (0.004f + 0.006f * (float)rnd.NextDouble());
                int o = k * 20;
                buf[o] = s; buf[o + 3] = p.X; buf[o + 5] = s; buf[o + 7] = p.Y; buf[o + 10] = s; buf[o + 11] = p.Z;
                buf[o + 12] = col.R; buf[o + 13] = col.G; buf[o + 14] = col.B; buf[o + 15] = 0.5f + 0.5f * (float)rnd.NextDouble();
                buf[o + 16] = 0.6f; buf[o + 17] = 0.5f;
            }
            _sparkle[slug] = buf;
        }
        int n = buf.Length / 20;
        if (n == 0) return;
        var quad = new QuadMesh { Size = new Vector2(2, 2), Material = Works.GlowMat() };
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = quad, InstanceCount = n };
        mm.Buffer = buf;
        inst.AddChild(new MultiMeshInstance3D { Name = "Sparkle", Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 1e5f });
    }

    /// <summary>Bounding radius of everything visible under a node, in its own space.</summary>
    static float Radius(Node3D root, bool solidOnly = false)
    {
        float r = 0;
        foreach (var mi in root.FindChildren("*", "MeshInstance3D", true, false).Cast<MeshInstance3D>())
        {
            if (solidOnly && IsGlow(mi)) continue;     // glows and beams don't count
            var t = Transform3D.Identity;
            for (Node q = mi; q != root && q is Node3D q3; q = q.GetParent()) t = q3.Transform * t;
            var box = mi.GetAabb();
            for (int i = 0; i < 8; i++) r = Mathf.Max(r, (t * box.GetEndpoint(i)).Length());
        }
        return r;
    }
    static MeshInstance3D Add(Node3D parent, Mesh m, Material mat, Vector3 pos, Basis? b = null, string name = null)
    {
        var mi = new MeshInstance3D { Mesh = m, MaterialOverride = mat, Position = pos, Basis = b ?? Basis.Identity, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        if (name != null) mi.Name = name;
        parent.AddChild(mi);
        mi.Position = pos;
        return mi;
    }
    static StandardMaterial3D Emis(Color c)
    {
        var m = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = c };
        m.SetMeta("c", c);
        return m;
    }
    static Material Hull => Works.HullMat();
    static readonly Basis Flat = new(Vector3.Right, Mathf.Pi / 2);      // turn a Y-axis primitive to lie along Z
    static Mesh Box(float x, float y, float z) => new BoxMesh { Size = new Vector3(x, y, z) };
    static Mesh Cyl(float r0, float r1, float h, int seg = 16) => new CylinderMesh { TopRadius = r1, BottomRadius = r0, Height = h, RadialSegments = seg, Rings = 1 };
    static Mesh Torus(float r, float t, int seg = 48) => new TorusMesh { InnerRadius = r - t, OuterRadius = r + t, Rings = seg, RingSegments = 8 };
    static Mesh Ball(float r, int seg = 24) => new SphereMesh { Radius = r, Height = r * 2, RadialSegments = seg, Rings = seg / 2 };

    MeshInstance3D Glow(Node3D parent, Color col, float size, Vector3 pos, float strength = 2f, float shape = 0f, string name = null)
    {
        var s = Spectacle.Sprite(col, size, strength);
        ((ShaderMaterial)s.MaterialOverride).SetShaderParameter("shape", shape);
        s.Position = pos;
        if (name != null) s.Name = name;
        parent.AddChild(s);
        return s;
    }

    /// <summary>A beam: two crossed additive quads along +Z from the origin, unit length (scaled by the caller).</summary>
    Node3D Beam(Node3D parent, Color col, float width, float length, float strength, string name = "beam")
    {
        var b = new Node3D { Name = name };
        for (int i = 0; i < 2; i++)
        {
            var m = Spectacle.Additive(col, strength);
            m.SetShaderParameter("shape", 3f);
            var q = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(width, length) }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 10000 };
            // the quad stands along Y; lay it along Z, and spin the second a quarter turn
            q.Basis = new Basis(Vector3.Forward, i * Mathf.Pi / 2) * new Basis(Vector3.Right, -Mathf.Pi / 2);
            q.Position = new Vector3(0, 0, length * 0.5f);
            b.AddChild(q);
        }
        parent.AddChild(b);
        return b;
    }

    Node3D Build(string id, int level, Colony c, float R, float S)
    {
        var n = new Node3D { Name = id };
        var rnd = new Random(Galaxy.StableHash(id));
        if (NewWorks.Contains(id))
        {
            if (id == "aegis")
            {
                // a shield round the whole system, held up by six emitter pylons
                var m = new ShaderMaterial { Shader = new Shader { Code = ShieldCode } };
                n.AddChild(new MeshInstance3D { Name = "dome", Mesh = Ball(1f, 96), MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
                for (int i = 0; i < 6; i++) if (Asset("aegis") is Node3D py) { py.Name = $"p{i}"; py.SetMeta("r0", Radius(py, true)); n.AddChild(py); }
                return n;
            }
            if (Asset(id) is Node3D a) { a.Name = "work"; a.SetMeta("r0", Radius(a, true)); n.AddChild(a); if (OS.GetCmdlineUserArgs().Contains("--trace")) GD.Print($"WORK {id} r0={(float)a.GetMeta("r0")} meshes={string.Join(";", a.FindChildren("*", "MeshInstance3D", true, false).Cast<MeshInstance3D>().Select(m => m.Name + ":" + m.GetAabb().Size + ":" + m.Mesh.GetSurfaceCount()))}"); }
            return n;
        }
        switch (id)
        {
            case "starbase" when StationAsset(level) is Node3D asset:
            {
                // the hand-built station for this tier (assets/stations), fitted to the in-game scale
                float want = R * 0.3f * (1f + level * 0.3f);
                asset.Scale = Vector3.One * (want / Mathf.Max(Radius(asset), 1e-3f));
                n.AddChild(asset);
                break;
            }
            case "starbase":
            {
                // fallback when the asset is missing: grows with each era: hub, docking ring, habitat rings, defence platforms, a citadel's crown
                float k = R * 0.22f * (1f + level * 0.25f);
                var root = new Node3D { Scale = Vector3.One * k };
                n.AddChild(root);
                Add(root, Cyl(0.14f, 0.14f, 1.1f, 12), Hull, Vector3.Zero);
                Add(root, Ball(0.2f), Hull, new Vector3(0, 0.55f, 0));
                Add(root, Torus(0.45f, 0.05f), Hull, Vector3.Zero);
                Add(root, Torus(0.45f, 0.012f), Emis(new Color(0.8f, 0.9f, 1.4f)), new Vector3(0, 0.05f, 0));
                for (int i = 0; i < 4; i++) Add(root, Box(0.05f, 0.05f, 0.42f), Hull, Vector3.Zero, new Basis(Vector3.Up, i * Mathf.Pi / 2)).Position = new Basis(Vector3.Up, i * Mathf.Pi / 2) * new Vector3(0, 0, 0.22f);
                if (level >= 1)
                    for (int i = 0; i < 6; i++)
                    {
                        var b = new Basis(Vector3.Up, i * Mathf.Tau / 6);
                        Add(root, Box(0.06f, 0.04f, 0.5f), Hull, b * new Vector3(0, -0.2f, 0.7f), b);
                        Add(root, Box(0.12f, 0.08f, 0.12f), Hull, b * new Vector3(0, -0.2f, 0.98f), b);
                        Add(root, Ball(0.015f), Emis(new Color(2f, 0.5f, 0.3f)), b * new Vector3(0, -0.14f, 0.98f));
                    }
                if (level >= 2)
                {
                    Add(root, Torus(0.8f, 0.06f, 64), Hull, new Vector3(0, -0.3f, 0));
                    Add(root, Torus(0.8f, 0.015f, 64), Emis(new Color(1.6f, 1.2f, 0.7f)), new Vector3(0, -0.25f, 0));
                    for (int s = -1; s <= 1; s += 2) Add(root, Box(1.8f, 0.01f, 0.35f), Emis(new Color(0.08f, 0.12f, 0.3f)), new Vector3(s * 1.4f, 0.3f, 0));
                }
                if (level >= 3)
                    for (int i = 0; i < 8; i++)
                    {
                        // defence platforms with ion cannons
                        var b = new Basis(Vector3.Up, i * Mathf.Tau / 8 + 0.2f);
                        var at = b * new Vector3(0, 0.1f * (i % 2 == 0 ? 1 : -1), 1.35f);
                        Add(root, Box(0.16f, 0.06f, 0.16f), Hull, at, b);
                        Add(root, Cyl(0.02f, 0.012f, 0.25f, 6), Hull, at + b * new Vector3(0, 0.05f, 0.1f), b * Flat);
                        Add(root, Ball(0.012f), Emis(new Color(0.4f, 0.8f, 2f)), at + b * new Vector3(0, 0.05f, 0.23f));
                    }
                if (level >= 4)
                {
                    Add(root, Cyl(0.22f, 0.02f, 1.3f, 12), Hull, new Vector3(0, 1.2f, 0));
                    Add(root, Torus(0.3f, 0.02f), Emis(new Color(1.8f, 1.5f, 1f)), new Vector3(0, 1.0f, 0));
                    Add(root, Torus(1.1f, 0.04f, 64), Hull, new Vector3(0, 0.35f, 0), new Basis(Vector3.Right, 0.3f));
                    Glow(root, new Color(1f, 0.9f, 0.7f), 0.15f, new Vector3(0, 1.9f, 0), 3f);
                }
                break;
            }
            case "foundry":
            {
                var root = new Node3D { Scale = Vector3.One * 0.7f * S };
                n.AddChild(root);
                Add(root, Cyl(1f, 1f, 0.12f, 8), Hull, Vector3.Zero);
                Add(root, Cyl(0.7f, 0.9f, 0.2f, 8), Hull, new Vector3(0, -0.15f, 0));
                Add(root, Ball(0.32f), Emis(new Color(2.4f, 0.9f, 0.25f)), new Vector3(0, 0.25f, 0));      // the crucible
                Add(root, Cyl(0.42f, 0.25f, 0.35f, 12), Hull, new Vector3(0, 0.15f, 0));
                Glow(root, new Color(1f, 0.5f, 0.15f), 0.9f, new Vector3(0, 0.4f, 0), 2.2f);
                for (int i = 0; i < 4; i++)
                {
                    var b = new Basis(Vector3.Up, i * Mathf.Pi / 2 + Mathf.Pi / 4);
                    Add(root, Box(0.18f, 0.7f, 0.18f), Hull, b * new Vector3(0, 0.4f, 0.72f), b);                // smelter towers
                    Add(root, Box(0.2f, 0.02f, 0.2f), Emis(new Color(2f, 0.7f, 0.2f)), b * new Vector3(0, 0.76f, 0.72f), b);
                }
                for (int i = 0; i < 12; i++)
                {
                    var b = new Basis(Vector3.Up, i * Mathf.Tau / 12);
                    Add(root, Box(0.008f, 0.22f, 0.45f), Emis(new Color(0.16f, 0.03f, 0.01f)), b * new Vector3(0, -0.12f, 1.22f), b);   // radiators glowing dull red
                }
                Add(root, Box(0.05f, 0.05f, 2.4f), Hull, new Vector3(0, 0.02f, -2f));                             // the intake mass driver
                for (int i = 0; i < 6; i++) Glow(root, new Color(1f, 0.7f, 0.4f), 0.05f, new Vector3(0, 0.05f, -0.8f - i * 0.5f), 2f);
                break;
            }
            case "yard":
            {
                float L = (0.8f + 0.3f * Ships.MaxClass(c.Civ)) * S, W = L * 0.18f;
                n.SetMeta("L", L);
                foreach (var (sx, sy) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                    Add(n, Box(W * 0.06f, W * 0.06f, L), Hull, new Vector3(sx * W * 0.5f, sy * W * 0.5f, 0));
                for (int i = 0; i <= 8; i++)
                {
                    float z = -L * 0.5f + L * i / 8f;
                    Add(n, Box(W * 1.05f, W * 0.04f, W * 0.04f), Hull, new Vector3(0, W * 0.5f, z));
                    Add(n, Box(W * 1.05f, W * 0.04f, W * 0.04f), Hull, new Vector3(0, -W * 0.5f, z));
                    Add(n, Box(W * 0.04f, W * 1.05f, W * 0.04f), Hull, new Vector3(W * 0.5f, 0, z));
                    Add(n, Box(W * 0.02f, W * 0.02f, W * 0.02f), Emis(new Color(2f, 1.6f, 0.8f)), new Vector3(W * 0.52f, W * 0.52f, z));
                }
                for (int i = 0; i < 3; i++)
                {
                    var g = Add(n, Box(W * 1.2f, W * 0.08f, W * 0.12f), Hull, new Vector3(0, W * 0.62f, 0), null, $"gantry{i}");
                    Add(g, Box(W * 0.05f, W * 0.5f, W * 0.05f), Hull, new Vector3(W * 0.2f * (i - 1), -W * 0.3f, 0));
                }
                var hull = new MeshInstance3D { Name = "hull", Mesh = Ships.Mesh(Math.Max(Ships.MaxClass(c.Civ), 2), Ships.Style(c.Civ)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                n.AddChild(hull);
                for (int i = 0; i < 10; i++) Glow(n, new Color(0.7f, 0.85f, 1f), W * 0.06f, new Vector3(((float)rnd.NextDouble() - 0.5f) * W * 0.7f, ((float)rnd.NextDouble() - 0.5f) * W * 0.7f, ((float)rnd.NextDouble() - 0.5f) * L * 0.8f), 4f, 0f, $"spark{i}");
                break;
            }
            case "laser":
            {
                // a hexagonal mosaic of mirrors on a truss, the laser fired through its centre
                var root = new Node3D { Scale = Vector3.One * 0.9f * S };
                n.AddChild(root);
                var tile = new CylinderMesh { TopRadius = 0.11f, BottomRadius = 0.11f, Height = 0.02f, RadialSegments = 6, Rings = 1 };
                var mirror = new StandardMaterial3D { AlbedoColor = new Color(0.5f, 0.75f, 0.7f), Metallic = 1f, Roughness = 0.05f, EmissionEnabled = true, Emission = new Color(0.05f, 0.2f, 0.15f) };
                var pts = new List<Vector3>();
                for (int q = -5; q <= 5; q++) for (int r = -5; r <= 5; r++) { if (Math.Abs(q + r) > 5) continue; pts.Add(new Vector3((q + r * 0.5f) * 0.2f, r * 0.1732f, 0)); }
                var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = tile, InstanceCount = pts.Count };
                for (int i = 0; i < pts.Count; i++) mm.SetInstanceTransform(i, new Transform3D(Flat * new Basis(Vector3.Up, 0.5236f), pts[i] + new Vector3(0, 0, -pts[i].LengthSquared() * 0.15f)));
                root.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = mirror, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
                Add(root, Cyl(0.05f, 0.05f, 1.2f, 8), Hull, new Vector3(0, 0, -0.6f), Flat);
                Add(root, Ball(0.12f), Hull, new Vector3(0, 0, -1.2f));
                Beam(n, new Color(0.4f, 1f, 0.7f), 0.15f * S, 140f * S, 2.5f);
                break;
            }
            case "antimatter":
            {
                var root = new Node3D { Scale = Vector3.One * 0.8f * S };
                n.AddChild(root);
                Add(root, Torus(1.1f, 0.06f, 64), Hull, Vector3.Zero);
                Add(root, Torus(1.1f, 0.025f, 64), Emis(new Color(0.5f, 0.7f, 2.4f)), new Vector3(0, 0.0f, 0), null, "ring");
                for (int i = 0; i < 8; i++)
                {
                    var b = new Basis(Vector3.Up, i * Mathf.Tau / 8);
                    Add(root, Box(0.12f, 0.16f, 0.12f), Hull, b * new Vector3(0, 0, 1.1f), b);                    // bending magnets
                }
                for (int i = 0; i < 4; i++)
                {
                    var b = new Basis(Vector3.Up, i * Mathf.Pi / 2 + 0.4f);
                    Add(root, Ball(0.13f), Emis(new Color(1.6f, 0.5f, 2.2f)), b * new Vector3(0, 0.25f, 0.5f));     // magnetic bottles
                    Add(root, Box(0.02f, 0.25f, 0.02f), Hull, b * new Vector3(0, 0.1f, 0.5f));
                }
                Add(root, Cyl(0.1f, 0.1f, 0.6f, 10), Hull, Vector3.Zero);
                for (int s = -1; s <= 1; s += 2) Add(root, Box(3.2f, 0.01f, 0.9f), Emis(new Color(0.06f, 0.1f, 0.3f)), new Vector3(s * 2.8f, 0, 0));
                break;
            }
            case "sentry":
            {
                var root = new Node3D { Scale = Vector3.One * 1.3f * S };
                n.AddChild(root);
                var dish = Add(root, new SphereMesh { Radius = 1f, Height = 1f, IsHemisphere = true, RadialSegments = 48, Rings = 12 }, new StandardMaterial3D { AlbedoColor = new Color(0.75f, 0.76f, 0.8f), Metallic = 0.8f, Roughness = 0.25f, CullMode = BaseMaterial3D.CullModeEnum.Disabled }, Vector3.Zero, new Basis(Vector3.Right, -Mathf.Pi / 2));
                dish.Scale = new Vector3(1, 0.35f, 1);
                for (int i = 0; i < 3; i++)
                {
                    var b = new Basis(Vector3.Forward, i * Mathf.Tau / 3);
                    Add(root, Box(0.03f, 0.03f, 1.3f), Hull, b * new Vector3(0, 0.55f, 0.55f), b * new Basis(Vector3.Right, 0.75f));
                }
                Add(root, Ball(0.12f), Hull, new Vector3(0, 0, 1.0f));
                Glow(root, new Color(0.9f, 0.9f, 0.6f), 0.2f, new Vector3(0, 0, 1.05f), 3f);
                break;
            }
            case "cracker":
            {
                var root = new Node3D { Scale = Vector3.One * 0.25f * S };
                n.AddChild(root);
                Add(root, Box(0.5f, 0.3f, 1.2f), Hull, Vector3.Zero);
                Add(root, Cyl(0.35f, 0.15f, 0.5f, 10), Hull, new Vector3(0, 0, 0.8f), Flat);
                Add(root, Ball(0.12f), Emis(new Color(2.5f, 1.2f, 0.4f)), new Vector3(0, 0, 1.08f));
                for (int s = -1; s <= 1; s += 2) Add(root, Box(1.4f, 0.02f, 0.5f), Emis(new Color(0.1f, 0.12f, 0.3f)), new Vector3(s * 0.95f, 0, -0.2f));
                var beam = Beam(n, new Color(1f, 0.55f, 0.2f), 0.06f * S, 1f, 4f);
                // beam pieces are unit length along +Z: the caller stretches them to the planet
                break;
            }
            case "nexus":
            {
                float k = R * 0.6f;
                var root = new Node3D { Scale = Vector3.One * k };
                n.AddChild(root);
                Add(root, Ball(0.28f), Emis(new Color(0.5f, 1.8f, 2.2f)), Vector3.Zero);
                Glow(root, new Color(0.4f, 0.95f, 1f), 0.7f, Vector3.Zero, 0.7f);
                for (int i = 0; i < 3; i++)
                {
                    var r = new Node3D { Name = $"r{i}" };
                    root.AddChild(r);
                    float rad = 0.6f + i * 0.22f;
                    Add(r, Torus(rad, 0.035f, 64), Hull, Vector3.Zero);
                    Add(r, Torus(rad, 0.01f, 64), Emis(new Color(0.5f, 1.6f, 2f)), new Vector3(0, 0.03f, 0));
                    for (int j = 0; j < 4; j++) Add(r, Box(0.1f, 0.06f, 0.1f), Hull, new Basis(Vector3.Up, j * Mathf.Pi / 2) * new Vector3(0, 0, rad));
                }
                for (int i = 0; i < 6; i++) Add(root, Box(0.015f, 0.015f, 0.55f), Hull, new Basis(Vector3.Up, i * Mathf.Pi / 3) * new Basis(Vector3.Right, 0.4f) * new Vector3(0, 0, 0.3f), new Basis(Vector3.Up, i * Mathf.Pi / 3) * new Basis(Vector3.Right, 0.4f));
                break;
            }
            case "lifter":
            {
                // star-relative: scaled to the star's radius by Place
                for (int s = 0; s < 2; s++)
                {
                    float y = s == 0 ? 1 : -1;
                    var j = new Node3D { Name = $"j{s}" };
                    n.AddChild(j);
                    var m = Spectacle.Additive(new Color(1f, 0.65f, 0.3f), 2.2f);
                    m.SetShaderParameter("shape", 2f);
                    for (int q = 0; q < 2; q++)
                    {
                        var quad = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(0.5f, 3.2f) }, MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 1000 };
                        quad.Basis = new Basis(Vector3.Up, q * Mathf.Pi / 2) * (y > 0 ? new Basis(Vector3.Right, Mathf.Pi) : Basis.Identity);
                        quad.Position = new Vector3(0, y * 2.6f, 0);
                        j.AddChild(quad);
                    }
                    Add(n, Torus(0.7f, 0.08f, 48), Hull, new Vector3(0, y * 4.2f, 0));
                    Add(n, Torus(0.7f, 0.025f, 48), Emis(new Color(2f, 1.2f, 0.5f)), new Vector3(0, y * 4.15f, 0));
                    for (int i = 0; i < 6; i++) Add(n, Box(0.06f, 0.4f, 0.06f), Hull, new Basis(Vector3.Up, i * Mathf.Pi / 3) * new Vector3(0, y * 4.2f, 0.85f));
                }
                break;
            }
            case "jumps":
            {
                Glow(n, new Color(0.9f, 0.95f, 1f), 1f, Vector3.Zero, 4f, 0f, "flash");
                Glow(n, new Color(0.6f, 0.75f, 1f), 1f, Vector3.Zero, 2.5f, 1f, "ring");
                var ship = new MeshInstance3D { Name = "ship", Mesh = Ships.Mesh(Math.Max(Ships.MaxClass(c.Civ), 5), Ships.Style(c.Civ)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                n.AddChild(ship);
                break;
            }
            case "beam":
            {
                var root = new Node3D { Scale = Vector3.One * 1.6f * S };
                n.AddChild(root);
                var lens = Add(root, Ball(1f, 48), new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.9f, 1f, 0.35f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Metallic = 1f, Roughness = 0.02f, EmissionEnabled = true, Emission = new Color(0.4f, 0.35f, 0.25f) }, Vector3.Zero);
                lens.Scale = new Vector3(1, 1, 0.08f);
                Add(root, Torus(1.02f, 0.05f, 64), Hull, Vector3.Zero, Flat);
                for (int i = 0; i < 6; i++)
                {
                    var b = new Basis(Vector3.Forward, i * Mathf.Pi / 3);
                    Add(root, Box(0.04f, 0.04f, 1.6f), Hull, b * new Vector3(0, 0.55f, -0.65f), b * new Basis(Vector3.Right, -0.6f));
                }
                Add(root, Ball(0.15f), Emis(new Color(2.5f, 2.2f, 1.6f)), new Vector3(0, 0, 0.2f));
                Beam(n, new Color(1f, 0.92f, 0.75f), 1.2f * S, 900f * S, 3f);
                break;
            }
            case "bishop":
            {
                n.AddChild(BishopRing(2.6f * S, 0.55f * S));
                break;
            }
            case "coord":
            {
                float k = R * 0.55f;
                var root = new Node3D { Scale = Vector3.One * k };
                n.AddChild(root);
                Add(root, Ball(0.35f), Hull, Vector3.Zero);
                Add(root, Ball(0.2f), Emis(new Color(2f, 1.5f, 0.6f)), new Vector3(0, 0, 0.22f));
                var dirs = new[] { Vector3.Up, Vector3.Down, Vector3.Left, Vector3.Right, Vector3.Forward, Vector3.Back,
                    new Vector3(1, 1, 1).Normalized(), new Vector3(-1, 1, -1).Normalized(), new Vector3(1, -1, -1).Normalized(), new Vector3(-1, -1, 1).Normalized() };
                foreach (var d in dirs)
                {
                    var b = Basis.LookingAt(d, Mathf.Abs(d.Y) > 0.9f ? Vector3.Right : Vector3.Up);
                    Add(root, Box(0.04f, 0.04f, 0.5f), Hull, d * 0.55f, b);
                    Add(root, Ball(0.1f), Hull, d * 0.82f);
                    Add(root, Ball(0.03f), Emis(new Color(2f, 1.4f, 0.5f)), d * 0.92f);
                }
                Add(root, Torus(1.15f, 0.025f, 64), Emis(new Color(1.6f, 1.1f, 0.4f)), Vector3.Zero, new Basis(Vector3.Right, 0.4f), "halo");
                break;
            }
            case "gateway":
            {
                var root = new Node3D { Scale = Vector3.One * 1.4f * S };
                n.AddChild(root);
                Add(root, Torus(1f, 0.12f, 64), Hull, Vector3.Zero, Flat);
                for (int i = 0; i < 8; i++)
                {
                    var b = new Basis(Vector3.Forward, i * Mathf.Tau / 8);
                    Add(root, Box(0.18f, 0.35f, 0.3f), Hull, b * new Vector3(0, 1.12f, 0), b);
                    Add(root, Ball(0.04f), Emis(new Color(1.4f, 0.8f, 2.4f)), b * new Vector3(0, 0.92f, 0));
                }
                var disc = new Node3D { Name = "disc" };
                root.AddChild(disc);
                Glow(disc, new Color(0.6f, 0.4f, 1f), 0.95f, Vector3.Zero, 1.6f, 1f);
                Glow(disc, new Color(0.5f, 0.35f, 1f), 0.8f, Vector3.Zero, 0.5f);
                break;
            }
            case "relay":
            {
                for (int i = 0; i < 3; i++)
                {
                    var b = new Node3D { Name = $"b{i}", Scale = Vector3.One * 0.35f * S };
                    n.AddChild(b);
                    Add(b, Cyl(0.06f, 0.06f, 1.6f, 8), Hull, Vector3.Zero);
                    Add(b, Torus(0.4f, 0.04f), Hull, Vector3.Zero, Flat);
                    Add(b, Torus(0.4f, 0.012f), Emis(new Color(0.5f, 1f, 2.2f)), new Vector3(0, 0.05f, 0), Flat);
                    for (int j = 0; j < 3; j++) Add(b, Box(0.5f, 0.02f, 0.2f), Emis(new Color(0.06f, 0.1f, 0.3f)), new Basis(Vector3.Up, j * Mathf.Tau / 3) * new Vector3(0, -0.4f, 0.55f), new Basis(Vector3.Up, j * Mathf.Tau / 3));
                    Glow(b, new Color(0.5f, 0.85f, 1f), 1f, new Vector3(0, 0.85f, 0), 2.5f, 0f, "pulse");
                }
                break;
            }
            case "wormhole":
            {
                // a bright Einstein ring around a dark throat, with a swirl of infalling light
                var root = new Node3D { Scale = Vector3.One * 1.6f * S };
                n.AddChild(root);
                Add(root, Ball(0.5f, 32), new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.02f, 0.03f, 0.08f) }, Vector3.Zero).Scale = new Vector3(1, 1, 0.1f);
                Glow(root, new Color(0.65f, 0.8f, 1f), 0.62f, Vector3.Zero, 3f, 1f);
                Glow(root, new Color(0.9f, 0.6f, 1f), 0.75f, Vector3.Zero, 1.2f, 1f);
                var swirl = new Node3D { Name = "swirl" };
                root.AddChild(swirl);
                for (int i = 0; i < 90; i++)
                {
                    float r = 0.55f + Mathf.Pow((float)rnd.NextDouble(), 2f) * 0.5f, a = (float)rnd.NextDouble() * Mathf.Tau;
                    Glow(swirl, new Color(0.6f, 0.75f, 1f), 0.008f + 0.012f * (float)rnd.NextDouble(), new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0), 2f);
                }
                Glow(root, new Color(0.4f, 0.5f, 1f), 1.6f, Vector3.Zero, 0.6f);
                break;
            }
            case "kugel":
            {
                float k = R * 0.35f;
                var root = new Node3D { Scale = Vector3.One * k };
                n.AddChild(root);
                Add(root, Ball(0.14f), new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Colors.Black }, Vector3.Zero);
                Glow(root, new Color(0.7f, 0.85f, 1f), 0.25f, Vector3.Zero, 2f, 1f);
                Glow(root, new Color(0.6f, 0.8f, 1f), 0.5f, Vector3.Zero, 1.2f);
                for (int i = 0; i < 3; i++)
                {
                    var cage = new Node3D { Name = $"c{i}" };
                    root.AddChild(cage);
                    Add(cage, Torus(0.6f + i * 0.08f, 0.025f), Hull, Vector3.Zero);
                    for (int j = 0; j < 4; j++) Add(cage, Ball(0.04f), Emis(new Color(0.6f, 1.2f, 2.4f)), new Basis(Vector3.Up, j * Mathf.Pi / 2) * new Vector3(0, 0, 0.6f + i * 0.08f));
                }
                break;
            }
            case "forge":
            {
                var root = new Node3D { Scale = Vector3.One * 1.5f * S };
                n.AddChild(root);
                Add(root, Ball(0.3f), new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Colors.Black }, Vector3.Zero);
                var swirl = new Node3D { Name = "swirl" };
                root.AddChild(swirl);
                for (int i = 0; i < 4; i++) Add(swirl, Torus(0.5f + i * 0.28f, 0.03f + i * 0.01f, 64), Emis(new Color(2.4f - i * 0.4f, 0.9f - i * 0.15f, 0.25f)), Vector3.Zero, new Basis(Vector3.Right, 0.05f * i));
                Glow(root, new Color(1f, 0.6f, 0.25f), 0.38f, Vector3.Zero, 2.5f, 1f);
                Glow(root, new Color(1f, 0.5f, 0.2f), 1.8f, Vector3.Zero, 0.8f);
                // feeding stations and the planet-stuff they pour in
                for (int i = 0; i < 3; i++)
                {
                    var b = new Basis(Vector3.Up, i * Mathf.Tau / 3);
                    Add(root, Box(0.25f, 0.12f, 0.25f), Hull, b * new Vector3(0, 0.1f, 2.1f), b);
                    for (int j = 0; j < 6; j++) Glow(root, new Color(1f, 0.7f, 0.4f), 0.05f, b * new Vector3(0, 0.08f, 2f - j * 0.25f) + b * new Vector3(Mathf.Sin(j * 0.6f) * 0.2f, 0, 0), 2f);
                }
                break;
            }
            case "memory":
            {
                float k = R * 0.9f;
                var root = new Node3D { Scale = Vector3.One * k };
                n.AddChild(root);
                Add(root, Cyl(0.03f, 0.005f, 2.4f, 8), Hull, Vector3.Zero);
                for (int i = 0; i < 80; i++)
                {
                    float u = i / 80f, a = u * Mathf.Tau * 4f;
                    for (int s = 0; s < 2; s++)
                    {
                        var col = new Color(1.2f + u, 0.8f + u * 0.4f, 2f - u);
                        Add(root, Ball(0.025f, 8), Emis(col), new Vector3(Mathf.Cos(a + s * Mathf.Pi) * 0.3f, -1.1f + u * 2.2f, Mathf.Sin(a + s * Mathf.Pi) * 0.3f));
                    }
                }
                Glow(root, new Color(1f, 0.85f, 1f), 0.6f, new Vector3(0, 1.25f, 0), 2.5f);
                break;
            }
            case "shield":
            {
                var m = new ShaderMaterial { Shader = new Shader { Code = ShieldCode } };
                n.AddChild(new MeshInstance3D { Mesh = Ball(1.22f, 64), MaterialOverride = m, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
                break;
            }
        }
        return n;
    }

    const string ShieldCode = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_back, shadows_disabled, fog_disabled;
uniform float time = 0.0;
varying vec3 v_o; varying vec3 v_n; varying vec3 v_w;
void vertex(){ v_o = VERTEX; v_n = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz); v_w = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment(){
	float f = 1.0 - abs(dot(normalize(v_n), normalize(CAMERA_POSITION_WORLD - v_w)));
	// a hexagonal lattice on the sphere, shimmering in waves
	vec3 p = normalize(v_o) * 18.0;
	vec2 uv = vec2(atan(p.z, p.x) * 3.0, p.y * 1.7);
	vec2 r = vec2(1.0, 1.732);
	vec2 a = mod(uv, r) - r * 0.5, b = mod(uv - r * 0.5, r) - r * 0.5;
	vec2 g = dot(a, a) < dot(b, b) ? a : b;
	float hex = smoothstep(0.38, 0.47, max(abs(g.x) * 0.866 + abs(g.y) * 0.5, abs(g.y)));
	float wave = 0.5 + 0.5 * sin(p.y * 0.6 - time * 1.5 + atan(p.z, p.x) * 2.0);
	ALBEDO = vec3(0.35, 0.65, 1.0) * (pow(f, 4.0) * 0.4 + hex * (0.03 + 0.12 * wave) * (0.2 + f));
}";

    /// <summary>A Bishop ring: an open band of land and sea held to its inside face by spin, with rim walls.</summary>
    static Node3D BishopRing(float radius, float width)
    {
        var root = new Node3D();
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        int seg = 160;
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * Mathf.Tau / seg, a1 = (i + 1) * Mathf.Tau / seg;
            Vector3 P(float a, float z) => new(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, z);
            void V(float a, float z, float u, float v) { st.SetNormal(-new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0)); st.SetUV(new Vector2(u, v)); st.AddVertex(P(a, z)); }
            float u0 = i / (float)seg, u1 = (i + 1) / (float)seg;
            V(a0, -width / 2, u0, 0); V(a1, -width / 2, u1, 0); V(a1, width / 2, u1, 1);
            V(a0, -width / 2, u0, 0); V(a1, width / 2, u1, 1); V(a0, width / 2, u0, 1);
        }
        var land = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = @"shader_type spatial;
render_mode cull_disabled;
float h(vec2 p){ return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float n(vec2 p){ vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f); return mix(mix(h(i), h(i + vec2(1, 0)), f.x), mix(h(i + vec2(0, 1)), h(i + vec2(1, 1)), f.x), f.y); }
void fragment(){
	if (!FRONT_FACING) {
		// the outside of the band is bare hull with a few lit ports
		ALBEDO = vec3(0.32, 0.33, 0.36); METALLIC = 0.7; ROUGHNESS = 0.4;
		EMISSION = vec3(1.0, 0.8, 0.5) * step(0.97, h(floor(vec2(UV.x * 900.0, UV.y * 12.0)))) * 1.5;
	} else {
	vec2 p = vec2(UV.x * 220.0, UV.y * 6.0);
	float e = n(p) * 0.6 + n(p * 3.1) * 0.3 + n(p * 9.0) * 0.1;
	float edge = smoothstep(0.0, 0.08, UV.y) * smoothstep(0.0, 0.08, 1.0 - UV.y);
	vec3 sea = vec3(0.05, 0.18, 0.35), grass = vec3(0.15, 0.32, 0.1), rock = vec3(0.4, 0.36, 0.3);
	vec3 col = e < 0.45 ? sea : mix(grass, rock, smoothstep(0.65, 0.8, e));
	col = mix(vec3(0.5), col, edge);
	float cloud = smoothstep(0.55, 0.75, n(p * 1.7 + 13.0));
	ALBEDO = mix(col, vec3(0.9), cloud * 0.6);
	ROUGHNESS = e < 0.45 ? 0.2 : 0.9;
	EMISSION = vec3(1.0, 0.75, 0.45) * step(0.96, h(floor(p * 8.0))) * step(0.45, e) * 1.2 + col * 0.05;
	}
}"
            }
        };
        st.GenerateTangents();
        root.AddChild(new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = land, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        // the two rim walls that hold the air in, and spokes of mirror-struts
        foreach (float z in new[] { -width / 2, width / 2 })
            root.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = radius * 0.975f, OuterRadius = radius * 1.005f, Rings = 160, RingSegments = 6 }, MaterialOverride = Works.HullMat(), Basis = new Basis(Vector3.Right, Mathf.Pi / 2), Position = new Vector3(0, 0, z), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        return root;
    }
}
