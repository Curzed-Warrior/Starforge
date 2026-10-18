using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// The row of message icons under the top bar, after Stellaris's: one small tile per alert you haven't looked at,
/// newest on the right, coloured by kind. Left-click to go there (or open the decision), right-click to dismiss.
/// Decisions stay until they are answered.
/// </summary>
public partial class Notifications : HBoxContainer
{
    Main _main;
    Simulation Sim => _main.Sim;
    public event Action<Alert> OpenAlert;
    readonly List<(Button B, Alert A)> _tiles = new();
    const int Max = 12;

    public void Setup(Main main)
    {
        _main = main;
        Ui.Pin(this, 0.5f, 0, new Vector2(0, 64));
        AddThemeConstantOverride("separation", 4);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    double _t;
    int _sig = -1;
    public override void _Process(double delta)
    {
        if (_main?.Sim == null) return;
        _t += delta;
        if (_t < 0.3) return;
        _t = 0;
        var show = Sim.Alerts.Where(a => a.Decision ? !a.Done : !a.Seen).Reverse().Take(Max).Reverse().ToList();
        int sig = show.Count == 0 ? 0 : show.Sum(a => a.GetHashCode() % 100000);
        if (sig == _sig) return;
        _sig = sig;
        foreach (var (b, _) in _tiles) b.QueueFree();
        _tiles.Clear();
        foreach (var a in show)
        {
            var col = EmpireScreen.CatColor(a.Cat);
            var b = Ui.Button(Glyph(a), $"{(int)a.Year} · {a.Title}\n{a.Text}\n\nLeft-click: {(a.Decision ? "decide" : a.Star != null ? "go there" : "open the inbox")}   Right-click: dismiss");
            b.CustomMinimumSize = new Vector2(34, 34);
            b.AddThemeColorOverride("font_color", col);
            b.AddThemeStyleboxOverride("normal", Ui.Box(new Color(0.04f, 0.06f, 0.09f, 0.85f), col * new Color(1, 1, 1, a.Decision ? 0.9f : 0.5f), 3, 4));
            var alert = a;
            b.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true } mb)
                {
                    if (mb.ButtonIndex == MouseButton.Right) { if (!alert.Decision) alert.Seen = true; _sig = -1; _t = 1; AcceptEvent(); }
                    else if (mb.ButtonIndex == MouseButton.Left) { Open(alert); AcceptEvent(); }
                }
            };
            AddChild(b);
            _tiles.Add((b, a));
        }
    }

    void Open(Alert a)
    {
        if (a.Decision || a.Star == null) { OpenAlert?.Invoke(a); return; }
        a.Seen = true; _sig = -1; _t = 1;
        if (a.Star != _main.ViewStar) _main.TravelTo(a.Star); else _main.SelectStar();
    }

    static string Glyph(Alert a) => a.Cat switch
    {
        "War" => "⚔", "Battle" => "✦", "Contact" => "◎", "Decision" => "?", "Milestone" => "★", "Crisis" => "!", "Unrest" => "≈", "Incident" => "◆", _ => "•"
    };
}
