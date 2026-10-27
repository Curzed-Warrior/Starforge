using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// The tech screen (T): eight branch tabs, tiers as columns, rows grouped by line. Above it, the three research slots with
/// the council's current hand, the danger meters and the exotic pools. The voice can steer from here: swap a card, redraw
/// a hand, pin, ban, decide an exclusive fork, gift or erase a tech.
/// </summary>
public partial class TechScreen : PanelContainer
{
    Main _main;
    Simulation Sim => _main.Sim;
    Civ _civ;
    char _branch = 'X';
    Tech _sel;
    OptionButton _civPick;
    HBoxContainer _tabs, _slots, _actions;
    VBoxContainer _detail;
    RichTextLabel _detailText, _meters;
    GridContainer _grid;
    ScrollContainer _scroll;
    readonly Dictionary<char, Button> _tabBtns = new();
    float _refresh;
    List<Civ> _civList = new();

    static readonly Color Known = new(0.56f, 0.82f, 1f), Now = new(1f, 0.72f, 0.42f), Avail = new(0.86f, 0.9f, 0.95f), Locked = new(0.38f, 0.42f, 0.48f), Closed = new(0.5f, 0.25f, 0.25f);

    public void Setup(Main main)
    {
        _main = main;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.02f, 0.035f, 0.06f, 0.94f), new Color(0.55f, 0.72f, 0.9f, 0.2f), 4, 12));
        AnchorLeft = 0.04f; AnchorRight = 0.96f; AnchorTop = 0.05f; AnchorBottom = 0.9f;
        Visible = false;
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 6);
        AddChild(v);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 12);
        head.AddChild(Ui.Label("TECHNOLOGY", 22, Ui.Accent, true));
        _civPick = new OptionButton { FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(260, 0) };
        _civPick.ItemSelected += i => { _civ = _civList[(int)i]; _sel = null; Rebuild(); };
        head.AddChild(_civPick);
        _meters = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(400, 0) };
        _meters.AddThemeFontSizeOverride("normal_font_size", 12);
        head.AddChild(_meters);
        var close = Ui.Button("Close  (T)");
        close.Pressed += () => Visible = false;
        head.AddChild(close);
        v.AddChild(head);

        _slots = new HBoxContainer();
        _slots.AddThemeConstantOverride("separation", 10);
        v.AddChild(_slots);

        _tabs = new HBoxContainer();
        _tabs.AddThemeConstantOverride("separation", 4);
        foreach (var br in Defs.Branches)
        {
            var b = Ui.Button(br.Name, br.Blurb);
            char id = br.Id;
            b.ToggleMode = true;
            b.Pressed += () => { _branch = id; Rebuild(); };
            _tabs.AddChild(b);
            _tabBtns[id] = b;
        }
        v.AddChild(_tabs);

        _scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Auto };
        _grid = new GridContainer { Columns = 12 };
        _grid.AddThemeConstantOverride("h_separation", 6);
        _grid.AddThemeConstantOverride("v_separation", 4);
        _scroll.AddChild(_grid);
        v.AddChild(_scroll);

        _detail = new VBoxContainer { CustomMinimumSize = new Vector2(0, 96) };
        _detailText = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, CustomMinimumSize = new Vector2(0, 50) };
        _detailText.AddThemeFontSizeOverride("normal_font_size", 13);
        _detail.AddChild(_detailText);
        _actions = new HBoxContainer();
        _actions.AddThemeConstantOverride("separation", 4);
        _detail.AddChild(_actions);
        v.AddChild(_detail);
    }

    public void Toggle()
    {
        Visible = !Visible;
        if (!Visible) return;
        _civ = (_main.ViewColony ?? Sim.HomeColony).Civ;
        _sel = null;
        Rebuild();
    }

    public override void _Process(double delta)
    {
        if (!Visible || _main == null) return;
        _refresh -= (float)delta;
        if (_refresh > 0) return;
        _refresh = 0.5f;
        UpdateSlots();
        UpdateMeters();
    }

    // ---------- building ----------
    void Rebuild()
    {
        if (_civ == null || _civ.Dead) _civ = Sim.Humanity;
        _civList = Sim.Civs.Where(c => !c.Dead && c.Colonies.Count > 0 && true).ToList();
        _civPick.Clear();
        foreach (var c in _civList) _civPick.AddItem((c.Exalted ? "✦ " : c.Fallen ? "↓ " : "") + Narrative2.Called(c, true));
        _civPick.Selected = Math.Max(0, _civList.IndexOf(_civ));
        // Beyond stays hidden until a gate has stirred for this civ (or the voice is looking at a civ that already knows it)
        bool beyond = _civ.Gates.Count > 0 || _civ.Techs.Any(id => Defs.TechById(id).B == 'Q');
        _tabBtns['Q'].Visible = beyond;
        if (_branch == 'Q' && !beyond) _branch = 'X';
        foreach (var (id, b) in _tabBtns) b.ButtonPressed = id == _branch;
        BuildGrid();
        UpdateSlots();
        UpdateMeters();
        ShowDetail();
    }

    void BuildGrid()
    {
        foreach (var c in _grid.GetChildren()) c.QueueFree();
        _grid.AddChild(Ui.Label("", 11));
        for (int t = 0; t <= 10; t++)
        {
            var l = Ui.Label($"TIER {t}", 11, Ui.Dim, true);
            l.HorizontalAlignment = HorizontalAlignment.Center;
            l.CustomMinimumSize = new Vector2(150, 0);
            _grid.AddChild(l);
        }
        var techs = Defs.Techs.Where(t => t.B == _branch).ToList();
        foreach (var line in techs.Select(t => t.Line).Distinct().OrderBy(l => l == "SPINE" ? "" : l.StartsWith("ASC") ? "zz" : l))
        {
            var ln = Ui.Label(LineName(line), 11, Ui.Warm, true);
            ln.CustomMinimumSize = new Vector2(96, 0);
            ln.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _grid.AddChild(ln);
            for (int tier = 0; tier <= 10; tier++)
            {
                var col = new VBoxContainer();
                col.AddThemeConstantOverride("separation", 3);
                foreach (var t in techs.Where(x => x.Line == line && x.Tier == tier)) col.AddChild(Card(t));
                _grid.AddChild(col);
            }
        }
    }

    static string LineName(string line) => line switch
    {
        "SPINE" => "THE SPINE", "PWR" => "Power", "MEG" => "Mega-engineering", "IND" => "Industry", "MIN" => "Mining", "MAT" => "Materials",
        "CMP" => "Computing", "SEN" => "Sensors", "PSI" => "Psionics", "FTL" => "Drives", "HUL" => "Hulls", "CIV" => "Civil fleet",
        "EWP" => "Energy weapons", "KWP" => "Kinetics", "MSL" => "Missiles", "SFC" => "Strike craft", "XWP" => "Exotic weapons", "PKL" => "World-killers",
        "ARM" => "Armour", "SHD" => "Shields", "DEF" => "Point defence", "STB" => "Starbases", "MIL" => "Military", "GND" => "Ground forces",
        "BIO" => "Biology", "XEN" => "Xenology", "TER" => "Terraforming", "COL" => "Colonies", "GOV" => "Government", "ECO" => "Economy",
        "DIP" => "Diplomacy", "ESP" => "Espionage", "ARC" => "Archaeology", "UMB" => "Umbral", "CHR" => "Chronogravitics", "GLY" => "Glyph",
        "STL" => "Stellarine", "LUX" => "Lucent", "ASC" => "Ascension", _ => line,
    };

    string State(Tech t) =>
        _civ.Knows(t.Id) ? "known" : _civ.Slots.Values.Any(s => s.Id == t.Id) ? "now" : Sim.Available(_civ, t) ? "avail" : Simulation.Closed(_civ, t) ? "closed" : "locked";

    Control Card(Tech t)
    {
        string st = State(t);
        var col = st switch { "known" => Known, "now" => Now, "avail" => Avail, "closed" => Closed, _ => Locked };
        var b = new Button { FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(150, 40), ClipText = true, Alignment = HorizontalAlignment.Left };
        string badges = (t.Keystone ? "◆" : "") + (t.Rare ? "✧" : "") + (t.Danger ? "⚠" : "") + (t.Event ? "⌘" : "") + (t.Atrocity ? "☠" : "") + (t.Exclusive != null ? "⑂" : "");
        b.Text = (badges.Length > 0 ? badges + " " : "") + t.Name;
        b.AddThemeColorOverride("font_color", col);
        b.AddThemeColorOverride("font_hover_color", Colors.White);
        b.AddThemeFontSizeOverride("font_size", 12);
        var bg = st switch { "known" => new Color(0.08f, 0.16f, 0.24f, 0.9f), "now" => new Color(0.22f, 0.14f, 0.05f, 0.9f), "avail" => new Color(0.07f, 0.1f, 0.14f, 0.8f), _ => new Color(0.04f, 0.05f, 0.07f, 0.6f) };
        var sb = Ui.Box(bg, t == _sel ? Ui.Warm : col with { A = st == "locked" ? 0.15f : 0.45f }, 3, 4);
        b.AddThemeStyleboxOverride("normal", sb);
        b.AddThemeStyleboxOverride("hover", Ui.Box(bg.Lightened(0.15f), Colors.White with { A = 0.6f }, 3, 4));
        b.AddThemeStyleboxOverride("pressed", sb);
        b.TooltipText = Tooltip(t);
        b.Pressed += () => { _sel = t; BuildGrid(); ShowDetail(); };
        return b;
    }

    string ReqText(Tech t) => t.Req.Length == 0 ? "nothing" : string.Join(" and ", t.Req.Select(g => g.Length == 1 ? TechName(g[0]) : "one of (" + string.Join(", ", g.Select(TechName)) + ")"));
    static string TechName(string id) => Defs.TryTech(id)?.Name ?? id;

    string Tooltip(Tech t)
    {
        var lines = new List<string> { $"{t.Name}  ·  {Defs.Branch(t.B).Name} tier {t.Tier}  ·  cost {t.CostFor(_civ):0}", t.Fx };
        var eff = t.Effects.Select(Describe).Where(x => x != null).ToList();
        if (eff.Count > 0) lines.Add("Effects: " + string.Join("; ", eff));
        lines.Add("Needs: " + ReqText(t));
        if (t.Exclusive != null) lines.Add("Exclusive with: " + string.Join(", ", Defs.ExclusiveGroups[t.Exclusive].Where(x => x != t.Id).Select(TechName)));
        if (t.Gate != null) lines.Add($"Opens only through the {TechData.P("gateEvents", t.Gate, "name").GetString()} event");
        if (t.Log != null) lines.Add("“" + t.Log + "”");
        return string.Join("\n", lines);
    }

    /// <summary>One effect in plain words.</summary>
    public static string Describe(Effect e)
    {
        string a0 = e.A.Length > 0 ? e.A[0] : "";
        return e.Kind switch
        {
            "mod" => ModWords(a0, e.A[1], e.A[2]),
            "flag" => FlagWords(a0),
            "arms" => $"{a0} weapons tier {e.A[1]}",
            "guard" => $"{(a0 == "pd" ? "point defence" : a0 == "base" ? "starbase defence" : a0)} tier {e.A[1]}",
            "hull" => $"{a0} hulls",
            "mega" => $"great work: {Megastructures.Catalogue.FirstOrDefault(m => m.Id == a0).Name ?? a0}",
            "res" => $"produces {a0}",
            "use" => $"runs on {a0}",
            "meter" => $"{a0} danger {e.A[1]}{e.A[2]}",
            "act" => $"can {a0.Replace('_', ' ')}",
            "path" => $"enters the {Simulation.PathName(a0)}",
            "apotheosis" => $"apotheosis +{a0}",
            "digital" => $"{double.Parse(a0, System.Globalization.CultureInfo.InvariantCulture) * 100:0}% digital",
            "cond" => "needs " + a0.Replace("&", " and ").Replace("_", " "),
            _ => null,
        };
    }

    static string ModWords(string f, string op, string v)
    {
        string what = f switch
        {
            "Mine" => "mining", "Foundry" => "industry", "Eff" => "collectors", "GasYield" => "gas yield", "Rp" => "research", "HubCost" => "mine cost",
            "Shield" => "flare losses", "PopCap" => "population ceiling", "Growth" => "growth", "PlanetCap" => "planet power ceiling", "ShipSpeed" => "ship speed",
            "RegionRate" => "regional expansion", "RegionPower" => "regional power", "Fleet" => "fleet strength", "Ground" => "ground forces", "Stealth" => "stealth",
            "Sense" => "sensor reach", "Range" => "seed-ship range", "Thrust" => "transit speed", "Diplo" => "diplomacy", "Unity" => "unity", "Admin" => "administration",
            "Trade" => "trade", "Terra" => "terraforming speed", "Uplift" => "uplift", "Habs" => "habitats", "Settle" => "settling speed", "Build" => "construction",
            "Spy" => "espionage", "WarLoss" => "war losses", "Base" => "starbase level", "Comets" => "comets", _ => f,
        };
        double x = double.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
        return op switch
        {
            "*" => x >= 1 ? $"{what} +{(x - 1) * 100:0}%" : $"{what} −{(1 - x) * 100:0}%",
            ">=" => f == "PlanetCap" ? $"{what} {x:0.0e0} W" : f == "ShipSpeed" ? $"{what} {x}c" : $"{what} at least {x}",
            _ => $"{what} at most {x}",
        };
    }

    static string FlagWords(string f) => f switch
    {
        "Fast" => "faster transit", "Jovian" => "the giants open", "Probes" => "lightsail probes", "Seed" => "seed ships", "Fold" => "fold drive", "Deterrent" => "deterrent",
        "Gate" => "fold gates", "GateNet" => "gate network", "Ark" => "Andromeda ark", "Belt" or "Mercury" => "the belt opens", "Elevator" => "space elevators",
        "Skimmers" => "gas skimmers", "OrbitalRing" => "orbital rings", "StarLift" => "star lifting", "Swarm" => "the swarm", "CoreTap" => "core tap", "Venus" => "Venus",
        "Outer" => "colonies anywhere", "Habitat" => "O'Neill habitats", "Terraform" => "terraforming", "Hive" => "hive mind", "Transcend" => "transcendence",
        "GrownHulls" => "grown (organic) hulls", _ => System.Text.RegularExpressions.Regex.Replace(f, "([a-z])([A-Z])", "$1 $2").ToLower(),
    };

    // ---------- slots ----------
    void UpdateSlots()
    {
        foreach (var c in _slots.GetChildren()) c.QueueFree();
        foreach (var area in Simulation.Areas)
        {
            var s = _civ.Slots[area];
            var p = Ui.Panel(0.5f);
            p.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var v = new VBoxContainer();
            p.AddChild(v);
            var now = s.Id != null ? Defs.TechById(s.Id) : null;
            v.AddChild(Ui.Label(area.ToUpper() + (now != null ? $"  ·  {now.Name}  {Math.Min(99, s.P / now.CostFor(_civ) * 100):0}%" : "  ·  idle"), 12, now != null ? Now : Ui.Dim, true));
            var bar = new ProgressBar { MinValue = 0, MaxValue = 1, Value = now != null ? Math.Min(1, s.P / now.CostFor(_civ)) : 0, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 4) };
            v.AddChild(bar);
            var hand = new HBoxContainer();
            hand.AddThemeConstantOverride("separation", 3);
            foreach (var id in s.Hand)
            {
                var t = Defs.TryTech(id);
                if (t == null) continue;
                var b = Ui.Button((id == s.Id ? "▶ " : "") + t.Name, "Swap: make the council research this card instead.\n\n" + Tooltip(t));
                b.AddThemeFontSizeOverride("font_size", 11);
                b.ClipText = true; b.CustomMinimumSize = new Vector2(110, 26); b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                b.Disabled = id == s.Id || _civ.Knows(id);
                string a = area, tid = id;
                b.Pressed += () => { Sim.SteerSwap(_civ, a, tid); UpdateSlots(); BuildGrid(); };
                hand.AddChild(b);
            }
            var redraw = Ui.Button("↻", "Redraw this hand");
            string ar = area;
            redraw.Pressed += () => { Sim.SteerRedraw(_civ, ar); UpdateSlots(); BuildGrid(); };
            hand.AddChild(redraw);
            v.AddChild(hand);
            _slots.AddChild(p);
        }
    }

    void UpdateMeters()
    {
        var parts = new List<string>();
        foreach (var m in Simulation.MeterNames)
        {
            double v = _civ.Meters.GetValueOrDefault(m), th = Simulation.Threshold(m);
            if (v < 0.5) continue;
            var col = v >= th ? Ui.Danger : v >= th * 0.6 ? Ui.Warm : Ui.Dim;
            parts.Add($"[color=#{Ui.Hex(col)}]{m} {v:0}/{th:0}[/color]");
        }
        var pools = Simulation.PoolNames.Where(p => _civ.Pools.ContainsKey(p) || _civ.M.Produce.ContainsKey(p)).Select(p => $"[color=#{Ui.Hex(Ui.God)}]{p} {_civ.Pools.GetValueOrDefault(p):0.0}[/color]");
        string status = _civ.Exalted ? $"[color=#ffd36b]Exalted[/color] · " : _civ.Fallen ? $"[color=#{Ui.Hex(Ui.Danger)}]Fallen[/color] · " : "";
        string path = _civ.Path != null ? $"{Simulation.PathName(_civ.Path)} · " : "";
        _meters.Text = $"{status}{path}{_civ.Techs.Count}/{Defs.Techs.Count} techs · apotheosis {_civ.ApoScore}" +
                       (parts.Count > 0 ? "\nDanger: " + string.Join("  ", parts) : "\nNo danger meters raised") +
                       (pools.Any() ? "   ·   Pools: " + string.Join("  ", pools) : "");
    }

    // ---------- selected tech and the voice's hand on it ----------
    void ShowDetail()
    {
        foreach (var c in _actions.GetChildren()) c.QueueFree();
        if (_sel == null) { _detailText.Text = $"[color=#{Ui.Hex(Ui.Dim)}]Select a tech to see what it does and to steer it. ◆ keystone  ✧ rare  ⚠ dangerous  ⌘ needs an event  ☠ atrocity  ⑂ exclusive choice[/color]"; return; }
        var t = _sel;
        string st = State(t);
        _detailText.Text = $"[b]{t.Name}[/b]  [color=#{Ui.Hex(Ui.Dim)}]{Defs.Branch(t.B).Name} · tier {t.Tier} · {st}[/color]\n" + Tooltip(t).Split('\n').Skip(1).Aggregate((a, b) => a + "\n" + b);
        void Act(string label, string tip, Action a) { var b = Ui.Button(label, tip); b.Pressed += () => { a(); Rebuild(); }; _actions.AddChild(b); }
        if (!_civ.Knows(t.Id))
        {
            Act("Gift it", $"Give {Narrative2.Called(_civ)} {t.Name} now, whatever it needs.", () => Sim.GiftTech(_civ, t));
            Act("Pin it next", "It comes up next in its slot (as soon as they can study it).", () => Sim.SteerPin(_civ, t));
            Act("Ban 50 years", "Every attempt to study it fails for fifty years.", () => Sim.SteerBan(_civ, t, 50));
            if (t.Exclusive != null && Simulation.ExclusiveOk(_civ, t))
                Act("Decide this fork", "Settle the exclusive choice now: the other options are off the table for them.", () => Sim.SteerDecide(_civ, t.Exclusive, t.Id));
        }
        else
        {
            Act("Erase it", $"{Narrative2.Called(_civ, true)} forget {t.Name}.", () => Sim.EraseTech(_civ, t));
            Act("Erase the idea", $"Nobody in the galaxy knows {t.Name} any more.", () => Sim.EraseTech(null, t));
        }
    }
}
