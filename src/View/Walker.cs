using System;
using System.Linq;
using Godot;

namespace Starforge.View;

/// <summary>
/// First person aboard a station, at real scale (metres). The station spins about its Y axis, so out in the
/// habitat ring "down" is away from the axis; in the spine, decks are held down by a gentle thrust gravity
/// along -Y. A suit jetpack (Space) climbs lift shafts and spoke ladders. Esc leaves.
/// </summary>
public partial class Walker : Node3D
{
    Main _main;
    Node3D _station;
    CharacterBody3D _body;
    Node3D _head;
    public Camera3D Cam;
    float _yaw, _pitch;
    Vector3 _up = Vector3.Up;
    Label _hint;
    const float RingR = 30f;          // beyond this distance from the axis, spin gravity rules
    const float G = 9.0f;

    public static Walker Board(Main main, string slug)
    {
        var path = $"res://assets/stations/{slug}/{slug}.glb";
        if (!ResourceLoader.Exists(path)) return null;
        var w = new Walker { _main = main, Name = "Walker" };
        w._station = GD.Load<PackedScene>(path).Instantiate<Node3D>();
        w.AddChild(w._station);
        return w;
    }

    public override void _Ready()
    {
        // glTF lamps arrive in photometric units (a 500 W Blender lamp is ~27,000 here): bring them to Godot's scale
        foreach (var l in _station.FindChildren("*", "Light3D", true, false).Cast<Light3D>())
        {
            float watts = l.LightEnergy / 54.35f;
            l.LightEnergy = Mathf.Clamp(watts / 400f, 0.3f, 6f);
            l.ShadowEnabled = false;
            if (l is OmniLight3D o) { o.OmniRange = 8f + Mathf.Sqrt(watts) * 0.9f; o.OmniAttenuation = 1.4f; }
            if (l is SpotLight3D sp) sp.SpotRange = 120f;
        }
        // the world outside the windows: the sun, and Earth filling the sky below the ring
        var sun = new DirectionalLight3D { LightEnergy = 1.6f, LightColor = new Color(1f, 0.96f, 0.9f), ShadowEnabled = true, Rotation = new Vector3(-0.6f, 0.7f, 0) };
        AddChild(sun);
        var earthMat = new StandardMaterial3D { AlbedoTexture = SystemView.Tex("8k_earth_daymap"), EmissionEnabled = true, EmissionTexture = SystemView.Tex("8k_earth_nightmap"), EmissionEnergyMultiplier = 0.6f, Roughness = 0.8f };
        var earth = new MeshInstance3D { Mesh = new SphereMesh { Radius = 6000, Height = 12000, RadialSegments = 128, Rings = 64 }, MaterialOverride = earthMat, Position = new Vector3(0, -6600, -2500) };
        earth.Rotation = new Vector3(0.4f, 0, 0.2f);
        AddChild(earth);
        var atmo = new MeshInstance3D { Mesh = new SphereMesh { Radius = 6080, Height = 12160, RadialSegments = 96, Rings = 48 }, Position = earth.Position,
            MaterialOverride = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(0.35f, 0.6f, 1f, 0.18f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Front } };
        AddChild(atmo);

        _body = new CharacterBody3D { FloorMaxAngle = 0.9f, FloorSnapLength = 0.4f };
        _body.AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.35f, Height = 1.75f } });
        _head = new Node3D { Position = new Vector3(0, 0.7f, 0) };
        _body.AddChild(_head);
        Cam = new Camera3D { Near = 0.05f, Far = 40000f, Fov = 75 };
        _head.AddChild(Cam);
        AddChild(_body);
        // start in the habitat ring's commons, standing on the outer floor
        float a = Mathf.DegToRad(31f);            // between two bulkheads, looking along the corridor
        Place(new Vector3(Mathf.Cos(a) * 42.6f, 0, -Mathf.Sin(a) * 42.6f), 0f, 0.12f);
        Cam.MakeCurrent();
        Input.MouseMode = Input.MouseModeEnum.Captured;

        _hint = new Label { Text = "WASD walk · mouse look · Space jetpack · Shift run · Esc leave the station", Position = new Vector2(24, 24), Modulate = new Color(1, 1, 1, 0.7f) };
        var layer = new CanvasLayer { Layer = 5 };
        layer.AddChild(_hint);
        AddChild(layer);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseMotion mm && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _yaw -= mm.Relative.X * 0.0025f;
            _pitch = Mathf.Clamp(_pitch - mm.Relative.Y * 0.0025f, -1.5f, 1.5f);
        }
        if (e is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.Escape) { GetViewport().SetInputAsHandled(); _main.Disembark(); }
        if (e is InputEventMouseButton mb && mb.Pressed) Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    Basis _frame = Basis.Identity;

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        var p = _body.Position;
        float r = new Vector2(p.X, p.Z).Length();
        // which way is down here: outward in the ring, along -Y in the spine and spokes
        var want = r > RingR ? -new Vector3(p.X, 0, p.Z) / r : Vector3.Up;
        _up = _up.Slerp(want, 1f - Mathf.Exp(-dt * 4f)).Normalized();
        _body.UpDirection = _up;
        // the body's frame turns with "up" so walking and looking stay level with the floor
        var fwd0 = _frame.Z;
        var fwd = (fwd0 - _up * fwd0.Dot(_up));
        if (fwd.LengthSquared() < 1e-4f) fwd = _up.Cross(Vector3.Right);
        _frame = Basis.LookingAt(-fwd.Normalized(), _up).Orthonormalized();
        var look = _frame * new Basis(Vector3.Up, _yaw);
        _body.Basis = look;
        _head.Basis = new Basis(Vector3.Right, _pitch);

        var input = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W)) input.Y -= 1;
        if (Input.IsKeyPressed(Key.S)) input.Y += 1;
        if (Input.IsKeyPressed(Key.A)) input.X -= 1;
        if (Input.IsKeyPressed(Key.D)) input.X += 1;
        if (OS.GetCmdlineUserArgs().Contains("--walkfwd")) input.Y = -1;          // test: walk straight ahead
        float speed = Input.IsKeyPressed(Key.Shift) ? 9f : 4.2f;
        var move = (look * new Vector3(input.X, 0, input.Y)).Normalized() * speed;

        var v = _body.Velocity;
        var vUp = _up * v.Dot(_up);
        var vFlat = v - vUp;
        vFlat = vFlat.Lerp(move, 1f - Mathf.Exp(-dt * (_body.IsOnFloor() ? 12f : 2f)));
        float g = r > RingR ? G * Mathf.Clamp((r - RingR) / 10f, 0.2f, 1f) : G * 0.4f;     // spin gravity grows toward the rim
        vUp -= _up * g * dt;
        if (Input.IsKeyPressed(Key.Space)) vUp += _up * 16f * dt;                         // the suit's jetpack
        if (vUp.Dot(_up) > 6f) vUp = _up * 6f;
        _body.Velocity = vFlat + vUp;
        _body.MoveAndSlide();
        if (OS.GetCmdlineUserArgs().Contains("--trace") && Engine.GetPhysicsFrames() % 20 == 0) GD.Print($"walker pos {_body.Position} up {_up} floor {_body.IsOnFloor()} v {_body.Velocity}");
        // safety: anyone who falls out of the station is caught and put back aboard
        if (_body.Position.Length() > 400f) { _body.Position = new Vector3(42.6f, 0, 0); _body.Velocity = Vector3.Zero; }
    }

    /// <summary>Test framing: stand somewhere and look somewhere.</summary>
    public void Place(Vector3 at, float yaw, float pitch)
    {
        _body.Position = at;
        float r = new Vector2(at.X, at.Z).Length();
        _up = r > RingR ? -new Vector3(at.X, 0, at.Z) / r : Vector3.Up;
        _frame = Basis.LookingAt(_up.Cross(Vector3.Up).LengthSquared() > 1e-3f ? _up.Cross(Vector3.Up).Normalized() : Vector3.Forward, _up);
        _yaw = yaw; _pitch = pitch;
    }
}
