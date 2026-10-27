using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// When history turns, the camera stops being a tool and becomes a cinematographer. A moment (first seed ship,
/// first of a kind of megastructure, a new era, first contact, a battle, an act of the voice) gets a composed
/// shot chosen from what is actually around the subject: the star behind it, a world beside it, the horizon of
/// a planet below it, the ship's own wake. Time slows to a walk while it plays; any key skips.
/// </summary>
public partial class Cinematics : Node
{
    public bool Enabled = true;
    public bool Playing => _cur != null;
    public bool Busy => _cur != null || _queue.Count > 0;

    public class Moment
    {
        public string Title, Text, Focus, Kind;
        public Colony Colony;
        public int Priority;
        public string Spectacle;          // a spectacle to start when the shot begins (arrival, battle...)
    }

    Main _m;
    readonly List<Moment> _queue = new();
    Moment _cur;
    enum Phase { Travel, Shoot, Out }
    Phase _phase;
    float _t, _dur, _cool;
    int _speedBefore;
    Func<Vector3> _subject;
    float _size;
    Func<float, (Vector3, Vector3, Vector3, float)> _shot;
    string _lastKind = "";
    Random _r = new();
    public string LastShot = "";
    static readonly bool _trace = OS.GetCmdlineUserArgs().Contains("--trace");
    int _frames, _slow; float _sumDt, _maxDt, _maxJump, _sumJump; D3 _lastRel;

    public void Setup(Main m) { _m = m; }

    /// <summary>Ask for a moment to be filmed. Lower-priority requests are dropped if the queue is busy.</summary>
    public void Request(Moment mo)
    {
        if (_m.Warping) return;
        if (!Enabled || _m.Rig.FreeFlight || mo.Focus is null or "none" or "galaxy")        // flying free: the title, but the camera stays yours
        {
            if (mo.Title != null) _m.Hud.ShowMilestone(mo.Title, mo.Text);
            return;
        }
        if (_queue.Count >= 2) { var low = _queue.OrderBy(q => q.Priority).First(); if (low.Priority >= mo.Priority) return; _queue.Remove(low); }
        _queue.Add(mo);
        _queue.Sort((a, b) => b.Priority.CompareTo(a.Priority));
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_cur == null) return;
        bool skip = e is InputEventKey { Pressed: true, Echo: false } || e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left or MouseButton.Right };
        if (!skip) return;
        GetViewport().SetInputAsHandled();
        End();
    }

    /// <summary>
    /// Called by Main each frame after the clock has advanced and before the camera moves: at true scale a world
    /// covers thousands of kilometres a frame at a day a second, so a shot framed after the camera has moved is a
    /// frame behind its subject.
    /// </summary>
    public void Step(float dt)
    {
        _cool -= dt;
        if (_cur == null)
        {
            if (_queue.Count == 0 || _cool > 0 || _m.Aboard != null) return;
            _cur = _queue[0]; _queue.RemoveAt(0);
            _t = 0;
            _speedBefore = _m.SpeedIdx;
            _m.SpeedIdx = 1;                             // a day a second: history at a watchable pace
            _m.Hud.Cinema(true);
            var star = _cur.Colony?.Star;
            if (star != null && star != _m.ViewStar && _cur.Focus != "galaxy") { _m.TravelTo(star); _phase = Phase.Travel; }
            else Begin();
            return;
        }
        _t += dt;
        if (_phase == Phase.Travel)
        {
            if (_m.Traveling && _t < 9f) return;
            Begin();
            return;
        }
        float u = Mathf.Clamp(_t / _dur, 0, 1);
        try
        {
            var (pos, look, up, fov) = _shot(u);
            _m.Rig.Shot = (pos, look, up, fov);
            if (_trace)
            {
                // smoothness: frame times, and how far the camera moves relative to the subject frame to frame
                _frames++; _sumDt += dt; _maxDt = Mathf.Max(_maxDt, dt); if (dt > 0.02f) _slow++;
                var rel = Frame.Sys(pos) - Frame.Sys(look);
                if (_frames > 1) { float j = (float)(rel - _lastRel).Length() / Mathf.Max(_size, 1e-6f); _maxJump = Mathf.Max(_maxJump, j); _sumJump += j; }
                _lastRel = rel;
            }
        }
        catch (Exception) { End(); return; }     // the subject went away (a ship arrived, a world was shattered)
        if (u >= 1) End();
    }

    void Begin()
    {
        _phase = Phase.Shoot;
        _t = 0;
        if (_cur.Spectacle != null) _m.SysView.Spectacle.Play(_cur.Spectacle, _cur.Colony, _cur.Colony?.Seat);
        if (!Resolve(_cur, out _subject, out _size, out bool moving)) { End(); return; }
        if (_cur.Title != null) _m.Hud.ShowMilestone(_cur.Title, _cur.Text);
        Compose(moving);
        if (OS.GetCmdlineUserArgs().Contains("--trace")) GD.Print($"cine {_cur.Focus} shot={LastShot} subject={_subject()} size={_size} cam={_shot(0).Item1}");
    }

    void End()
    {
        if (_cur == null) return;
        if (_trace) GD.Print($"cine end t={_t:0.0} phase={_phase} shot={LastShot} frames={_frames} avgDt={_sumDt / Mathf.Max(_frames, 1) * 1000:0.0}ms maxDt={_maxDt * 1000:0.0}ms slow(>20ms)={_slow} jump(max/avg, subject sizes per frame)={_maxJump:0.000}/{_sumJump / Mathf.Max(_frames - 1, 1):0.000}");
        _frames = 0; _slow = 0; _sumDt = _maxDt = _maxJump = _sumJump = 0;
        _m.Rig.Shot = null;
        if (_phase == Phase.Shoot && _subject != null)
        {
            var sub = _subject; float size = _size;
            Vector3 last;
            try { last = sub(); } catch (Exception) { last = Vector3.Zero; }
            // subjects are in view space; the rig wants system coordinates
            _m.Rig.Adopt(() => { try { return Frame.Sys(sub()); } catch (Exception) { return Frame.Sys(last); } }, size * 1.1f);
        }
        _m.SpeedIdx = _speedBefore;
        _m.Hud.Cinema(false);
        _m.DirectorHold(25f);
        _cur = null;
        _cool = 4f;
    }

    // ---------- what is being filmed ----------
    // Positions are worked out fresh, against this frame's origin. BodyView.TrueView and SystemView.TrueStar are only
    // refreshed after the camera has moved, a frame late, and at true scale and a day a second a world moves several
    // of its own radii every frame: a shot hung on a stale position shakes.
    Vector3 StarV() => Frame.View(_m.SysView.Offset);
    Vector3 BodyV(Body b) => Frame.View(_m.SysView.Offset + b.WorldPosD(_m.VisYear));

    bool Resolve(Moment mo, out Func<Vector3> subject, out float size, out bool moving)
    {
        var sv = _m.SysView;
        moving = false;
        subject = null; size = 1;
        string f = mo.Focus;
        if (f == "ship")
        {
            var ship = sv.Spectacle.Followed;
            if (ship != null && GodotObject.IsInstanceValid(ship))
            {
                subject = () => GodotObject.IsInstanceValid(ship) ? Frame.View(sv.Offset + ship.Position) : throw new InvalidOperationException();      // star-relative: current this frame
                size = Mathf.Max(ship.Scale.X, 0.05f) * 0.6f;
                moving = true;
                return true;
            }
            f = "seat";
        }
        if (f.StartsWith("mega:"))
        {
            string name = f[5..];
            var mega = sv.Works.Mega;
            bool Match(string n) => n == name || name.EndsWith("Shipyard") && n.EndsWith("Shipyard");
            var hit = mega.Active.FindIndex(a => Match(a.Name));
            if (hit < 0) { f = "seat"; }
            else
            {
                size = mega.Active[hit].Size;
                subject = () => { var i = mega.Active.FindIndex(a => Match(a.Name)); return i < 0 ? throw new InvalidOperationException() : Frame.View(mega.Active[i].Pos); };
                return true;
            }
        }
        if (f == "starbase")
        {
            var sb = sv.Works.Mega.Active.FindIndex(a => a.Name is "Outpost" or "Starport" or "Starhold" or "Star Fortress" or "Citadel");
            if (sb >= 0) return Resolve(new Moment { Focus = "mega:" + sv.Works.Mega.Active[sb].Name, Colony = mo.Colony }, out subject, out size, out moving);
            f = "seat";
        }
        if (f.StartsWith("body:")) { var b = sv.Sys.Find(f[5..]); if (b != null) { var bb = b; subject = () => BodyV(bb); size = b.Radius; return true; } f = "seat"; }
        if (f == "seat" && mo.Colony?.Seat != null && mo.Colony.Star == _m.ViewStar)
        {
            var seat = mo.Colony.Seat;
            subject = () => BodyV(seat);
            size = seat.Radius;
            return true;
        }
        if (f is "star" or "seat")
        {
            subject = StarV;
            size = sv.Sys.StarRadius;
            return true;
        }
        return false;
    }

    // ---------- composing a shot from what's around ----------
    static float Ease(float u) => u * u * (3 - 2 * u);

    void Compose(bool moving)
    {
        var sv = _m.SysView;
        var P0 = _subject();
        float S = _size;
        var star = StarV();
        bool isStar = (P0 - star).Length() < sv.Sys.StarRadius * 0.5f;
        var toStar = isStar ? Vector3.Forward : (star - P0).Normalized();
        // the most imposing neighbour: big and close, as seen from the subject
        BodyView nb = null; float best = 0;
        foreach (var bv in sv.Bodies)
        {
            if (bv.B.Kind == BodyKind.Belt || bv.B.Mined > 0.9f) continue;
            float d = (BodyV(bv.B) - P0).Length();
            if (d < S * 0.5f) continue;                           // the subject itself
            float score = bv.B.Radius / Mathf.Max(d, 1e-3f);
            if (score > best) { best = score; nb = bv; }
        }
        var options = new List<string>();
        if (moving) options.Add("chase");
        if (!isStar) options.Add("backlit");
        options.Add("arc");
        if (nb != null && best > 0.003f && !isStar) options.Add("reveal");
        float nd = nb == null ? 0 : (BodyV(nb.B) - P0).Length();
        if (nb != null && best > 0.02f && nd > nb.B.Radius * 1.6f && nd < S * 25f && !isStar) options.Add("rise");   // only when the subject will read against the horizon
        if (moving) options.Add("chase");             // ships are usually best followed
        var pick = options.Where(o => o != _lastKind).ToList();
        if (pick.Count == 0) pick = options;
        string kind = pick[_r.Next(pick.Count)];
        if (Array.Find(OS.GetCmdlineUserArgs(), a => a.StartsWith("--cineshot=")) is string cs && options.Contains(cs[11..])) kind = cs[11..];   // test
        _lastKind = kind;
        LastShot = kind;

        float side = _r.Next(2) == 0 ? -1 : 1;
        float lift = (float)(_r.NextDouble() * 0.5 - 0.15);
        var up = Vector3.Up;
        var across = toStar.Cross(up).Normalized();
        if (across.LengthSquared() < 0.01f) across = Vector3.Right;
        _dur = 11f;

        switch (kind)
        {
            case "backlit":
            {
                // the star burns behind the subject: rim light and glare, a slow push in
                var away = -toStar;
                _shot = u =>
                {
                    var P = _subject();
                    float e = Ease(u);
                    float d = S * Mathf.Lerp(3.8f, 2.3f, e);
                    var off = (away * 0.85f + across * side * 0.35f + up * (lift + 0.1f)).Normalized() * d;
                    return (P + off, P + toStar * S * 0.6f, up, Mathf.Lerp(42f, 36f, e));
                };
                break;
            }
            case "reveal":
            {
                // start tight on the lit face, pull back until the neighbour world fills the background
                var N = nb;
                _shot = u =>
                {
                    var P = _subject();
                    var NP = BodyV(N.B);
                    var behind = (P - NP).Normalized();
                    var lit = (toStar * 0.6f + behind * 0.8f + across * side * 0.3f + up * lift).Normalized();
                    float e = Ease(u);
                    float far = Mathf.Clamp((P - NP).Length() * 0.4f, S * 5f, S * 9f);
                    float d = Mathf.Lerp(S * 2.6f, far, e * e);
                    var look = P.Lerp(NP, 0.35f * e);
                    return (P + lit * d, look, up, Mathf.Lerp(40f, 52f, e));
                };
                break;
            }
            case "rise":
            {
                // from just above a nearby world's horizon, looking across it at the subject; the camera climbs
                var N = nb;
                float R = N.B.Radius;
                _shot = u =>
                {
                    var P = _subject();
                    var NP = BodyV(N.B);
                    var toP = (P - NP).Normalized();
                    var perp = toP.Cross(up).Normalized();
                    if (perp.LengthSquared() < 0.01f) perp = Vector3.Right;
                    float e = Ease(u);
                    // stand on the world's limb so the line of sight to the subject skims the horizon,
                    // tilted down a little so the curve of the world fills the bottom of the frame
                    var vertical = (perp.Cross(toP)).Normalized();
                    var dir = (vertical * (lift > 0.1f ? 1f : -1f) + toP * 0.12f + perp * side * 0.1f).Normalized();
                    float h = Mathf.Lerp(1.02f, 1.12f, e);
                    var cam = NP + dir * R * h;
                    var upv = (cam - NP).Normalized();
                    var look = P - upv * (P - cam).Length() * Mathf.Lerp(0.16f, 0.08f, e);
                    return (cam, look, upv, Mathf.Lerp(38f, 44f, e));
                };
                break;
            }
            case "chase":
            {
                // ride behind the ship, then let it pull away toward the stars
                _dur = 10f;
                // remembered in system coordinates: view space is re-centred on the subject every frame
                var prev = Frame.Sys(_subject());
                Vector3 vel = Vector3.Forward;
                D3 camAt = default; bool init = false;
                float lastU = 0;
                _shot = u =>
                {
                    var P = _subject();
                    var Ps = Frame.Sys(P);
                    var dv = (Ps - prev).F; prev = Ps;
                    float dt = Mathf.Max((u - lastU) * _dur, 0f); lastU = u;
                    if (dv.LengthSquared() > 1e-12f) vel = vel.Lerp(dv.Normalized(), 1f - Mathf.Exp(-dt * 8f)).Normalized();
                    var ideal = Frame.Sys(P - vel * S * 9f + up * S * 3f * (1 + lift) + vel.Cross(up).Normalized() * side * S * 2f);
                    if (!init) { camAt = ideal; init = true; }
                    float rate = u < 0.55f ? 7f : Mathf.Lerp(7f, 0f, (u - 0.55f) / 0.45f);     // stop following: it pulls away
                    camAt = camAt.Lerp(ideal, 1f - Mathf.Exp(-dt * rate));
                    return (Frame.View(camAt), P, up, 46f);
                };
                break;
            }
            default:
            {
                // an arc around the subject, starting on its lit side
                float a0 = Mathf.Atan2(toStar.X, toStar.Z) + side * 0.6f;
                float sweep = side * (0.9f + (float)_r.NextDouble() * 0.8f);
                float dist = isStar ? S * 9f : S * (2.6f + (float)_r.NextDouble() * 1.4f);
                _shot = u =>
                {
                    var P = _subject();
                    float e = Ease(u);
                    float a = a0 + sweep * e;
                    float el = lift + 0.15f + 0.1f * e;
                    var off = new Vector3(Mathf.Sin(a) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(a) * Mathf.Cos(el)) * dist * Mathf.Lerp(1f, 1.25f, e);
                    return (P + off, P, up, 44f);
                };
                break;
            }
        }
    }
}
