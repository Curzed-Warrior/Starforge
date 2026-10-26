using System;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// Orbit camera with logarithmic zoom spanning a few metres to the width of the galaxy, so one wheel takes you from
/// a shuttle's hull out past the edge of the galaxy. Follows a moving target, or flies free (Tab).
/// Targets are double-precision system coordinates; the camera keeps its target at the view origin (see Frame).
/// Main calls Step() once a frame, before anything that places itself relative to the origin.
/// </summary>
public partial class CameraRig : Node3D
{
    public Camera3D Cam;
    public Func<D3> Target = () => D3.Zero;
    public float MaxDist = 1e15f;              // a battle keeps the camera within reach of the fight
    public float MinDist = 0.02f;               // set per target (e.g. planet radius * 1.15)
    public float Yaw = 0.6f, Pitch = -0.35f;
    public float LogDist = Mathf.Log(60f);
    public bool UserControl = true;
    public static float FarOverride = -1;
    public static float NearK = 0.002f;
    public bool AutoOrbit;                      // slow cinematic drift
    public float FlyT = 1f;                     // 0..1 while travelling to a new target
    /// <summary>A cinematic shot in control of the camera: position, point looked at, up, field of view (view space).</summary>
    public (Vector3 Pos, Vector3 Look, Vector3 Up, float Fov)? Shot;

    // ---------- free flight ----------
    /// <summary>Roaming the system on your own: WASD to fly, mouse to look, wheel to change speed.</summary>
    public bool Free { get; private set; }
    D3 _freePos;
    float _freeSpeed = 1f;                      // system units per second, scaled by the wheel
    public float FreeSpeed => _freeSpeed;
    /// <summary>Where the free camera is, in system coordinates.</summary>
    public D3 FreePos => _freePos;
    public event Action FreeChanged;

    public void ToggleFree()
    {
        Free = !Free;
        if (Free)
        {
            _freePos = Frame.Sys(Cam.GlobalPosition);
            // fly at a speed that suits what we were looking at: a tenth of its distance per second
            _freeSpeed = Mathf.Max(Dist * 0.25f, 1e-5f);
            var fwd = -Cam.GlobalBasis.Z;
            _fYaw = Mathf.Atan2(-fwd.X, -fwd.Z); _fPitch = Mathf.Asin(Mathf.Clamp(fwd.Y, -1, 1));
            Shot = null; FlyT = 1f;
        }
        else
        {
            // back to orbiting: whatever is straight ahead at the same distance becomes the target
            var ahead = _freePos + (-Cam.GlobalBasis.Z) * Mathf.Max(_freeSpeed * 4f, MinDist * 2f);
            Target = () => ahead; MinDist = Mathf.Max(_freeSpeed * 0.01f, 1e-6f);
            Adopt(Target, MinDist);
        }
        FreeChanged?.Invoke();
    }
    float _fYaw, _fPitch;

    /// <summary>Hand the camera back to the orbit controls from wherever a shot left it, without a jump.</summary>
    public void Adopt(Func<D3> target, float minDist)
    {
        Target = target; MinDist = minDist;
        var t = target();
        var camSys = Frame.Sys(Cam.GlobalPosition);
        var dd = camSys - t;
        float dist = Mathf.Max((float)dd.Length(), minDist * 1.2f);
        var dir = (Vector3)dd.F.Normalized();
        Yaw = _yawT = Mathf.Atan2(dir.X, dir.Z);
        Pitch = _pitchT = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(dir.Y, -1, 1)), -1.45f, 1.45f);
        LogDist = _logT = Mathf.Log(dist);
        _smoothTarget = t; FlyT = 1f;
        Cam.Fov = 50f;
    }

    float _yawT, _pitchT, _logT;
    public float PitchT { set => _pitchT = value; }
    public float YawT { set => _yawT = value; }
    D3 _smoothTarget;
    D3 _flyFrom;
    float _flyLogFrom, _flyLogTo, _flyDur;
    double _flySpan;
    bool _dragging;
    Vector2 _lastMouse;

    public float Dist => Mathf.Exp(LogDist);
    /// <summary>Radius of the sky shell distant things are projected onto (inside the far plane).</summary>
    public float SkyK => Cam.Far * 0.5f;
    /// <summary>The point the camera orbits, in system coordinates.</summary>
    public D3 Focus => _smoothTarget;

    public override void _Ready()
    {
        Cam = new Camera3D { Fov = 50f, Near = 0.01f, Far = 2e8f, Current = true };
        AddChild(Cam);
        _yawT = Yaw; _pitchT = Pitch; _logT = LogDist;
        _smoothTarget = Target();
    }

    public void SetView(float yaw, float pitch, float dist)
    {
        Yaw = _yawT = yaw; Pitch = _pitchT = pitch; LogDist = _logT = Mathf.Log(dist);
        _smoothTarget = Target();
        FlyT = 1f;
    }

    /// <summary>
    /// Travel straight to a new target: no climbing out and dropping back in. The view distance changes one way only,
    /// from where it is to where it ends; the gap closes on a log scale, so a hop between moons and a jump across
    /// light-years both arrive the same way, decelerating smoothly onto the target. Short: about a second, two at most.
    /// A duration given explicitly (the opening shot) is kept.
    /// </summary>
    public void FlyTo(Func<D3> target, float endDist, float minDist, float duration = -1f)
    {
        if (Free) { Free = false; FreeChanged?.Invoke(); }
        _flyFrom = _smoothTarget;
        Target = target;
        MinDist = minDist;
        _flyLogFrom = LogDist;
        _flyLogTo = Mathf.Log(Mathf.Max(endDist, minDist * 1.2f));
        _flySpan = (_flyFrom - target()).Length();
        double scale = Math.Max(Math.Min(Dist, Math.Exp(_flyLogTo)), 1e-9);
        _flyDur = duration > 0 ? duration
            : Mathf.Clamp(0.8f + 0.12f * (float)Math.Log(1 + _flySpan / scale) + 0.1f * Mathf.Abs(_flyLogTo - _flyLogFrom), 0.8f, 2.2f);
        FlyT = 0f;
    }

    /// <summary>Instantly shift everything by an offset (used when the world origin is rebased onto a new star).</summary>
    public void Rebase(D3 delta)
    {
        _smoothTarget += delta;
        _flyFrom += delta;
        _freePos += delta;
        Frame.Origin += delta;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!UserControl) return;
        if (e is InputEventMouseButton mb)
        {
            if (Free)
            {
                if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed) _freeSpeed *= 1.35f;
                else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed) _freeSpeed /= 1.35f;
                _freeSpeed = Mathf.Clamp(_freeSpeed, 1e-6f, 1e13f);
            }
            // true scale spans thirty orders of magnitude from a hull to the galaxy: each notch is a bigger step than it was
            else if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed) _logT -= 0.24f;
            else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed) _logT += 0.24f;
            if (mb.ButtonIndex == MouseButton.Right || mb.ButtonIndex == MouseButton.Middle)
            {
                _dragging = mb.Pressed;
                _lastMouse = mb.Position;
            }
        }
        else if (e is InputEventMouseMotion mm && _dragging)
        {
            var d = mm.Position - _lastMouse;
            _lastMouse = mm.Position;
            if (Free)
            {
                _fYaw -= d.X * 0.004f;
                _fPitch = Mathf.Clamp(_fPitch - d.Y * 0.004f, -1.55f, 1.55f);
            }
            else
            {
                _yawT -= d.X * 0.005f;
                _pitchT = Mathf.Clamp(_pitchT - d.Y * 0.005f, -1.5f, 1.5f);
            }
        }
    }

    public override void _Process(double delta) { }

    /// <summary>Move the camera for this frame and re-centre the view's origin on what it looks at.</summary>
    public void Step(float dt)
    {
        if (Free) { StepFree(dt); return; }
        if (UserControl)
        {
            float k = 1.2f * dt;
            if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) _yawT += k;
            if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) _yawT -= k;
            if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) _pitchT = Mathf.Clamp(_pitchT - k * 0.7f, -1.5f, 1.5f);
            if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) _pitchT = Mathf.Clamp(_pitchT + k * 0.7f, -1.5f, 1.5f);
            if (Input.IsKeyPressed(Key.Q) || Input.IsKeyPressed(Key.Pageup)) _logT -= 2.5f * dt;
            if (Input.IsKeyPressed(Key.E) || Input.IsKeyPressed(Key.Pagedown)) _logT += 2.5f * dt;
        }
        if (AutoOrbit) _yawT += dt * 0.02f;
        _logT = Mathf.Clamp(_logT, Mathf.Log(MinDist), Mathf.Log(Mathf.Max(MaxDist, MinDist * 2f)));

        var tgt = Target();
        if (FlyT < 1f)
        {
            FlyT = Mathf.Min(1f, FlyT + dt / _flyDur);
            float u = FlyT * FlyT * (3f - 2f * FlyT);
            // the gap left shrinks geometrically from the whole span to a sliver of the final view distance, so the
            // arrival is always seen decelerating at the scale of what we arrive at (kept in doubles: light-years)
            double eps = Math.Min(_flySpan, Math.Exp(_flyLogTo) * 0.01);
            double left = _flySpan <= eps * 1.001 || _flySpan <= 0 ? 1 - u
                : (_flySpan * Math.Pow(eps / _flySpan, u) - eps) / (_flySpan - eps);
            if (FlyT >= 1f) left = 0;
            _smoothTarget = tgt + (_flyFrom - tgt) * Math.Clamp(left, 0, 1);
            LogDist = Mathf.Lerp(_flyLogFrom, _flyLogTo, u);
            if (FlyT >= 1f) _logT = LogDist;
        }
        else
        {
            _smoothTarget = tgt;
            LogDist = Mathf.Lerp(LogDist, _logT, 1f - Mathf.Exp(-dt * 8f));
        }
        LogDist = Mathf.Max(LogDist, Mathf.Log(MinDist));
        Yaw = Mathf.Lerp(Yaw, _yawT, 1f - Mathf.Exp(-dt * 10f));
        Pitch = Mathf.Lerp(Pitch, _pitchT, 1f - Mathf.Exp(-dt * 10f));

        float dist = Dist;
        var dir = new Vector3(Mathf.Cos(Pitch) * Mathf.Sin(Yaw), -Mathf.Sin(Pitch), Mathf.Cos(Pitch) * Mathf.Cos(Yaw));
        if (Shot is { } sh)
        {
            // a shot is framed in view space about last frame's origin: re-centre on what it looks at, so the subject
            // is precise however far it is from where the camera was before (the shot's next frame uses the new origin)
            var posSys = Frame.Sys(sh.Pos);
            Frame.Origin = Frame.Sys(sh.Look);
            var camV = Frame.View(posSys);
            var f = -camV;
            float d = Mathf.Max(f.Length(), 1e-6f);
            Cam.GlobalPosition = camV;
            var up = Mathf.Abs(f.Normalized().Dot(sh.Up.Normalized())) > 0.98f ? Vector3.Forward : sh.Up;
            Cam.GlobalBasis = Basis.LookingAt(f / d, up);
            Cam.Fov = sh.Fov;
            Cam.Near = Mathf.Max(d * 0.0003f, 1e-10f);     // shots skim planets' horizons: keep the near plane close
            Cam.Far = Cam.Near * DepthRatio;
            Frame.SetDepth(Cam.GlobalPosition, Cam.Far);
            return;
        }
        Frame.Origin = _smoothTarget;
        Cam.GlobalPosition = dir * dist;
        // basis from the local offset: global LookAt loses precision far from the origin
        Cam.GlobalBasis = Basis.LookingAt(-dir, Mathf.Abs(Pitch) > 1.49f ? Vector3.Forward : Vector3.Up);
        Planes(dist);
    }

    /// <summary>Depth range: the near plane follows the distance to what we look at; the far plane is a fixed multiple of it.</summary>
    void Planes(float dist)
    {
        Cam.Near = Mathf.Max(dist * NearK, 1e-10f);
        Cam.Far = FarOverride > 0 ? FarOverride : Cam.Near * DepthRatio;
        Frame.SetDepth(Cam.GlobalPosition, Cam.Far);
    }
    /// <summary>Far plane over near plane. The depth buffer holds about 1e7 before everything vanishes; scaled space
    /// (Frame.Squash) brings whatever lies further inside it.</summary>
    const float DepthRatio = 3e6f;

    void StepFree(float dt)
    {
        Shot = null;
        var basis = new Basis(Vector3.Up, _fYaw) * new Basis(Vector3.Right, _fPitch);
        var fwd = -basis.Z; var right = basis.X;
        var move = Vector3.Zero;
        if (UserControl)
        {
            if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) move += fwd;
            if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) move -= fwd;
            if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) move += right;
            if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) move -= right;
            if (Input.IsKeyPressed(Key.E) || Input.IsKeyPressed(Key.Pageup)) move += Vector3.Up;
            if (Input.IsKeyPressed(Key.Q) || Input.IsKeyPressed(Key.Pagedown)) move -= Vector3.Up;
        }
        float boost = Input.IsKeyPressed(Key.Shift) ? 10f : Input.IsKeyPressed(Key.Ctrl) ? 0.1f : 1f;
        if (move.LengthSquared() > 0) _freePos += move.Normalized() * (_freeSpeed * boost * dt);
        Frame.Origin = _freePos;
        _smoothTarget = _freePos;
        Cam.GlobalPosition = Vector3.Zero;
        Cam.GlobalBasis = basis;
        Cam.Fov = 60f;
        Planes(_freeSpeed * 0.5f);
    }
}
