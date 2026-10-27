using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>Everything drawn over the world: clock, civilisation card, chronicle, controls, labels, title cards.</summary>
public partial class Hud : CanvasLayer
{
    Main _main;
    Simulation Sim => _main.Sim;
    Control _root;
    RichTextLabel _focusText;
    TopBar _top;
    Outliner _outliner;
    EmpireScreen _empire;
    Notifications _notes;
    FindBox _find;
    public void ToggleFind() => _find.Toggle();
    VBoxContainer _ticker;
    readonly List<(Label L, LogEntry E, double T)> _tick = new();
    Control _milestone; Label _msTitle, _msText; float _msT = -1;
    PanelContainer _chron, _tech, _focusPanel;
    HistoryChart _chart;
    RichTextLabel _chronText;
    GridContainer _techGrid;
    readonly List<Button> _speedBtns = new();
    Button _auto, _gift, _smite, _inspire, _fix, _direct, _protect, _guide, _peace, _powersBtn, _buildBtn;
    PanelContainer _powers, _build;
    Button _boardBtn;
    Label _powersHint, _buildHint;
    readonly Dictionary<string, Button> _powerBtns = new(), _buildBtns = new();

    static readonly (string Id, string Name, string Tip)[] Powers =
    {
        ("seed", "Seed Life", "Select a dead world: rain, then seas, then green. It becomes good to live on."),
        ("ignite", "Ignite", "Select a gas giant: set it burning as a second, smaller sun."),
        ("shatter", "Shatter", "Select a world: break it apart. Anyone living there dies."),
        ("nova", "Nova", "Make this system's star erupt. The swarm burns; the inner worlds are scoured."),
        ("reveal", "Revelation", "Hand them the answer to what they are researching now."),
        ("orbit", "Shift Orbit", "Select a world: walk it into the band where water stays liquid. Cold worlds start to thaw."),
        ("herald", "Herald Ship", "A crewed seed ship nobody built leaves for the nearest free star (or the one you drew them to)."),
        ("visitor", "Summon", "Call the visitor down on the homeworld now. What happens next is up to them."),
    };

    /// <summary>Cosmic powers and the megastructure catalogue, opened from the bottom bar (P and N).</summary>
    void BuildPowers()
    {
        _powers = Ui.Panel(0.6f);
        Ui.Pin(_powers, 0.5f, 1, new Vector2(0, -92));
        var v = new VBoxContainer();
        _powers.AddChild(v);
        var title = Ui.Label("COSMIC POWERS  ·  the voice is not limited; everything it does is witnessed", 12, Ui.Accent);
        v.AddChild(title);
        var h = new GridContainer { Columns = 4 };
        h.AddThemeConstantOverride("h_separation", 6);
        h.AddThemeConstantOverride("v_separation", 6);
        foreach (var (id, name, tip) in Powers)
        {
            var b = Ui.Button(name, tip);
            b.CustomMinimumSize = new Vector2(132, 34);
            string k = id;
            b.Pressed += () => _main.Cast(k);
            h.AddChild(b);
            _powerBtns[id] = b;
        }
        v.AddChild(h);
        _powersHint = Ui.Label("", 11, Ui.Dim);
        v.AddChild(_powersHint);
        _powers.Visible = false;
        _root.AddChild(_powers);

        _build = Ui.Panel(0.6f);
        Ui.Pin(_build, 0.5f, 1, new Vector2(0, -92));
        var bv = new VBoxContainer();
        _build.AddChild(bv);
        bv.AddChild(Ui.Label("RAISE A MEGASTRUCTURE  ·  any of them, in the system you are viewing", 12, Ui.Accent));
        var grid = new GridContainer { Columns = 5 };
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);
        foreach (var (id, name, _, blurb) in Megastructures.Catalogue)
        {
            if (id is "starbase" or "jumps") continue;
            var b = Ui.Button(name, blurb);
            b.CustomMinimumSize = new Vector2(176, 30);
            string k = id, n = name;
            b.Pressed += () => _main.RaiseHere(k, n);
            grid.AddChild(b);
            _buildBtns[id] = b;
        }
        bv.AddChild(grid);
        _buildHint = Ui.Label("", 11, Ui.Dim);
        bv.AddChild(_buildHint);
        _build.Visible = false;
        _root.AddChild(_build);
    }

    public void TogglePowers() { _powers.Visible = !_powers.Visible; if (_powers.Visible) _build.Visible = _voice.Visible = false; }
    public void ToggleBuild() { _build.Visible = !_build.Visible; if (_build.Visible) _powers.Visible = _voice.Visible = false; }

    void UpdatePowers()
    {
        _boardBtn.Visible = _main.CanBoard;
        _powersBtn.Modulate = _powers.Visible ? Ui.Accent : Colors.White;
        _buildBtn.Modulate = _build.Visible ? Ui.Accent : Colors.White;
        var c = _main.ViewColony;
        var b = _main.SelectedBody;
        if (_powers.Visible)
        {
            foreach (var (id, name, _) in Powers)
            {
                var btn = _powerBtns[id];
                bool target = id switch
                {
                    "seed" => b != null && Simulation.Seedable(b),
                    "ignite" => b != null && Simulation.Ignitable(b),
                    "shatter" => b != null && Sim.Shatterable(c, b),
                    "reveal" => Sim.CanReveal((c ?? Sim.HomeColony).Civ),
                    "orbit" => b != null && _main.ViewSystem != null && Simulation.Movable(_main.ViewSystem, b),
                    "herald" => Sim.CanHerald(c),
                    "visitor" => Sim.CanSummon(Sim.HomeColony),
                    _ => true,
                };
                btn.Disabled = !target;
            }
            _powersHint.Text = b == null ? $"Click a world to target it; Nova strikes {_main.ViewSystem?.Name}'s star."
                                         : $"Target: {b.Name}" + (c != null && b == c.Seat ? " (a capital cannot be shattered)" : "");
        }
        if (_build.Visible)
        {
            foreach (var (id, _, _, _) in Megastructures.Catalogue)
            {
                if (!_buildBtns.TryGetValue(id, out var btn)) continue;
                bool here = c != null && (c.Raised.Contains(id) || c.Civ.M.Megas.Contains(id));
                float bp = c?.BuildProgress(id) ?? 0f;
                btn.Disabled = c == null || here;
                btn.Modulate = !here ? Colors.White : bp >= 1f ? new Color(0.6f, 0.9f, 0.7f) : new Color(1f, 0.8f, 0.45f);
                var site = c != null && c.Civ.Sites.TryGetValue(id, out var st) ? st : null;
                btn.TooltipText = !here ? "" : bp >= 1f ? "Standing" : bp > 0f ? $"Under construction: {bp:P0}"
                    : site != null && site != c ? $"Being built at {site.Sys.Name}"
                    : site == null && !Colony.IsEverywhere(id) && c.Civ.M.Megas.Contains(id) ? "Planned: waiting for a suitable star (a quiet sunlike star for a ringworld, a massive one for a black hole)"
                    : "Planned: waiting for a free work crew";
            }
            _buildHint.Text = c == null ? "Nobody lives in this system: megastructures need a colony to serve."
                                        : $"Green: standing in {c.Sys.Name}. Amber: under construction or planned.";
        }
    }
    HBoxContainer _inspireRow;
    Control _labels;
    readonly Dictionary<object, Button> _labelBtns = new();
    float _uiT;
    float _sysOuter = 370f;
    readonly List<Control> _chrome = new();
    public void SetChrome(bool on) { foreach (var c in _chrome) c.Visible = on; _labels.Visible = on; }

    public void Setup(Main main)
    {
        _main = main;
        Layer = 10;
        _root = new Control { Theme = Ui.Theme(), MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        _labels = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _labels.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_labels);

        // the empire's numbers across the top, the outliner down the right, the empire screen on E
        _top = new TopBar();
        _top.Setup(main);
        _root.AddChild(_top);
        _chrome.Add(_top);
        _outliner = new Outliner();
        _outliner.Setup(main);
        _root.AddChild(_outliner);
        _chrome.Add(_outliner);
        _empire = new EmpireScreen();
        _empire.Setup(main);
        _top.OpenEmpire += () => ToggleEmpire(EmpireScreen.TabOverview);
        _top.OpenInbox += () => ToggleEmpire(EmpireScreen.TabInbox);
        _outliner.OpenAlert += a => _empire.Show(a);
        _notes = new Notifications();
        _notes.Setup(main);
        _root.AddChild(_notes);
        _chrome.Add(_notes);
        _notes.OpenAlert += a => _empire.Show(a);
        _find = new FindBox();
        _find.Setup(main);
        _outliner.OpenTech += ToggleTech;
        _outliner.OpenWar += ShowWar;
        _outliner.OpenInterior += () => OpenEmpire(EmpireScreen.TabInterior);
        Simulation.WorkName = id => Megastructures.Catalogue.FirstOrDefault(x => x.Id == id).Name ?? id;
        Sim.Alerted += OnAlert;
        BuildTicker();
        _chrome.Add(_ticker);
        BuildControls();
        BuildPowers();
        BuildFocus();
        BuildMilestone();
        BuildChronicle();
        BuildTech();
        _root.AddChild(_empire);
        Battle = new BattleCommandPanel();
        Battle.Setup(main);
        _root.AddChild(Battle);
        _root.AddChild(_find);
        Sim.Logged += OnLog;

    }

    public BattleCommandPanel Battle { get; private set; }
    public bool EmpireOpen => _empire.Visible;
    public void ToggleEmpire(int tab) { _empire.Toggle(tab); if (_empire.Visible) { _chron.Visible = false; } }
    /// <summary>Open the empire screen at a tab, leaving it open if it already shows that tab (a shortcut, not a toggle).</summary>
    public void OpenEmpire(int tab) { _empire.Open(tab); _chron.Visible = false; }
    public void ScrollEmpire(int px) => _empire.ScrollTab(px);
    public void EmpireInteriorPage(int page) => _empire.InteriorPageTo(page);
    public void CloseEmpire() => _empire.Visible = false;
    public void ShowWar(int warId) { _empire.ShowWar(warId); _chron.Visible = false; }

    /// <summary>An alert arrives: pause if the player asked to be stopped for this kind of news.</summary>
    void OnAlert(Alert a)
    {
        if (_main.Warping) return;
        if (Sim.Humanity.Policy.PauseOn.Contains(a.Cat) && _main.SpeedIdx > 0)
        {
            _main.SetSpeed(0);
            Toast($"Paused · {a.Title}{(a.Decision ? " · open the inbox (I) to decide" : "")}");
        }
        else if (a.Decision) Toast($"Decision waiting · {a.Title}");
    }

    // ---------- Chronicle ticker ----------
    void BuildTicker()
    {
        _ticker = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _ticker.CustomMinimumSize = new Vector2(720, 0);
        Ui.Pin(_ticker, 0, 1, new Vector2(24, -84));
        _ticker.Alignment = BoxContainer.AlignmentMode.End;
        _ticker.AddThemeConstantOverride("separation", 6);
        _root.AddChild(_ticker);
    }

    void OnLog(LogEntry e)
    {
        if (_main.Warping) return;
        var l = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(720, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        l.AddThemeFontSizeOverride("font_size", e.Kind is "era" or "omen" or "god" or "break" ? 16 : 14);
        string where = e.Colony != null && e.Colony != Sim.HomeColony ? e.System + " · " : "";
        l.Text = $"{(int)e.Year}   {where}{e.Text}";
        l.AddThemeColorOverride("font_color", Ui.KindColor(e.Kind));
        l.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
        l.AddThemeConstantOverride("shadow_offset_x", 1); l.AddThemeConstantOverride("shadow_offset_y", 1);
        _ticker.AddChild(l);
        _tick.Add((l, e, Time.GetTicksMsec() / 1000.0));
        while (_tick.Count > 5) { _tick[0].L.QueueFree(); _tick.RemoveAt(0); }
        if (_chron.Visible) AppendChron(e);
    }

    // ---------- Controls ----------
    void BuildControls()
    {
        var p = Ui.Panel(0.55f);
        Ui.Pin(p, 0.5f, 1, new Vector2(0, -30));
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 4);
        p.AddChild(h);
        for (int i = 0; i < Main.SpeedNames.Length; i++)
        {
            int k = i;
            var b = Ui.Button(Main.SpeedNames[i], i == 0 ? "Pause (Space)" : "Time speed (+/-)");
            b.CustomMinimumSize = new Vector2(i == 0 ? 54 : 46, 30);
            b.Pressed += () => _main.SetSpeed(k);
            h.AddChild(b);
            _speedBtns.Add(b);
        }
        h.AddChild(new VSeparator());
        _auto = Ui.Button("Director", "Autoplay: the camera follows the story and time paces itself (F)");
        _auto.ToggleMode = true;
        _auto.Toggled += on => _main.Autoplay = on;
        h.AddChild(_auto);
        h.AddChild(new VSeparator());
        _gift = Ui.Button("Comet", "Drop a rich comet into the system. Pushes them outward.");
        _gift.Pressed += () => Sim.Gift(_main.ViewColony ?? Sim.HomeColony);
        h.AddChild(_gift);
        _smite = Ui.Button("Flare", "Make the star flare. Burns the swarm, humbles the council.");
        _smite.Pressed += () => Sim.Smite(_main.ViewColony ?? Sim.HomeColony);
        h.AddChild(_smite);
        _inspire = Ui.Button("Dream", "Send a shared dream: steer what they research next.");
        _inspire.Pressed += () => _inspireRow.Visible = !_inspireRow.Visible;
        h.AddChild(_inspire);
        _fix = Ui.Button("Intervene", "End the current crisis.");
        _fix.Pressed += () => { var c = _main.ViewColony; if (c?.Crisis != null) Narrative.FixCrisis(c); };
        h.AddChild(_fix);
        _peace = Ui.Button("Peace", "Silence every war humanity is fighting.");
        _peace.Pressed += () => Sim.ImposePeace();
        h.AddChild(_peace);
        h.AddChild(new VSeparator());
        _powersBtn = Ui.Button("Powers", "Cosmic powers: seed life, ignite, shatter, nova, revelation (P)");
        _powersBtn.Pressed += TogglePowers;
        h.AddChild(_powersBtn);
        _buildBtn = Ui.Button("Build", "Raise any megastructure in the system you are viewing (N)");
        _buildBtn.Pressed += ToggleBuild;
        h.AddChild(_buildBtn);
        var voiceBtn = Ui.Button("Voice", "The voice's greater acts: stars, worlds, peoples, councils, laws (U)");
        voiceBtn.Pressed += ToggleVoice;
        h.AddChild(voiceBtn);
        _boardBtn = Ui.Button("Board", "Go aboard the Outpost in first person (J)");
        _boardBtn.Pressed += () => _main.Board();
        h.AddChild(_boardBtn);
        _root.AddChild(p);
        _chrome.Add(p);

        _inspireRow = new HBoxContainer { Visible = false };
        Ui.Pin(_inspireRow, 0.5f, 1, new Vector2(160, -86));
        foreach (var br in Defs.Branches)
        {
            var b = Ui.Button(br.Name, br.Dream);
            char id = br.Id;
            b.Pressed += () => { Sim.Inspire((_main.ViewColony ?? Sim.HomeColony).Civ, id); _inspireRow.Visible = false; };
            _inspireRow.AddChild(b);
        }
        _root.AddChild(_inspireRow);

        var keys = Ui.Label("Right-drag orbit · Wheel zoom · Tab free flight · Click a name to fly there · G galaxy · X core · H home · E empire · I inbox · F1–F4 empire tabs · Ctrl+F or / find · C chronicle · T technology · F director · P powers · N build · U voice · J board · V hide HUD · L labels · M music · Space pause", 11, new Color(0.5f, 0.56f, 0.62f, 0.8f));
        keys.HorizontalAlignment = HorizontalAlignment.Center;
        keys.CustomMinimumSize = new Vector2(900, 0);
        Ui.Pin(keys, 0.5f, 1, new Vector2(0, -8));
        _root.AddChild(keys);
        _chrome.Add(keys);
    }

    // ---------- Focus panel ----------
    void BuildFocus()
    {
        _focusPanel = Ui.Panel(0.5f);
        _focusPanel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _focusPanel.Position = new Vector2(22, 72);
        _focusPanel.CustomMinimumSize = new Vector2(300, 0);
        var v = new VBoxContainer();
        _focusPanel.AddChild(v);
        _focusText = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, CustomMinimumSize = new Vector2(290, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        v.AddChild(_focusText);
        var h = new HBoxContainer();
        _direct = Ui.Button("Point a constructor here", "The unseen voice: order them to mine this world.");
        _direct.Pressed += () => { var c = _main.ViewColony; if (c != null && _main.SelectedBody != null) Sim.DirectHub(c, _main.SelectedBody); };
        h.AddChild(_direct);
        _protect = Ui.Button("Protect them", "The unseen voice: forbid harm to this world's natives.");
        _protect.Pressed += () => { var c = _main.ViewColony; if (c != null && _main.SelectedBody != null) Sim.Protect(c, _main.SelectedBody); };
        h.AddChild(_protect);
        _guide = Ui.Button("Draw them here", "The next human seed ship in range will come to this star.");
        _guide.Pressed += () => Sim.Guide(_main.ViewStar);
        h.AddChild(_guide);
        v.AddChild(h);
        _root.AddChild(_focusPanel);
        _chrome.Add(_focusPanel);
    }

    PanelContainer _menu;
    VBoxContainer _menuButtons;
    Label _menuTitle, _menuSub;
    public bool MenuOpen => _menu != null && _menu.Visible;
    public bool ShowLabels = true;

    /// <summary>Title screen (first run) or pause menu (Esc).</summary>
    public void ShowMenu(bool title)
    {
        if (_menu == null)
        {
            _menu = Ui.Panel(0.72f);
            _menu.CustomMinimumSize = new Vector2(440, 0);
            Ui.Pin(_menu, 0.74f, 0.5f, Vector2.Zero);
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 10);
            _menu.AddChild(v);
            _menuTitle = Ui.Label("STARFORGE", 44, Colors.White, true);
            _menuTitle.HorizontalAlignment = HorizontalAlignment.Center;
            v.AddChild(_menuTitle);
            _menuSub = Ui.Label("", 14, Ui.Dim);
            _menuSub.HorizontalAlignment = HorizontalAlignment.Center;
            _menuSub.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _menuSub.CustomMinimumSize = new Vector2(400, 0);
            v.AddChild(_menuSub);
            _menuButtons = new VBoxContainer();
            _menuButtons.AddThemeConstantOverride("separation", 6);
            v.AddChild(_menuButtons);
            _root.AddChild(_menu);
        }
        foreach (var c in _menuButtons.GetChildren()) c.QueueFree();
        void Add(string text, string tip, Action a)
        {
            var b = Ui.Button(text, tip);
            b.CustomMinimumSize = new Vector2(400, 40);
            b.AddThemeFontSizeOverride("font_size", 17);
            b.Pressed += a;
            _menuButtons.AddChild(b);
        }
        if (title)
        {
            _menuSub.Text = "Earth, 2061. One world, one species, and the whole galaxy ahead of it. You are a voice they cannot locate.";
            Add("Begin", "Start in 2061. Press F any time to let the director tour the story for you", () => { HideMenu(); _main.BeginNew(); });
            string[] drives = { null, "Expansionist", "Contemplative", "Industrious" };
            string[] ethics = { null, "Reverent", "Pragmatic", "Ruthless" };
            int di = Array.IndexOf(drives, Simulation.StartDrive), ei = Array.IndexOf(ethics, Simulation.StartEthic);
            Add($"Their drive: {Simulation.StartDrive ?? "fate decides"}", "What humanity wants most. Changing it rebuilds the world.", () => { Simulation.StartDrive = drives[(di + 1) % drives.Length]; _main.Reseed(keepSeed: true); });
            Add($"Their conscience: {Simulation.StartEthic ?? "fate decides"}", "How much they are willing to destroy. It drifts with what they do.", () => { Simulation.StartEthic = ethics[(ei + 1) % ethics.Length]; _main.Reseed(keepSeed: true); });
            if (FileAccess.FileExists(Main.SavePath) || FileAccess.FileExists(Main.AutoPath))
                Add("Continue", "Load the most recent save", () => _main.LoadLatest());
            Add("New Galaxy", "A different galaxy, different neighbours, a different humanity", () => _main.Reseed());
            Add("Quit", null, () => GetTree().Quit());
            var cr = Ui.Label("Planet textures: Solar System Scope, CC BY 4.0", 10, new Color(0.5f, 0.55f, 0.6f));
            cr.HorizontalAlignment = HorizontalAlignment.Center;
            _menuButtons.AddChild(cr);
        }
        else
        {
            _menuSub.Text = $"{(int)Sim.Year} · paused";
            Add("Resume", null, HideMenu);
            Add("Save  (F5)", null, () => { _main.SaveNow(); HideMenu(); });
            if (FileAccess.FileExists(Main.SavePath)) Add("Load  (F9)", null, () => _main.LoadLatest());
            Add("Chronicle  (C)", null, () => { HideMenu(); ToggleChronicle(); });
            Add(_main.MusicOn ? "Music: on  (M)" : "Music: off  (M)", null, () => { _main.ToggleMusic(); ShowMenu(false); });
            Add(_main.Cine.Enabled ? "Cinematics: on" : "Cinematics: off", "Film the turning points of history (any key skips one)", () => { _main.Cine.Enabled = !_main.Cine.Enabled; ShowMenu(false); });
            Add(_main.LowQuality ? "Graphics: performance" : "Graphics: quality", "Performance mode renders the 3D view at 70% resolution", () => { _main.ToggleQuality(); ShowMenu(false); });
            Add("Technology  (T)", null, () => { HideMenu(); ToggleTech(); });
            Add("Quit to title", null, () => _main.Reseed(keepSeed: true));
            Add("Quit", null, () => GetTree().Quit());
        }
        _menu.Visible = true;
        _menu.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.02f, 0.03f, 0.05f, title ? 0.35f : 0.75f), new Color(0.55f, 0.72f, 0.9f, title ? 0.08f : 0.16f), 4, 14));
        SetChrome(!title);
    }

    public void HideMenu() { if (_menu != null) _menu.Visible = false; SetChrome(true); _main.MenuClosed(); }

    Label _toast; float _toastT;
    public void Toast(string text)
    {
        if (_toast == null)
        {
            _toast = Ui.Label("", 14, Ui.Accent);
            _toast.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _toast.HorizontalAlignment = HorizontalAlignment.Center;
            _toast.CustomMinimumSize = new Vector2(640, 0);
            Ui.Pin(_toast, 0.5f, 0, new Vector2(0, 108));
            _root.AddChild(_toast);
        }
        _toast.Text = text;
        _toastT = 2.5f + text.Length / 25f;
    }

    // ---------- Title cards ----------
    void BuildMilestone()
    {
        _milestone = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _milestone.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var v = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        v.CustomMinimumSize = new Vector2(900, 0);
        Ui.Pin(v, 0.5f, 0.4f, Vector2.Zero);
        _msBox = v;
        _msTitle = Ui.Label("", 54, Colors.White, true);
        _msTitle.HorizontalAlignment = HorizontalAlignment.Center;
        _msTitle.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        _msTitle.AddThemeConstantOverride("shadow_outline_size", 8);
        _msText = Ui.Label("", 19, new Color(0.85f, 0.9f, 0.96f));
        _msText.HorizontalAlignment = HorizontalAlignment.Center;
        _msText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _msText.CustomMinimumSize = new Vector2(900, 0);
        _msText.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        _msText.AddThemeConstantOverride("shadow_outline_size", 6);
        v.AddChild(_msTitle);
        v.AddChild(_msText);
        _milestone.AddChild(v);
        _root.AddChild(_milestone);
    }

    ColorRect _barTop, _barBot;
    Control _msBox;
    float _cinema, _cinemaT;
    readonly Dictionary<Control, bool> _wasVisible = new();
    /// <summary>Letterbox bars slide in and the interface steps aside while a cinematic plays.</summary>
    public void Cinema(bool on)
    {
        if (_barTop == null)
        {
            _barTop = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
            _barBot = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
            _root.AddChild(_barTop); _root.AddChild(_barBot);
        }
        _cinemaT = on ? 1 : 0;
        if (on) { _wasVisible.Clear(); foreach (var c in _chrome) { _wasVisible[c] = c.Visible; c.Visible = false; } _powers.Visible = _build.Visible = false; }
        else foreach (var (c, v) in _wasVisible) c.Visible = v;
    }
    void UpdateCinema(float dt)
    {
        if (_barTop == null) return;
        _cinema = Mathf.MoveToward(_cinema, _cinemaT, dt * 1.5f);
        var vp = GetViewport().GetVisibleRect().Size;
        float h = vp.Y * 0.11f * (_cinema * _cinema * (3 - 2 * _cinema));
        _barTop.Position = Vector2.Zero; _barTop.Size = new Vector2(vp.X, h);
        _barBot.Position = new Vector2(0, vp.Y - h); _barBot.Size = new Vector2(vp.X, h);
        _barTop.Visible = _barBot.Visible = h > 0.5f;
        _ticker.Visible = _cinema < 0.05f && !Battle.Active;
        // titles sit in the lower third during a cinematic, like a film's, and centre-screen otherwise
        Ui.Pin(_msBox, 0.5f, _cinemaT > 0 ? 0.76f : 0.4f, Vector2.Zero);
    }

    public void ShowMilestone(string title, string text)
    {
        if (_main.Warping) return;
        _msTitle.Text = title.ToUpper();
        _msText.Text = text;
        _msT = 0;
        _milestone.Visible = true;
        _milestone.Modulate = new Color(1, 1, 1, 0);
    }

    // ---------- Chronicle and technology panels ----------
    void BuildChronicle()
    {
        _chron = Ui.Panel(0.85f);
        _chron.CustomMinimumSize = new Vector2(820, 640);
        Ui.Pin(_chron, 0.5f, 0.5f, Vector2.Zero);
        _chron.Visible = false;
        var v = new VBoxContainer();
        _chron.AddChild(v);
        v.AddChild(Ui.Label("CHRONICLE", 22, Ui.Accent, true));
        _chart = new HistoryChart { CustomMinimumSize = new Vector2(800, 150), Sim = Sim };
        v.AddChild(_chart);
        // the label scrolls itself, so it can open on (and follow) the newest entry
        _chronText = new RichTextLabel { BbcodeEnabled = true, ScrollActive = true, ScrollFollowing = true, CustomMinimumSize = new Vector2(800, 440) };
        v.AddChild(_chronText);
        _root.AddChild(_chron);
    }

    void AppendChron(LogEntry e)
    {
        string where = e.Colony != null && e.Colony != Sim.HomeColony ? $"[color=#{Ui.Hex(Ui.Dim)}]{e.System} · [/color]" : "";
        _chronText.AppendText($"[color=#{Ui.Hex(Ui.Dim)}]{(int)e.Year}[/color]  {where}[color=#{Ui.Hex(Ui.KindColor(e.Kind))}]{e.Text}[/color]\n");
    }

    public void ToggleChronicle()
    {
        _chron.Visible = !_chron.Visible;
        _tech.Visible = false;
        if (_chron.Visible)
        {
            _chart.QueueRedraw();
            _chronText.Clear();
            foreach (var e in Sim.Chronicle.Skip(Math.Max(0, Sim.Chronicle.Count - 400))) AppendChron(e);
        }
    }

    TechScreen _techScreen;
    VoicePanel _voice;
    void BuildTech()
    {
        _techScreen = new TechScreen();
        _techScreen.Setup(_main);
        _root.AddChild(_techScreen);
        _tech = _techScreen;
        _voice = new VoicePanel();
        _voice.Setup(_main);
        _root.AddChild(_voice);
    }

    public void ToggleVoice() { _voice.Toggle(); if (_voice.Visible) { _powers.Visible = _build.Visible = false; } }

    public void ToggleTech()
    {
        _techScreen.Toggle();
        _chron.Visible = false;
    }

    // ---------- Per-frame ----------
    public override void _Process(double delta)
    {
        UpdateCinema((float)delta);
        ProcessInner(delta);
    }

    void ProcessInner(double delta)
    {
        float dt = (float)delta;
        _uiT += dt;
        if (_uiT > 0.25f)
        {
            _uiT = 0;
            UpdateFocus();
            UpdateButtons();
        }
        // fade ticker lines with age
        double now = Time.GetTicksMsec() / 1000.0;
        foreach (var (l, e, t) in _tick)
        {
            float age = (float)(now - t);
            float a = Mathf.Clamp(1.3f - age / 30f, 0.25f, 1f);
            l.Modulate = new Color(1, 1, 1, a);
        }
        // title card
        if (_msT >= 0)
        {
            _msT += dt;
            float a = _msT < 1.2f ? _msT / 1.2f : _msT < 8f ? 1 : 1 - (_msT - 8f) / 2f;
            _milestone.Modulate = new Color(1, 1, 1, Mathf.Clamp(a, 0, 1));
            if (_msT > 10f) { _msT = -1; _milestone.Visible = false; }
        }
        if (_toast != null) { _toastT -= dt; _toast.Modulate = new Color(1, 1, 1, Mathf.Clamp(_toastT, 0, 1)); }
        UpdateLabels();
    }

    public static string Sci(double v)
    {
        if (v <= 0) return "0";
        int e = (int)Math.Floor(Math.Log10(v));
        string sup = string.Concat(e.ToString().Select(ch => "⁰¹²³⁴⁵⁶⁷⁸⁹"[ch - '0']));
        return $"{v / Math.Pow(10, e):0.0}×10{sup}";
    }

    void UpdateButtons()
    {
        UpdatePowers();
        for (int i = 0; i < _speedBtns.Count; i++) _speedBtns[i].Modulate = i == _main.SpeedIdx ? Ui.Accent : Colors.White;
        _auto.ButtonPressed = _main.Autoplay;
        var c = _main.ViewColony;
        _fix.Visible = c?.Crisis != null;
        var b = _main.SelectedBody;
        _direct.Visible = c != null && b != null && c.Civ.Main && c.CanMine(b, true);
        _protect.Visible = c != null && b != null && c.Natives.TryGetValue(b, out var n) && !n.Protected && n.Contact != "cleared";
        _peace.Visible = Sim.Humanity.AtWar.Count > 0;
        var vs = _main.ViewStar;
        _guide.Visible = c == null && b == null && vs.Owner < 0 && Sim.Humanity.M.Seed;
        _guide.Disabled = !Sim.CanGuide(vs);
        if (Sim.Guided == vs) { _guide.Text = "They are coming"; _guide.Disabled = true; } else _guide.Text = "Draw them here";
    }

    void UpdateFocus()
    {
        var b = _main.SelectedBody;
        var c = _main.ViewColony;
        var sys = _main.ViewSystem;
        if (sys == null) { _focusPanel.Visible = false; return; }
        _focusPanel.Visible = !Battle.Active && !_main.InGalaxyView && !(MenuOpen && _labels.Visible == false) && _cinemaT <= 0;
        string dim = Ui.Hex(Ui.Dim);
        if (b == null)
        {
            var st = sys.Star;
            string owner = st.Owner >= 0 ? $"[color=#{Ui.Hex(Sim.Civs[st.Owner].Color)}]{Sim.Civs[st.Owner].Name}[/color]" : "unclaimed";
            string txt = $"[font_size=20][b]{sys.Name}[/b][/font_size]\n[color=#{dim}]{st.Cls} star · {st.Lum:0.###} L☉ · {sys.Bodies.Count} worlds[/color]\n{owner}";
            var dep = Sim.DepositsOf(st);
            txt += $"\n[color=#{dim}]{dep.Describe.Replace("\n", $"[/color]\n[color=#{Ui.Hex(Ui.Omen)}]")}[/color]";
            if (c != null)
            {
                if (!c.Home) txt += $"\n[color=#{dim}]Generation {c.Gen} · founded {(int)c.Founded}[/color]";
                txt += $"\nSwarm: {Pct(c.Capture)} of the star's light\nPower {Sci(c.Power)} W · K {c.Kardashev:0.000}\nPopulation {Pop(c.Pop)}";
                if (c.Habitats > 0) txt += $"\nHabitats: {c.Habitats}";
                if (c.ProjectName != null) txt += $"\n[color=#{Ui.Hex(Ui.Warm)}]{c.ProjectName}: {(c.ProjectDone ? "complete" : $"{c.ProjectP * 100:0}%")}[/color]";
                // the economy: who decides, how much the foundries can make, and what it is being spent on
                txt += $"\n[color=#{dim}]{c.Gov.Name} · stability {c.Stability * 100:0}%[/color]";
                txt += $"\n[color=#{dim}]Industry {Sci(c.IndustryKg)} kg/yr · {c.WorksShare * 100:0}% to great works[/color]";
                var building = c.BuildRate.Keys.Where(id => c.BuildProgress(id) < 1f).OrderBy(c.YearsLeft).Take(4).ToList();
                foreach (var id in building)
                {
                    double y = c.YearsLeft(id);
                    string name = Megastructures.Catalogue.FirstOrDefault(x => x.Id == id).Name ?? id;
                    string eta = double.IsInfinity(y) ? "stalled" : y > 2000 ? "millennia" : y < 1 ? "months" : $"~{y:0} yr";
                    txt += $"\n[color=#{Ui.Hex(Ui.Warm)}]Building {name} {c.BuildProgress(id) * 100:0}% · {eta}[/color]";
                }
                if (c.LabourBound) txt += $"\n[color=#{dim}]Limited by hands on site, not materials[/color]";
                if (c.BuildRate.Count > building.Count) txt += $"\n[color=#{dim}]and {c.BuildRate.Count - building.Count} more · {c.BuildSlots} crews[/color]";
            }
            _focusText.Text = txt;
            return;
        }
        string kind = b.Kind switch
        {
            BodyKind.Terran => "Terrestrial world", BodyKind.Ocean => "Ocean world", BodyKind.Desert => "Desert world", BodyKind.Lava => "Lava world",
            BodyKind.Ice => "Ice world", BodyKind.Gas => "Gas giant", BodyKind.IceGiant => "Ice giant", BodyKind.Belt => "Asteroid belt",
            BodyKind.Moon => $"Moon of {b.Parent?.Name}", _ => "Rocky world"
        };
        var lines = new List<string> { $"[font_size=20][b]{b.Name}[/b][/font_size]", $"[color=#{dim}]{kind} · {b.Mass:0.###} Earth masses[/color]" };
        if (c != null)
        {
            if (b == c.Seat && c.Home) lines.Add($"Cradle of humanity");
            if (c.Settled.TryGetValue(b, out float s)) lines.Add($"[color=#{Ui.Hex(Ui.Warm)}]Settled[/color] · {(s < 1 ? $"growing ({s * 100:0}%)" : "established")}");
            if (c.Hubs.TryGetValue(b, out int lvl) && lvl > 0) lines.Add($"[color=#{Ui.Hex(Ui.Warm)}]Mining works level {lvl}[/color]");
            if (b.Mined > 0.001f) lines.Add($"{b.Mined * 100:0.#}% disassembled");
            if (c.Stations.TryGetValue(b, out int st) && st > 0) lines.Add($"{st} orbital station{(st > 1 ? "s" : "")}");
            if (c.Natives.TryGetValue(b, out var n)) lines.Add($"[color=#{Ui.Hex(Ui.Omen)}]Inhabited: the {n.Name}[/color] · {n.Contact ?? "unaware"}{(n.Protected ? " · protected" : "")}");
            if (c.Rival != null && c.Rival.Planet == b) lines.Add($"[color=#{Ui.Hex(Ui.Danger)}]Held by the {c.Rival.Name}[/color]");
            if (b.Name == "Earth" && c.Home)
            {
                if (c.ElevatorP >= 0) lines.Add(c.ElevatorP >= 1 ? "Space elevator" : $"Space elevator: {c.ElevatorP * 100:0}%");
                if (c.RingP >= 0) lines.Add(c.RingP >= 1 ? "Orbital ring" : $"Orbital ring: {c.RingP * 100:0}%");
            }
            if (b.Name == "Mars" && c.TerraformP >= 0) lines.Add($"Terraforming: {c.TerraformP * 100:0}%");
            if (c.Missions.Any(m => m.Target == b)) lines.Add($"[color=#{Ui.Hex(Ui.Accent)}]Convoy en route[/color]");
        }
        if (b.Life) lines.Add($"[color=#{Ui.Hex(new Color(0.5f, 1f, 0.6f))}]Living biosphere[/color]");
        _focusText.Text = string.Join("\n", lines);
    }

    static string Pct(double v) => v <= 0 ? "0%" : v < 1e-6 ? "<0.0001%" : v < 1e-4 ? $"{v * 100:0.##e0}%" : v < 0.01 ? $"{v * 100:0.###}%" : $"{v * 100:0.#}%";
    static string Pop(double b) => b < 1 ? $"{b * 1000:0} million" : $"{b:0.0} billion";

    // ---------- World labels ----------
    void UpdateLabels()
    {
        _labels.Visible = ShowLabels && !(MenuOpen && !_chrome[0].Visible) && _cinemaT <= 0;
        if (!ShowLabels) return;
        var cam = _main.Cam;
        var wanted = new Dictionary<object, (Vector2 Pos, string Text, Color Col, float Alpha)>();
        var vp = GetViewport().GetVisibleRect().Size;
        if (!_main.InGalaxyView && _main.SysView != null)
        {
            var sv = _main.SysView;
            _sysOuter = sv.Sys.Bodies.Count > 0 ? sv.Sys.Bodies.Max(x => x.Orbit) : 300f;
            float camFromStar = cam.GlobalPosition.DistanceTo(sv.TrueStar);
            var bodyLabels = new List<(Body B, Vector2 Pos, Color Col, float Alpha, float Rank)>();
            foreach (var bv in sv.Bodies)
            {
                var b = bv.B;
                var wp = bv.TrueView;
                if (b.Kind == BodyKind.Belt) wp = sv.TrueStar + new Vector3(b.Orbit, 0, 0).Rotated(Vector3.Up, -0.6f);
                if (cam.IsPositionBehind(wp)) continue;
                float d = cam.GlobalPosition.DistanceTo(wp);
                float alpha;
                if (b.Parent != null)
                {
                    float pd = cam.GlobalPosition.DistanceTo(Frame.View(b.Parent.WorldPosD(_main.VisYear)));
                    alpha = Mathf.Clamp(1.5f - pd / (b.Parent.Radius * 25f), 0, 1);
                }
                else alpha = Mathf.Clamp(2.2f - camFromStar / (_sysOuter * 6f), 0, 1);
                if (d < b.Radius * 3f) alpha = Mathf.Min(alpha, Mathf.Clamp((d / b.Radius - 1.8f), 0, 1));
                if (alpha <= 0.02f) continue;
                var sp = cam.UnprojectPosition(wp);
                float rpx = b.Radius / Mathf.Max(d, 0.001f) * vp.Y / (2 * Mathf.Tan(Mathf.DegToRad(cam.Fov / 2)));
                var c = _main.ViewColony;
                Color col = c != null && c.Settled.ContainsKey(b) ? Ui.Warm : c != null && c.Hubs.ContainsKey(b) ? new Color(1f, 0.55f, 0.35f) : Ui.Dim;
                if (b == _main.SelectedBody) col = Colors.White;
                float rank = (b == _main.SelectedBody ? 1000 : 0) + (c != null && c.Settled.ContainsKey(b) ? 100 : 0) + (c != null && c.Hubs.ContainsKey(b) ? 50 : 0) + (b.Parent == null ? 20 : 0) + b.Radius;
                bodyLabels.Add((b, sp + new Vector2(0, Mathf.Clamp(rpx, 0, 300) + 6), col, alpha, rank));
            }
            // from far out the inner worlds bunch together: the one that matters most keeps its name, the rest wait to be zoomed apart
            // seen from far out the star's own name comes first (it is what the view is of)
            bool starWins = !cam.IsPositionBehind(sv.TrueStar) && camFromStar > _sysOuter * 1.6f;
            var keep = new List<Vector2>();
            if (starWins) keep.Add(cam.UnprojectPosition(sv.TrueStar) + new Vector2(0, 14));
            foreach (var l in bodyLabels.OrderByDescending(x => x.Rank))
            {
                if (keep.Any(q => Mathf.Abs(q.X - l.Pos.X) < 56 && Mathf.Abs(q.Y - l.Pos.Y) < 15)) continue;
                keep.Add(l.Pos);
                wanted[l.B] = (l.Pos, l.B.Name, l.Col, l.Alpha);
            }
            var vis = sv.Spectacle.Visitor;
            if (vis != null && !cam.IsPositionBehind(vis.GlobalPosition))
                wanted["visitor"] = (cam.UnprojectPosition(vis.GlobalPosition) + new Vector2(0, 18), "The visitor", Ui.Omen, 1f);
            if (sv.Sys.Second != null)
            {
                var p2 = sv.TrueStar + sv.Sys.Second.Pos(_main.VisYear);
                if (!cam.IsPositionBehind(p2))
                    wanted["second"] = (cam.UnprojectPosition(p2) + new Vector2(0, 14), sv.Sys.Second.Name, Ui.Text, Mathf.Clamp(2.2f - camFromStar / (_sysOuter * 6f), 0, 1));
            }
            foreach (var m in sv.Works.Mega.Active)
            {
                // megastructure names appear once you're inside the system and not right on top of them
                var mp = Frame.View(m.Pos);
                float dm = cam.GlobalPosition.DistanceTo(mp);
                if (cam.IsPositionBehind(mp) || dm < m.Size * 1.2f || dm > Mathf.Max(m.Size * 45f, 60f)) continue;
                wanted["mega:" + m.Name] = (cam.UnprojectPosition(mp) + new Vector2(0, 12), sv.Works.Mega.Building.TryGetValue(m.Name, out var bp) ? $"{m.Name} · {bp:P0} built" : m.Name, new Color(0.7f, 0.85f, 1f), Mathf.Clamp(1.6f - dm / Mathf.Max(m.Size * 30f, 40f), 0.25f, 0.85f));
            }
            if (!cam.IsPositionBehind(sv.TrueStar) && camFromStar > sv.Sys.StarRadius * 4)
            {
                float a = Mathf.Clamp(camFromStar / (_sysOuter * 1.6f) - 0.3f, 0, 1) * Mathf.Clamp(3f - camFromStar / (_sysOuter * 160f), 0, 1);
                var spStar = cam.UnprojectPosition(sv.TrueStar) + new Vector2(0, 14);
                if (a > 0.02f && (starWins || !keep.Any(q => Mathf.Abs(q.X - spStar.X) < 56 && Mathf.Abs(q.Y - spStar.Y) < 15))) wanted["star"] = (spStar, sv.Sys.Name, Ui.Text, a);
            }
        }
        // stars of the galaxy: colonies and famous neighbours, or whole empires when the view is wide
        float zoomGal = cam.GlobalPosition.DistanceTo(Frame.Star) / Main.G;
        var placed = wanted.Values.Select(v => v.Pos).ToList();
        bool Free(Vector2 p, float r) { foreach (var q in placed) if (q.DistanceSquaredTo(p) < r * r) return false; placed.Add(p); return true; }
        if (_main.GalaxyOutside > 0.35f)
        {
            foreach (var civ in Sim.Civs.Where(c => !c.Dead && c.Colonies.Count > 0))
            {
                var centre = Vector3.Zero;
                foreach (var col in civ.Colonies) centre += col.Star.Pos;
                centre /= civ.Colonies.Count;
                var wp = _main.GalWorld(centre);
                if (cam.IsPositionBehind(wp)) continue;
                var sp = cam.UnprojectPosition(wp);
                if (!Free(sp, 60)) continue;
                wanted[civ] = (sp, $"{Narrative2.Called(civ, true).ToUpper()}  ·  {civ.Colonies.Count}", civ.Color, Mathf.Clamp((_main.GalaxyOutside - 0.35f) * 3f, 0, 1));
            }
        }
        {
            var core = _main.CoreWorld();
            float dc = cam.GlobalPosition.DistanceTo(core) / Main.G;
            if (!cam.IsPositionBehind(core) && (zoomGal > 40f || dc < 30f) && dc > 1.2f)
            {
                var sp = cam.UnprojectPosition(core);
                if (Free(sp, 40)) wanted["core"] = (sp + new Vector2(0, 14), "Sagittarius A*", new Color(1f, 0.85f, 0.6f), 0.9f);
            }
        }
        foreach (var lm in _main.Gal.Landmarks)
        {
            var lw = _main.LandmarkWorld(lm);
            float dl = cam.GlobalPosition.DistanceTo(lw) / Main.G;
            // nebulae are galaxy-scale sights: inside a star system their names float over the planets as clutter
            if (cam.IsPositionBehind(lw) || dl < lm.Radius * 1.6f || dl > 600f || zoomGal < 0.02f) continue;
            var sp = cam.UnprojectPosition(lw);
            if (sp.X < 0 || sp.Y < 0 || sp.X > vp.X || sp.Y > vp.Y) continue;
            if (Free(sp, 30)) wanted[lm] = (sp + new Vector2(0, 12), lm.Name, new Color(1f, 0.7f, 0.8f), Mathf.Clamp(1.2f - dl / 600f, 0.3f, 0.9f));
        }
        if (zoomGal > 0.02f && zoomGal < 400f)
        {
            int shown = 0;
            foreach (var s in _main.LabelStars())
            {
                var wp = _main.StarWorld(s);
                if (cam.IsPositionBehind(wp)) continue;
                var sp = cam.UnprojectPosition(wp);
                if (sp.X < 0 || sp.Y < 0 || sp.X > vp.X || sp.Y > vp.Y) continue;
                float d = cam.GlobalPosition.DistanceTo(wp) / Main.G;
                float a = Mathf.Clamp(1.4f - d / (zoomGal * 1.6f + 0.5f), 0, 1) * Mathf.Clamp(zoomGal * 20f, 0, 1) * Mathf.Clamp((400f - zoomGal) / 200f, 0, 1);
                if (s == _main.ViewStar && zoomGal < 0.05f) continue;
                if (a < 0.05f) continue;
                if (!Free(sp, 28)) continue;
                Color col = s.Owner >= 0 ? Sim.Civs[s.Owner].Color : Ui.Dim;
                wanted[s] = (sp + new Vector2(0, 9), s.Name, col, a);
                if (++shown > 40) break;
            }
        }
        foreach (var key in _labelBtns.Keys.ToList())
            if (!wanted.ContainsKey(key)) { _labelBtns[key].QueueFree(); _labelBtns.Remove(key); }
        foreach (var (key, v) in wanted)
        {
            if (!_labelBtns.TryGetValue(key, out var btn))
            {
                btn = new Button { Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
                btn.AddThemeFontSizeOverride("font_size", 12);
                btn.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
                btn.AddThemeConstantOverride("shadow_offset_x", 1); btn.AddThemeConstantOverride("shadow_offset_y", 1);
                var k2 = key;
                btn.Pressed += () => _main.OnLabelClicked(k2);
                if (key is Civ) btn.AddThemeFontSizeOverride("font_size", 15);
                _labels.AddChild(btn);
                _labelBtns[key] = btn;
            }
            btn.Text = v.Text;
            btn.AddThemeColorOverride("font_color", v.Col);
            btn.AddThemeColorOverride("font_hover_color", Colors.White);
            btn.Modulate = new Color(1, 1, 1, v.Alpha);
            btn.Size = btn.GetMinimumSize();
            btn.Position = v.Pos - new Vector2(btn.Size.X / 2, 0);
        }
    }
}

/// <summary>Humanity's Kardashev rating (blue) and number of systems (warm) over time.</summary>
public partial class HistoryChart : Control
{
    public Simulation Sim;

    public override void _Draw()
    {
        var tl = Sim.Timeline;
        var r = new Rect2(Vector2.Zero, Size);
        DrawRect(r, new Color(1, 1, 1, 0.03f));
        if (tl.Count < 2) return;
        double y0 = tl[0].Year, y1 = tl[^1].Year;
        float W = Size.X - 50, H = Size.Y - 22;
        float X(double y) => 40 + (float)((y - y0) / Math.Max(y1 - y0, 1) * W);
        // Kardashev 0.7 .. 3.2
        float KY(double k) => 6 + H - (float)((k - 0.7) / 2.5 * H);
        foreach (var (k, lbl) in new[] { (1.0, "I"), (2.0, "II"), (3.0, "III") })
        {
            DrawLine(new Vector2(40, KY(k)), new Vector2(40 + W, KY(k)), new Color(1, 1, 1, 0.08f));
            DrawString(Ui.Font, new Vector2(18, KY(k) + 4), lbl, HorizontalAlignment.Left, -1, 11, Ui.Dim);
        }
        int maxSys = Math.Max(1, tl.Max(t => t.Systems));
        float SY(int n) => 6 + H - (float)(Math.Log(1 + n) / Math.Log(1 + maxSys) * H);
        var ptsK = new Vector2[tl.Count];
        var ptsS = new Vector2[tl.Count];
        for (int i = 0; i < tl.Count; i++) { ptsK[i] = new Vector2(X(tl[i].Year), KY(tl[i].K)); ptsS[i] = new Vector2(X(tl[i].Year), SY(tl[i].Systems)); }
        DrawPolyline(ptsS, new Color(Ui.Warm, 0.7f), 1.5f, true);
        DrawPolyline(ptsK, Ui.Accent, 2f, true);
        DrawString(Ui.Font, new Vector2(40, Size.Y - 2), $"{(int)y0}", HorizontalAlignment.Left, -1, 11, Ui.Dim);
        DrawString(Ui.Font, new Vector2(40 + W - 30, Size.Y - 2), $"{(int)y1}", HorizontalAlignment.Left, -1, 11, Ui.Dim);
        DrawString(Ui.Font, new Vector2(48, 16), "Kardashev", HorizontalAlignment.Left, -1, 11, Ui.Accent);
        DrawString(Ui.Font, new Vector2(120, 16), $"systems (max {maxSys})", HorizontalAlignment.Left, -1, 11, Ui.Warm);
    }
}
