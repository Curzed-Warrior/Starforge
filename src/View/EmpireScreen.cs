using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// The empire screen (E): an overview of the polity, the policies that decide what the council runs by itself and
/// what waits on you, per-system overrides, and the inbox of news and decisions.
/// </summary>
public partial class EmpireScreen : PanelContainer
{
    Main _main;
    Simulation Sim => _main.Sim;
    TabContainer _tabs;
    RichTextLabel _overview;
    readonly ColorRect[] _factionBars = new ColorRect[3];
    Label _legend;
    VBoxContainer _policies, _systems, _inbox;
    int _inboxSig = -1;

    public const int TabOverview = 0, TabPolicies = 1, TabSystems = 2, TabInbox = 3;

    public void Setup(Main main)
    {
        _main = main;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.02f, 0.03f, 0.045f, 0.97f), new Color(0.55f, 0.72f, 0.9f, 0.2f), 4, 12));
        CustomMinimumSize = new Vector2(960, 660);
        Ui.Pin(this, 0.5f, 0.5f, new Vector2(0, 10));
        Visible = false;
        var v = new VBoxContainer();
        AddChild(v);
        var head = new HBoxContainer();
        head.AddChild(Ui.Label("EMPIRE", 22, Ui.Accent, true));
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var close = Ui.Button("Close", "E / Esc");
        close.Pressed += () => Visible = false;
        head.AddChild(close);
        v.AddChild(head);
        _tabs = new TabContainer { CustomMinimumSize = new Vector2(930, 590) };
        _tabs.TabChanged += _ => { _inboxSig = -1; Refresh(); };
        v.AddChild(_tabs);

        // overview
        var ov = new VBoxContainer { Name = "Overview" };
        _overview = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, CustomMinimumSize = new Vector2(900, 0) };
        ov.AddChild(_overview);
        var fb = new HBoxContainer { CustomMinimumSize = new Vector2(900, 6) };
        fb.AddThemeConstantOverride("separation", 2);
        var fc = new[] { new Color(0.45f, 0.75f, 1f), new Color(0.75f, 0.6f, 1f), new Color(1f, 0.65f, 0.35f) };
        for (int i = 0; i < 3; i++) { _factionBars[i] = new ColorRect { Color = fc[i], CustomMinimumSize = new Vector2(10, 6) }; fb.AddChild(_factionBars[i]); }
        ov.AddChild(fb);
        _legend = Ui.Label("", 12, Ui.Dim);
        ov.AddChild(_legend);
        _tabs.AddChild(ov);

        _policies = Scroll("Policies");
        _systems = Scroll("Ledger");
        _inbox = Scroll("Inbox");
    }

    VBoxContainer Scroll(string name)
    {
        var sc = new ScrollContainer { Name = name, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var v = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        v.AddThemeConstantOverride("separation", 6);
        sc.AddChild(v);
        _tabs.AddChild(sc);
        return v;
    }

    public void Open(int tab)
    {
        Visible = true;
        _tabs.CurrentTab = tab;
        _inboxSig = -1;
        Refresh();
    }

    public void Toggle(int tab) { if (Visible && _tabs.CurrentTab == tab) Visible = false; else Open(tab); }

    /// <summary>Open the inbox scrolled to one alert.</summary>
    public void Show(Alert a) { Open(TabInbox); _focus = a; }
    Alert _focus;

    double _t;
    public override void _Process(double delta)
    {
        if (!Visible || _main?.Sim == null) return;
        _t += delta;
        if (_t < 0.5) return;
        _t = 0;
        if (_tabs.CurrentTab == TabOverview) RefreshOverview();
        if (_tabs.CurrentTab == TabInbox) RefreshInbox();
    }

    void Refresh()
    {
        if (_main?.Sim == null) return;
        switch (_tabs.CurrentTab)
        {
            case TabOverview: RefreshOverview(); break;
            case TabPolicies: BuildPolicies(); break;
            case TabSystems: BuildSystems(); break;
            case TabInbox: RefreshInbox(); break;
        }
    }

    // ---------- overview ----------
    void RefreshOverview()
    {
        var civ = Sim.Humanity;
        string dim = Ui.Hex(Ui.Dim);
        var gov = Government.Of(civ);
        string lead = civ.Leader != null ? $"Speaker {civ.Leader.Name}" : "";
        string wars = civ.AtWar.Count > 0 ? $"\n[color=#{Ui.Hex(Ui.Danger)}]At war with {string.Join(", ", civ.AtWar.Where(i => i < Sim.Civs.Count).Select(i => Narrative2.Called(Sim.Civs[i])))}[/color]" : "";
        string met = civ.Met.Count > 0 ? $"\n[color=#{dim}]Known: {string.Join(", ", civ.Met.Where(i => i < Sim.Civs.Count && !Sim.Civs[i].Dead).Select(i => $"{Sim.Civs[i].Name} ({civ.Relations.GetValueOrDefault(i):+0;-0})"))}[/color]" : "";
        string belief = civ.Faith > 0.05f || civ.Dread > 0.05f
            ? $"\n[color=#{Ui.Hex(Ui.Omen)}]Belief in the voice {civ.Faith * 100:0}%[/color]{(civ.Dread > 0.05f ? $"   [color=#{Ui.Hex(Ui.Danger)}]dread {civ.Dread * 100:0}%[/color]" : "")}" : "";
        string auto = string.Join("  ·  ", Enum.GetValues<Area>().Select(a => civ.Policy.IsManual(a) ? $"[color=#{Ui.Hex(Ui.Warm)}]{a}: you[/color]" : $"{a}: council"));
        _overview.Text = $"[font_size=26][color=#{Ui.Hex(civ.Color)}][b]{civ.Name.ToUpper()}[/b][/color][/font_size]\n" +
            $"[color=#{Ui.Hex(Ui.Warm)}]{gov.Name}[/color] · {civ.Faction(civ.Drive)} {(gov == Government.Assembly ? "hold the council" : "ascendant")} · {civ.Drive} · {civ.Ethic}\n" +
            $"[color=#{dim}]{gov.About}[/color]\n" +
            (civ.Emergency ? $"[color=#{Ui.Hex(Ui.Danger)}][b]STATE OF EMERGENCY[/b] · {civ.EmergencyCause}[/color]\n" : "") +
            $"{lead}\n\n" +
            $"Systems {civ.Colonies.Count} · population {civ.FmtPop()} · power {Hud.Sci(civ.Power)} W · Kardashev {Defs.Kardashev(civ.GalacticPower):0.000}\n" +
            $"Who decides: {auto}\n" +
            $"War culture: {Cultures.Describe(civ.Culture)}\n" +
            $"{Simulation.Level(civ.Mobilisation).GetProperty("name").GetString()} · {civ.MilShare * 100:0}% of industry to the war (efficiency {civ.Efficiency * 100:0}%) · " +
            $"materiel {Hud.Sci(civ.Materiel)} kg · navy {Sim.FleetsOf(civ).Sum(f => f.Ships):0} hulls in {Sim.FleetsOf(civ).Count()} fleets · {Sim.ArmiesOf(civ).Count()} armies" + belief + wars + met;
        var keys = Defs.Drives.Keys.ToArray();
        _legend.Text = string.Join(" · ", keys.Select(k => $"{civ.Faction(k)} {civ.Inf.GetValueOrDefault(k) * 100:0}%"));
        for (int i = 0; i < 3; i++) _factionBars[i].CustomMinimumSize = new Vector2(Math.Max(2, civ.Inf.GetValueOrDefault(keys[i]) * 890f), 6);
    }

    // ---------- policies ----------
    void BuildPolicies()
    {
        Clear(_policies);
        var civ = Sim.Humanity;
        var pol = civ.Policy;
        _policies.AddChild(Ui.Label("WHO DECIDES", 14, Ui.Accent, true));
        _policies.AddChild(Note("The council runs every area by itself, following your priorities. Take an area into your own hands and its decisions come to you as proposals instead."));
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 14);
        foreach (var a in Enum.GetValues<Area>())
        {
            grid.AddChild(Ui.Label(a.ToString(), 15, Ui.Text, true));
            var d = Ui.Label(Policy.Describe(a), 12, Ui.Dim);
            d.CustomMinimumSize = new Vector2(600, 0);
            d.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            grid.AddChild(d);
            var b = Ui.Button(pol.IsManual(a) ? "You decide" : "Council", "Switch who decides");
            b.CustomMinimumSize = new Vector2(130, 28);
            b.Modulate = pol.IsManual(a) ? Ui.Warm : Colors.White;
            var area = a;
            b.Pressed += () => { Sim.SetManual(civ, area, !pol.IsManual(area)); BuildPolicies(); };
            grid.AddChild(b);
        }
        _policies.AddChild(grid);

        _policies.AddChild(Ui.Label("ECONOMY", 14, Ui.Accent, true));
        var gov = Government.Of(civ);
        Slider(_policies, "Industry to great works", pol.WorksShare, 0.05f, 0.9f, 0.01f, v => pol.WorksShare = v, v => $"{v * 100:0}%",
            pol.IsManual(Area.Economy) ? "The rest goes to the swarm, ships and homes." : $"Only used when you decide the economy. Now the {gov.Name.ToLower()} sets it ({gov.Works * 100:0}%, more once the swarm is finished).", !pol.IsManual(Area.Economy));

        _policies.AddChild(Ui.Label("PRODUCTION", 14, Ui.Accent, true));
        bool prod = pol.IsManual(Area.Production);
        var mrow = new HBoxContainer();
        mrow.AddChild(new Label { Text = "Mobilisation", CustomMinimumSize = new Vector2(200, 0) });
        var mob = new OptionButton { CustomMinimumSize = new Vector2(240, 0), Disabled = !prod };
        int levels = WarData.P("mobilisation").GetArrayLength();
        for (int i = 0; i < levels; i++) mob.AddItem(Simulation.Level(i).GetProperty("name").GetString(), i);
        mob.Selected = prod ? pol.Mobilisation : civ.Mobilisation;
        mob.ItemSelected += i => { pol.Mobilisation = (int)i; };
        mrow.AddChild(mob);
        var mtip = Ui.Label(prod ? "Factories take three years to retool, and new lines start slow." : "Only used when you decide production. Now the council chooses.", 11, Ui.Dim);
        mtip.CustomMinimumSize = new Vector2(360, 0); mtip.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        mrow.AddChild(mtip);
        _policies.AddChild(mrow);
        var service = new CheckBox { Text = "Mandatory service", ButtonPressed = civ.MandatoryService, Disabled = !prod,
            TooltipText = "Every citizen serves: more soldiers, felt as a burden by a people that prizes its freedom." };
        service.Toggled += on => civ.MandatoryService = on;
        _policies.AddChild(service);

        _policies.AddChild(Ui.Label("GREAT WORKS PRIORITIES", 14, Ui.Accent, true));
        _policies.AddChild(Note("How much of the works budget each kind of great work gets, on top of what the ruling faction and the government want."));
        string[] kindTips = { "foundries, yards, launch loops, forges", "star lifters, swarms, stellar engines", "computers, archives, sentinels", "gateways, relays, lanes", "habitats and ringworlds", "fortresses, shields, superweapons" };
        for (int i = 0; i < Policy.WorkKinds.Length; i++)
        {
            string k = Policy.WorkKinds[i];
            Slider(_policies, k, pol.W(k), 0, 2, 0.1f, v => pol.Weight[k] = v, v => $"×{v:0.0}", kindTips[i]);
        }
        _policies.AddChild(Ui.Label("RESEARCH PRIORITIES", 14, Ui.Accent, true));
        _policies.AddChild(Note("How research is split between the three slots."));
        foreach (var k in Simulation.Areas) Slider(_policies, k, pol.W(k), 0, 2, 0.1f, v => pol.Weight[k] = v, v => $"×{v:0.0}", null);

        if (pol.Declined.Count > 0)
        {
            _policies.AddChild(Ui.Label("GREAT WORKS YOU TURNED DOWN", 14, Ui.Accent, true));
            var h = new HFlowContainer();
            foreach (var id in pol.Declined.ToList())
            {
                var b = Ui.Button($"Allow the {Simulation.WorkName(id)}", "The council will propose it again");
                b.Pressed += () => { pol.Declined.Remove(id); BuildPolicies(); };
                h.AddChild(b);
            }
            _policies.AddChild(h);
        }

        _policies.AddChild(Ui.Label("PAUSE WHEN", 14, Ui.Accent, true));
        var pf = new HFlowContainer();
        foreach (var cat in Policy.Categories)
        {
            var cb = new CheckBox { Text = CatName(cat), ButtonPressed = pol.PauseOn.Contains(cat), FocusMode = FocusModeEnum.None };
            string c = cat;
            cb.Toggled += on => { if (on) pol.PauseOn.Add(c); else pol.PauseOn.Remove(c); };
            pf.AddChild(cb);
        }
        _policies.AddChild(pf);
    }

    // ---------- systems: the ledger ----------
    string _sort = "Industry";
    bool _desc = true;
    static readonly string[] Cols = { "System", "Population", "Industry", "Power", "K", "Stability", "Works" };

    void BuildSystems()
    {
        Clear(_systems);
        var civ = Sim.Humanity;
        _systems.AddChild(Ui.Label("LEDGER", 14, Ui.Accent, true));
        _systems.AddChild(Note("Every system you hold. Click a column to sort; set a system's share of industry for great works by hand on the right."));
        var grid = new GridContainer { Columns = Cols.Length + 2 };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 2);
        foreach (var c in Cols)
        {
            var h = Ui.Button(c + (c == _sort ? (_desc ? " ▾" : " ▴") : ""), "Sort");
            h.Flat = true;
            h.AddThemeColorOverride("font_color", c == _sort ? Ui.Accent : Ui.Dim);
            string key = c;
            h.Pressed += () => { if (_sort == key) _desc = !_desc; else { _sort = key; _desc = key != "System"; } BuildSystems(); };
            grid.AddChild(h);
        }
        grid.AddChild(Ui.Label("By hand", 12, Ui.Dim));
        grid.AddChild(Ui.Label("", 12));
        double Key(Colony c) => _sort switch
        {
            "Population" => c.Pop, "Industry" => c.IndustryKg, "Power" => c.Power, "K" => c.Kardashev, "Stability" => c.Stability, "Works" => c.WorksShare, _ => 0
        };
        var rows = _sort == "System" ? civ.Colonies.OrderBy(c => c.Sys.Name).ToList() : civ.Colonies.OrderBy(Key).ToList();
        if (_desc && _sort != "System" || !_desc && _sort == "System") rows.Reverse();
        foreach (var c in rows.Take(150))
        {
            var col = c;
            var go = Ui.Button(c.Sys.Name + (c.Home ? "  ⌂" : "") + (c.Crisis != null ? "  !" : ""), "Fly there");
            go.Flat = true;
            go.Alignment = HorizontalAlignment.Left;
            go.CustomMinimumSize = new Vector2(170, 0);
            if (c.Crisis != null) go.AddThemeColorOverride("font_color", Ui.Danger);
            go.Pressed += () => { Visible = false; _main.TravelTo(col.Star); };
            grid.AddChild(go);
            grid.AddChild(Cell(c.Pop < 1 ? $"{c.Pop * 1000:0} M" : $"{c.Pop:0.0} B"));
            grid.AddChild(Cell($"{Hud.Sci(c.IndustryKg)} kg/yr"));
            grid.AddChild(Cell($"{Hud.Sci(c.Power)} W"));
            grid.AddChild(Cell($"{c.Kardashev:0.00}"));
            grid.AddChild(Cell($"{c.Stability * 100:0}%", c.Stability < 0.7 ? Ui.Danger : null));
            var val = Cell($"{c.WorksShare * 100:0}%");
            grid.AddChild(val);
            var cb = new CheckBox { ButtonPressed = c.WorksOverride.HasValue, FocusMode = FocusModeEnum.None, TooltipText = "Set this system's works share by hand" };
            var s = new HSlider { MinValue = 0, MaxValue = 0.95, Step = 0.01, Value = c.WorksOverride ?? c.WorksShare, CustomMinimumSize = new Vector2(150, 0), Editable = c.WorksOverride.HasValue };
            cb.Toggled += on => { col.WorksOverride = on ? s.Value : null; s.Editable = on; val.Text = $"{col.WorksShare * 100:0}%"; };
            s.ValueChanged += v => { if (col.WorksOverride.HasValue) { col.WorksOverride = v; val.Text = $"{v * 100:0}%"; } };
            grid.AddChild(cb);
            grid.AddChild(s);
        }
        _systems.AddChild(grid);
        if (civ.Colonies.Count > 150) _systems.AddChild(Note($"and {civ.Colonies.Count - 150} more"));
    }

    static Label Cell(string text, Color? col = null) => Ui.Label(text, 13, col ?? Ui.Text);

    // ---------- inbox ----------
    void RefreshInbox()
    {
        var alerts = Sim.Alerts;
        int sig = alerts.Count * 31 + alerts.Count(a => a.Done);
        foreach (var a in alerts) a.Seen = a.Seen || !a.Decision;
        if (sig == _inboxSig) return;
        _inboxSig = sig;
        Clear(_inbox);
        var waiting = alerts.Where(a => a.Decision && !a.Done).OrderBy(a => a.Year).ToList();
        if (waiting.Count > 0)
        {
            _inbox.AddChild(Ui.Label("WAITING FOR YOU", 14, Ui.Warm, true));
            foreach (var a in waiting) Card(a);
        }
        _inbox.AddChild(Ui.Label("NEWS", 14, Ui.Accent, true));
        foreach (var a in alerts.Where(a => !(a.Decision && !a.Done)).Reverse().Take(80)) Card(a);
        if (_focus != null) { var f = _focus; _focus = null; Callable.From(() => { foreach (var n in _inbox.GetChildren()) if (n is Control c && c.HasMeta("alert") && (ulong)c.GetMeta("alert") == (ulong)f.GetHashCode()) { (_inbox.GetParent() as ScrollContainer)?.EnsureControlVisible(c); break; } }).CallDeferred(); }
    }

    void Card(Alert a)
    {
        var p = new PanelContainer();
        p.SetMeta("alert", (ulong)a.GetHashCode());
        bool open = a.Decision && !a.Done;
        p.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.05f, 0.07f, 0.1f, open ? 0.9f : 0.5f), CatColor(a.Cat) * new Color(1, 1, 1, open ? 0.6f : 0.2f), 3, 6));
        var v = new VBoxContainer();
        p.AddChild(v);
        var h = new HBoxContainer();
        h.AddChild(Ui.Label($"{(int)a.Year}", 12, Ui.Dim));
        h.AddChild(Ui.Label(CatName(a.Cat).ToUpper(), 10, CatColor(a.Cat)));
        var title = Ui.Label(a.Title, 15, Colors.White, true);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        h.AddChild(title);
        if (a.Star != null)
        {
            var go = Ui.Button("Go", "Fly there");
            var st = a.Star;
            go.Pressed += () => { Visible = false; _main.TravelTo(st); };
            h.AddChild(go);
        }
        v.AddChild(h);
        var txt = Ui.Label(a.Text, 13, Ui.Text);
        txt.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        txt.CustomMinimumSize = new Vector2(860, 0);
        v.AddChild(txt);
        if (open)
        {
            var row = new HBoxContainer();
            for (int i = 0; i < a.Choices.Count; i++)
            {
                int k = i;
                var b = Ui.Button(a.Choices[i].Label, i == 0 ? "The council's recommendation" : null);
                if (i == 0) b.Modulate = Ui.Accent;
                b.Pressed += () => { Sim.Decide(a, k); _inboxSig = -1; RefreshInbox(); };
                row.AddChild(b);
            }
            v.AddChild(row);
        }
        else if (a.Outcome != null) v.AddChild(Ui.Label($"→ {a.Outcome}", 12, Ui.Dim));
        _inbox.AddChild(p);
    }

    // ---------- helpers ----------
    static void Clear(Node n) { foreach (var c in n.GetChildren()) { n.RemoveChild(c); c.QueueFree(); } }

    static Label Note(string text)
    {
        var l = Ui.Label(text, 12, Ui.Dim);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(880, 0);
        return l;
    }

    static void Slider(VBoxContainer to, string name, float value, float min, float max, float step, Action<float> set, Func<float, string> fmt, string tip, bool disabled = false)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = name, CustomMinimumSize = new Vector2(200, 0), TooltipText = tip ?? "", MouseFilter = MouseFilterEnum.Stop });
        var s = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = value, CustomMinimumSize = new Vector2(360, 0), Editable = !disabled, TooltipText = tip ?? "" };
        var val = new Label { Text = fmt(value), CustomMinimumSize = new Vector2(70, 0) };
        s.ValueChanged += v => { set((float)v); val.Text = fmt((float)v); };
        row.AddChild(s); row.AddChild(val);
        if (tip != null) { var t = Ui.Label(tip, 11, Ui.Dim); t.CustomMinimumSize = new Vector2(280, 0); t.AutowrapMode = TextServer.AutowrapMode.WordSmart; row.AddChild(t); }
        to.AddChild(row);
    }

    public static string CatName(string cat) => cat switch
    {
        "War" => "Wars and losses", "Battle" => "Battles won", "Contact" => "Contact and diplomacy", "Decision" => "Decisions",
        "Milestone" => "Milestones", "Crisis" => "Crises", "Unrest" => "Unrest", _ => cat
    };

    public static Color CatColor(string cat) => cat switch
    {
        "War" => Ui.Danger, "Battle" => new Color(1f, 0.6f, 0.45f), "Contact" => Ui.Omen, "Decision" => Ui.Warm,
        "Milestone" => Ui.Accent, "Crisis" => Ui.Danger, "Unrest" => new Color(1f, 0.8f, 0.4f), _ => Ui.Text
    };
}
