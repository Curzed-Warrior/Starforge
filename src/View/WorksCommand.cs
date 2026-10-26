using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// The battle the player is commanding, drawn at true scale with the same hulls, beams and explosions as any other battle
/// (WorksFleets.cs). Each unit is its own wedge, placed by its orders: back from the line by its depth, across it by its
/// lateral offset. Ships fall away over the round as the session's exchange plays out (Prev to Ships), fire is aimed where
/// orders aim it, and each unit reports where it is so the panel and input can find it on screen.
/// </summary>
public partial class Works
{
    /// <summary>Per unit id: where its hulls are (true system position) and roughly how wide (true units), for markers and picking.</summary>
    public readonly Dictionary<int, (D3 At, float Radius)> CmdAnchors = new();
    readonly Dictionary<int, List<int>> _unitHulls = new();
    readonly Dictionary<int, (float Lat, float Dep)> _unitOff = new();
    readonly List<int> _spanFleet = new();
    readonly Dictionary<int, int> _cmdDrawn = new();
    readonly List<(D3 At, float R, float T0, int Seed)> _cmdKills = new();
    bool _cmdOn;

    /// <summary>Where a true system position is drawn this frame, in view space (for projecting markers to the screen).</summary>
    public Vector3 ViewPos(D3 at) => Drawn(at, out _, out _);

    void LayOutCommand(Simulation sim, Colony c, CommandSession s)
    {
        int ca = s.A.Civ.Id, cb = s.B.Civ.Id;
        _fleetsHere.Clear(); _civsHere.Clear(); _civsHere.Add(ca); _civsHere.Add(cb);
        _cmdOn = true; _unitHulls.Clear(); _unitOff.Clear(); CmdAnchors.Clear(); _entries.Clear();
        double p = s.Over ? 1 : s.Progress;
        int total = Math.Max(1, s.A.Units.Count + s.B.Units.Count), cap = Math.Max(1, DrawMax / total);
        double lossA = 0, lossB = 0;
        foreach (var (side, civId) in new[] { (s.A, ca), (s.B, cb) })
        {
            int style = Ships.Style(sim.Civs[civId]);
            foreach (var u in side.Units)
            {
                double shown = u.Prev + (u.Ships - u.Prev) * p;
                double hp = Starforge.Sim.Battles.HullHp(u.Hull);
                if (side == s.A) lossA += Math.Max(0, u.Prev - u.Ships) * hp; else lossB += Math.Max(0, u.Prev - u.Ships) * hp;
                if (u.Gone || shown <= 1e-9) continue;
                int n = Math.Min(Ships.DrawCount(shown, DrawMax), cap);
                _entries.Add(new Entry { Civ = civId, Fleet = u.Id, Key = style * Ships.Classes + u.Hull, N = n, Alive = n, LenKm = Ships.LengthKm(u.Hull) });
            }
        }
        if (_entries.Count == 0) return;
        float maxLen = 0;
        foreach (var e in _entries) maxLen = Math.Max(maxLen, e.LenKm);

        var (anchor, R, seatBody) = Anchor(c);
        float Angle(int civId) => civId * 2.39996f + 0.7f;
        Vector3 Dir(int civId) { float a = Angle(civId); return new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); }
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
        foreach (var side in new[] { s.A, s.B })
            foreach (var u in side.Units)
                _unitOff[u.Id] = ((float)u.Lateral * gap * 0.8f, (float)u.Depth * gap * 0.6f);
        _sideHulls[0].Clear(); _sideHulls[1].Clear(); _hulls.Clear();
        float depthA = Formation(ca, centre - axis * (gap * 0.5f), axis, dcam, false, 0);
        float depthB = Formation(cb, centre + axis * (gap * 0.5f), -axis, dcam, false, 1);
        float trueHalf = maxLen * StarSystem.Km * 0.5f;
        _battleOn = true; _battleSeat = seatBody; _battleOff = centre - anchor;
        _battleR = (gap * 0.5f + Math.Max(depthA, depthB)) * trueHalf / Math.Max(bigHalf, 1e-12f);
        BattleView = (s.PlayerIsA ? -axis : axis).Rotated(Vector3.Up, 1.3f);

        // where each unit is, for markers and picking
        foreach (var (id, idx) in _unitHulls)
        {
            if (idx.Count == 0) continue;
            var first = _hulls[idx[0]].At; Vector3 sum = Vector3.Zero;
            foreach (int i in idx) sum += (_hulls[i].At - first).F;
            var mid = first + sum / idx.Count;
            float rad = 0; foreach (int i in idx) rad = Math.Max(rad, (_hulls[i].At - mid).F.Length() + _hulls[i].Half);
            CmdAnchors[id] = (mid, rad);
        }

        // fire and explosions
        float shareA = lossA + lossB > 0 ? (float)(lossA / (lossA + lossB)) : 0.5f;
        string famA = Starforge.Sim.Battles.BestFamily(sim.Civs[ca]), famB = Starforge.Sim.Battles.BestFamily(sim.Civs[cb]);
        if (_famOverride?.Length == 2) { famA = _famOverride[0]; famB = _famOverride[1]; }
        bool running = !s.Paused || s.Progress > 0;
        ScheduleBursts(null, 0, shareA);
        CommandKills(s, sim);
        if (running && !s.Over)
        {
            Fire(0, famA, sim.Civs[ca].Color);
            Fire(1, famB, sim.Civs[cb].Color);
            int k = 0;
            foreach (var (side, si, fam) in new[] { (s.A, 0, famA), (s.B, 1, famB) })
                foreach (var u in side.Units)
                {
                    if (u.FocusId < 0 || u.Gone || !_unitHulls.TryGetValue(u.Id, out var mine) || !_unitHulls.TryGetValue(u.FocusId, out var theirs)) continue;
                    FireAt(mine, theirs, si + 2 * (++k), fam, sim.Civs[side.Civ.Id].Color, 5);
                }
        }
        _beamLimit = BeamMax;
        Explosions();
        FlushHulls();
    }

    /// <summary>Hulls that fall out of a unit's drawn count blow up where the unit is: a fireball and shock ring each, over KillLife.</summary>
    void CommandKills(CommandSession s, Simulation sim)
    {
        foreach (var side in new[] { s.A, s.B })
            foreach (var u in side.Units)
            {
                int n = _unitHulls.TryGetValue(u.Id, out var l) ? l.Count : 0;
                if (_cmdDrawn.TryGetValue(u.Id, out int was) && n < was && l != null && l.Count > 0)
                    for (int k = 0; k < was - n && _cmdKills.Count < 64; k++)
                    {
                        var h = _hulls[l[(int)(Hash(u.Id, _cmdKills.Count, k) % (uint)l.Count)]];
                        _cmdKills.Add((h.At, h.Half * 1.6f, _fxT, (int)Hash(u.Id, k, (int)(_fxT * 100))));
                    }
                _cmdDrawn[u.Id] = n;
            }
        _cmdKills.RemoveAll(k => (_fxT - k.T0) / KillLife > 1f);
        foreach (var k in _cmdKills) _blasts.Add((k.At, k.R, (_fxT - k.T0) / KillLife, k.Seed, true));
    }
}
