using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// The right-hand list, after Stellaris's outliner: decisions waiting, wars, systems, great works under way, research
/// and ships in flight. Every row is a shortcut: click to fly there or open the screen it belongs to. Sections fold.
/// </summary>
public partial class Outliner : PanelContainer
{
    Main _main;
    Simulation Sim => _main.Sim;
    VBoxContainer _list;
    readonly HashSet<string> _folded = new() { "Ships" };
    public event Action<Alert> OpenAlert;
    public event Action OpenTech;
    public event Action<int> OpenWar;
    public event Action OpenInterior;

    public void Setup(Main main)
    {
        _main = main;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.02f, 0.035f, 0.055f, 0.6f), new Color(0.55f, 0.72f, 0.9f, 0.14f), 3, 6));
        CustomMinimumSize = new Vector2(290, 0);
        Ui.Pin(this, 1, 0, new Vector2(-10, 62));
        var sc = new ScrollContainer { CustomMinimumSize = new Vector2(280, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 1);
        sc.AddChild(_list);
        AddChild(sc);
        _scroll = sc;
    }
    ScrollContainer _scroll;

    double _t = 1;
    bool _dirty;
    public override void _Process(double delta)
    {
        if (_main?.Sim == null || !Visible) return;
        _t += delta;
        // never rebuild under the pointer (a click lands on release, and the row must still be there), unless it asked to
        bool hovered = GetGlobalRect().HasPoint(GetGlobalMousePosition());
        if (_t < 0.5 || hovered && !_dirty) return;
        _t = 0; _dirty = false;
        Rebuild();
    }

    // Rows are pooled: the list is described afresh twice a second, and the same buttons are re-labelled. Creating
    // and freeing dozens of nodes a second made garbage, and garbage collections are frame hitches.
    readonly List<(bool Head, string L, string R, Color Col, string Tip, Action Click, bool Sub)> _spec = new();
    readonly List<(Button B, Label L, Label R)> _pool = new();
    readonly List<Action> _acts = new();
    readonly List<Fleet> _fleets = new();
    readonly List<Colony> _moving = new();

    static double ShipsIn(Fleet f)
    {
        double n = 0;
        foreach (var s in f.Squadrons) n += s.Ships;
        return n;
    }

    static readonly Comparison<Fleet> FleetOrder = (a, b) =>
        a.Moving != b.Moving ? (a.Moving ? -1 : 1) : a.Moving ? a.Arrive.CompareTo(b.Arrive) : ShipsIn(b).CompareTo(ShipsIn(a));

    static readonly Comparison<Colony> MovementOrder = (a, b) =>
        a.Unrest != b.Unrest ? b.Unrest.CompareTo(a.Unrest) : b.Grievance.CompareTo(a.Grievance);

    static string UpFirst(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    /// <summary>How long ago, in days for the first two months and in months after.</summary>
    static string Ago(double years)
    {
        int d = (int)(years * 365.25);
        if (d < 60) return d == 1 ? "1 day ago" : $"{d} days ago";
        int m = (int)(years * 12);
        return $"{m} months ago";
    }

    void Rebuild()
    {
        var sim = Sim;
        var civ = sim.Humanity;
        _spec.Clear();

        var decisions = sim.Alerts.Where(a => a.Decision && !a.Done).OrderBy(a => a.Year).ToList();
        if (decisions.Count > 0)
        {
            Header("Decisions", decisions.Count, Ui.Warm);
            if (!_folded.Contains("Decisions"))
                foreach (var a in decisions.Take(8)) Row(a.Title, $"{(int)a.Year}", () => OpenAlert?.Invoke(a), Ui.Warm, a.Text);
        }

        int nWars = 0;
        foreach (var w in sim.Wars) if (w.Open && Simulation.Side(w, civ) >= 0) nWars++;
        if (nWars > 0)
        {
            Header("Wars", nWars, Ui.Danger);
            if (!_folded.Contains("Wars"))
            {
                double mapYears = WarData.D("interface", "battleMapYears");
                foreach (var w in sim.Wars)
                {
                    int s = Simulation.Side(w, civ);
                    if (!w.Open || s < 0) continue;
                    var enemy = sim.Lead(w, 1 - s);
                    float score = s == 0 ? w.Score : -w.Score, exh = s == 0 ? w.ExhA : w.ExhB;
                    int id = w.Id;
                    Row(Narrative2.Called(enemy, true), $"{score:+0;-0;0} · tired {exh:0}%", () => OpenWar?.Invoke(id), enemy.Color,
                        "War score from our side, and how worn out we are. Click to open the war.");
                    // the newest battle of this war, while the galaxy map still marks it
                    for (int i = sim.Battles.Count - 1; i >= 0; i--)
                    {
                        var b = sim.Battles[i];
                        if (b.WarId != id) continue;
                        double age = sim.Year - b.Year;
                        if (age < mapYears)
                        {
                            int star = b.Star;
                            Row($"Battle at {(star >= 0 && star < sim.Gal.Stars.Count ? sim.Gal.Stars[star].Name : "?")}, {Ago(age)}", "", () => _main.GoToBattle(star), Ui.Warm,
                                "Click to fly to the battle.", true);
                        }
                        break;
                    }
                }
            }
        }

        // the player's fleets: those under way first, soonest arrival first, then the biggest in port
        _fleets.Clear();
        foreach (var f in sim.Fleets) if (f.Civ == civ.Id) _fleets.Add(f);
        if (_fleets.Count > 0)
        {
            _fleets.Sort(FleetOrder);
            Header("Fleets", _fleets.Count, new Color(0.75f, 0.85f, 1f));
            if (!_folded.Contains("Fleets"))
            {
                for (int i = 0; i < _fleets.Count && i < 8; i++)
                {
                    var f = _fleets[i];
                    var dest = sim.Gal.Stars[f.Moving ? f.To : f.At];
                    double n = ShipsIn(f);
                    string hulls = $"{n:#,0} {(n >= 0.5 && n < 1.5 ? "ship" : "ships")}";
                    Row(f.Name, f.Moving ? $"{hulls} · {Math.Max(0, f.Arrive - sim.Year):0.0}y" : $"{hulls} · {f.Mission}", () => _main.TravelTo(dest), Ui.Text,
                        f.Moving ? $"{UpFirst(f.Mission)}: on the way to {dest.Name}. Click to go there." : $"{UpFirst(f.Mission)}, at {dest.Name}. Click to go there.");
                }
                if (_fleets.Count > 8) Row($"and {_fleets.Count - 8} more", "", null, Ui.Dim);
            }
        }

        // movements: the systems where one has risen, the furthest up the ladder first
        _moving.Clear();
        foreach (var c in civ.Colonies) if (c.Movement != null) _moving.Add(c);
        if (_moving.Count > 0)
        {
            _moving.Sort(MovementOrder);
            Header("Movements", _moving.Count, Ui.Warm);
            if (!_folded.Contains("Movements"))
            {
                var names = Simulation.UnrestNames;
                for (int i = 0; i < _moving.Count && i < 6; i++)
                {
                    var c = _moving[i];
                    Row(c.Sys.Name, names[Math.Clamp(c.Unrest, 0, names.Length - 1)], () => OpenInterior?.Invoke(), c.Unrest >= 3 ? Ui.Danger : Ui.Warm,
                        $"{UpFirst(c.Movement)}. Click to open the Interior tab.");
                }
                if (_moving.Count > 6) Row($"and {_moving.Count - 6} more", "", null, Ui.Dim);
            }
        }

        var cols = civ.Colonies.OrderByDescending(c => c.Home).ThenByDescending(c => c.Power).ToList();
        Header("Systems", cols.Count, Ui.Accent);
        if (!_folded.Contains("Systems"))
        {
            foreach (var c in cols.Take(12))
            {
                string flag = c.Crisis != null ? "  ⚠" : c.Civ.Emergency && c.Home ? "  !" : "";
                Row(c.Sys.Name + flag, $"{(c.Pop < 1 ? $"{c.Pop * 1000:0}M" : $"{c.Pop:0.#}B")} · K{c.Kardashev:0.00}", () => _main.TravelTo(c.Star),
                    c == _main.ViewColony ? Colors.White : c.Crisis != null ? Ui.Danger : Ui.Text,
                    $"{c.Gov.Name} · stability {c.Stability * 100:0}% · industry {Hud.Sci(c.IndustryKg)} kg/yr" + (c.WorksOverride.HasValue ? $" · works share set by hand ({c.WorksOverride:P0})" : ""));
            }
            if (cols.Count > 12) Row($"and {cols.Count - 12} more", "", null, Ui.Dim);
        }

        var works = new List<(Colony C, string Id, float P, double Y)>();
        foreach (var c in civ.Colonies)
            foreach (var id in c.BuildRate.Keys)
                if (c.BuildProgress(id) < 1f && (!Colony.IsEverywhere(id) || c.Home)) works.Add((c, id, c.BuildProgress(id), c.YearsLeft(id)));
        Header("Great works", works.Count, new Color(0.7f, 0.85f, 1f));
        if (!_folded.Contains("Great works"))
            foreach (var w in works.OrderBy(w => w.Y).Take(10))
            {
                string eta = double.IsInfinity(w.Y) ? "stalled" : w.Y > 2000 ? "millennia" : w.Y < 1 ? "months" : $"~{w.Y:0}y";
                { var wc = w.C; var wid = w.Id; Row($"{Simulation.WorkName(w.Id)}", $"{w.P * 100:0}% · {eta}", () => _main.GoToWork(wc, wid), Ui.Text, $"At {w.C.Sys.Name}. Click to go there."); }
            }

        Header("Research", civ.Slots.Values.Count(s => s.Id != null), new Color(0.6f, 1f, 0.8f));
        if (!_folded.Contains("Research"))
            foreach (var s in civ.Slots.Values)
            {
                if (s.Id == null) { Row($"{s.Area}: choose", "", () => { var a = sim.Alerts.FirstOrDefault(x => x.Key == "research:" + s.Area && !x.Done); if (a != null) OpenAlert?.Invoke(a); else OpenTech?.Invoke(); }, Ui.Warm); continue; }
                var t = Defs.TechById(s.Id);
                Row(t.Name, $"{Math.Min(99, s.P / t.CostFor(civ) * 100):0}%", () => OpenTech?.Invoke(), Ui.Text, $"{s.Area} · {Defs.Branch(t.B).Name}: {t.Fx}");
            }

        var ships = sim.Ships.Where(s => s.Civ == civ).OrderBy(s => s.Arrive).ToList();
        if (ships.Count > 0)
        {
            Header("Ships", ships.Count, Ui.Dim);
            if (!_folded.Contains("Ships"))
                foreach (var s in ships.Take(8))
                    Row($"{s.Name} → {s.Target.Name}", $"{s.Arrive - sim.Year:0}y", () => _main.TravelTo(s.Target), s.Hostile ? Ui.Danger : Ui.Text);
        }
        Apply();
        // keep the list on screen: it scrolls past the space between the top bar and the controls
        var vp = GetViewportRect().Size;
        _scroll.CustomMinimumSize = new Vector2(280, Mathf.Min(_list.GetCombinedMinimumSize().Y, vp.Y - 200));
    }

    void Header(string title, int n, Color col)
    {
        bool folded = _folded.Contains(title);
        _spec.Add((true, $"{(folded ? "▸" : "▾")} {title.ToUpper()}  {n}", "", col, folded ? "Show" : "Fold",
            () => { if (!_folded.Remove(title)) _folded.Add(title); _t = 1; _dirty = true; }, false));
    }

    void Row(string left, string right, Action click, Color col, string tip = null, bool sub = false) => _spec.Add((false, left, right, col, tip ?? "", click, sub));

    /// <summary>Label the pooled buttons from the description, making more only when the list grows.</summary>
    void Apply()
    {
        while (_pool.Count < _spec.Count)
        {
            int idx = _pool.Count;
            var b = Ui.Button("", null);
            b.Flat = true;
            b.CustomMinimumSize = new Vector2(270, 22);
            b.Pressed += () => { if (idx < _acts.Count) _acts[idx]?.Invoke(); };
            var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            h.SetAnchorsPreset(LayoutPreset.FullRect);
            h.OffsetLeft = 4; h.OffsetRight = -6;
            var l = Ui.Label("", 13, Ui.Text);
            l.ClipText = true;
            l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            l.MouseFilter = MouseFilterEnum.Ignore;
            var r = Ui.Label("", 12, Ui.Dim);
            r.MouseFilter = MouseFilterEnum.Ignore;
            h.AddChild(l); h.AddChild(r);
            b.AddChild(h);
            _list.AddChild(b);
            _pool.Add((b, l, r));
        }
        _acts.Clear();
        for (int i = 0; i < _pool.Count; i++)
        {
            var (b, l, r) = _pool[i];
            if (i >= _spec.Count) { b.Visible = false; continue; }
            var sp = _spec[i];
            b.Visible = true;
            _acts.Add(sp.Click);
            if (b.TooltipText != sp.Tip) b.TooltipText = sp.Tip;
            b.Disabled = sp.Click == null && !sp.Head;
            if (l.Text != sp.L) l.Text = sp.L;
            if (r.Text != sp.R) r.Text = sp.R;
            l.AddThemeColorOverride("font_color", sp.Col);
            l.AddThemeFontSizeOverride("font_size", sp.Head ? 11 : sp.Sub ? 12 : 13);
            l.OffsetLeft = 0;
            ((HBoxContainer)l.GetParent()).OffsetLeft = sp.Head ? 4 : sp.Sub ? 24 : 12;
        }
    }
}
