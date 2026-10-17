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
    readonly List<(bool Head, string L, string R, Color Col, string Tip, Action Click)> _spec = new();
    readonly List<(Button B, Label L, Label R)> _pool = new();
    readonly List<Action> _acts = new();

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

        var wars = civ.AtWar.Where(i => i < sim.Civs.Count && !sim.Civs[i].Dead).Select(i => sim.Civs[i]).ToList();
        if (wars.Count > 0)
        {
            Header("Wars", wars.Count, Ui.Danger);
            if (!_folded.Contains("Wars"))
                foreach (var w in wars)
                {
                    var front = w.Colonies.OrderBy(c => civ.Colonies.Count == 0 ? 0 : civ.Colonies.Min(m => m.Star.Pos.DistanceSquaredTo(c.Star.Pos))).FirstOrDefault();
                    Row(Narrative2.Called(w, true), $"{sim.WarStrength(civ) / Math.Max(sim.WarStrength(w), 1e-9):0.0}×", front == null ? null : () => _main.TravelTo(front.Star), w.Color,
                        $"Our strength against theirs. Click to fly to the nearest of their systems.");
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
            () => { if (!_folded.Remove(title)) _folded.Add(title); _t = 1; _dirty = true; }));
    }

    void Row(string left, string right, Action click, Color col, string tip = null) => _spec.Add((false, left, right, col, tip ?? "", click));

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
            l.AddThemeFontSizeOverride("font_size", sp.Head ? 11 : 13);
            l.OffsetLeft = 0;
            ((HBoxContainer)l.GetParent()).OffsetLeft = sp.Head ? 4 : 12;
        }
    }
}
