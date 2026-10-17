using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// The voice's larger acts (U): stars kindled, snuffed and moved, worlds forged, peoples raised up, councils possessed,
/// lanes opened, regions sealed, civs exalted or cast down, and the laws of the universe rewritten. Everything acts on
/// what you are looking at: the viewed star and system, the selected world, the civilisation that holds it.
/// </summary>
public partial class VoicePanel : PanelContainer
{
    Main _main;
    Simulation Sim => _main.Sim;
    VBoxContainer _v;
    Label _hint;
    StarInfo _marked;
    float _refresh;

    public void Setup(Main main)
    {
        _main = main;
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.03f, 0.03f, 0.07f, 0.9f), new Color(0.78f, 0.66f, 1f, 0.3f), 4, 10));
        Ui.Pin(this, 0.5f, 1, new Vector2(0, -92));
        Visible = false;
        _v = new VBoxContainer();
        _v.AddThemeConstantOverride("separation", 4);
        AddChild(_v);
    }

    public void Toggle() { Visible = !Visible; if (Visible) Rebuild(); }

    public override void _Process(double delta)
    {
        if (!Visible || _main == null) return;
        _refresh -= (float)delta;
        if (_refresh <= 0) { _refresh = 0.6f; Rebuild(); }
    }

    HBoxContainer Row(string title)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 4);
        var l = Ui.Label(title, 11, Ui.God, true);
        l.CustomMinimumSize = new Vector2(92, 0);
        h.AddChild(l);
        _v.AddChild(h);
        return h;
    }

    void Btn(HBoxContainer row, string label, string tip, bool enabled, Action a)
    {
        var b = Ui.Button(label, tip);
        b.Disabled = !enabled;
        b.AddThemeFontSizeOverride("font_size", 12);
        b.Pressed += () => { a(); _refresh = 0; _main.WorldsChanged(); };
        row.AddChild(b);
    }

    void Rebuild()
    {
        foreach (var c in _v.GetChildren()) c.QueueFree();
        var star = _main.ViewStar;
        var sys = _main.ViewSystem;
        var col = _main.ViewColony;
        var body = _main.SelectedBody;
        var civ = col?.Civ;
        _v.AddChild(Ui.Label($"THE VOICE  ·  acts on {star?.Name}{(body != null ? " · " + body.Name : "")}{(civ != null ? " · " + Narrative2.Called(civ, true) : "")}", 12, Ui.God, true));

        var r = Row("STARS");
        Btn(r, "Kindle a star", "Light a new star in the dark near this one.", star != null, () => { var s = Sim.KindleStar(star.Pos); _main.Toast($"{s.Name} burns."); });
        Btn(r, "Snuff this star", "Put it out. Anyone living here is gone. A horror.", star != null && !star.Dead && star != Sim.Gal.Sol, () => Sim.SnuffStar(star));
        Btn(r, "Mark it", "Remember this star, for a lane or a move.", star != null, () => { _marked = star; _main.Toast($"{star.Name} is marked."); });
        Btn(r, _marked != null ? $"Move it beside {_marked.Name}" : "Move it (mark a star first)", "Carry this star, worlds and all, to the marked star's neighbourhood.", star != null && _marked != null && _marked != star,
            () => { var to = _marked.Pos + (star.Pos - _marked.Pos).Normalized() * 1.0f; Sim.MoveStar(star, to); _main.Refocus(); });
        Btn(r, _marked != null ? $"Open a lane to {_marked.Name}" : "Open a lane", "A permanent door between this star and the marked one.", star != null && _marked != null && _marked != star, () => Sim.OpenLane(star, _marked));
        Btn(r, "Seal this region", "No seed ship crosses the edge of this reach, in or out.", star != null, () => Sim.Quarantine(star));

        r = Row("WORLDS");
        foreach (var (k, n) in new[] { (BodyKind.Terran, "living world"), (BodyKind.Ocean, "ocean world"), (BodyKind.Desert, "desert"), (BodyKind.Ice, "ice world"), (BodyKind.Gas, "gas giant") })
            Btn(r, "Forge " + n, $"A new {n} condenses in this system's free lanes.", sys != null && sys.Bodies.Count < 14, () => Sim.ForgeWorld(sys, k));

        r = Row("PEOPLES");
        bool natives = col != null && body != null && col.Natives.TryGetValue(body, out var nat) && nat.Contact != "cleared";
        Btn(r, "Uplift them now", "The natives of the selected world wake knowing how to reach the stars, and found a civilisation of their own.", natives, () => Sim.UpliftNow(col, body));
        Btn(r, "Seed a species", "Place a brand-new people in this unclaimed system.", star != null && star.Owner < 0 && !star.Dead && star.Special == null, () => Sim.SeedSpecies(star));
        if (col?.Crisis != null)
        {
            Btn(r, "Bend fate: mercy", "The crisis here ends as well as it could.", true, () => Sim.BendFate(col, "mercy"));
            Btn(r, "Bend fate: ruin", "The crisis here ends as badly as it could.", true, () => Sim.BendFate(col, "ruin"));
        }
        else if (col != null && col.VisitorT >= 0 && col.VisitorStage < 2)
            foreach (var o in new[] { "gift", "study", "take", "silent" })
                Btn(r, "Visitor: " + o, "Decide how the visitor's stay ends.", true, () => Sim.BendFate(col, o));

        if (civ != null)
        {
            r = Row("COUNCIL");
            foreach (var d in Defs.Drives.Keys)
                Btn(r, $"Possess: {civ.Faction(d)}", $"Speak through the Speaker: the {civ.Faction(d)} ({d}) take the council.", civ.Drive != d, () => Sim.Possess(civ, d));
            Btn(r, civ.Exalted ? "Exalted" : "Raise to Exalted", "Lift them as near to godhood as mortals go. They are still not gods.", !civ.Exalted, () => Sim.Exalt(civ, $"The voice lifts {Narrative2.Called(civ)} halfway out of the world. Their worlds become sacred ground. They are not gods. They are the nearest thing."));
            Btn(r, "Cast down", "Strip their exaltation: they become mortal again.", civ.Exalted, () => Sim.CastDown(civ, false));
            Btn(r, "Make them Fallen", "Break them: slow, proud and finished growing, like the Vessh.", !civ.Fallen, () => Sim.CastDown(civ, true));
        }

        r = Row("LAWS");
        foreach (var (id, name, about) in Simulation.LawList)
        {
            bool on = Sim.Laws.Contains(id);
            Btn(r, (on ? "✓ " : "") + name, about + (on ? " (click to lift)" : ""), true, () => Sim.RewriteLaw(id, !on));
        }
    }
}
