using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// Battle command: the controls over the commanded battle in the system view. Top bar (round, pause and speed, leave it to
/// the admirals), force list on the left, a marker over every unit, the order bar along the bottom and the selected unit's
/// card on the right. Input comes through Input Map actions in _UnhandledInput, so the UI has first call and keys can be
/// rebound: left-click or box-drag selects, right-click on the ground moves, right-click on an enemy focuses fire.
/// While a battle is open this panel takes the keys it uses before the rest of the game does.
/// </summary>
public partial class BattleCommandPanel : Control
{
    static readonly (string Action, Key Key, string Hint)[] Keys =
    {
        ("cmd_pause", Key.Space, "pause or go"), ("cmd_focus", Key.F, "focus fire"), ("cmd_formation", Key.G, "formation"),
        ("cmd_screen", Key.C, "screen"), ("cmd_retreat", Key.R, "retreat"), ("cmd_split", Key.X, "split"), ("cmd_merge", Key.M, "merge"),
        ("cmd_clear", Key.V, "doctrine"), ("cmd_all", Key.B, "select all"), ("cmd_cancel", Key.Escape, "cancel"), ("cmd_frame", Key.Home, "frame the battle"),
        ("cmd_follow", Key.Z, "follow selection"), ("cmd_speed_up", Key.Equal, "faster"), ("cmd_speed_down", Key.Minus, "slower"),
    };

    /// <summary>Register the battle command actions with their default keys, unless the project already defines them (rebinding keeps its own).</summary>
    static void RegisterActions()
    {
        foreach (var (action, key, _) in Keys)
        {
            if (InputMap.HasAction(action)) continue;
            InputMap.AddAction(action);
            InputMap.ActionAddEvent(action, new InputEventKey { Keycode = key });
        }
    }

    enum Pending { None, Move, Focus, Screen }

    Main _main;
    Simulation Sim => _main.Sim;
    CommandSession _s;
    Works Works => _main.SysView?.Works;
    PanelContainer _top, _left, _card, _result, _splitBar;
    HBoxContainer _orders;
    Label _title, _hint;
    ProgressBar _round;
    Button _go, _auto;
    readonly List<Button> _speeds = new();
    OptionButton _form;
    VBoxContainer _list;
    RichTextLabel _cardText;
    Label _resultText;
    Control _markers, _box;
    LineEdit _splitN;
    readonly Dictionary<int, Button> _marker = new();
    readonly HashSet<int> _sel = new();
    readonly Dictionary<int, HashSet<int>> _groups = new();
    Pending _pending;
    int _lastId = -1;
    Vector2 _boxStart; bool _boxing;
    float _t;
    int _sig;

    public bool Active => _s != null;

    public void Setup(Main main)
    {
        _main = main;
        RegisterActions();
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        Visible = false;

        _markers = new Control { MouseFilter = MouseFilterEnum.Ignore }; _markers.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(_markers);
        _box = new BoxDraw(this) { MouseFilter = MouseFilterEnum.Ignore }; _box.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(_box);

        // top bar: the battle, the round, time
        _top = Ui.Panel(0.7f); Ui.Pin(_top, 0.5f, 0, new Vector2(0, 10));
        var th = new HBoxContainer(); th.AddThemeConstantOverride("separation", 10); _top.AddChild(th);
        _title = Ui.Label("", 15, Ui.Accent, true); th.AddChild(_title);
        _round = new ProgressBar { CustomMinimumSize = new Vector2(150, 12), MaxValue = 1, ShowPercentage = false, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        th.AddChild(_round);
        _go = Ui.Button("Begin", "Start or hold the battle clock (Space)"); _go.CustomMinimumSize = new Vector2(80, 28); _go.Pressed += TogglePause; th.AddChild(_go);
        foreach (var sp in new[] { 1.0, 2.0, 4.0 })
        {
            var b = Ui.Button($"{sp:0}×", "Battle speed"); double v = sp;
            b.Pressed += () => { if (_s != null) _s.Speed = v; };
            th.AddChild(b); _speeds.Add(b);
        }
        _auto = Ui.Button("Leave it to the admirals", "Fight the rest out at once, with your orders as they stand"); _auto.Pressed += () => { _s?.AutoFinish(); };
        th.AddChild(_auto);
        AddChild(_top);

        // left: the forces
        _left = Ui.Panel(0.6f); Ui.Pin(_left, 0, 0, new Vector2(10, 60));
        var sc = new ScrollContainer { CustomMinimumSize = new Vector2(290, 420), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _list = new VBoxContainer(); _list.AddThemeConstantOverride("separation", 3); _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        sc.AddChild(_list); _left.AddChild(sc); AddChild(_left);

        // right: the selected unit
        _card = Ui.Panel(0.6f); Ui.Pin(_card, 1, 0, new Vector2(-300, 60));
        _cardText = new RichTextLabel { BbcodeEnabled = true, FitContent = true, CustomMinimumSize = new Vector2(270, 0), ScrollActive = false };
        _card.AddChild(_cardText); AddChild(_card);

        // bottom: orders
        var bottom = Ui.Panel(0.7f); Ui.Pin(bottom, 0.5f, 1, new Vector2(0, -14));
        var bv = new VBoxContainer(); bv.AddThemeConstantOverride("separation", 4); bottom.AddChild(bv);
        _hint = Ui.Label("", 12, Ui.Dim); bv.AddChild(_hint);
        _orders = new HBoxContainer(); _orders.AddThemeConstantOverride("separation", 6); bv.AddChild(_orders);
        Order("Move", "Right-click the ground, or press then click", () => Arm(Pending.Move));
        Order("Focus fire [F]", "Aim the selection's fire at one enemy unit", () => Arm(Pending.Focus));
        _form = new OptionButton { FocusMode = FocusModeEnum.None, TooltipText = "Formation [G cycles]" };
        foreach (var f in WarData.P("battle", "command", "formations").EnumerateObject()) _form.AddItem(f.Name);
        _form.ItemSelected += i => { if (_s != null) _s.SetFormation(Selected(), _form.GetItemText((int)i)); };
        _orders.AddChild(_form);
        Order("Screen [C]", "The selection covers one of your other units", () => Arm(Pending.Screen));
        Order("Retreat [R]", "Pull the selection out of the battle; it leaves after a round under fire", () => _s?.Retreat(Selected()));
        Order("Split [X]", "Divide the selection: down to a single ship", () => { if (Selected().Any()) _splitBar.Visible = !_splitBar.Visible; });
        Order("Merge [M]", "Fold the selection's pieces of one squadron back together", MergeSelected);
        Order("Doctrine [V]", "Cancel the selection's orders: back to the admiral's doctrine", () => _s?.Clear(Selected()));
        Order("Frame [Home]", "See the whole battle", () => _main.FrameBattle());

        _splitBar = Ui.Panel(0.8f); _splitBar.Visible = false; Ui.Pin(_splitBar, 0.5f, 1, new Vector2(0, -110));
        var sh = new HBoxContainer(); sh.AddThemeConstantOverride("separation", 6); _splitBar.AddChild(sh);
        sh.AddChild(Ui.Label("Split off", 13, Ui.Dim));
        foreach (var (txt, frac) in new[] { ("1 ship", -1.0), ("1/10", 0.1), ("1/4", 0.25), ("Half", 0.5) })
        {
            var b = Ui.Button(txt); double fr = frac;
            b.Pressed += () => SplitSelected(fr < 0 ? 1 : 0, fr);
            sh.AddChild(b);
        }
        _splitN = new LineEdit { PlaceholderText = "ships", CustomMinimumSize = new Vector2(90, 0), FocusMode = FocusModeEnum.Click };
        _splitN.TextSubmitted += t => { if (double.TryParse(t, out var n)) SplitSelected(n, 0); };
        sh.AddChild(_splitN);
        AddChild(_splitBar);
        AddChild(bottom);

        // result
        _result = Ui.Panel(0.85f); Ui.Pin(_result, 0.5f, 0.4f, Vector2.Zero); _result.Visible = false;
        var rv = new VBoxContainer(); rv.AddThemeConstantOverride("separation", 10); _result.AddChild(rv);
        _resultText = Ui.Label("", 18, Ui.Text, true); _resultText.HorizontalAlignment = HorizontalAlignment.Center; rv.AddChild(_resultText);
        var cont = Ui.Button("Continue"); cont.CustomMinimumSize = new Vector2(140, 34); cont.Pressed += Close; rv.AddChild(cont);
        AddChild(_result);
    }

    void Order(string text, string tip, Action a) { var b = Ui.Button(text, tip); b.Pressed += a; _orders.AddChild(b); }

    // ---------- opening and closing ----------
    public void Open(CommandSession s)
    {
        _s = s; _sel.Clear(); _groups.Clear(); _pending = Pending.None; _lastId = -1; _sig = -1;
        _result.Visible = false; _splitBar.Visible = false; Visible = true;
        _main.Hud.SetChrome(false);
        foreach (var m in _marker.Values) m.QueueFree();
        _marker.Clear();
        _go.Text = "Begin";
        Select(s.Mine.Units.Select(u => u.Id));
    }

    void Close()
    {
        if (_s == null) return;
        _s = null; Visible = false;
        _main.Hud.SetChrome(true);
        _main.EndBattleCamera();
        Sim.CloseCommand();
    }

    void TogglePause() { if (_s == null || _s.Over) return; _s.Paused = !_s.Paused; _go.Text = _s.Paused ? "Go" : "Hold"; }

    // ---------- selection ----------
    IEnumerable<CmdUnit> Selected() => _s == null ? Enumerable.Empty<CmdUnit>() : _s.Mine.Units.Where(u => _sel.Contains(u.Id) && !u.Gone).ToList();
    void Select(IEnumerable<int> ids, bool add = false) { if (!add) _sel.Clear(); foreach (var id in ids) _sel.Add(id); _lastId = _sel.Count > 0 ? _sel.Last() : -1; _splitBar.Visible = false; }
    void Arm(Pending p) { if (!Selected().Any()) { _main.Hud.Toast("Select a unit first."); return; } _pending = _pending == p ? Pending.None : p; }

    void MergeSelected()
    {
        if (_s == null) return;
        var us = Selected().ToList();
        foreach (var g in us.GroupBy(u => u.Sq))
        {
            var l = g.ToList();
            for (int i = 1; i < l.Count; i++) { _s.Merge(l[0], l[i]); _sel.Remove(l[i].Id); }
        }
    }

    void SplitSelected(double n, double frac)
    {
        if (_s == null) return;
        var made = new List<int>();
        foreach (var u in Selected().ToList())
        {
            double ships = frac > 0 ? Math.Floor(u.Ships * frac) : Math.Floor(n);
            if (ships < 1) ships = 1;
            var piece = _s.Split(u, ships);
            if (piece != null) made.Add(piece.Id);
        }
        if (made.Count == 0) _main.Hud.Toast("It cannot be split that way (a unit is at least one ship, and a side holds only " + Units.MaxPerSide + " units).");
        else Select(made);
        _splitBar.Visible = false;
    }

    CmdUnit UnitAt(Vector2 screen, bool enemy)
    {
        var cam = _main.Cam; var w = Works; if (w == null || _s == null) return null;
        CmdUnit best = null; float bd = 28f;
        foreach (var u in (enemy ? _s.Theirs : _s.Mine).Units)
        {
            if (u.Gone || !w.CmdAnchors.TryGetValue(u.Id, out var a)) continue;
            var vp = w.ViewPos(a.At); if (cam.IsPositionBehind(vp)) continue;
            float d = cam.UnprojectPosition(vp).DistanceTo(screen);
            if (d < bd) { bd = d; best = u; }
        }
        return best;
    }

    // ---------- input ----------
    public override void _UnhandledInput(InputEvent e)
    {
        if (_s == null || !Visible) return;
        if (e is InputEventKey && _splitN.HasFocus()) return;
        if (_s.Over) return;
        bool handled = true;
        if (e.IsActionPressed("cmd_pause")) TogglePause();
        else if (e.IsActionPressed("cmd_focus")) Arm(Pending.Focus);
        else if (e.IsActionPressed("cmd_screen")) Arm(Pending.Screen);
        else if (e.IsActionPressed("cmd_retreat")) _s.Retreat(Selected());
        else if (e.IsActionPressed("cmd_split")) { if (Selected().Any()) _splitBar.Visible = !_splitBar.Visible; }
        else if (e.IsActionPressed("cmd_merge")) MergeSelected();
        else if (e.IsActionPressed("cmd_clear")) _s.Clear(Selected());
        else if (e.IsActionPressed("cmd_all")) Select(_s.Mine.Units.Where(u => !u.Gone).Select(u => u.Id));
        else if (e.IsActionPressed("cmd_frame")) _main.FrameBattle();
        else if (e.IsActionPressed("cmd_follow")) { if (_lastId >= 0) _main.FollowUnit(_lastId); }
        else if (e.IsActionPressed("cmd_speed_up")) SetSpeed(1);
        else if (e.IsActionPressed("cmd_speed_down")) SetSpeed(-1);
        else if (e.IsActionPressed("cmd_formation"))
        {
            var us = Selected().ToList();
            if (us.Count > 0) { int next = (_form.GetItemCount() + Array.IndexOf(Enumerable.Range(0, _form.GetItemCount()).Select(i => _form.GetItemText(i)).ToArray(), us[0].Formation) + 1) % _form.GetItemCount(); _form.Select(next); _s.SetFormation(us, _form.GetItemText(next)); }
        }
        else if (e.IsActionPressed("cmd_cancel")) { if (_pending != Pending.None) _pending = Pending.None; else _sel.Clear(); }
        else if (e is InputEventKey { Pressed: true, Echo: false } k && k.Keycode >= Key.Key1 && k.Keycode <= Key.Key9)
        {
            int g = (int)(k.Keycode - Key.Key0);
            if (k.CtrlPressed) _groups[g] = new HashSet<int>(_sel);
            else if (_groups.TryGetValue(g, out var set)) Select(set.Where(id => _s.Mine.Units.Any(u => u.Id == id && !u.Gone)));
        }
        else if (e is InputEventMouseButton mb) handled = Mouse(mb);
        else if (e is InputEventMouseMotion mm && _boxing) { _box.QueueRedraw(); handled = false; }
        else handled = false;
        if (handled) GetViewport().SetInputAsHandled();
    }

    void SetSpeed(int d) { var all = new[] { 1.0, 2.0, 4.0 }; int i = Math.Clamp(Array.IndexOf(all, _s.Speed) + d, 0, 2); _s.Speed = all[i]; }

    bool Mouse(InputEventMouseButton mb)
    {
        if (mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed) { _boxStart = mb.Position; _boxing = true; return false; }
            if (!_boxing) return false;
            _boxing = false; _box.QueueRedraw();
            var end = mb.Position;
            if (_pending != Pending.None) { Resolve(end); return true; }
            bool add = mb.ShiftPressed || mb.CtrlPressed;
            if (_boxStart.DistanceTo(end) < 6)
            {
                var u = UnitAt(end, false);
                if (u != null) { if (mb.DoubleClick) Select(_s.Mine.Units.Where(x => x.Fleet == u.Fleet && !x.Gone).Select(x => x.Id)); else Select(new[] { u.Id }, add); }
                else if (!add) { _sel.Clear(); _lastId = -1; }
                return true;
            }
            var r = new Rect2(_boxStart, Vector2.Zero).Expand(end);
            var cam = _main.Cam; var w = Works;
            var inBox = _s.Mine.Units.Where(u => !u.Gone && w != null && w.CmdAnchors.TryGetValue(u.Id, out var a) && !cam.IsPositionBehind(w.ViewPos(a.At)) && r.HasPoint(cam.UnprojectPosition(w.ViewPos(a.At)))).Select(u => u.Id);
            Select(inBox, add);
            return true;
        }
        if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
        {
            if (_pending != Pending.None) { _pending = Pending.None; return true; }
            if (!Selected().Any()) return false;
            var foe = UnitAt(mb.Position, true);
            if (foe != null) _s.Focus(Selected(), foe);
            else MoveTo(mb.Position);
            return true;
        }
        return false;
    }

    void Resolve(Vector2 at)
    {
        var p = _pending; _pending = Pending.None;
        if (p == Pending.Move) MoveTo(at);
        else if (p == Pending.Focus) { var foe = UnitAt(at, true); if (foe != null) _s.Focus(Selected(), foe); }
        else if (p == Pending.Screen) { var own = UnitAt(at, false); if (own != null) _s.Screen(Selected(), own); }
    }

    /// <summary>Send the selection to the point on the battle plane under the mouse: across the line, and back from it.</summary>
    void MoveTo(Vector2 screen)
    {
        var w = Works; var cam = _main.Cam;
        if (w == null || !w.CmdFrame(out var centreView, out var axis, out float gap)) return;
        var o = cam.ProjectRayOrigin(screen); var d = cam.ProjectRayNormal(screen);
        if (Mathf.Abs(d.Y) < 1e-5f) return;
        float t = (centreView.Y - o.Y) / d.Y; if (t < 0) return;
        var hit = o + d * t - centreView;
        // our side's frame: fwd toward the enemy; behind the line is depth, across it lateral
        var fwd = _s.PlayerIsA ? axis : -axis;
        var right = fwd.Cross(Vector3.Up).Normalized();
        double along = hit.Dot(fwd) / gap;
        double depth = Math.Max(0, (-0.5 - along) / 0.6), lateral = Math.Clamp(hit.Dot(right) / (gap * 0.8), -1.5, 1.5);
        // clicking in the line or ahead of it means "go to the line"
        if (depth < 0.15) depth = 0;
        _s.Move(Selected(), depth, lateral);
    }

    // ---------- per frame ----------
    public override void _Process(double delta)
    {
        if (_s == null) return;
        _t += (float)delta;
        _title.Text = $"{Sim.Gal.Stars[_s.Star].Name}  ·  round {Math.Max(1, _s.Round)} of {(int)WarData.D("battle", "rounds")}";
        _round.Value = _s.Progress;
        foreach (var (b, i) in _speeds.Select((b, i) => (b, i))) b.Modulate = Math.Abs(_s.Speed - new[] { 1.0, 2.0, 4.0 }[i]) < 1e-9 ? Ui.Warm : Colors.White;
        _go.Text = !_s.Paused ? "Hold" : _s.Round == 0 ? "Begin" : "Go";
        if (_s.Over) { _go.Disabled = _auto.Disabled = true; ShowResult(); }
        _hint.Text = _pending switch
        {
            Pending.Move => "Click where the selection should go. Behind the line is out of range; to the sides is a flank.",
            Pending.Focus => "Click the enemy unit to focus on.",
            Pending.Screen => "Click the unit of yours to cover.",
            _ => _s.Paused && _s.Round == 0 ? "The battle has not begun. Select units, give orders, then press Begin (Space). Right-click the ground to move, an enemy to focus fire."
                                              : "Left-click or drag to select · Ctrl+1..9 groups · right-click the ground to move, an enemy to focus fire · Home frames the battle",
        };
        UpdateMarkers();
        if (_t > 0.25f || _sig != Signature()) { _t = 0; RebuildList(); UpdateCard(); }
    }

    int Signature() => _s.Mine.Units.Count * 31 + _s.Theirs.Units.Count * 7 + _sel.Count * 3 + (int)_pending + _s.Round * 101;

    void ShowResult()
    {
        if (_result.Visible) return;
        var r = _s.Result;
        bool won = r.AWins == _s.PlayerIsA;
        double lost = _s.PlayerIsA ? r.LostA : r.LostB, foe = _s.PlayerIsA ? r.LostB : r.LostA;
        _resultText.Text = $"{(won ? "VICTORY" : "DEFEAT")}\nWe lost {Fmt(lost)} ships. They lost {Fmt(foe)}.";
        _result.Visible = true;
    }

    void UpdateMarkers()
    {
        var cam = _main.Cam; var w = Works; if (w == null) return;
        var all = _s.A.Units.Concat(_s.B.Units);
        foreach (var u in all)
        {
            bool mine = _s.Mine.Units.Contains(u);
            if (!_marker.TryGetValue(u.Id, out var b))
            {
                b = new Button { FocusMode = FocusModeEnum.None, Flat = false, MouseFilter = MouseFilterEnum.Stop, CustomMinimumSize = new Vector2(0, 20) };
                b.AddThemeFontSizeOverride("font_size", 11);
                int id = u.Id;
                b.GuiInput += ev => { if (ev is InputEventMouseButton { Pressed: true } mbt) MarkerClick(id, mbt); };
                _markers.AddChild(b); _marker[u.Id] = b;
            }
            bool show = !u.Gone && w.CmdAnchors.TryGetValue(u.Id, out var a) && !cam.IsPositionBehind(w.ViewPos(a.At));
            b.Visible = show;
            if (!show) continue;
            var anchor = w.CmdAnchors[u.Id];
            var sp = cam.UnprojectPosition(w.ViewPos(anchor.At));
            double shown = u.Prev + (u.Ships - u.Prev) * _s.Progress;
            b.Text = $"{Defs.Hulls[u.Hull]} {Fmt(shown)}" + (u.Retreat ? " ↩" : u.FocusId >= 0 ? " ⌖" : "");
            b.Position = sp - new Vector2(b.Size.X * 0.5f, 34);
            b.Modulate = mine ? (_sel.Contains(u.Id) ? Ui.Warm : Ui.Accent) : Ui.Danger;
        }
    }

    void MarkerClick(int id, InputEventMouseButton mb)
    {
        var u = _s.Find(id); if (u == null) return;
        bool mine = _s.Mine.Units.Contains(u);
        if (mb.ButtonIndex == MouseButton.Left)
        {
            if (_pending == Pending.Focus && !mine) { _s.Focus(Selected(), u); _pending = Pending.None; }
            else if (_pending == Pending.Screen && mine) { _s.Screen(Selected(), u); _pending = Pending.None; }
            else if (mine) { if (mb.DoubleClick) _main.FollowUnit(id); else Select(new[] { id }, mb.ShiftPressed || mb.CtrlPressed); }
            else _lastId = id;
        }
        else if (mb.ButtonIndex == MouseButton.Right && !mine && Selected().Any()) _s.Focus(Selected(), u);
    }

    void RebuildList()
    {
        _sig = Signature();
        foreach (var c in _list.GetChildren()) c.QueueFree();
        _list.AddChild(Ui.Label("YOUR FORCES", 12, Ui.Accent, true));
        foreach (var u in _s.Mine.Units) _list.AddChild(Row(u, true));
        _list.AddChild(Ui.Label("THE ENEMY", 12, Ui.Danger, true));
        foreach (var u in _s.Theirs.Units) _list.AddChild(Row(u, false));
    }

    Control Row(CmdUnit u, bool mine)
    {
        var b = Ui.Button($"{Defs.Hulls[u.Hull]} · {Fmt(u.Ships)} · {Describe(u)}");
        b.Alignment = HorizontalAlignment.Left; b.ClipText = true; b.Disabled = u.Gone && !mine;
        b.Modulate = !mine ? Ui.Danger : _sel.Contains(u.Id) ? Ui.Warm : u.Gone ? Ui.Dim : Colors.White;
        int id = u.Id;
        b.Pressed += () =>
        {
            if (mine) Select(new[] { id }, Input.IsKeyPressed(Key.Shift) || Input.IsKeyPressed(Key.Ctrl));
            else if (_pending == Pending.Focus) { _s.Focus(Selected(), u); _pending = Pending.None; }
            else _lastId = id;
        };
        return b;
    }

    string Describe(CmdUnit u)
    {
        if (u.Gone) return "left the field";
        if (u.Retreat) return "retreating";
        var parts = new List<string>();
        if (u.HasMove && (u.MoveDepth > 0.05 || Math.Abs(u.MoveLateral) > 0.05)) parts.Add(u.MoveDepth > 0.4 ? "reserve" : "flank");
        if (u.FocusId >= 0) parts.Add("focus");
        if (u.ScreenId >= 0) parts.Add("screen");
        if (u.Formation != "line") parts.Add(u.Formation);
        return parts.Count == 0 ? "doctrine" : string.Join(", ", parts);
    }

    void UpdateCard()
    {
        var u = _lastId >= 0 ? _s.Find(_lastId) : null;
        if (u == null) { _cardText.Text = "[color=#" + Ui.Hex(Ui.Dim) + "]Nothing selected.[/color]"; return; }
        bool mine = _s.Mine.Units.Contains(u);
        var f = u.Fleet;
        string adm = f.Admiral == null ? "no admiral" : $"Admiral {f.Admiral.Name} ({f.Admiral.Trait}, skill {f.Admiral.Skill:0.00})";
        _cardText.Text = $"[b]{Defs.Hulls[u.Hull]}[/b]  [color=#{Ui.Hex(mine ? Ui.Accent : Ui.Danger)}]{(mine ? "yours" : "enemy")}[/color]\n" +
            $"{Fmt(u.Ships)} ships · {Forces.Level(u.Xp)}\n{f.Name} · {adm}\nMorale {f.Morale * 100:0}% · supply {f.Supply * 100:0}%\n" +
            $"Orders: {Describe(u)}\nDepth {u.Depth:0.0} · lateral {u.Lateral:0.0}";
    }

    static string Fmt(double n) => n >= 1e9 ? $"{n / 1e9:0.##}B" : n >= 1e6 ? $"{n / 1e6:0.##}M" : n >= 1e4 ? $"{n / 1e3:0.#}k" : $"{Math.Round(n):0}";

    /// <summary>The drag-select rectangle.</summary>
    sealed partial class BoxDraw : Control
    {
        readonly BattleCommandPanel _p;
        public BoxDraw(BattleCommandPanel p) { _p = p; }
        public override void _Draw()
        {
            if (!_p._boxing) return;
            var r = new Rect2(_p._boxStart, Vector2.Zero).Expand(GetLocalMousePosition());
            DrawRect(r, new Color(0.56f, 0.82f, 1f, 0.12f), true);
            DrawRect(r, new Color(0.56f, 0.82f, 1f, 0.7f), false, 1f);
        }
    }
}
