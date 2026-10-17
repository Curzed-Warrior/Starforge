using System;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// The strip across the top of the screen, after Stellaris's: the empire, what it commands (power, industry, matter,
/// research, people, systems), the inbox, and the date. Every figure is a real quantity; hover for the breakdown.
/// </summary>
public partial class TopBar : PanelContainer
{
    Main _main;
    Simulation Sim => _main.Sim;
    Button _name, _inbox, _date;
    Label _speed;
    ColorRect _kBar;
    readonly System.Collections.Generic.Dictionary<string, (Label Value, Control Box)> _items = new();
    public event Action OpenEmpire, OpenInbox;

    public void Setup(Main main)
    {
        _main = main;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.02f, 0.035f, 0.055f, 0.82f), new Color(0.55f, 0.72f, 0.9f, 0.18f), 0, 6));
        AnchorLeft = 0; AnchorRight = 1; AnchorTop = AnchorBottom = 0;
        OffsetLeft = OffsetRight = OffsetTop = 0;
        MouseFilter = MouseFilterEnum.Stop;
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 18);
        AddChild(h);

        _name = Ui.Button("", "Your empire: government, factions and policies (E)");
        _name.Flat = true;
        _name.AddThemeFontOverride("font", Ui.Bold);
        _name.AddThemeFontSizeOverride("font_size", 18);
        _name.Pressed += () => OpenEmpire?.Invoke();
        h.AddChild(_name);

        Item(h, "k", "KARDASHEV");
        var kb = new ColorRect { Color = new Color(1, 1, 1, 0.1f), CustomMinimumSize = new Vector2(70, 3) };
        _kBar = new ColorRect { Color = Ui.Accent, Size = new Vector2(0, 3) };
        kb.AddChild(_kBar);
        ((VBoxContainer)_items["k"].Box).AddChild(kb);
        Item(h, "power", "POWER");
        Item(h, "industry", "INDUSTRY");
        Item(h, "stock", "STOCKPILE");
        Item(h, "research", "RESEARCH");
        Item(h, "pop", "POPULATION");
        Item(h, "systems", "SYSTEMS");
        Item(h, "wars", "AT WAR");

        h.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        _inbox = Ui.Button("Inbox", "News and decisions waiting for you (I)");
        _inbox.CustomMinimumSize = new Vector2(110, 34);
        _inbox.Pressed += () => OpenInbox?.Invoke();
        h.AddChild(_inbox);

        var dv = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        dv.AddThemeConstantOverride("separation", -4);
        _date = Ui.Button("", "Pause / resume (Space)");
        _date.Flat = true;
        _date.AddThemeFontOverride("font", Ui.Bold);
        _date.AddThemeFontSizeOverride("font_size", 20);
        _date.Pressed += () => _main.SetSpeed(_main.SpeedIdx == 0 ? 2 : 0);
        dv.AddChild(_date);
        _speed = Ui.Label("", 11, Ui.Dim);
        _speed.HorizontalAlignment = HorizontalAlignment.Right;
        dv.AddChild(_speed);
        h.AddChild(dv);
    }

    void Item(HBoxContainer h, string id, string caption)
    {
        var v = new VBoxContainer { MouseFilter = MouseFilterEnum.Stop };
        v.AddThemeConstantOverride("separation", -2);
        v.AddChild(Ui.Label(caption, 9, Ui.Dim));
        var val = Ui.Label("", 15, Ui.Text);
        val.MouseFilter = MouseFilterEnum.Ignore;
        v.AddChild(val);
        h.AddChild(v);
        _items[id] = (val, v);
    }

    void Set(string id, string value, string tip, Color? col = null)
    {
        var (l, box) = _items[id];
        l.Text = value;
        l.AddThemeColorOverride("font_color", col ?? Ui.Text);
        box.TooltipText = tip;
    }

    double _t;
    public override void _Process(double delta)
    {
        if (_main?.Sim == null) return;
        var sim = Sim;
        var civ = sim.Humanity;
        // the clock updates every frame; the economy four times a second
        double y = sim.Year;
        int year = (int)Math.Floor(y);
        var d0 = new DateTime(2001, 1, 1).AddDays((y - year) * 365);
        _date.Text = _main.SpeedIdx > 0 && Main.Speeds[_main.SpeedIdx] >= 1 ? $"{year}" : $"{d0:dd MMM} {year}";
        _speed.Text = _main.SpeedIdx == 0 ? "PAUSED" : $"{Main.SpeedNames[_main.SpeedIdx]} / s{(_main.Autoplay ? " · director" : "")}";
        _speed.AddThemeColorOverride("font_color", _main.SpeedIdx == 0 ? Ui.Warm : Ui.Dim);
        _t += delta;
        if (_t < 0.25) return;
        _t = 0;

        _name.Text = civ.Name.ToUpper();
        _name.AddThemeColorOverride("font_color", civ.Color);
        var hc = sim.HomeColony;
        double P = civ.GalacticPower, k = Defs.Kardashev(P);
        string era = civ.M.Transcend ? "Transcendence" : k >= 3.0 ? "Type III" : k >= 2.6 ? "The Expansion" : k >= 2.2 ? "The Reach"
            : hc != null && hc.Era >= 0 ? Defs.Eras[Math.Min(hc.Era, Defs.Eras.Length - 1)].Name : "";
        Set("k", $"{k:0.000}", $"Kardashev rating: the power a civilisation commands.\nType I 10¹⁶ W · Type II 10²⁶ W · Type III 10³⁶ W.\nEra: {era}");
        _kBar.Size = new Vector2((float)Math.Clamp(k / 3.0, 0, 1) * 70f, 3);

        var cols = civ.Colonies;
        Set("power", $"{Hud.Sci(civ.Power)} W", "Power collected across the empire." + Top(cols, c => c.Power, c => $"{Hud.Sci(c.Power)} W"));
        double ind = cols.Sum(c => c.IndustryKg);
        double works = cols.Sum(c => c.IndustryKg * c.WorksShare);
        Set("industry", $"{Hud.Sci(ind)} kg/yr", $"What the foundries can make in a year. {works / Math.Max(ind, 1e-9) * 100:0}% goes to great works ({(civ.Policy.IsManual(Area.Economy) ? "your setting" : "the government's choice")})." + Top(cols, c => c.IndustryKg, c => $"{Hud.Sci(c.IndustryKg)} kg/yr"));
        double stock = cols.Sum(c => c.Stock) * Defs.SwarmKg;
        Set("stock", $"{Hud.Sci(stock)} kg", "Refined matter waiting in the stockpiles, ready for the great works." + Top(cols, c => c.Stock, c => $"{Hud.Sci(c.Stock * Defs.SwarmKg)} kg"));
        double rp = cols.Sum(c => c.RpRate);
        Set("research", $"{rp:0.#}/yr", "Research arriving each year, split across the three slots by the council's taste and your priorities.\n" +
            string.Join("\n", civ.Slots.Values.Where(s => s.Id != null).Select(s => { var t = Defs.TechById(s.Id); return $"{s.Area}: {t.Name} {Math.Min(99, s.P / t.CostFor(civ) * 100):0}%"; })));
        Set("pop", civ.FmtPop(), "People across the empire." + Top(cols, c => c.Pop, c => c.Pop < 1 ? $"{c.Pop * 1000:0} million" : $"{c.Pop:0.0} billion"));
        int ships = sim.Ships.Count(s => s.Civ == civ);
        Set("systems", $"{cols.Count}", $"Star systems held.{(ships > 0 ? $" {ships} ships in flight." : "")}");
        var wars = civ.AtWar.Where(i => i < sim.Civs.Count && !sim.Civs[i].Dead).Select(i => sim.Civs[i]).ToList();
        _items["wars"].Box.Visible = wars.Count > 0;
        if (wars.Count > 0) Set("wars", $"{wars.Count}", "At war with " + string.Join(", ", wars.Select(w => Narrative2.Called(w))), Ui.Danger);

        int decide = sim.Alerts.Count(a => a.Decision && !a.Done);
        int unseen = sim.Alerts.Count(a => !a.Seen && !a.Decision);
        _inbox.Text = decide > 0 ? $"Decide ({decide})" : unseen > 0 ? $"Inbox ({unseen})" : "Inbox";
        _inbox.Modulate = decide > 0 ? Ui.Warm : unseen > 0 ? Ui.Accent : Colors.White;
    }

    static string Top(System.Collections.Generic.List<Colony> cols, Func<Colony, double> by, Func<Colony, string> fmt)
    {
        var top = cols.OrderByDescending(by).Take(5).ToList();
        return top.Count < 2 ? "" : "\n" + string.Join("\n", top.Select(c => $"  {c.Sys.Name}: {fmt(c)}")) + (cols.Count > 5 ? $"\n  and {cols.Count - 5} more" : "");
    }
}
