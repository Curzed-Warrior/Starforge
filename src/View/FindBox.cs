using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>Find a system (Ctrl+F or /): type part of a name, Enter or click to fly there. Your systems come first.</summary>
public partial class FindBox : PanelContainer
{
    Main _main;
    LineEdit _edit;
    VBoxContainer _list;

    public void Setup(Main main)
    {
        _main = main;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.02f, 0.035f, 0.055f, 0.95f), new Color(0.55f, 0.72f, 0.9f, 0.3f), 4, 10));
        CustomMinimumSize = new Vector2(420, 0);
        Ui.Pin(this, 0.5f, 0, new Vector2(0, 110));
        var v = new VBoxContainer();
        AddChild(v);
        v.AddChild(Ui.Label("FIND A SYSTEM", 12, Ui.Accent, true));
        _edit = new LineEdit { PlaceholderText = "Star or system name…", CustomMinimumSize = new Vector2(400, 32) };
        _edit.TextChanged += Search;
        _edit.TextSubmitted += _ => { if (_list.GetChildCount() > 0 && _list.GetChild(0) is Button b) b.EmitSignal(BaseButton.SignalName.Pressed); };
        v.AddChild(_edit);
        _list = new VBoxContainer();
        v.AddChild(_list);
        Visible = false;
    }

    public void Toggle()
    {
        Visible = !Visible;
        if (!Visible) return;
        _edit.Text = "";
        Search("");
        _edit.GrabFocus();
    }

    public override void _Input(InputEvent e)
    {
        if (Visible && e is InputEventKey { Pressed: true, Keycode: Key.Escape }) { Visible = false; GetViewport().SetInputAsHandled(); }
    }

    void Search(string text)
    {
        foreach (var c in _list.GetChildren()) c.QueueFree();
        var sim = _main.Sim;
        var t = text.Trim();
        var mine = sim.Humanity.Colonies.Select(c => c.Star);
        var hits = (t.Length == 0 ? mine.Take(8)
            : mine.Where(s => s.Name.Contains(t, System.StringComparison.OrdinalIgnoreCase))
                .Concat(sim.Gal.Stars.Where(s => s.Owner >= 0 && s.Name.Contains(t, System.StringComparison.OrdinalIgnoreCase)))
                .Concat(sim.Gal.Stars.Where(s => s.Name.StartsWith(t, System.StringComparison.OrdinalIgnoreCase))))
            .Distinct().Take(8).ToList();
        foreach (var s in hits)
        {
            string owner = s.Owner >= 0 && s.Owner < sim.Civs.Count ? sim.Civs[s.Owner].Name : "unclaimed";
            float ly = s.Pos.DistanceTo(sim.Gal.Sol.Pos) * Galaxy.LyPerUnit;
            var b = Ui.Button($"{s.Name}   ·   {s.Cls}   ·   {owner}   ·   {ly:0} ly from Sol");
            b.Alignment = HorizontalAlignment.Left;
            b.CustomMinimumSize = new Vector2(400, 28);
            if (s.Owner >= 0 && s.Owner < sim.Civs.Count) b.AddThemeColorOverride("font_color", sim.Civs[s.Owner].Color);
            var st = s;
            b.Pressed += () => { Visible = false; _main.TravelTo(st); };
            _list.AddChild(b);
        }
        if (hits.Count == 0 && t.Length > 0) _list.AddChild(Ui.Label("No star by that name.", 12, Ui.Dim));
    }
}
