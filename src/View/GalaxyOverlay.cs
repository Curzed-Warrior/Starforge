using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// Galaxy-scale view of the simulation: colony markers, ships crossing between stars, the war layers (fleets and
/// their courses, armies aboard, fronts, occupied and blockaded systems, recent battles), and the territory
/// textures that tint empires and darken every star wrapped in a swarm.
/// </summary>
public partial class GalaxyOverlay : Node3D
{
    Main _main;
    Simulation _sim;
    MultiMeshInstance3D _markers, _shipHeads, _warMarkers;
    ShaderMaterial _markerMat;
    MeshInstance3D _lines, _warLines, _frontLines;
    ImmediateMesh _im, _warIm, _frontIm;
    ShaderMaterial _lineMat, _frontMat;
    public ImageTexture OwnTex, CapTex;
    Image _own, _cap;
    MeshInstance3D _borders;
    ShaderMaterial _borderMat;
    public const int TexSize = 256;
    public const float TexExtent = 1150f;   // galaxy units from centre to texture edge
    float _texT = 99f;
    public bool ShowBorders = true;
    public float TranscendT = -1f;   // seconds since Transcendence; < 0 never
    const int MaxMarkers = 3000, MaxHeads = 1000, MaxWarMarkers = 512;

    public void Setup(Main main, Simulation sim)
    {
        _main = main; _sim = sim;
        _markerMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/marker.gdshader"), RenderPriority = 7 };
        _markers = Make(MaxMarkers);
        _shipHeads = Make(MaxHeads);
        _warMarkers = Make(MaxWarMarkers);     // the war layers: their own budget, so colonies never crowd them out
        AddChild(_markers);
        AddChild(_shipHeads);
        AddChild(_warMarkers);

        _im = new ImmediateMesh();
        _lineMat = new ShaderMaterial
        {
            RenderPriority = 6,
            Shader = new Shader
            {
                Code = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled, skip_vertex_transform;
uniform float sky_k = 1e6;
uniform float alpha = 1.0;
varying vec4 v_col;
void vertex(){
	vec3 w = VERTEX;
	vec3 cw = CAMERA_POSITION_WORLD;
	float d = length(w - cw);
	if (d > sky_k) w = cw + (w - cw) * (sky_k / d);
	POSITION = PROJECTION_MATRIX * VIEW_MATRIX * vec4(w, 1.0);
	v_col = COLOR;
}
void fragment(){ ALBEDO = v_col.rgb * v_col.a * alpha; }"
            }
        };
        _lines = new MeshInstance3D { Mesh = _im, MaterialOverride = _lineMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 16384 };
        AddChild(_lines);
        // the war layers' lines (fleet courses, fronts): rebuilt with the markers, not every frame
        _warIm = new ImmediateMesh();
        _warLines = new MeshInstance3D { Mesh = _warIm, MaterialOverride = _lineMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 16384 };
        AddChild(_warLines);
        // fronts: mixed over the map rather than added, so their red (and dark casing) reads over bright territory
        _frontMat = new ShaderMaterial
        {
            RenderPriority = 6,
            Shader = new Shader
            {
                Code = @"shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled, skip_vertex_transform;
uniform float sky_k = 1e6;
uniform float alpha = 1.0;
varying vec4 v_col;
void vertex(){
	vec3 w = VERTEX;
	vec3 cw = CAMERA_POSITION_WORLD;
	float d = length(w - cw);
	if (d > sky_k) w = cw + (w - cw) * (sky_k / d);
	POSITION = PROJECTION_MATRIX * VIEW_MATRIX * vec4(w, 1.0);
	v_col = COLOR;
}
void fragment(){ ALBEDO = v_col.rgb; ALPHA = v_col.a * alpha; }"
            }
        };
        _frontIm = new ImmediateMesh();
        _frontLines = new MeshInstance3D { Mesh = _frontIm, MaterialOverride = _frontMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 16384 };
        AddChild(_frontLines);

        _own = Image.CreateEmpty(TexSize, TexSize, false, Image.Format.Rgba8);
        _cap = Image.CreateEmpty(TexSize, TexSize, false, Image.Format.Rgba8);
        OwnTex = ImageTexture.CreateFromImage(_own);
        CapTex = ImageTexture.CreateFromImage(_cap);

        _borderMat = new ShaderMaterial
        {
            RenderPriority = 5,
            Shader = new Shader
            {
                Code = @"shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
uniform sampler2D own : filter_linear, repeat_disable;
uniform float strength = 0.0;
uniform vec2 sol = vec2(0.5);
uniform float wave = -1.0;      // Transcendence: a ring of light running out from Sol, in uv
uniform float after = 0.0;      // and the shimmer it leaves behind
uniform float time = 0.0;
void fragment(){
	vec4 o = texture(own, UV);
	// glowing edges where one empire meets another or empty space
	vec2 px = vec2(1.0 / 256.0);
	vec4 n = texture(own, UV + vec2(px.x, 0)) + texture(own, UV - vec2(px.x, 0)) + texture(own, UV + vec2(0, px.y)) + texture(own, UV - vec2(0, px.y));
	float edge = clamp(length(o.rgb * 4.0 - n.rgb) * 1.5, 0.0, 1.0) * max(o.a, n.a * 0.25);
	float d = length(UV - sol);
	float ring = wave > 0.0 ? exp(-(d - wave) * (d - wave) * 1600.0) + 0.35 * step(d, wave) * exp(-(wave - d) * 12.0) : 0.0;
	float shim = after * (0.5 + 0.5 * sin(d * 300.0 - time * 1.5));
	vec3 light = vec3(1.0, 0.93, 0.78) * o.a * (ring * 2.5 + shim * 0.25);
	ALBEDO = o.rgb * (o.a * 0.10 + edge * 0.6) * strength + light;
}"
            }
        };
        _borderMat.SetShaderParameter("own", OwnTex);
        _borders = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(TexExtent * 2, TexExtent * 2) }, MaterialOverride = _borderMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_borders);
    }

    MultiMeshInstance3D Make(int n)
    {
        var quad = new QuadMesh { Size = new Vector2(1, 1), Material = _markerMat };
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = quad, InstanceCount = n, VisibleInstanceCount = 0 };
        return new MultiMeshInstance3D { Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 16384, CustomAabb = new Aabb(-Vector3.One * 1e10f, Vector3.One * 2e10f) };
    }

    public void Tick(float dt)
    {
        var cam = _main.Cam;
        float skyK = cam.Far * 0.5f;
        float zoomGal = cam.GlobalPosition.DistanceTo(Frame.Star) / Main.G;
        // overlays fade in as the view widens beyond the home system
        float vis = Mathf.Clamp((zoomGal - 0.15f) / 2f, 0, 1);
        _markerMat.SetShaderParameter("sky_k", skyK);
        _markerMat.SetShaderParameter("fade", vis);
        _lineMat.SetShaderParameter("sky_k", skyK);
        _lineMat.SetShaderParameter("alpha", vis);
        _frontMat.SetShaderParameter("sky_k", skyK);
        _frontMat.SetShaderParameter("alpha", vis);

        // galaxy-plane borders, positioned in world space
        _borders.Position = _main.GalWorld(Vector3.Zero);
        _borders.Scale = Vector3.One * Main.G;
        _borderMat.SetShaderParameter("strength", ShowBorders ? Mathf.Clamp((zoomGal - 60f) / 300f, 0, 1) : 0);
        if (TranscendT >= 0f)
        {
            TranscendT += dt;
            var sol = _sim.HomeColony.Star.Pos;
            _borderMat.SetShaderParameter("sol", new Vector2((sol.X + TexExtent) / (TexExtent * 2), (sol.Z + TexExtent) / (TexExtent * 2)));
            _borderMat.SetShaderParameter("wave", TranscendT < 14f ? TranscendT * 0.07f : -1f);
            _borderMat.SetShaderParameter("after", Mathf.Clamp(TranscendT / 10f, 0, 1));
            _borderMat.SetShaderParameter("time", TranscendT);
        }

        UpdateMarkers(zoomGal);
        UpdateShips(zoomGal);
        _texT += dt;
        if (_texT > 1.0f) { _texT = 0; UpdateTerritory(); }
    }

    float[] _markerBuf = new float[MaxMarkers * 20];
    float _markerT = 99f;
    StarInfo _markerView;

    /// <summary>All colony markers in one buffer upload (12 transform + 4 colour + 4 custom floats each).</summary>
    void UpdateMarkers(float zoomGal)
    {
        _markerT += (float)GetProcessDeltaTime();
        if (_markerT < 0.3f && _markerView == _main.ViewStar) return;
        _markerT = 0; _markerView = _main.ViewStar;
        var mm = _markers.Multimesh;
        int k = 0;
        var buf = _markerBuf;
        foreach (var c in _sim.Colonies)
        {
            if (k >= MaxMarkers) break;
            var wp = _main.StarWorld(c.Star);
            float size = c.Home ? 11f : c.Civ.Main ? 7f : 5.5f;
            var col = c.Civ.Exalted ? c.Civ.Color.Lerp(Gold, 0.6f) : c.Civ.Color;
            if (c.Civ.Exalted) size *= 1.3f;
            int o = k * 20;
            buf[o] = 1; buf[o + 1] = 0; buf[o + 2] = 0; buf[o + 3] = wp.X;
            buf[o + 4] = 0; buf[o + 5] = 1; buf[o + 6] = 0; buf[o + 7] = wp.Y;
            buf[o + 8] = 0; buf[o + 9] = 0; buf[o + 10] = 1; buf[o + 11] = wp.Z;
            buf[o + 12] = col.R; buf[o + 13] = col.G; buf[o + 14] = col.B; buf[o + 15] = c.Complete ? 0.9f : 0.7f;
            buf[o + 16] = size; buf[o + 17] = c.Complete ? 1f : 0f; buf[o + 18] = c.Civ.Main ? 1f : 0f; buf[o + 19] = 0;
            k++;
        }
        // the exotic layer's places (once someone has found them), and stars the voice has lit or put out
        foreach (var st in _sim.Gal.Stars)
        {
            if (k >= MaxMarkers) break;
            Color col; float size;
            if (st.Special != null && (_sim.ShowHiddenToVoice || _sim.Revealed.Contains(st.Special)))
            {
                col = st.Special switch { "vault" => new Color(0.75f, 0.45f, 1f), "gate" => Gold, _ => st.Dead ? new Color(0.6f, 0.15f, 0.1f) : new Color(0.5f, 0.95f, 1f) };
                size = 10f;
            }
            else if (st.Kindled && !st.Dead) { col = new Color(1f, 0.95f, 0.8f); size = 6f; }
            else if (st.Dead && st.Special == null && _sim.Edited.Contains(st)) { col = new Color(0.45f, 0.12f, 0.08f); size = 5f; }
            else continue;
            var wp = _main.StarWorld(st);
            int o = k * 20;
            buf[o] = 1; buf[o + 1] = 0; buf[o + 2] = 0; buf[o + 3] = wp.X;
            buf[o + 4] = 0; buf[o + 5] = 1; buf[o + 6] = 0; buf[o + 7] = wp.Y;
            buf[o + 8] = 0; buf[o + 9] = 0; buf[o + 10] = 1; buf[o + 11] = wp.Z;
            buf[o + 12] = col.R; buf[o + 13] = col.G; buf[o + 14] = col.B; buf[o + 15] = 0.95f;
            buf[o + 16] = size; buf[o + 17] = 1f; buf[o + 18] = 1f; buf[o + 19] = 0;
            k++;
        }
        mm.Buffer = buf;
        mm.VisibleInstanceCount = k;
        UpdateWarLayers();
    }
    static readonly Color Gold = new(1f, 0.82f, 0.38f);

    /// <summary>One marker into a buffer: position, colour, alpha, size in px, style, main-civ flag, style-specific w.</summary>
    static void Put(float[] buf, int k, Vector3 wp, Color col, float alpha, float size, float style, float main, float w)
    {
        int o = k * 20;
        buf[o] = 1; buf[o + 1] = 0; buf[o + 2] = 0; buf[o + 3] = wp.X;
        buf[o + 4] = 0; buf[o + 5] = 1; buf[o + 6] = 0; buf[o + 7] = wp.Y;
        buf[o + 8] = 0; buf[o + 9] = 0; buf[o + 10] = 1; buf[o + 11] = wp.Z;
        buf[o + 12] = col.R; buf[o + 13] = col.G; buf[o + 14] = col.B; buf[o + 15] = alpha;
        buf[o + 16] = size; buf[o + 17] = style; buf[o + 18] = main; buf[o + 19] = w;
    }

    const float CeasefireDash = 4f;
    readonly float[] _warBuf = new float[MaxWarMarkers * 20];
    readonly Dictionary<int, Fleet> _fleetById = new();
    readonly HashSet<int> _carrying = new();

    /// <summary>A war's fronts as last computed, and the war state they were computed from.</summary>
    sealed class FrontCache
    {
        public List<(int From, int To)> Fronts;
        public float Score;
        public int Battles;
        public bool Open, Ceasefire;
        public long Occupation;
        public double Year;
    }
    readonly Dictionary<int, FrontCache> _fronts = new();

    /// <summary>
    /// The war layers, rebuilt with the markers (0.3 s) into their own marker buffer (MaxWarMarkers, so colonies never
    /// starve them) and their own line mesh. Markers go in by priority, so the lowest are dropped first when the budget is
    /// full: battles younger than interface.battleMapYears, the player's fleets (and armies aboard), occupied and
    /// blockaded systems, other fleets. Moving fleets get a course line. The fronts of every open war (the player's bright
    /// and thick, others faint) and of every standing ceasefire (dim and dashed) are drawn as lines.
    /// </summary>
    void UpdateWarLayers()
    {
        _warIm.ClearSurfaces();
        _frontIm.ClearSurfaces();
        var wm = _warMarkers.Multimesh;
        if (Works.HiddenParts.Contains("warmap")) { wm.VisibleInstanceCount = 0; return; }
        var buf = _warBuf;
        int k = 0;
        double year = _sim.Year;
        int me = _sim.Humanity.Id;
        bool any = false;

        double showYears = WarData.D("interface", "battleMapYears");
        for (int i = _sim.Battles.Count - 1; i >= 0 && k < MaxWarMarkers; i--)
        {
            var b = _sim.Battles[i];
            double age = year - b.Year;
            if (age > showYears) break;
            if (age < 0) continue;
            Put(buf, k++, _main.StarWorld(_sim.Gal.Stars[b.Star]), new Color(1f, 0.85f, 0.6f), 1f, 26f, 7f, 0f, (float)(1 - age / showYears));
        }

        _fleetById.Clear();
        _carrying.Clear();
        foreach (var f in _sim.Fleets) _fleetById[f.Id] = f;
        foreach (var a in _sim.Armies)
            if (a.Fleet >= 0 && _fleetById.TryGetValue(a.Fleet, out var af) && af.Moving) _carrying.Add(af.Id);

        FleetMarkers(ref k, ref any, me, true);

        long occupation = 0;        // changes whenever any system is taken or freed: the fronts are recomputed then
        foreach (var c in _sim.Colonies)
        {
            bool occupied = c.OccupiedBy >= 0, blockaded = c.BlockadedUntil > year;
            if (occupied) occupation += (c.Star.Id + 1L) * 1000003L + c.OccupiedBy;
            if (!occupied && !blockaded || k >= MaxWarMarkers) continue;
            var wp = _main.StarWorld(c.Star);
            if (occupied) Put(buf, k++, wp, _sim.Civs[c.OccupiedBy].Color, 0.95f, 15f, 5f, 0f, 0f);
            if (blockaded && k < MaxWarMarkers) Put(buf, k++, wp, Ui.Danger, 0.9f, occupied ? 21f : 15f, 6f, 0f, 0f);
        }

        FleetMarkers(ref k, ref any, me, false);

        // fronts: open wars, and ceasefires (a closed war whose front stays frozen) unless a later war between the two leads replaced it
        var cam = _main.Cam;
        float pxAngle = Mathf.DegToRad(cam.Fov) / Mathf.Max(GetViewport().GetVisibleRect().Size.Y, 1f);
        var camPos = cam.GlobalPosition;
        if (any) _warIm.SurfaceEnd();
        bool anyFront = false;
        for (int wi = 0; wi < _sim.Wars.Count; wi++)
        {
            var w = _sim.Wars[wi];
            if (!w.Open && !w.Ceasefire || !w.Open && Superseded(wi)) { _fronts.Remove(w.Id); continue; }
            var fronts = Fronts(w, occupation, year);
            if (fronts.Count == 0) continue;
            if (!anyFront) { _frontIm.SurfaceBegin(Mesh.PrimitiveType.Lines); anyFront = true; }
            bool mine = w.Has(me);
            float alpha = mine ? 0.9f : 0.3f;
            if (w.Ceasefire) alpha *= 0.45f;
            var red = Ui.Danger.SrgbToLinear();      // the shader writes linear colour; the HUD red is sRGB
            var col = new Color(red.R, red.G, red.B, alpha);
            var casing = new Color(0f, 0f, 0f, alpha * 0.7f);
            foreach (var (from, to) in fronts)
            {
                if (from > to && fronts.Contains((to, from))) continue;     // both leads often name the same front
                StarInfo sa = _sim.Gal.Stars[from], sb = _sim.Gal.Stars[to];
                var a = _main.StarWorld(sa);
                var b = _main.StarWorld(sb);
                // one pixel across the line at each end, so the width holds however far away each end is
                // (unit vectors first: galaxy-scale world lengths overflow a float when multiplied)
                var along = (b - a).Normalized();
                var pa = along.Cross((a - camPos).Normalized()).Normalized() * (a - camPos).Length() * pxAngle;
                var pb = along.Cross((b - camPos).Normalized()).Normalized() * (b - camPos).Length() * pxAngle;
                // a ceasefire's front: every other piece of about CeasefireDash galaxy units
                int segs = w.Ceasefire ? Math.Max(3, (int)(sa.Pos.DistanceTo(sb.Pos) / CeasefireDash) | 1) : 1;
                int step = w.Ceasefire ? 2 : 1;
                if (mine)
                {
                    // the player's fronts: three red lines a pixel apart inside a dark casing, so they read over any territory
                    FrontLine(a, b, pa, pb, -2f, segs, step, casing); FrontLine(a, b, pa, pb, 2f, segs, step, casing);
                    for (int l = -1; l <= 1; l++) FrontLine(a, b, pa, pb, l, segs, step, col);
                }
                else FrontLine(a, b, pa, pb, 0f, segs, step, col);
            }
        }
        if (anyFront) _frontIm.SurfaceEnd();
        wm.Buffer = buf;
        wm.VisibleInstanceCount = k;
    }

    /// <summary>One front line, offset sideways by off pixels, drawn as every step-th of segs pieces.</summary>
    void FrontLine(Vector3 a, Vector3 b, Vector3 pa, Vector3 pb, float off, int segs, int step, Color col)
    {
        var a0 = a + pa * off; var b0 = b + pb * off;
        for (int i = 0; i < segs; i += step)
        {
            _frontIm.SurfaceSetColor(col); _frontIm.SurfaceAddVertex(a0.Lerp(b0, (float)i / segs));
            _frontIm.SurfaceSetColor(col); _frontIm.SurfaceAddVertex(a0.Lerp(b0, (float)(i + 1) / segs));
        }
    }

    /// <summary>Chevrons (style 3) for the player's fleets or everyone else's, armies aboard (style 4), and course lines.</summary>
    void FleetMarkers(ref int k, ref bool any, int me, bool player)
    {
        var buf = _warBuf;
        foreach (var f in _sim.Fleets)
        {
            if (k >= MaxWarMarkers) return;
            if ((f.Civ == me) != player) continue;
            double ships = 0;
            foreach (var sq in f.Squadrons) ships += sq.Ships;
            if (ships <= 0) continue;
            var col = _sim.Civs[f.Civ].Color;
            var p = _main.GalWorld(_sim.FleetPos(f));
            float size = Mathf.Clamp(6f + 1.6f * (float)Math.Log10(Math.Max(ships, 1)), 6f, 17f);
            Put(buf, k, p, col, 1f, size, 3f, 0f, 0f);
            int o = k++ * 20;
            buf[o] = 0;                                                // basis x column = course (the shader turns the chevron along it)
            if (!f.Moving) continue;
            var to = _main.StarWorld(_sim.Gal.Stars[f.To]);
            var course = (to - p).Normalized();
            buf[o] = course.X; buf[o + 4] = course.Y; buf[o + 8] = course.Z;
            if (_carrying.Contains(f.Id) && k < MaxWarMarkers) Put(buf, k++, p, col.Lerp(Colors.White, 0.4f), 1f, 7f, 4f, 0f, 0f);
            if (!any) { _warIm.SurfaceBegin(Mesh.PrimitiveType.Lines); any = true; }
            _warIm.SurfaceSetColor(new Color(col.R, col.G, col.B, 0.55f)); _warIm.SurfaceAddVertex(p);
            _warIm.SurfaceSetColor(new Color(col.R, col.G, col.B, 0.0f)); _warIm.SurfaceAddVertex(to);
        }
    }

    /// <summary>
    /// A war's fronts, from the cache unless the war's state changed (score, battles, open or frozen, any system taken
    /// or freed) or a sim year has passed since they were computed: Simulation.Fronts scans every colony of the war's civs.
    /// </summary>
    List<(int From, int To)> Fronts(War w, long occupation, double year)
    {
        if (_fronts.TryGetValue(w.Id, out var fc) && fc.Score == w.Score && fc.Battles == w.Battles && fc.Open == w.Open
            && fc.Ceasefire == w.Ceasefire && fc.Occupation == occupation && year >= fc.Year && year - fc.Year < 1.0)
            return fc.Fronts;
        fc ??= new FrontCache();
        fc.Fronts = _sim.Fronts(w); fc.Score = w.Score; fc.Battles = w.Battles; fc.Open = w.Open; fc.Ceasefire = w.Ceasefire;
        fc.Occupation = occupation; fc.Year = year;
        _fronts[w.Id] = fc;
        return fc.Fronts;
    }

    /// <summary>A later war, open or frozen, between the same two leads as war wi.</summary>
    bool Superseded(int wi)
    {
        var w = _sim.Wars[wi];
        int a = w.A[0], b = w.B[0];
        for (int i = wi + 1; i < _sim.Wars.Count; i++)
        {
            var x = _sim.Wars[i];
            if ((x.Open || x.Ceasefire) && x.Has(a) && x.Has(b)) return true;
        }
        return false;
    }

    void UpdateShips(float zoomGal)
    {
        _im.ClearSurfaces();
        var heads = _shipHeads.Multimesh;
        int k = 0;
        bool lines = _sim.Ships.Count > 0 || _sim.Lanes.Count > 0 || _sim.Quarantines.Count > 0;
        if (lines)
        {
            _im.SurfaceBegin(Mesh.PrimitiveType.Lines);
            // the voice's permanent lanes, and the walls round the regions it has sealed
            foreach (var (la, lb) in _sim.Lanes)
            {
                _im.SurfaceSetColor(new Color(0.8f, 0.6f, 1f, 0.7f)); _im.SurfaceAddVertex(_main.StarWorld(la));
                _im.SurfaceSetColor(new Color(0.8f, 0.6f, 1f, 0.7f)); _im.SurfaceAddVertex(_main.StarWorld(lb));
            }
            foreach (var (qc, qr) in _sim.Quarantines)
                for (int i = 0; i < 64; i++)
                {
                    float a0 = i * Mathf.Tau / 64, a1 = (i + 1) * Mathf.Tau / 64;
                    _im.SurfaceSetColor(new Color(1f, 0.4f, 0.35f, 0.4f)); _im.SurfaceAddVertex((qc + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * qr - _main.ViewStar.Pos) * Main.G);
                    _im.SurfaceSetColor(new Color(1f, 0.4f, 0.35f, 0.4f)); _im.SurfaceAddVertex((qc + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * qr - _main.ViewStar.Pos) * Main.G);
                }
            foreach (var s in _sim.Ships)
            {
                float u = s.Progress(_sim.Year);
                var a = _main.StarWorld(s.Origin);
                var b = _main.StarWorld(s.Target);
                var p = a.Lerp(b, u);
                var col = s.Civ.Color;
                _im.SurfaceSetColor(new Color(col.R, col.G, col.B, 0.02f));
                _im.SurfaceAddVertex(a);
                _im.SurfaceSetColor(new Color(col.R, col.G, col.B, 0.3f));
                _im.SurfaceAddVertex(p);
                _im.SurfaceSetColor(new Color(col.R, col.G, col.B, 0.12f));
                _im.SurfaceAddVertex(p);
                _im.SurfaceSetColor(new Color(col.R, col.G, col.B, 0.0f));
                _im.SurfaceAddVertex(b);
                if (k < MaxHeads)
                {
                    heads.SetInstanceTransform(k, new Transform3D(Basis.Identity, p));
                    heads.SetInstanceColor(k, new Color(col.R * 0.5f + 0.5f, col.G * 0.5f + 0.5f, col.B * 0.5f + 0.5f, 1f));
                    heads.SetInstanceCustomData(k, new Color(4.5f, 2f, 0, 0));
                    k++;
                }
            }
            _im.SurfaceEnd();
        }
        // the Andromeda Ark: a long stream of ships leaving the galaxy
        if (_sim.ArkYear > 0 && _sim.Humanity.Colonies.Count > 0)
        {
            var andro = _main.AndromedaWorld();
            var homes = _sim.Humanity.Colonies;
            for (int i = 0; i < 120 && k < MaxHeads; i++)
            {
                var from = homes[(i * 37) % homes.Count];
                double age = _sim.Year - _sim.ArkYear - i * 2.0;
                if (age < 0) continue;
                float u = (float)Math.Min(1, age / 1200.0);
                var a = _main.StarWorld(from.Star);
                var p = a.Lerp(andro, u * 0.06f);
                heads.SetInstanceTransform(k, new Transform3D(Basis.Identity, p));
                heads.SetInstanceColor(k, new Color(0.85f, 0.92f, 1f, 1f - u));
                heads.SetInstanceCustomData(k, new Color(3.5f, 2f, 0, 0));
                k++;
            }
        }
        heads.VisibleInstanceCount = k;
    }

    /// <summary>Rebuild the ownership and capture maps the galaxy shaders sample.</summary>
    float[] _ownW, _capV;
    byte[] _ownBytes, _capBytes;

    void UpdateTerritory()
    {
        int N = TexSize * TexSize;
        _ownW ??= new float[N]; _capV ??= new float[N];
        _ownBytes ??= new byte[N * 4]; _capBytes ??= new byte[N * 4];
        Array.Clear(_ownW); Array.Clear(_capV); Array.Clear(_ownBytes);
        const float splat = 22f;    // galaxy units each sim star stands for
        float pxPer = TexSize / (TexExtent * 2);
        float rpx = splat * pxPer;
        int rad = (int)Math.Ceiling(rpx) + 1;
        foreach (var c in _sim.Colonies)
        {
            var p = c.Star.Pos;
            float cx = (p.X + TexExtent) * pxPer, cz = (p.Z + TexExtent) * pxPer;
            var col = c.Civ.Exalted ? c.Civ.Color.Lerp(Gold, 0.65f) : c.Civ.Color;      // Exalted territory burns gold
            byte cr = (byte)(col.R * 255), cg = (byte)(col.G * 255), cb = (byte)(col.B * 255);
            float capf = (float)Math.Clamp(Math.Max(c.Capture * 0.35, c.Region), 0, 1);
            for (int y = (int)cz - rad; y <= (int)cz + rad; y++)
            {
                if (y < 0 || y >= TexSize) continue;
                for (int x = (int)cx - rad; x <= (int)cx + rad; x++)
                {
                    if (x < 0 || x >= TexSize) continue;
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cz;
                    float d2 = (dx * dx + dy * dy) / (rpx * rpx);
                    if (d2 > 1) continue;
                    float w = 1 - d2;
                    int i = y * TexSize + x;
                    if (w > _ownW[i])
                    {
                        _ownW[i] = w;
                        int o = i * 4;
                        _ownBytes[o] = cr; _ownBytes[o + 1] = cg; _ownBytes[o + 2] = cb; _ownBytes[o + 3] = (byte)(w * 255);
                    }
                    float v = capf * w;
                    if (v > _capV[i]) _capV[i] = v;
                }
            }
        }
        for (int i = 0; i < N; i++) { int o = i * 4; _capBytes[o] = (byte)(_capV[i] * 255); _capBytes[o + 1] = 0; _capBytes[o + 2] = 0; _capBytes[o + 3] = 255; }
        _own.SetData(TexSize, TexSize, false, Image.Format.Rgba8, _ownBytes);
        _cap.SetData(TexSize, TexSize, false, Image.Format.Rgba8, _capBytes);
        OwnTex.Update(_own);
        CapTex.Update(_cap);
    }
}
