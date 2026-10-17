using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace Starforge.View;

/// <summary>
/// First person aboard a station, at real scale (metres). Rev B: gravity is generated, 1 g down (-Y) everywhere, so the
/// ring's decks are flat floors and the spokes are level walkways from the spine's hub deck. Loads the furnished build
/// (assets/stations/&lt;slug&gt;/&lt;slug&gt;_interior.glb, falling back to the shell) and its interior_lights.json: ceiling
/// lights, and where the Earth and the sun are, so the view out of the windows matches the renders. Space = suit
/// jetpack (lift shafts). Esc leaves.
/// </summary>
public partial class Walker : Node3D
{
    Main _main;
    Node3D _station;
    CharacterBody3D _body;
    Node3D _head;
    public Camera3D Cam;
    float _yaw, _pitch;
    Label _hint;
    const float G = 9.8f;
    readonly List<OmniLight3D> _lamps = new();
    Vector3 _spawn = new(0, 0.8f, -41.2f);
    float _spawnYaw = Mathf.Pi / 2;
    JsonElement _extras;
    MeshInstance3D _earth, _clouds;
    const float OrbitRate = 2 * Mathf.Pi / (92.6f * 60f);     // low Earth orbit: one lap in ~92.6 min

    public static Walker Board(Main main, string slug)
    {
        var path = $"res://assets/stations/{slug}/{slug}_interior.glb";
        if (!ResourceLoader.Exists(path)) path = $"res://assets/stations/{slug}/{slug}.glb";
        if (!ResourceLoader.Exists(path)) return null;
        var w = new Walker { _main = main, Name = "Walker" };
        w._station = GD.Load<PackedScene>(path).Instantiate<Node3D>();
        w.AddChild(w._station);
        var json = ProjectSettings.GlobalizePath($"res://assets/stations/{slug}/interior_lights.json");
        if (System.IO.File.Exists(json)) w._extras = JsonDocument.Parse(System.IO.File.ReadAllText(json)).RootElement;
        return w;
    }

    static Vector3 V(JsonElement a) => new(a[0].GetSingle(), a[1].GetSingle(), a[2].GetSingle());

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
        // the generated interior is partly single-sided sheets (floor plates): collide with both faces
        foreach (var cs in _station.FindChildren("*", "CollisionShape3D", true, false).Cast<CollisionShape3D>())
            if (cs.Shape is ConcavePolygonShape3D cp) cp.BackfaceCollision = true;
        bool have = _extras.ValueKind == JsonValueKind.Object;
        // ceiling lights: one small omni under each panel; only the ones near you are switched on (see _PhysicsProcess)
        if (have)
            foreach (var p in _extras.GetProperty("lights").EnumerateArray())
            {
                var lamp = new OmniLight3D { Position = V(p), LightEnergy = 0.45f, LightColor = new Color(1f, 0.92f, 0.82f), OmniRange = 4.5f, OmniAttenuation = 1.2f, Visible = false };
                AddChild(lamp); _lamps.Add(lamp);
            }
        // the world outside the windows: the sun, and the Earth where the renders put it
        var sunDir = have ? V(_extras.GetProperty("sun_dir")).Normalized() : new Vector3(-0.47f, -0.57f, -0.67f);
        var sun = new DirectionalLight3D { LightEnergy = 1.6f, LightColor = new Color(1f, 0.96f, 0.9f), ShadowEnabled = true, DirectionalShadowMaxDistance = 60f };
        AddChild(sun);
        sun.LookAt(sun.GlobalPosition + sunDir, Mathf.Abs(sunDir.Y) > 0.99f ? Vector3.Forward : Vector3.Up);
        var ePos = have ? V(_extras.GetProperty("earth_pos")) : new Vector3(-254, -1569, -584);
        float eR = have ? _extras.GetProperty("earth_r").GetSingle() : 1593f;
        var earthMat = new StandardMaterial3D { AlbedoTexture = SystemView.Tex("8k_earth_daymap"), EmissionEnabled = true, EmissionTexture = SystemView.Tex("8k_earth_nightmap"), EmissionEnergyMultiplier = 0.35f, Roughness = 0.9f, MetallicSpecular = 0.1f, AlbedoColor = new Color(0.62f, 0.62f, 0.62f) };
        var earth = new MeshInstance3D { Mesh = new SphereMesh { Radius = eR, Height = 2 * eR, RadialSegments = 192, Rings = 96 }, MaterialOverride = earthMat, Position = ePos,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        earth.Rotation = new Vector3(Mathf.Pi / 2, 0, -1.83f);    // pole sideways: oceans and cloud below, not the ice cap
        AddChild(earth); _earth = earth;
        // clouds: the map's brightness is the cloud cover
        var cloudShader = new Shader { Code = "shader_type spatial; render_mode cull_back, depth_draw_never; uniform sampler2D clouds : source_color, filter_linear_mipmap;" +
            " void fragment() { float c = texture(clouds, UV).r; ALBEDO = vec3(1.0); ALPHA = smoothstep(0.08, 0.9, c); ROUGHNESS = 0.95; SPECULAR = 0.0; }" };
        var cloudMat = new ShaderMaterial { Shader = cloudShader };
        cloudMat.SetShaderParameter("clouds", SystemView.Tex("8k_earth_clouds"));
        _clouds = new MeshInstance3D { Mesh = new SphereMesh { Radius = eR * 1.004f, Height = eR * 2.008f, RadialSegments = 192, Rings = 96 }, MaterialOverride = cloudMat,
            Position = ePos, Rotation = earth.Rotation, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_clouds);
        var atmo = new MeshInstance3D { Mesh = new SphereMesh { Radius = eR * 1.02f, Height = eR * 2.04f, RadialSegments = 128, Rings = 64 }, Position = ePos,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(0.35f, 0.6f, 1f, 0.18f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Front } };
        AddChild(atmo);

        _body = new CharacterBody3D { FloorMaxAngle = 0.9f, FloorSnapLength = 0.3f, UpDirection = Vector3.Up };
        _body.AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = 0.3f, Height = 1.75f } });
        _head = new Node3D { Position = new Vector3(0, 0.7f, 0) };
        _body.AddChild(_head);
        Cam = new Camera3D { Near = 0.05f, Far = 8000f, Fov = 75 };
        _head.AddChild(Cam);
        AddChild(_body);
        if (have) { _spawn = V(_extras.GetProperty("spawn")); _spawnYaw = _extras.GetProperty("spawn_yaw").GetSingle(); }
        Place(_spawn, _spawnYaw, 0.05f);           // the Deck A concourse at Spoke 2
        Cam.MakeCurrent();
        Input.MouseMode = Input.MouseModeEnum.Captured;

        _hint = new Label { Text = "WASD walk · mouse look · Shift run · Space jetpack · Esc leave the station", Position = new Vector2(24, 24), Modulate = new Color(1, 1, 1, 0.7f) };
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

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        var look = new Basis(Vector3.Up, _yaw);
        _body.Basis = look;
        _head.Basis = new Basis(Vector3.Right, _pitch);

        var input = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W)) input.Y -= 1;
        if (Input.IsKeyPressed(Key.S)) input.Y += 1;
        if (Input.IsKeyPressed(Key.A)) input.X -= 1;
        if (Input.IsKeyPressed(Key.D)) input.X += 1;
        if (OS.GetCmdlineUserArgs().Contains("--walkfwd")) input.Y = -1;          // test: walk straight ahead
        float speed = Input.IsKeyPressed(Key.Shift) ? 7f : 3.6f;
        var move = (look * new Vector3(input.X, 0, input.Y)).Normalized() * speed;

        var v = _body.Velocity;
        var flat = new Vector3(v.X, 0, v.Z).Lerp(move, 1f - Mathf.Exp(-dt * (_body.IsOnFloor() ? 12f : 2f)));
        float vy = v.Y - G * dt;
        if (Input.IsKeyPressed(Key.Space)) vy += 16f * dt;                       // the suit's jetpack
        vy = Mathf.Min(vy, 6f);
        _body.Velocity = new Vector3(flat.X, vy, flat.Z);
        _body.MoveAndSlide();

        // the station's orbit: the ground slides past under the windows at the real rate
        _earth.RotateObjectLocal(Vector3.Up, OrbitRate * dt);
        _clouds.RotateObjectLocal(Vector3.Up, OrbitRate * 1.03f * dt);      // weather drifts a little against the ground
        // lights: only those within reach are on (the renderer's per-view light budget is ~512)
        if (Engine.GetPhysicsFrames() % 15 == 0)
        {
            var at = _body.Position;
            foreach (var l in _lamps) l.Visible = l.Position.DistanceSquaredTo(at) < 22f * 22f;
        }
        if (OS.GetCmdlineUserArgs().Contains("--trace") && Engine.GetPhysicsFrames() % 20 == 0) GD.Print($"walker pos {_body.Position} floor {_body.IsOnFloor()} v {_body.Velocity}");
        // safety: anyone who falls out of the station is caught and put back aboard
        if (_body.Position.Length() > 400f) { _body.Position = _spawn; _body.Velocity = Vector3.Zero; }
    }

    /// <summary>Stand somewhere and look somewhere (yaw about +Y; 0 looks along -Z).</summary>
    public void Place(Vector3 at, float yaw, float pitch)
    {
        _body.Position = at;
        _body.Velocity = Vector3.Zero;
        _yaw = yaw; _pitch = pitch;
    }
}
