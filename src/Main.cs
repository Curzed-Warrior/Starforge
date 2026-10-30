using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Starforge.Sim;
using Starforge.View;

namespace Starforge;

/// <summary>
/// Entry point. One continuous world: the viewed star sits at the origin, the galaxy is scaled by G around it.
/// Command line (after "--"): --shot=abs.png [--shotframe=N] --focus=Earth|Sun|Galaxy --dist=D --yaw=Y --pitch=P
/// --warp=YEARS [--trace] --seed=N --speed=I --star=Name; --stagebattle [--focus=battle] [--battlefam=A,B] [--battlerun=SECONDS] [--stagelayers] stages a fight here;
/// --stageunrest, --stageslips and --intpage=N stage the Interior, Military and Economy tabs' rarer states;
/// --stagewarworks [--focus=works:factory|slip|barracks|specops|army] stages the home system's war works and frames one
/// (--stagelayers also stages the galaxy map's war markers nearby: an occupied, a blockaded and a both system, a moving
/// fleet carrying an army and an idle fleet)
/// </summary>
public partial class Main : Node3D
{
    /// <summary>World units (Earth radii) per galaxy unit. One galaxy unit is 50 light-years, at true scale.</summary>
    public const float G = 7.425e10f;
    /// <summary>1 while time flows, 0 when paused: everything animated but the camera and the interface follows it.</summary>
    public static float Anim = 1f;
    public static readonly double[] Speeds = { 0, 1 / 365.25, 7 / 365.25, 1 / 12.0, 1, 4, 16, 64 };
    public static readonly string[] SpeedNames = { "Pause", "1 day", "1 wk", "1 mo", "1 yr", "4 yr", "16 yr", "64 yr" };

    Galaxy _gal;
    int _seed;
    public static string PendingLoad;
    static int? _nextSeed;
    static bool _skipTitle;
    public static float TitleYaw = -0.6f;
    int _speedBeforeMenu = 2;
    float _autosaveT;
    /// <summary>The autosave being written on a worker (SaveGame.SaveInBackground), until it is finished and reported.</summary>
    System.Threading.Tasks.Task _autosave;
    /// <summary>The simulation threw (SimCrashed): time stays stopped and nothing is saved over the good saves until one is loaded.</summary>
    bool _simFaulted;
    public const string SavePath = "user://starforge_save.json", AutoPath = "user://starforge_autosave.json";
    public Simulation Sim;
    GalaxyView _galView;
    GalaxyOverlay _overlay;
    public SystemView SysView;
    SystemView _oldView;
    CameraRig _rig;
    Hud _hud;
    PostFx _post;
    Music _music;
    float _moodT;
    string _fx;
    string _forceProject;
    Colony _pendingArrival;
    float _forceTerraform = -1;
    Node3D _following;
    int _seqN, _seqEvery; string _seqDir;
    Vector3 _lastCamGal; float _warpFx; bool _traveling;
    bool _slowSaved;
    public StarInfo ViewStar;
    public Body SelectedBody;
    public int SpeedIdx = 2;
    /// <summary>The director: camera tours and paces time on its own. Off by default now the player runs the empire; F turns it on.</summary>
    public bool Autoplay = false;
    float _slowT;
    public bool Warping;
    public double VisYear;
    int _gc0, _gc1, _gc2;
    static readonly bool _traceHitch = OS.GetCmdlineUserArgs().Contains("--hitch");
    string _shot;
    int _shotFrame = 90, _frame;
    float _warp; bool _trace;

    Godot.Environment _env;
    float _directorT;
    Vector2 _pressPos;

    public Camera3D Cam => _rig.Cam;
    public bool InGalaxyView => _rig.Dist > 0.05f * G;
    public StarSystem ViewSystem => SysView?.Sys;
    public Colony ViewColony => Sim.Colonies.FirstOrDefault(c => c.Star == ViewStar);
    public Galaxy Gal => _gal;
    public float GalaxyOutside => _galView?.LastOutside ?? 0f;

    static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

    public override void _Ready()
    {
        // no blocking full collections mid-frame: a gen-2 pause is a visible stutter in a cinematic or a camera glide
        System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.SustainedLowLatency;
        // test: --selftest[=filter] runs the headless self-tests and quits (exit 1 if any failed)
        if (Array.Find(OS.GetCmdlineUserArgs(), x => x == "--selftest" || x.StartsWith("--selftest=")) is string st)
        {
            int failed = Starforge.Tests.SelfTest.Run(st.Length > 11 ? st[11..] : null);
            GetTree().Quit(failed > 0 ? 1 : 0);
            return;
        }
        int seed = _nextSeed ?? 20610;
        string focus = "Earth", star = null;
        float dist = -1, yaw = float.NaN, pitch = float.NaN;
        int speed = -1;
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--shot=")) _shot = a[7..];
            else if (a.StartsWith("--shotframe=")) _shotFrame = int.Parse(a[12..]);
            else if (a.StartsWith("--focus=")) focus = a[8..];
            else if (a.StartsWith("--star=")) star = a[7..].Replace('_', ' ');
            else if (a.StartsWith("--dist=")) dist = F(a[7..]);
            else if (a.StartsWith("--yaw=")) yaw = F(a[6..]);
            else if (a.StartsWith("--pitch=")) pitch = F(a[8..]);
            else if (a.StartsWith("--seed=")) seed = int.Parse(a[7..]);
            else if (a.StartsWith("--far=")) CameraRig.FarOverride = F(a[6..]);
            else if (a.StartsWith("--neark=")) CameraRig.NearK = F(a[8..]);
            else if (a.StartsWith("--warp=")) _warp = F(a[7..]);
            else if (a.StartsWith("--speed=")) speed = int.Parse(a[8..]);
            else if (a == "--trace") _trace = true;
            else if (a == "--profile") Simulation.Profile = true;
            else if (a == "--auto") Autoplay = true;
            else if (a.StartsWith("--tyaw=")) TitleYaw = F(a[7..]);
            else if (a.StartsWith("--sky=")) { var q = a[6..].Split(','); GalaxyView.SkyOverride = q.Select(F).ToArray(); }
            else if (a.StartsWith("--hide=")) Works.HiddenParts = a[7..];
            else if (a.StartsWith("--fx=")) _fx = a[5..];
            else if (a.StartsWith("--project=")) _forceProject = a[10..];
            else if (a.StartsWith("--terraform=")) _forceTerraform = F(a[12..]);
            else if (a.StartsWith("--seq=")) { var p2 = a[6..].Split(','); _seqN = int.Parse(p2[0]); _seqEvery = int.Parse(p2[1]); _seqDir = p2.Length > 2 ? p2[2] : null; }
        }

        string loadFailed = null;
        if (PendingLoad != null && FileAccess.FileExists(PendingLoad))
        {
            // a save neither it nor its backup can read starts a new game rather than a broken scene
            try { (Sim, seed) = SaveGame.Load(PendingLoad); _gal = Sim.Gal; }
            catch (Exception ex) { GD.PrintErr(ex.ToString()); loadFailed = "That save could not be read. A new game has begun."; PendingLoad = null; }
        }
        if (Sim == null)
        {
            Sim = Runs.NewGame(seed);
            _gal = Sim.Gal;
        }
        bool loaded = PendingLoad != null;
        PendingLoad = null;
        _seed = seed;
        Func<int> logLines = _trace ? Runs.TraceChronicle(Sim) : () => 0;
        if (_warp > 0)
        {
            double y0 = Sim.Year;
            try { Warp(_warp); }
            catch (Exception ex) { GD.PrintErr(ex.ToString()); if (_trace) { GetTree().Quit(1); return; } }
            if (_trace) { Runs.PrintWarStats(Sim, logLines(), Sim.Year - y0); Runs.PrintStrifeStats(Sim, logLines(), Sim.Year - y0); Runs.PrintSpecOpsStats(Sim); }
        }
        if (OS.GetCmdlineUserArgs().Contains("--savetest"))
        {
            Runs.SaveTest(Sim, seed);
            GetTree().Quit(); return;
        }
        if (_trace && _shot == null) { GetTree().Quit(); return; }
        VisYear = Sim.Year;

        SetupEnvironment();
        _galView = new GalaxyView();
        AddChild(_galView);
        _galView.Setup(_gal);

        _rig = new CameraRig();
        AddChild(_rig);

        var start = star != null ? _gal.Stars.FirstOrDefault(s => s.Name == star) ?? _gal.Sol : _gal.Sol;
        ViewStar = start;
        PlaceGalaxy();
        BuildSystem(start);
        Sim.Focus = ViewColony ?? Sim.HomeColony;

        _overlay = new GalaxyOverlay();
        AddChild(_overlay);
        _overlay.Setup(this, Sim);
        if (Sim.Humanity.M.Transcend) _overlay.TranscendT = 30f;
        _galView.SetTerritory(_overlay.CapTex);

        _post = new PostFx();
        AddChild(_post);
        _hud = new Hud();
        AddChild(_hud);
        _hud.Setup(this);
        if (loadFailed != null) _hud.Toast(loadFailed);
        Sim.Logged += OnLogged;
        _cine = new Cinematics { Name = "Cinematics" };
        AddChild(_cine);
        _cine.Setup(this);
        WireMoments();
        Sim.Effect += (c, kind, b) =>
        {
            if (Warping || c == null) return;
            if (kind == "arrival" && c.Civ.Main && Autoplay && !_cine.Busy && !_rig.FreeFlight && _directorT > 20f && c.Star != ViewStar) { TravelTo(c.Star); _pendingArrival = c; _directorT = 0; return; }
            if (c.Star != ViewStar) return;
            SysView.Spectacle.Play(kind, c, b);
            if (kind is "flare" or "battle") _music?.Sfx("boom");
            if (kind is "arrival" or "comet") _music?.Sfx("shimmer");
            if (kind == "seedlaunch") _music?.Sfx("whoosh");
            if (kind == "seedlaunch" && Autoplay && !_rig.FreeFlight) FollowLaunch();
        };
        if (_shot == null) { _music = new Music(); AddChild(_music); Sim.Logged += e => { if (!Warping && (e.Civ.Main || e.Colony == Sim.Focus)) _music.Chime(e.Kind); }; }

        Focus(focus, dist, instant: true);
        if (OS.GetCmdlineUserArgs().Contains("--lit") && SelectedBody != null)
        {
            // frame the world from its sunlit side, a little off-axis
            var bp = SelectedBody.WorldPos(VisYear).Normalized();
            yaw = Mathf.Atan2(-bp.X, -bp.Z) + 0.5f;
        }
        if (!float.IsNaN(yaw) || !float.IsNaN(pitch))
            _rig.SetView(float.IsNaN(yaw) ? _rig.Yaw : yaw, float.IsNaN(pitch) ? _rig.Pitch : pitch, _rig.Dist);
        if (speed >= 0) SpeedIdx = speed;
        else if (_shot != null) SpeedIdx = 0;
        if ((_shot == null || OS.GetCmdlineUserArgs().Contains("--title")) && _warp <= 0 && !loaded && _seqN == 0)
        {
            if (_skipTitle) { _skipTitle = false; BeginNew(); }
            else
            {
                // title screen: Earth's night side, the Sun about to rise over its limb, time stopped
                var earth = SysView.Sys.Find("Earth");
                var ep = earth.WorldPos(VisYear);
                var away = ep.Normalized();
                float tyaw = Mathf.Atan2(away.X, away.Z) + TitleYaw;
                var right = Vector3.Up.Cross(away).Normalized();
                _rig.Target = () => earth.WorldPosD(VisYear) + (right * 1.05f + Vector3.Up * 0.05f);
                _rig.MinDist = 0.5f;
                _rig.SetView(tyaw, -0.06f, 3.4f);
                SpeedIdx = 0;
                _hud.ShowMenu(true);
            }
        }
        else if (_seqN > 0 && !loaded) BeginNew();
        if (_shot != null && !OS.GetCmdlineUserArgs().Contains("--auto")) Autoplay = false;
        if (_seqN > 0) { _hud.Visible = true; }
    }

    public void BeginNew()
    {
        ShowOpening();
        var earth = SysView.Sys.Find("Earth");
        SelectedBody = earth;
        _rig.SetView(2.4f, -0.12f, 60f);
        _rig.FlyTo(() => earth.WorldPosD(VisYear), 4.2f, earth.Radius * 1.08f, 14f);
        SpeedIdx = 1;
    }

    public void Reseed(bool keepSeed = false)
    {
        _nextSeed = keepSeed ? _seed : (int)(GD.Randi() % 1000000);
        GetTree().ReloadCurrentScene();
    }

    public void LoadLatest()
    {
        string p = FileAccess.FileExists(SavePath) ? SavePath : AutoPath;
        if (FileAccess.FileExists(SavePath) && FileAccess.FileExists(AutoPath) && FileAccess.GetModifiedTime(AutoPath) > FileAccess.GetModifiedTime(SavePath)) p = AutoPath;
        PendingLoad = p;
        GetTree().ReloadCurrentScene();
    }

    public void SaveNow()
    {
        if (_simFaulted) { _hud.Toast("The simulation stopped on an error. Saving now would keep the broken state: load a save (F9) instead."); return; }
        if (Sim.Holding) { _hud.Toast("Finish the battle first."); return; }
        SaveGame.Save(Sim, _seed, SavePath); _hud.Toast("Saved");
    }

    /// <summary>
    /// The simulation threw: stop time, write what happened (seed, year, stack) and an emergency save beside it, so
    /// the game can go on from the last autosave and the bug can be reproduced headless from the seed or the save.
    /// The fault is latched: the half-stepped universe is never stepped again or autosaved over the good saves.
    /// </summary>
    void SimCrashed(Exception ex)
    {
        _simFaulted = true;
        SpeedIdx = 0;
        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string log = $"user://crash_{stamp}.txt", save = $"user://crash_{stamp}.json";
        GD.PrintErr(ex.ToString());
        string saved = "no";
        try { SaveGame.Save(Sim, _seed, save); saved = save; } catch (Exception e2) { GD.PrintErr("emergency save failed: " + e2.Message); }
        try { System.IO.File.WriteAllText(ProjectSettings.GlobalizePath(log), $"Starforge simulation error\nseed {_seed}\nyear {Sim.Year:0.00}\nemergency save {saved}\n\n{ex}\n"); } catch { }
        _hud?.Toast($"The simulation hit an error in {Sim.Year:0} and was paused. Report: {ProjectSettings.GlobalizePath(log)}");
    }

    /// <summary>F12: the frame as it is; Shift+F12: the same without the interface. Saved to user://screenshots.</summary>
    async void Screenshot(bool clean)
    {
        bool hud = _hud.Visible;
        if (clean && hud) { _hud.Visible = false; await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); }
        var img = GetViewport().GetTexture().GetImage();
        if (clean && hud) _hud.Visible = true;
        string dir = ProjectSettings.GlobalizePath("user://screenshots");
        System.IO.Directory.CreateDirectory(dir);
        string where = SelectedBody?.Name ?? ViewStar?.Name ?? "galaxy";
        string path = System.IO.Path.Combine(dir, $"starforge_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{Sim.Year:0000}_{where.Replace(' ', '_')}.png");
        img.SavePng(path);
        _hud.Toast("Screenshot saved: " + path);
    }

    /// <summary>Shift+F6: the chronicle written out as a history book (Markdown) in user://histories.</summary>
    void ExportHistory()
    {
        string dir = ProjectSettings.GlobalizePath("user://histories");
        System.IO.Directory.CreateDirectory(dir);
        string path = System.IO.Path.Combine(dir, $"history_seed{_seed}_{Sim.Year:0000}.md");
        System.IO.File.WriteAllText(path, HistoryExport.Markdown(Sim, _seed));
        _hud.Toast("History written: " + path);
    }

    public void MenuClosed() { if (SpeedIdx == 0) SpeedIdx = _speedBeforeMenu; }

    void ShowOpening() => _hud.ShowMilestone("Starforge", "Earth, 2061. Nine point seven billion people, one world, and a handful of footholds beyond it. They are about to reach for the stars. You are a voice they cannot locate.");

    void Warp(float years)
    {
        Warping = true;
        Runs.Warp(Sim, years, _trace, OS.GetCmdlineUserArgs().Contains("--techlog"));
        Warping = false;
    }

    void PlaceGalaxy()
    {
        _galView.Scale = Vector3.One * G;
        _galView.Position = GalWorld(Vector3.Zero);
    }

    /// <summary>A point in galaxy units, in system coordinates (the viewed star at zero; double precision).</summary>
    public D3 GalSys(Vector3 gal) => new((double)(gal.X - ViewStar.Pos.X) * G, (double)(gal.Y - ViewStar.Pos.Y) * G, (double)(gal.Z - ViewStar.Pos.Z) * G);
    /// <summary>A point in galaxy units, in view space (for drawing and labels).</summary>
    public Vector3 GalWorld(Vector3 gal) => Frame.View(GalSys(gal));
    public D3 StarSys(StarInfo s) => GalSys(s.Pos);
    public Vector3 StarWorld(StarInfo s) => GalWorld(s.Pos);
    public Vector3 CoreWorld() => GalWorld(Vector3.Zero);
    public Vector3 AndromedaWorld() => GalWorld(_galView.AndromedaPos);

    public void Toast(string msg) => _hud.Toast(msg);
    /// <summary>The voice changed the viewed system's worlds or star: rebuild what is drawn.</summary>
    public void WorldsChanged()
    {
        if (SysView == null || Sim.SystemFor(ViewStar).Bodies.Count == _viewBodies && _viewDead == ViewStar.Dead && _viewPos == ViewStar.Pos) return;
        Refocus();
        SysView.QueueFree();
        BuildSystem(ViewStar);
        SelectedBody = null;
    }
    bool _viewDead; Vector3 _viewPos; int _viewBodies;
    /// <summary>The viewed star moved: put the galaxy back around it.</summary>
    public void Refocus() { PlaceGalaxy(); }

    void BuildSystem(StarInfo s)
    {
        _viewDead = s.Dead; _viewPos = s.Pos; _viewBodies = Sim.SystemFor(s).Bodies.Count;
        SysView = new SystemView();
        AddChild(SysView);
        SysView.Build(Sim.SystemFor(s));
    }

    void SetupEnvironment()
    {
        _env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0, 0, 0),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.02f, 0.022f, 0.03f),
            TonemapMode = Godot.Environment.ToneMapper.Agx,
            TonemapExposure = 1.0f,
            GlowEnabled = true,
            GlowIntensity = 0.45f,
            GlowStrength = 1.0f,
            GlowBloom = 0.02f,
            GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Additive,
            GlowHdrThreshold = 1.4f,
            GlowHdrScale = 2.0f,
            AdjustmentEnabled = true,
            AdjustmentSaturation = 1.15f,
            AdjustmentContrast = 1.06f,
        };
        for (int i = 0; i < 7; i++) _env.SetGlowLevel(i, i switch { 0 => 0.4f, 1 => 0.6f, 2 => 0.7f, 3 => 0.6f, 4 => 0.5f, 5 => 0.4f, _ => 0.3f });
        // metal needs something to reflect: a dim sky with the Milky Way in it, for reflections only (the background
        // stays black). It never changes, so its radiance is baked once.
        _env.Sky = new Sky { SkyMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/reflect_sky.gdshader") }, RadianceSize = Sky.RadianceSizeEnum.Size256 };
        _env.ReflectedLightSource = Godot.Environment.ReflectionSource.Sky;
        AddChild(new WorldEnvironment { Environment = _env });
    }

    // ---------- Focus and travel ----------
    void Focus(string name, float dist, bool instant)
    {
        if (name == "Galaxy") { ToGalaxy(instant, dist); return; }
        if (name == "Core") { ToCore(instant, dist); return; }
        var lmk = _gal.Landmarks.FirstOrDefault(l => l.Name.Replace(" ", "") == name.Replace(" ", "").Replace("_", ""));
        if (lmk != null) { ToLandmark(lmk, instant, dist); return; }
        if (name == "Sun" || name == "Sol" || name == "Star") { SelectStar(instant, dist); return; }
        var b = SysView.Sys.Find(name) ?? SysView.Sys.Bodies.FirstOrDefault(x => x.Kind != BodyKind.Belt);
        SelectBody(b, instant, dist);
    }

    public void SelectBody(Body b, bool instant = false, float dist = -1)
    {
        if (b == null) return;
        SelectedBody = b;
        Func<D3> target = () => b.WorldPosD(VisYear);
        float extra = b.Ring ? b.RingOut * 0.9f : 1f;
        float min = b.Radius * 1.08f, def = dist > 0 ? dist : b.Radius * 4.5f * extra;
        if (b.Kind == BodyKind.Belt) { target = () => D3.Zero; min = 10f; def = dist > 0 ? dist : b.Orbit * 2.2f; }
        if (instant) { _rig.Target = target; _rig.MinDist = min; _rig.SetView(_rig.Yaw, _rig.Pitch, def); }
        else _rig.FlyTo(target, def, min);
    }

    public void SelectStar(bool instant = false, float dist = -1)
    {
        SelectedBody = null;
        float r = SysView.Sys.StarRadius;
        float outer = SysView.Sys.Bodies.Count > 0 ? SysView.Sys.Bodies.Max(b => b.Orbit) : r * 20;
        float def = dist > 0 ? dist : outer * 2.4f;
        if (instant) { _rig.Target = () => D3.Zero; _rig.MinDist = r * 1.3f; _rig.SetView(_rig.Yaw, Mathf.Min(_rig.Pitch, -0.45f), def); }
        else _rig.FlyTo(() => D3.Zero, def, r * 1.3f);
    }

    public Vector3 LandmarkWorld(Galaxy.Landmark lm) => GalWorld(lm.Pos);

    public void ToLandmark(Galaxy.Landmark lm, bool instant = false, float dist = -1)
    {
        SelectedBody = null;
        float r = lm.Radius * G;
        float def = dist > 0 ? dist : r * 2.8f;
        Func<D3> tgt = () => GalSys(lm.Pos);
        if (instant) { _rig.Target = tgt; _rig.MinDist = r * 0.2f; _rig.SetView(_rig.Yaw, -0.15f, def); }
        else _rig.FlyTo(tgt, def, r * 0.2f);
        _hud?.Toast(lm.About);
    }

    /// <summary>Fly to the galactic core and its black hole.</summary>
    public void ToCore(bool instant = false, float dist = -1)
    {
        SelectedBody = null;
        float rs = GalaxyView.CoreRs * G;
        float def = dist > 0 ? dist : rs * 38f;
        if (instant) { _rig.Target = () => GalSys(Vector3.Zero); _rig.MinDist = rs * 3f; _rig.SetView(_rig.Yaw, -0.12f, def); }
        else _rig.FlyTo(() => GalSys(Vector3.Zero), def, rs * 3f);
    }

    public void ToGalaxy(bool instant = false, float dist = -1)
    {
        SelectedBody = null;
        float def = dist > 0 ? dist : Galaxy.Radius * 2.4f * G;
        if (instant) { _rig.Target = () => GalSys(Vector3.Zero); _rig.MinDist = 1f; _rig.SetView(_rig.Yaw, -0.9f, def); }
        else _rig.FlyTo(() => GalSys(Vector3.Zero), def, 1f);
    }

    /// <summary>Fly to another star: rebase the world onto it, keep the old system visible until we're clear of it.</summary>
    public void TravelTo(StarInfo s, bool closeIn = true)
    {
        if (s == ViewStar) { SelectStar(); return; }
        var delta = StarSys(s);
        if (_oldView != null) _oldView.QueueFree();
        _oldView = SysView;
        _oldView.Offset = -delta;
        ViewStar = s;
        _rig.Rebase(-delta);
        PlaceGalaxy();
        BuildSystem(s);
        Sim.Focus = ViewColony ?? Sim.Focus;
        SelectedBody = null;
        _traveling = true;
        float span = (float)delta.Length();
        if (span > 1e9f) _music?.Sfx("whoosh");
        float outer = SysView.Sys.Bodies.Count > 0 ? SysView.Sys.Bodies.Max(b => b.Orbit) : 100;
        _rig.FlyTo(() => D3.Zero, outer * 2.4f, SysView.Sys.StarRadius * 1.3f);
    }

    // ---------- cinematics ----------
    Cinematics _cine;
    public Cinematics Cine => _cine;
    public Hud Hud => _hud;
    public CameraRig Rig => _rig;
    public bool Traveling => _traveling;
    public void DirectorHold(float s) => _directorT = -s;
    double _nextBattleCine;

    /// <summary>Which turns of history get filmed, and what the camera should look at.</summary>
    void WireMoments()
    {
        Sim.MilestoneReached += (c, title, text, focus) =>
        {
            // a departure or arrival somewhere else is re-staged when the camera gets there
            string spec = focus == "ship" && c != null && c.Star != ViewStar ? (title == "A Second Sun" ? "arrival" : "seedlaunch") : null;
            _cine.Request(new Cinematics.Moment { Title = title, Text = text, Focus = focus, Colony = c, Priority = 4, Spectacle = spec });
        };
        // megastructures take years to build: the chronicle marks the first stone, the camera the last
        Sim.MegaStarted += (c, id) =>
        {
            if (_trace && c.Civ.Main && c.Home) GD.Print($"MEGA {Sim.Year:0000.0} begin {id} at {c.Sys.Name} (era {c.Era}, foundries {c.Foundries})");
            if (!c.Civ.Main || !(c.Home || c == Sim.Focus) || Warping) return;
            var (name, _) = MegaName(id);
            double yrs = c.YearsLeft(id);
            string eta = double.IsInfinity(yrs) || yrs > 5000 ? "Nobody can say when it will be finished." : $"At this pace it will take about {Math.Max(1, Math.Round(yrs / 5) * 5):0} years.";
            Sim.Log(c, c.Raised.Contains(id) ? $"The {name} at {c.Sys.Name} begins to assemble itself out of the dark." : $"The {c.Gov.Name.ToLower()} commits {Hud.Sci(Colony.SpecOf(id).Kg)} kg to the {name} at {c.Sys.Name}. {eta}", "era", null);
        };
        var firstBuilt = new HashSet<string>();
        Sim.MegaFinished += (c, id) =>
        {
            if (_trace && c.Civ.Main && c.Home) GD.Print($"MEGA {Sim.Year:0000.0} done  {id} at {c.Sys.Name}");
            if (!c.Civ.Main || !Megastructures.Modelled.Contains(id)) return;
            var (name, blurb) = MegaName(id);
            if (c.Home || c == Sim.Focus) Sim.Log(c, $"The {name} at {c.Sys.Name} is complete.", "era", null);
            if (!firstBuilt.Add(id) || Warping) return;
            _cine.Request(new Cinematics.Moment { Title = $"The First {name}", Text = $"Year {Sim.Year:0}. {blurb}.", Focus = "mega:" + name, Colony = c, Priority = 3 });
        };
        Sim.EraReached += c =>
        {
            if (c.Era <= 0 || Defs.Eras[c.Era].Name == "Type I") return;      // Type I has its own milestone
            _cine.Request(new Cinematics.Moment { Title = Defs.Eras[c.Era].Name, Text = $"Year {Sim.Year:0}. {Defs.Eras[c.Era].Text}", Focus = "starbase", Colony = c, Priority = 3 });
        };
        Sim.CommandOpened += OnCommandOpened;
        Sim.BattleFought += (source, target, att) =>
        {
            if (!(source.Civ.Main || target.Civ.Main) || Sim.Year < _nextBattleCine) return;
            _nextBattleCine = Sim.Year + 40;
            _cine.Request(new Cinematics.Moment { Focus = "seat", Colony = target, Priority = 2, Spectacle = target.Star != ViewStar ? "battle" : null });
        };
    }

    static (string Name, string Blurb) MegaName(string id)
    {
        foreach (var (mid, name, _, blurb) in Megastructures.Catalogue) if (mid == id) return (id == "yard" ? "Orbital Shipyard" : name, blurb);
        return (id, "");
    }

    // ---------- cosmic powers and raised structures ----------
    string _pendingMega;
    float _pendingMegaT;

    public void Cast(string power)
    {
        var c = ViewColony; var sys = ViewSystem; var b = SelectedBody;
        bool ok = power switch
        {
            "seed" => b != null && Sim.SeedLife(c, sys, b),
            "ignite" => b != null && Sim.Ignite(c, sys, b),
            "shatter" => b != null && Sim.Shatter(c, sys, b),
            "nova" => Sim.Nova(c, sys),
            "reveal" => Sim.Reveal((c ?? Sim.HomeColony).Civ),
            "orbit" => b != null && Sim.ShiftOrbit(c, sys, b),
            "herald" => Sim.Herald(c),
            "visitor" => Sim.Summon(Sim.HomeColony),
            _ => false,
        };
        if (!ok) { _hud.Toast("That power cannot touch this."); return; }
        if (power is not ("reveal" or "herald")) SysView.Spectacle.Play(power, c, b);
        if (power == "visitor" && ViewStar != Sim.HomeColony.Star) TravelTo(Sim.HomeColony.Star);
        _music?.Sfx(power is "shatter" or "nova" or "ignite" ? "boom" : "shimmer");
        if (power is "nova" or "shatter" or "ignite" or "seed" or "orbit")
            _cine.Request(new Cinematics.Moment { Focus = power == "nova" ? "star" : "body:" + b.Name, Colony = c, Priority = 5 });
    }

    public void RaiseHere(string id, string name)
    {
        if (!Sim.Raise(ViewColony, id, name)) { _hud.Toast(ViewColony == null ? "Nobody lives here to use it." : "It already stands here."); return; }
        _music?.Sfx("shimmer");
        _pendingMega = name; _pendingMegaT = 0;
    }

    /// <summary>Jump to a great work wherever it stands (or is going up): to its system, then onto the structure.</summary>
    public void GoToWork(Colony c, string id)
    {
        if (c == null) return;
        if (c.Star != ViewStar) TravelTo(c.Star);
        _pendingMega = Simulation.WorkName(id); _pendingMegaT = 0;
    }

    int _pendingBattle = -1; float _pendingBattleT;
    /// <summary>Jump to a battle: to its system, then (once there and the fleets are drawn) onto the fight itself.</summary>
    public void GoToBattle(int star)
    {
        if (star < 0 || star >= Sim.Gal.Stars.Count) return;
        var s = Sim.Gal.Stars[star];
        if (s != ViewStar) TravelTo(s);
        _pendingBattle = star; _pendingBattleT = 0;
    }

    /// <summary>The player has a battle to command: go to it, stop the game's clock, and put the controls up.</summary>
    void OnCommandOpened(CommandSession s)
    {
        _nextBattleCine = Sim.Year + 40;
        GoToBattle(s.Star);
        _hud.CloseEmpire();
        _hud.Battle.Open(s);
        // test: --commandgo starts the battle clock at 4x as soon as it opens
        if (OS.GetCmdlineUserArgs().Contains("--commandgo")) { s.Paused = false; s.Speed = 4; }
        // test: --commandorders gives one of each order, then runs the clock
        if (OS.GetCmdlineUserArgs().Contains("--commandorders"))
        {
            var m = s.Mine.Units; var t = s.Theirs.Units;
            var big = m.OrderByDescending(u => u.Ships).First();
            var piece = s.Split(big, Math.Floor(big.Ships / 3));
            s.Focus(new[] { big }, t[0]); s.SetFormation(m.Take(2).ToList(), "wedge");
            if (piece != null) s.Move(new[] { piece }, 0, 1);
            s.Retreat(new[] { m.Last() });
            s.Paused = false; s.Speed = 4;
        }
    }

    /// <summary>See the whole of the commanded battle.</summary>
    public void FrameBattle()
    {
        if (SysView?.Works == null || !SysView.Works.BattleHere(out var centre, out float r)) return;
        _rig.FlyTo(BattleTarget(centre), r * Works.BattleFrame, r * 0.02f);
        var v = SysView.Works.BattleView;
        _rig.YawT = Mathf.Atan2(v.X, v.Z); _rig.PitchT = Works.BattlePitch;
    }

    /// <summary>Fly in on one unit and keep it in the middle of the view.</summary>
    public void FollowUnit(int id)
    {
        var w = SysView?.Works; if (w == null || !w.CmdAnchors.TryGetValue(id, out var a)) return;
        D3 last = a.At;
        D3 Track() { if (IsInstanceValid(w) && w.CmdAnchors.TryGetValue(id, out var x)) last = x.At; return last; }
        _rig.FlyTo(Track, Math.Max(a.Radius * 3f, 1e-6f), Math.Max(a.Radius * 0.15f, 1e-7f));
    }

    /// <summary>The battle is over: the camera may go anywhere again.</summary>
    public void EndBattleCamera() => _rig.MaxDist = 1e15f;

    void FlyToPendingBattle(float dt)
    {
        if (_pendingBattle < 0 || _traveling) return;
        _pendingBattleT += dt;
        if (_pendingBattle != ViewStar.Id) { _pendingBattle = -1; return; }
        if (SysView.Works.BattleHere(out var centre, out float r))
        {
            SelectedBody = null;
            _rig.FlyTo(BattleTarget(centre), r * Works.BattleFrame, r * 0.2f);
            var v = SysView.Works.BattleView;
            _rig.YawT = Mathf.Atan2(v.X, v.Z); _rig.PitchT = Works.BattlePitch;
            _pendingBattle = -1;
        }
        else if (_pendingBattleT > 1f) { _hud.Toast("The battle is over: the fleets have moved on."); _pendingBattle = -1; }
    }

    /// <summary>The battle's centre as it moves with its world, the last known one once it is over.</summary>
    Func<D3> BattleTarget(D3 start)
    {
        var last = start; var works = SysView.Works;
        return () => { if (IsInstanceValid(works) && works.BattleHere(out var c, out _)) last = c; return last; };
    }

    void FlyToPendingMega(float dt)
    {
        if (_pendingMega == null) return;
        if (_traveling) return;                       // wait until we're in the system and its works are built
        _pendingMegaT += dt;
        var mega = SysView.Works.Mega.Active;
        // a few names change with the times (the shipyard grows into the mega shipyard)
        var hit = mega.FindIndex(a => a.Name == _pendingMega || _pendingMega == "Mega Shipyard" && a.Name.EndsWith("Shipyard"));
        if (hit >= 0) { OnLabelClicked("mega:" + mega[hit].Name); _pendingMega = null; }
        else if (_pendingMegaT > 3f) { _hud.Toast($"{_pendingMega} has nothing to see yet."); _pendingMega = null; }
    }

    void StageBattle()
    {
        var me = Sim.Humanity; int star = ViewStar.Id;
        var aliens = Sim.Civs.Where(x => x.Alien && !x.Dead && x.Colonies.Count > 0 && x != me)
            .OrderBy(x => x.Colonies.Min(c => c.Star.Pos.DistanceSquaredTo(ViewStar.Pos))).ToList();
        var foe = aliens.FirstOrDefault(x => Sim.FleetsOf(x).Any(f => f.Ships > 0)) ?? aliens.FirstOrDefault();
        if (foe == null) { GD.Print("--stagebattle: no alien empire"); return; }
        Fleet Ready(Civ civ)
        {
            var f = Sim.FleetsOf(civ).FirstOrDefault(x => x.Name == Forces.FleetName(0)) ?? Sim.FleetsOf(civ).FirstOrDefault();
            if (f == null) { f = new Fleet { Id = Sim.NextFleetId++, Civ = civ.Id, At = star, Name = Forces.FleetName(0) }; Sim.Fleets.Add(f); }
            // an alien fleet with no ships gets a squadron of its best hull. The player's early navy (shuttles) would be
            // specks beside an old empire's line, so for this test the Home Fleet is a heavy line of the modelled hulls
            if (civ.Alien) { if (f.Ships <= 0) f.Add(civ.Hull, 12, 0); }
            else
            {
                f.Squadrons.Clear();
                foreach (var (name, n) in new[] { ("Carrier", 3), ("Titan", 3), ("Juggernaut", 3), ("Colossus", 3), ("Dreadnought", 3) })
                    if (Array.IndexOf(Defs.Hulls, name) is int hi and >= 0) f.Add(hi, n, 0);
            }
            f.At = star; f.To = -1; f.Target = -1;
            return f;
        }
        var mine = Ready(me); var theirs = Ready(foe);
        // a limited war, so the Wars tab offers peace and ceasefire
        var w = Sim.DeclareWar(me, foe, "border_claim") ?? Sim.WarBetween(me, foe);
        if (w == null) { GD.Print($"--stagebattle: no war possible with {foe.Name}"); return; }
        // an existing war is used as it is: say so, since its stakes decide what the Wars tab offers
        if (w.Reason != "border_claim") GD.Print($"--stagebattle: using the war already open with {foe.Name} ({w.Reason}, not border_claim)");
        Sim.SetManual(me, Area.Fleets, true); Sim.SetManual(foe, Area.Fleets, true);
        _nextBattleCine = Sim.Year + 40;            // no cinematic flying off to film it: the view stays on this battle
        if (OS.GetCmdlineUserArgs().Contains("--stagecommand")) me.Policy.Battle = BattleMode.Command;
        var r = Sim.TestFight(w, star);
        if (r == null) { GD.Print($"STAGECOMMAND {me.Name} vs {foe.Name} at {ViewStar.Name}: ships {mine.Ships:0}/{theirs.Ships:0}"); return; }
        // whoever lost stays to be seen
        foreach (var f in new[] { mine, theirs }) if (Sim.Fleets.Contains(f)) { f.At = star; f.To = -1; f.Target = -1; }
        // --battlerun=S: time runs at a week a second for S real seconds, then pauses, so the staged fight's losses blow up
        // one by one on screen (the same moment at any frame rate). The war ends in a ceasefire right after the fight (a
        // proper close, so the screens agree), so the sim fights no second battle meanwhile; the staged heavy line's
        // upkeep is paid, so it isn't laid up
        // (the peace is made while time is still stopped, so its news doesn't pause the run)
        if (BattleRunTo() > 0) { Sim.EndWarWith(w, r.AWins, new() { WarModel.TermById("ceasefire") }); me.Materiel += 1e40; _battleRunT = 0; }
        SpeedIdx = BattleRunTo() > 0 ? 2 : 0;
        GD.Print($"STAGEBATTLE {me.Name} vs {foe.Name} at {ViewStar.Name}: aWins={r.AWins} lostA={r.LostA:0.0} lostB={r.LostB:0.0} ships {mine.Ships:0}/{theirs.Ships:0} fam {Starforge.Sim.Battles.BestFamily(me)}/{Starforge.Sim.Battles.BestFamily(foe)}");
        if (OS.GetCmdlineUserArgs().Contains("--stagelayers")) StageMapLayers(me, foe);
    }

    /// <summary>Test: the home system gets a war economy to draw (six foundries, a hull half built on each slip, three
    /// barracks classes, two special-forces courses, three armies on the ground).</summary>
    void StageWarWorks()
    {
        var me = Sim.Humanity; var home = me.Colonies.FirstOrDefault(c => c.Home);
        if (home == null) { GD.Print("--stagewarworks: no home system"); return; }
        home.Foundries = Math.Max(home.Foundries, 6);
        int slips = Sim.SlipsOf(home);
        while (me.Yard.Count < slips) me.Yard.Insert(0, new Slip());
        for (int i = 0; i < slips; i++) { me.Yard[i].Hull = Math.Max(1, me.Hull - i); me.Yard[i].Progress = 0.3 + 0.3 * i; }
        for (int i = 0; i < 3; i++) me.Barracks.Add(new Training { Troops = 1, Left = 5, At = home.Star.Id });
        for (int i = 0; i < 2; i++) me.Academy.Add(new Training { Troops = 1, Left = 5, At = home.Star.Id });
        for (int i = 0; i < 3; i++) Sim.Armies.Add(new Army { Id = Sim.NextArmyId++, Civ = me.Id, At = home.Star.Id, Troops = 1e6, Fleet = -1 });
        GD.Print($"STAGEWARWORKS foundries={home.Foundries} slips={slips} hulls={string.Join(",", me.Yard.Take(slips).Select(y => y.Hull))}");
    }

    /// <summary>Test: the nearest systems beyond this one become occupied, blockaded and both, and two fleets of the player's
    /// appear there (one under way with an army aboard, one in port), so every galaxy-map war marker can be seen at once.</summary>
    void StageMapLayers(Civ me, Civ foe)
    {
        var near = Sim.Colonies.Where(c => c.Star.Pos.DistanceTo(ViewStar.Pos) > 8).OrderBy(c => c.Star.Pos.DistanceSquaredTo(ViewStar.Pos)).ToList();
        if (near.Count < 6) { GD.Print("--stagelayers: too few systems nearby"); return; }
        near[0].OccupiedBy = foe.Id; near[1].BlockadedUntil = Sim.Year + 5; near[2].OccupiedBy = foe.Id; near[2].BlockadedUntil = Sim.Year + 5;
        var moving = new Fleet { Id = Sim.NextFleetId++, Civ = me.Id, At = near[3].Star.Id, To = near[4].Star.Id, Depart = Sim.Year - 0.1, Arrive = Sim.Year + 0.2, Name = "Staged" };
        moving.Add(0, 5000, 0); Sim.Fleets.Add(moving);
        Sim.Armies.Add(new Army { Id = Sim.NextArmyId++, Civ = me.Id, At = near[3].Star.Id, Troops = 1e6, Fleet = moving.Id });
        var idle = new Fleet { Id = Sim.NextFleetId++, Civ = me.Id, At = near[5].Star.Id, Name = "Staged in port" };
        idle.Add(0, 1e6, 0); Sim.Fleets.Add(idle);
        GD.Print($"STAGELAYERS occupied={near[0].Star.Name} blockaded={near[1].Star.Name} both={near[2].Star.Name} moving {near[3].Star.Name}->{near[4].Star.Name} with army, in port {near[5].Star.Name}");
    }

    static float? _battleRunTo;
    static float BattleRunTo() => _battleRunTo ??= Array.Find(OS.GetCmdlineUserArgs(), x => x.StartsWith("--battlerun=")) is string s ? F(s[12..]) : 0;
    float _battleRunT = -1;

    public void OnLabelClicked(object key)
    {
        switch (key)
        {
            case Body b: SelectBody(b); break;
            case "star": SelectStar(); break;
            case "core": ToCore(); break;
            case "visitor" when SysView.Spectacle.Visitor != null:
                SelectedBody = null;
                var vn = SysView.Spectacle.Visitor;
                _rig.FlyTo(() => IsInstanceValid(vn) ? SysView.Offset + vn.Position : D3.Zero, 0.6f, 0.17f); break;
            case "second" when SysView.Sys.Second != null:
                SelectedBody = null;
                var sec = SysView.Sys.Second;
                _rig.FlyTo(() => sec.Pos(VisYear), sec.Radius * 6f, sec.Radius * 1.3f); break;
            case string mk when mk.StartsWith("mega:"):
            {
                var mega = SysView.Works.Mega;
                string mname = mk[5..];
                var hit = mega.Active.Find(a => a.Name == mname);
                if (hit.Name == null) break;
                SelectedBody = null;
                _rig.FlyTo(() => { var a = mega.Active.Find(x => x.Name == mname); return a.Name == null ? D3.Zero : a.Pos; }, hit.Size * 3.5f, hit.Size * 0.4f);
                break;
            }
            case Galaxy.Landmark lm: ToLandmark(lm); break;
            case StarInfo s: TravelTo(s); break;
            case Civ c when c.Colonies.Count > 0:
                var home = c.Colonies.OrderBy(x => x.Founded).First();
                TravelTo(home.Star); break;
        }
    }

    /// <summary>Stars worth naming on screen: colonies and the famous neighbours, nearest first.</summary>
    List<StarInfo> _labelCache = new();
    double _labelT = -1;
    public IEnumerable<StarInfo> LabelStars()
    {
        // re-ranked a few times a second; the camera moves, the set barely changes
        double now = Time.GetTicksMsec() / 1000.0;
        if (now - _labelT > 0.3)
        {
            _labelT = now;
            var camGal = ViewStar.Pos + Frame.Sys(Cam.GlobalPosition).F / G;
            _labelCache = _gal.Stars.Where(s => s.Owner >= 0 || s.Id < 40)
                .OrderBy(s => s.Pos.DistanceSquaredTo(camGal) / (s.Owner == Sim.Humanity.Id ? 4f : 1f)).Take(120).ToList();
        }
        return _labelCache;
    }

    /// <summary>Ride along behind a departing seed ship for its first minutes.</summary>
    void FollowLaunch()
    {
        var ship = SysView.Spectacle.Followed;
        if (ship == null) return;
        SelectedBody = null;
        _following = ship;
        float fd = 1.2f * ship.Scale.X * 0.5f;                   // a little over the ark's length away
        _rig.FlyTo(() => IsInstanceValid(ship) ? SysView.Offset + ship.Position : D3.Zero, fd, fd * 0.05f);      // star-relative, moved before the camera
        _directorT = -20f;
    }

    public bool MusicOn => _music != null && _music.Volume > 0;
    public void ToggleMusic() { if (_music != null) { _music.Volume = _music.Volume > 0 ? 0f : 0.8f; _hud.Toast(_music.Volume > 0 ? "Music on" : "Music off"); } }
    public bool LowQuality;
    public void ToggleQuality()
    {
        LowQuality = !LowQuality;
        GetViewport().Scaling3DScale = LowQuality ? 0.7f : 1f;
        GetViewport().Msaa3D = LowQuality ? Viewport.Msaa.Disabled : Viewport.Msaa.Msaa2X;
        _hud.Toast(LowQuality ? "Performance mode" : "Quality mode");
    }

    public void SetSpeed(int i) { SpeedIdx = Math.Clamp(i, 0, Speeds.Length - 1); }

    // ---------- Input ----------
    // ---------- boarding a station in first person ----------
    public Walker Aboard;
    int _speedBeforeBoard;
    /// <summary>Which station in the viewed system has an interior you can walk (the Outpost, for now).</summary>
    public bool CanBoard => Aboard == null && ViewColony != null && SysView.Works.Mega.Active.Exists(a => a.Name == "Outpost");

    public void Board()
    {
        if (!CanBoard) return;
        Aboard = Walker.Board(this, "outpost");
        if (Aboard == null) return;
        AddChild(Aboard);
        SysView.Visible = false; _overlay.Visible = false; _hud.Visible = false;
        _rig.UserControl = false;
        _speedBeforeBoard = SpeedIdx; SpeedIdx = 1;                  // time slows to a walk while you're aboard
        _music?.Sfx("whoosh");
    }

    public void Disembark()
    {
        if (Aboard == null) return;
        Aboard.QueueFree(); Aboard = null;
        SysView.Visible = true; _overlay.Visible = true; _hud.Visible = true;
        _rig.UserControl = true;
        _rig.Cam.MakeCurrent();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        SpeedIdx = _speedBeforeBoard;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (SysView == null || Aboard != null) return;
        if (e is InputEventKey k && k.Pressed && !k.Echo)
        {
            switch (k.Keycode)
            {
                case Key.Space: SpeedIdx = SpeedIdx == 0 ? 2 : 0; break;
                case Key.Equal: case Key.KpAdd: SetSpeed(SpeedIdx + 1); break;
                case Key.Minus: case Key.KpSubtract: SetSpeed(SpeedIdx - 1); break;
                case Key.G: ToGalaxy(); break;
                case Key.Tab: _rig.ToggleFree(); _hud.Toast(_rig.FreeFlight ? "Free flight · WASD fly · Q/E down/up · right-drag look · wheel speed · Shift ×10 · Ctrl ×0.1 · Tab to orbit" : "Orbit camera"); break;
                case Key.X: ToCore(); break;
                case Key.V: _hud.Visible = !_hud.Visible; break;
                case Key.M: ToggleMusic(); break;
                case Key.L: _hud.ShowLabels = !_hud.ShowLabels; break;
                case Key.P: _hud.TogglePowers(); break;
                case Key.J: Board(); break;
                case Key.N: _hud.ToggleBuild(); break;
                case Key.H: if (ViewStar != _gal.Sol) TravelTo(_gal.Sol); else SelectBody(SysView.Sys.Find("Earth")); break;
                case Key.C: _hud.ToggleChronicle(); break;
                case Key.E when !_rig.FreeFlight: _hud.ToggleEmpire(EmpireScreen.TabOverview); break;
                case Key.I: _hud.ToggleEmpire(EmpireScreen.TabInbox); break;
                // Paradox-style: F1-F4, F6-F8 and F10 open the empire screen's tabs, Ctrl+F or / finds a system
                case Key.F1: _hud.ToggleEmpire(EmpireScreen.TabOverview); break;
                case Key.F2: _hud.ToggleEmpire(EmpireScreen.TabPolicies); break;
                case Key.F3: _hud.ToggleEmpire(EmpireScreen.TabSystems); break;
                case Key.F4: _hud.ToggleEmpire(EmpireScreen.TabInbox); break;
                case Key.F6 when k.ShiftPressed: ExportHistory(); break;
                case Key.F6: _hud.ToggleEmpire(EmpireScreen.TabWars); break;
                case Key.F7: _hud.ToggleEmpire(EmpireScreen.TabMilitary); break;
                case Key.F8: _hud.ToggleEmpire(EmpireScreen.TabEconomy); break;
                case Key.F10: _hud.ToggleEmpire(EmpireScreen.TabInterior); break;
                case Key.Slash: _hud.ToggleFind(); break;
                case Key.F when k.CtrlPressed: _hud.ToggleFind(); break;
                case Key.T: _hud.ToggleTech(); break;
                case Key.U: _hud.ToggleVoice(); break;
                case Key.F: Autoplay = !Autoplay; break;
                case Key.B: _overlay.ShowBorders = !_overlay.ShowBorders; break;
                case Key.F5: SaveNow(); break;
                case Key.F12: Screenshot(k.ShiftPressed); break;
                case Key.F9: if (FileAccess.FileExists(SavePath)) { PendingLoad = SavePath; GetTree().ReloadCurrentScene(); } break;
                case Key.Escape:
                    if (_hud.MenuOpen) _hud.HideMenu();
                    else if (_hud.EmpireOpen) _hud.CloseEmpire();
                    else { _speedBeforeMenu = SpeedIdx == 0 ? 2 : SpeedIdx; SpeedIdx = 0; _hud.ShowMenu(false); }
                    break;
                case Key.F11: DisplayServer.WindowSetMode(DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen); break;
                default:
                    if (k.Keycode >= Key.Key1 && k.Keycode <= Key.Key9)
                    {
                        int idx = (int)(k.Keycode - Key.Key1);
                        var list = SysView.Sys.Bodies;
                        if (idx < list.Count) SelectBody(list[idx]);
                    }
                    else if (k.Keycode == Key.Key0 || k.Keycode == Key.Backspace) SelectStar();
                    break;
            }
        }
        if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed) _pressPos = mb.Position;
            else if (mb.Position.DistanceTo(_pressPos) < 5) Pick(mb.Position);
        }
    }

    void Pick(Vector2 sp)
    {
        var cam = Cam;
        float best = float.MaxValue; Body pick = null;
        float pxPerRad = GetViewport().GetVisibleRect().Size.Y / (2 * Mathf.Tan(Mathf.DegToRad(cam.Fov / 2)));
        foreach (var bv in SysView.Bodies)
        {
            if (bv.B.Kind == BodyKind.Belt) continue;
            var wp = bv.TrueView;
            if (cam.IsPositionBehind(wp)) continue;
            float d = cam.GlobalPosition.DistanceTo(wp);
            float r = Mathf.Max(bv.B.Radius / d * pxPerRad, 10f);
            float dd = cam.UnprojectPosition(wp).DistanceTo(sp);
            if (dd < r && d < best) { best = d; pick = bv.B; }
        }
        if (pick != null) { SelectBody(pick); return; }
        var starV = Frame.Star;
        if (!cam.IsPositionBehind(starV) && cam.UnprojectPosition(starV).DistanceTo(sp) < Mathf.Max(SysView.Sys.StarRadius / cam.GlobalPosition.DistanceTo(starV) * pxPerRad, 14f)) SelectStar();
    }

    // ---------- The director ----------
    /// <summary>The director's sense of tempo: slow when history is dense, fast across the long quiet stretches.</summary>
    int PacedSpeed()
    {
        var h = Sim.HomeColony;
        int s;
        if (Sim.Humanity.Colonies.Count > 25) s = 6;
        else if (Sim.Humanity.Colonies.Count > 1 || Sim.Ships.Any(x => x.Civ.Main)) s = 5;
        else if (h.Capture > 1e-4) s = 4;
        else if (Sim.Year < 2072) s = 3;
        else s = 4;
        if (_slowT > 0) s = Math.Max(2, s - 2);
        return s;
    }

    readonly Random _tour = new();
    /// <summary>When nothing has happened for a while, the director finds something worth looking at.</summary>
    void IdleTour()
    {
        if (_rig.FreeFlight) return;                 // the director never takes the camera from someone flying it
        var mine = Sim.Humanity.Colonies;
        int pick = _tour.Next(10);
        if (pick < 3 && mine.Count > 1)
        {
            var newest = mine.OrderByDescending(c => c.Founded).Take(3).ElementAt(_tour.Next(Math.Min(3, mine.Count)));
            if (newest.Star != ViewStar) { TravelTo(newest.Star); return; }
        }
        if (pick < 4 && mine.Count > 6 && !InGalaxyView) { ToGalaxy(); return; }
        if (pick < 6 && ViewStar != _gal.Sol) { TravelTo(_gal.Sol); return; }
        if (InGalaxyView) { TravelTo(ViewStar); return; }
        var bodies = SysView.Sys.All().Where(b => b.Kind != BodyKind.Belt).ToList();
        var c0 = ViewColony;
        var interesting = bodies.Where(b => c0 != null && (c0.Settled.ContainsKey(b) || c0.Hubs.ContainsKey(b)) || b.Ring || b.Life).ToList();
        var list = interesting.Count > 0 ? interesting : bodies;
        // a third of the time, one of the system's megastructures
        var megas = SysView.Works.Mega.Active;
        if (megas.Count > 0 && _tour.Next(3) == 0) { OnLabelClicked("mega:" + megas[_tour.Next(megas.Count)].Name); return; }
        // the homeworld and the busiest worlds come up more often
        if (c0 != null && c0.Seat != null && _tour.Next(3) == 0) list = new() { c0.Seat };
        if (list.Count > 0)
        {
            var b = list[_tour.Next(list.Count)];
            var bv = SysView.Bodies.Find(x => x.B == b);
            // half the time, hang low over one of its cities and watch the traffic come and go
            var spinB = bv?.Spin.GlobalBasis.Orthonormalized() ?? Basis.Identity;
            var sunDir = bv == null ? Vector3.Zero : (SysView.GlobalPosition - bv.GlobalPosition).Normalized();
            if (bv != null && _tour.Next(2) == 0 && CloseUp.PickSite(c0, b, _tour, spinB, sunDir) is Vector3 site)
            {
                SelectBody(b, false, b.Radius * (1.3f + 0.5f * (float)_tour.NextDouble()));
                var d = (spinB * site).Normalized();
                _rig.YawT = _rig.Yaw + Mathf.Wrap(Mathf.Atan2(d.X, d.Z) + 0.1f - _rig.Yaw, -Mathf.Pi, Mathf.Pi);   // the short way round
                _rig.PitchT = Mathf.Clamp(-Mathf.Asin(d.Y) + 0.1f, -1.45f, 1.45f);
            }
            else SelectBody(b, false, b.Radius * (b.Ring ? 7f : 3.5f + (float)_tour.NextDouble() * 2f));
        }
        else SelectStar();
    }

    void OnLogged(LogEntry e)
    {
        if (e.Civ.Main && e.Text.StartsWith("Breakthrough: Transcendence") && !Warping)
        {
            _overlay.TranscendT = 0f; _music?.Sfx("shimmer");
            if (Autoplay) { ToGalaxy(); _rig.PitchT = -1.25f; _directorT = -40f; }
            return;
        }
        if (!Autoplay || Warping || _cine.Busy || _rig.FreeFlight) return;
        if (e.Where is string w && w == "visitor" && e.Colony?.Star == ViewStar) { OnLabelClicked("visitor"); _directorT = -15f; return; }
        if (e.Text.StartsWith("Breakthrough: Galactic Core Tap")) { ToCore(); _directorT = -40f; return; }
        if (e.Text.StartsWith("Breakthrough: Andromeda Ark") || e.Text.StartsWith("Type III")) { ToGalaxy(); _directorT = -40f; return; }
        // slow down only for history you can see: events here, or humanity-wide turning points
        bool here = e.Colony?.Star == ViewStar;
        bool turning = e.Civ.Main && (e.Kind == "omen" || e.Kind == "god" || e.Kind == "era" && (e.Colony == null || e.Colony.Home));
        if ((here && e.Kind is "era" or "omen" or "god" or "loss") || turning) _slowT = Mathf.Max(_slowT, here ? 8f : 5f);
        if (e.Colony == null || e.Colony.Star != ViewStar)
        {
            return;
        }
        if (_directorT < 0f) return;   // holding on a big moment
        if (_directorT < 6f && e.Kind is "council" or "event") return;
        if (e.Where is Body b) { SelectBody(b, false, b.Radius * (b.Ring ? 9f : 5f)); _directorT = 0; }
        else if (e.Kind is "era" or "omen" or "break") { SelectStar(); _directorT = 0; }
    }

    public override void _Process(double delta)
    {
        if (SysView == null) return;
        float dt = (float)delta;
        Sim.CommandEnabled = !Warping;
        var cmd = Sim.Command;
        cmd?.Tick(dt);
        bool battleLive = cmd != null && !cmd.Paused && !cmd.Over;
        if (cmd != null && SysView.Works != null && SysView.Works.BattleHere(out _, out float battleR)) _rig.MaxDist = battleR * Works.BattleFrame * 2.5f;
        if (_simFaulted && SpeedIdx != 0) { SpeedIdx = 0; _hud.Toast("The simulation stopped on an error. Load a save (F9) to go on."); }
        double speed = Sim.Holding || _simFaulted ? 0 : Speeds[SpeedIdx];
        // keep a frame budget: huge speeds are sliced, never stall the render loop
        double want = speed * dt;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        double done = 0;
        while (done < want && sw.ElapsedMilliseconds < 12)
        {
            double step = Math.Min(want - done, 1.0);
            try { Sim.Step(step); }
            catch (Exception ex) { SimCrashed(ex); break; }
            done += step;
        }
        long tSim = sw.ElapsedTicks;
        if (!Sim.Holding) VisYear += Math.Min(speed, 0.25) * dt;
        if (Math.Abs(VisYear - Sim.Year) > 50) VisYear = Sim.Year;

        // animation runs on the game's clock: when time stops, so does everything but the camera
        float vdt = SpeedIdx == 0 && !battleLive ? 0f : dt;
        Anim = SpeedIdx == 0 && !battleLive ? 0f : 1f;
        SysView.Sync(ViewColony, Sim);
        SysView.VisYear = VisYear;
        SysView.Prepare(vdt);
        _oldView?.Prepare(vdt);
        long tPrep = sw.ElapsedTicks;
        using (Prof.T("cine")) _cine.Step(dt);
        // the camera moves next and re-centres the world on what it looks at; everything else places itself after
        _rig.Step(dt);
        _galView.Position = GalWorld(Vector3.Zero);
        long tCam = sw.ElapsedTicks;
        SysView.Update(VisYear, vdt, _rig.Cam);
        long tUpd = sw.ElapsedTicks;
        if (_oldView != null)
        {
            _oldView.Update(VisYear, vdt, _rig.Cam);
            if (_rig.FlyT >= 1f || _rig.Cam.GlobalPosition.DistanceTo(_oldView.GlobalPosition) > 1e9f) { _oldView.QueueFree(); _oldView = null; }
        }
        float zoomGal = _rig.Cam.GlobalPosition.DistanceTo(Frame.Star) / G;
        _galView.CoreTap = Sim.Humanity.Techs.Contains("E9") ? 1f : 0f;
        _galView.Tune(zoomGal, G, _rig.SkyK);
        _overlay.Tick(dt);
        if (_traceHitch && (sw.ElapsedMilliseconds > 10 || dt > 0.025f))
        {
            double ms(long t) => t * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            GD.Print($"HITCH dt={dt * 1000:0}ms process={sw.ElapsedMilliseconds}ms sim={ms(tSim):0.0} prepare={ms(tPrep - tSim):0.0} cine+cam={ms(tCam - tPrep):0.0} update={ms(tUpd - tCam):0.0} rest={ms(sw.ElapsedTicks - tUpd):0.0} cine={_cine.Playing} gc={GC.CollectionCount(0) - _gc0}/{GC.CollectionCount(1) - _gc1}/{GC.CollectionCount(2) - _gc2} | {Prof.Flush()}");
        }
        else if (_traceHitch) Prof.Flush();
        if (_traceHitch) { _gc0 = GC.CollectionCount(0); _gc1 = GC.CollectionCount(1); _gc2 = GC.CollectionCount(2); }
        FlyToPendingMega(dt);
        FlyToPendingBattle(dt);
        // camera speed in light-years per second drives the streak effect
        var camGal = ViewStar.Pos + Frame.Sys(_rig.Cam.GlobalPosition).F / G;
        float lyps = dt > 0 ? camGal.DistanceTo(_lastCamGal) * Galaxy.LyPerUnit / dt : 0;
        _lastCamGal = camGal;
        if (_rig.FlyT >= 1f) _traveling = false;
        float targetWarp = _traveling ? Mathf.Clamp((Mathf.Log(lyps + 1f) / Mathf.Log(10f) - 0.5f) / 2.5f, 0f, 0.85f) : 0f;
        _warpFx = Mathf.Lerp(_warpFx, targetWarp, 1f - Mathf.Exp(-dt * 6f));
        _post.Warp = _warpFx;
        _post.Update(_rig.Cam, SysView, dt);
        _moodT += dt;
        _autosaveT += dt;
        // the autosave takes its snapshot here and writes it on a worker (one at a time); never in the middle of a
        // cinematic, which waits for it to end
        if (_autosave is { IsCompleted: true })
        {
            if (_autosave.IsFaulted)
            {
                string why = _autosave.Exception?.GetBaseException().Message;
                GD.PrintErr("autosave failed: " + why); _hud.Toast("Autosave failed: " + why);
            }
            else _hud.Toast("Autosaved");
            _autosave = null;
        }
        if (_autosaveT > 300f && _autosave == null && !_cine.Busy && !Sim.Holding && !_simFaulted && _shot == null && _seqN == 0)
        { _autosaveT = 0; _autosave = SaveGame.SaveInBackground(Sim, _seed, AutoPath); }
        if (_music != null && _moodT > 5f) { _moodT = 0; _music.Mood = Music.MoodFor(Sim); }

        if (_following != null && !IsInstanceValid(_following)) { _following = null; SelectStar(); }
        if (_pendingArrival != null && _rig.FlyT >= 1f && !_traveling)
        {
            if (_pendingArrival.Star == ViewStar) { SysView.Spectacle.Play("arrival", _pendingArrival, _pendingArrival.Seat); FollowLaunch(); }
            _pendingArrival = null;
        }
        if (Autoplay)
        {
            _directorT += dt; _rig.AutoOrbit = true;
            _slowT -= dt;
            if (SpeedIdx > 0 && _directorT > 1f && !_hud.MenuOpen && !_cine.Busy) SpeedIdx = PacedSpeed();
            if (_directorT > 28f && !_cine.Busy && _following == null && !_hud.MenuOpen && _rig.FlyT >= 1f) { IdleTour(); _directorT = 0; }
        }
        else _rig.AutoOrbit = false;

        if (_forceProject != null && ViewColony != null) { var pp = _forceProject.Split(':'); ViewColony.Project = pp[0]; ViewColony.ProjectP = pp.Length > 1 ? F(pp[1]) : 1; ViewColony.ProjectDone = ViewColony.ProjectP >= 1; }
        if (_frame == 5 && OS.GetCmdlineUserArgs().Contains("--visitor")) { Sim.HomeColony.VisitorT = Sim.Year - 5; Sim.HomeColony.VisitorStage = 1; }
        if (_frame == 30 && OS.GetCmdlineUserArgs().Contains("--visitor")) OnLabelClicked("visitor");
        if (_frame == 10 && OS.GetCmdlineUserArgs().Contains("--open=chron")) _hud.ToggleChronicle();
        // tests: --open=empire|policies|systems|inbox|wars|military|economy|interior opens the empire screen; --manual=all|Research,Colonisation,... takes areas into the player's hands
        if (_frame == 10 && Array.Find(OS.GetCmdlineUserArgs(), x => x.StartsWith("--open=")) is string op && Array.IndexOf(new[] { "empire", "policies", "systems", "inbox", "wars", "military", "economy", "interior" }, op[7..]) is int tab and >= 0)
            _hud.ToggleEmpire(tab);
        // test: --stageunrest spreads grievance across the stages in four of the player's systems, two with a movement
        // (skipped for a hive), and holds those values every frame after, so a later shot still shows them
        if (_frame >= 5 && OS.GetCmdlineUserArgs().Contains("--stageunrest"))
        {
            var me = Sim.Humanity;
            if (me.M.Hive) { if (_frame == 5) GD.Print("--stageunrest skipped: humanity is a hive"); }
            else
            {
                var pick = me.Colonies.Where(x => !x.Home).Take(4).ToList();
                float[] gv = { 30, 50, 70, 90 }; int[] st = { 1, 2, 3, 4 };
                for (int i = 0; i < pick.Count; i++)
                {
                    var c = pick[i]; c.Grievance = gv[i]; c.Unrest = st[i];
                    if (i >= 2 && c.Movement == null) { c.CauseKey ??= "poverty"; c.Cause ??= "it is poor beside the core's wealth"; Sim.Rise(c); }
                }
            }
        }
        // test: --stageslips holds the player's first slips waiting for materiel and sabotages the second shipyard system,
        // so the Military and Economy tabs show both hold-ups (held every frame: the sim recomputes Waiting each step)
        if (_frame >= 5 && OS.GetCmdlineUserArgs().Contains("--stageslips"))
        {
            var me = Sim.Humanity;
            for (int i = 0; i < Math.Min(3, me.Yard.Count); i++) me.Yard[i].Waiting = true;
            var yards = me.Colonies.Where(c => Sim.SlipsOf(c) > 0).Skip(1).FirstOrDefault();
            if (yards != null && yards.YardDownUntil <= Sim.Year) yards.YardDownUntil = Sim.Year + 5;
            if (_frame == 5 && yards == null) GD.Print("--stageslips: only one shipyard system, nothing to sabotage");
        }
        // test: --intpage=N shows page N (from 1) of the Interior tab
        if (_frame == 12 && Array.Find(OS.GetCmdlineUserArgs(), x => x.StartsWith("--intpage=")) is string ip && int.TryParse(ip[10..], out int pg)) _hud.EmpireInteriorPage(pg - 1);
        // test: --stagebattle puts Humanity's Home Fleet and the nearest alien fleet at the view star, at war, fights once and pauses
        if (_frame == 5 && (OS.GetCmdlineUserArgs().Contains("--stagebattle") || OS.GetCmdlineUserArgs().Contains("--stagecommand"))) StageBattle();
        if (_battleRunT >= 0 && (_battleRunT += dt) >= BattleRunTo()) { SpeedIdx = 0; _battleRunT = -1; }
        // test: --focus=battle frames the battle here (once the fleets have been drawn)
        if (_frame == 8 && OS.GetCmdlineUserArgs().Contains("--focus=battle"))
        {
            if (SysView.Works.BattleHere(out var bc, out float br))
            {
                SelectedBody = null;
                _rig.Target = BattleTarget(bc); _rig.MinDist = br * 0.2f;
                string Arg(string k) => Array.Find(OS.GetCmdlineUserArgs(), x => x.StartsWith(k))?[k.Length..];
                var v = SysView.Works.BattleView;
                _rig.SetView(Arg("--yaw=") is string y ? F(y) : Mathf.Atan2(v.X, v.Z), Arg("--pitch=") is string pt ? F(pt) : Works.BattlePitch, Arg("--dist=") is string d ? F(d) : br * Works.BattleFrame);
                GD.Print($"FOCUSBATTLE yaw={_rig.Yaw:0.00} (the player's side; +3.14 for the far side) radius={br}");
            }
            else GD.Print("--focus=battle: no battle here");
        }
        // test: --stagewarworks gives the home system a war economy to look at: foundries, a hull on each slip, barracks
        // classes, special-forces courses and armies on the ground
        if (_frame == 5 && OS.GetCmdlineUserArgs().Contains("--stagewarworks")) StageWarWorks();
        // test: --focus=works:KIND (factory, slip, barracks, specops, army) frames the first of that kind of war works here
        if (_frame == 8 && Array.Find(OS.GetCmdlineUserArgs(), x => x.StartsWith("--focus=works:")) is string fw)
        {
            if (SysView.Works.WorksHere(fw[14..], out var wc, out float ws))
            {
                SelectedBody = null;
                var works = SysView.Works; string kind = fw[14..];
                _rig.Target = () => IsInstanceValid(works) && works.WorksHere(kind, out var c, out _) ? c : wc; _rig.MinDist = ws * 0.2f;
                string Arg(string k) => Array.Find(OS.GetCmdlineUserArgs(), x => x.StartsWith(k))?[k.Length..];
                _rig.SetView(Arg("--yaw=") is string y ? F(y) : 0.6f, Arg("--pitch=") is string pt ? F(pt) : 0.35f, (Arg("--dist=") is string d ? F(d) : 1.6f) * ws);
            }
            else GD.Print($"--focus=works: no {fw[14..]} here");
        }
        // test: --scroll=N scrolls the open empire tab down N px
        if (_frame == 30 && Array.Find(OS.GetCmdlineUserArgs(), x => x.StartsWith("--scroll=")) is string scr) _hud.ScrollEmpire(int.Parse(scr[9..]));
        if (_frame == 2 && Array.Find(OS.GetCmdlineUserArgs(), x => x.StartsWith("--manual=")) is string mn)
            foreach (var ar in Enum.GetValues<Area>()) if (mn[9..] == "all" || mn[9..].Split(',').Contains(ar.ToString())) Sim.SetManual(Sim.Humanity, ar, true);
        // test: --decide answers every proposal with the council's own pick, at once and without pausing
        if (_frame == 2 && OS.GetCmdlineUserArgs().Contains("--decide"))
        {
            Sim.Humanity.Policy.PauseOn.Clear();
            int n = 0;
            Sim.Alerted += a => { if (a.Decision) { try { Sim.Decide(a, 0); } catch (Exception ex) { GD.PrintErr($"DECIDE FAILED {a.Title}: {ex}"); } if (++n % 10 == 1 || a.Key?.StartsWith("research:") == false) GD.Print($"DECIDE {Sim.Year:0} {a.Title} -> {a.Outcome}"); } };
        }
        if (_frame == 10 && OS.GetCmdlineUserArgs().Contains("--open=tech")) _hud.ToggleTech();
        if (_frame == 10 && OS.GetCmdlineUserArgs().Contains("--open=powers")) _hud.TogglePowers();
        if (_frame == 10 && OS.GetCmdlineUserArgs().Contains("--open=voice")) _hud.ToggleVoice();
        if (_frame == 30 && Array.Find(OS.GetCmdlineUserArgs(), x => x.StartsWith("--board")) is string bd)
        {
            // test: --board[=x,y,z,yaw,pitch]
            Board();
            var v = bd.Length > 8 ? bd[8..].Split(',') : null;
            if (Aboard != null && v != null) Aboard.CallDeferred(Walker.MethodName.Place, new Vector3(F(v[0]), F(v[1]), F(v[2])), F(v[3]), F(v[4]));
        }
        if (_frame == 10 && OS.GetCmdlineUserArgs().Contains("--open=build")) _hud.ToggleBuild();
        if (_frame == 20 && OS.GetCmdlineUserArgs().Contains("--free")) _rig.ToggleFree();
        if (_frame == 60 && Array.Find(OS.GetCmdlineUserArgs(), x => x.StartsWith("--crisis=")) is string cr) Narrative.StartCrisis(Sim.HomeColony, cr[9..]);     // test: a crisis now          // test: free flight from wherever the view starts
        if (_forceTerraform >= 0 && ViewColony != null && ViewColony.Home) ViewColony.TerraformP = _forceTerraform;
        if (_frame == 8 && SelectedBody != null && Array.Find(OS.GetCmdlineUserArgs(), x => x.StartsWith("--over=")) is string ov)
        {
            // hang the camera above a place on the selected world (test framing)
            var ll = ov[7..].Split(',');
            var bv = SysView.Bodies.Find(x => x.B == SelectedBody);
            var d = (bv.Spin.GlobalBasis.Orthonormalized() * CloseUp.LatLon(F(ll[0]), F(ll[1]))).Normalized();
            float tilt = ll.Length > 2 ? F(ll[2]) : 0f;
            _rig.SetView(Mathf.Atan2(d.X, d.Z) + tilt, -Mathf.Asin(d.Y) + tilt * 0.3f, _rig.Dist);
        }
        if (_frame == 8 && Array.Exists(OS.GetCmdlineUserArgs(), a => a.StartsWith("--gallery")))
        {
            // every hull class side by side, smallest to largest, beside the selected world
            var g = new Node3D();
            SysView.AddChild(g);
            var cb = _rig.Cam.GlobalBasis;
            g.GlobalTransform = new Transform3D(cb, _rig.Cam.GlobalPosition - cb.Z * 1.0f - cb.Y * 0.15f);
            // two rows (the first nine hulls, then the tech bible's eight); sizes compressed so the giants fit
            int gst = Array.Find(OS.GetCmdlineUserArgs(), a => a.StartsWith("--gallery=")) is string gs ? int.Parse(gs[10..]) : 0;
            for (int row = 0; row < 2; row++)
            {
                float x = 0;
                for (int i = row * 9; i < Math.Min(Ships.Classes, row * 9 + 9); i++)
                {
                    float sc = 0.034f * Mathf.Sqrt(Ships.ClassScale[i]);
                    x += sc * 1.25f;
                    g.AddChild(new MeshInstance3D { Mesh = Ships.Mesh(i, gst), Position = new Vector3(x - 0.72f, 0.12f - row * 0.24f, 0), Basis = (new Basis(Vector3.Up, 2.3f) * new Basis(Vector3.Right, 0.35f)).Scaled(Vector3.One * sc) });
                    x += sc * 1.25f;
                }
            }
        }
        if (_frame == 2 && Array.Find(OS.GetCmdlineUserArgs(), x => x.StartsWith("--build=")) is string bf) Megastructures.ForceProg = F(bf[8..]);
        if (_frame == 2 && Array.Find(OS.GetCmdlineUserArgs(), x => x.StartsWith("--mega=")) is string mg)
            foreach (var (mid, _, _, _) in Megastructures.Catalogue) if (mg == "--mega=all" || mg[7..].Split(',').Contains(mid)) Megastructures.Force.Add(mid);
        if (_frame == 30 && Array.Find(OS.GetCmdlineUserArgs(), x => x.StartsWith("--megafocus=")) is string mf)
        {
            // frame one structure (test): --megafocus=Name_With_Underscores[,dist]
            var parts = mf[12..].Split(',');
            string mwant = parts[0].Replace('_', ' ');
            var mega = SysView.Works.Mega;
            var hit = mega.Active.FindIndex(a => a.Name == mwant);
            if (hit >= 0)
            {
                SelectedBody = null;
                _rig.Target = () => mega.Active.Find(a => a.Name == mwant).Pos;
                _rig.MinDist = mega.Active[hit].Size * 0.05f;
                _rig.SetView(_rig.Yaw, -0.35f, parts.Length > 1 ? F(parts[1]) * mega.Active[hit].Size : mega.Active[hit].Size * 3.5f);
            }
        }
        if (_frame == 25 && Array.Find(OS.GetCmdlineUserArgs(), x => x.StartsWith("--cast=")) is string cast)
            foreach (var k in cast[7..].Split(','))
            {
                // test: --cast=seed|ignite|shatter|nova|reveal|raise:id
                if (k.StartsWith("raise:")) { var id = k[6..]; RaiseHere(id, Megastructures.Catalogue.First(m => m.Id == id).Name); }
                else Cast(k);
            }
        if (_frame == 26 && Array.Find(OS.GetCmdlineUserArgs(), x => x.StartsWith("--voice=")) is string vo)
            foreach (var k in vo[8..].Split(','))
            {
                // test: --voice=kindle,snuff,forge:Terran,lane,seal,species,uplift,law:nowar,exalt,castdown,fall,possess:Industrious,gift:UMB-2,erase:X1,eraseall:X1,pin:X5,ban:X5,move
                var kv = k.Split(':');
                var civ = (ViewColony ?? Sim.HomeColony).Civ;
                var near = Sim.Gal.Stars[ViewStar.Near[3]];
                string r = kv[0] switch
                {
                    "kindle" => Sim.KindleStar(ViewStar.Pos).Name,
                    "snuff" => Sim.SnuffStar(ViewStar).ToString(),
                    "forge" => Sim.ForgeWorld(ViewSystem, Enum.Parse<BodyKind>(kv[1])).Name,
                    "lane" => Sim.OpenLane(ViewStar, near).ToString(),
                    "seal" => "sealed",
                    "species" => Sim.SeedSpecies(Sim.Gal.Stars.First(st => st.Owner < 0 && !st.Dead && st.Special == null && st.Pos.DistanceTo(ViewStar.Pos) < 30))?.Name ?? "none",
                    "uplift" => Sim.Colonies.FirstOrDefault(c => c.Natives.Count > 0) is { } nc ? Sim.UpliftNow(nc, nc.Natives.Keys.First())?.Name ?? "none" : "no natives",
                    "law" => "law",
                    "exalt" => Sim.Exalt(civ, "The voice lifts them.").ToString(),
                    "castdown" => "cast",
                    "fall" => "fall",
                    "possess" => "possess",
                    "gift" => Sim.GiftTech(civ, Defs.TechById(kv[1])).ToString(),
                    "erase" => Sim.EraseTech(civ, Defs.TechById(kv[1])).ToString(),
                    "eraseall" => Sim.EraseTech(null, Defs.TechById(kv[1])).ToString(),
                    "pin" => "pin", "ban" => "ban",
                    "move" => Sim.MoveStar(near, ViewStar.Pos + new Vector3(0.6f, 0, 0.4f)).ToString(),
                    _ => "?",
                };
                if (kv[0] == "seal") Sim.Quarantine(ViewStar);
                if (kv[0] == "law") Sim.RewriteLaw(kv[1], true);
                if (kv[0] == "castdown") Sim.CastDown(civ, false);
                if (kv[0] == "fall") Sim.CastDown(civ, true);
                if (kv[0] == "possess") Sim.Possess(civ, kv[1]);
                if (kv[0] == "pin") Sim.SteerPin(civ, Defs.TechById(kv[1]));
                if (kv[0] == "ban") Sim.SteerBan(civ, Defs.TechById(kv[1]), 50);
                GD.Print($"VOICE {k} -> {r}");
                WorldsChanged();
            }
        // test: --goto=Name flies to a world of this system, or to a star by name
        if (_frame == 30 && Array.Find(OS.GetCmdlineUserArgs(), x => x.StartsWith("--goto=")) is string gt)
        {
            var star = Sim.Gal.Stars.FirstOrDefault(st => st.Name == gt[7..].Replace('_', ' '));
            if (star != null) TravelTo(star); else SelectBody(SysView.Sys.Find(gt[7..]));
        }
        if (_frame == 40 && Array.Find(OS.GetCmdlineUserArgs(), x => x.StartsWith("--cine=")) is string cn)
        {
            // test: --cine=focus[,Title]  e.g. --cine=starbase,The_Swarm  --cine=body:Mars  --cine=mega:Orbital_Foundry
            var parts = cn[7..].Replace('_', ' ').Split(',');
            _cine.Request(new Cinematics.Moment { Focus = parts[0], Title = parts.Length > 1 ? parts[1] : null, Text = "", Colony = ViewColony ?? Sim.HomeColony, Priority = 9 });
        }
        if (_fx != null && _frame == 20) foreach (var k in _fx.Split(',')) { if (k == "transcend") { _overlay.TranscendT = 0f; ToGalaxy(); _rig.PitchT = -1.25f; continue; } SysView.Spectacle.Play(k, ViewColony, SelectedBody); if (k == "seedlaunch") FollowLaunch(); }
        if (_frame > 400 && Engine.GetFramesPerSecond() < 12 && !_slowSaved && OS.GetCmdlineUserArgs().Contains("--catchslow")) { _slowSaved = true; GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://shots/slow.png")); GD.Print($"CAUGHT slow at {Sim.Year:0.0} cam {_rig.Cam.GlobalPosition} dist {_rig.Dist} sel {SelectedBody?.Name} view {ViewStar.Name} following {_following != null}"); }
        if (_seqN > 0 || _shot == null) _frame++;
        if (_seqN > 0)
        {
            if (_frame % _seqEvery == 0)
            {
                int i = _frame / _seqEvery;
                GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_seqDir, $"seq_{i:000}.png"));
                GD.Print($"seq {i} year {Sim.Year:0.0} speed {SpeedNames[SpeedIdx]} view {ViewStar.Name} sel {SelectedBody?.Name}");
                if (i >= _seqN) GetTree().Quit();
            }
        }
        if (_shot != null && ++_frame == _shotFrame)
        {
            GetViewport().GetTexture().GetImage().SavePng(_shot);
            GD.Print($"FPS {Engine.GetFramesPerSecond()} year {Sim.Year:0.0}");
            GetTree().Quit();
        }
    }
}
