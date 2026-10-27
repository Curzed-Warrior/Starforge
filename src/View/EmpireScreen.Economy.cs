using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// The Economy tab (F8): mobilisation level, the civilian/military split, materiel flow, shortages and the slips.
/// Rebuilt at most every 0.5 s, and only when a figure shown on it has changed.
/// </summary>
public partial class EmpireScreen
{
    int _ecoSig = -1;
    const int SlipSystemCap = 12;

    static string Q(double v) => v.ToString("0.###e+0");

    /// <summary>war.json has no prose for the levels, so describe one from its numbers.</summary>
    static string MobilisationNote(System.Text.Json.JsonElement lvl)
    {
        double mil = lvl.GetProperty("military").GetDouble(), growth = lvl.GetProperty("growth").GetDouble(), research = lvl.GetProperty("research").GetDouble();
        string cost = growth >= 0.999 && research >= 0.999 ? "Growth and research are unhurt."
            : $"Growth runs at {growth * 100:0}% and research at {research * 100:0}%.";
        return $"{mil * 100:0}% of industry goes to the military. {cost}";
    }

    partial void RefreshEconomy()
    {
        var me = Sim.Humanity;
        var slips = me.Yard;
        var shortages = Sim.Shortages(me);
        var h = new HashCode();
        h.Add(me.Id); h.Add(me.Mobilisation);
        h.Add((int)(me.MilShare * 1000)); h.Add((int)(me.Efficiency * 1000));
        h.Add(Q(me.Materiel)); h.Add(Q(me.MadeKgYr)); h.Add(Q(me.UpkeepKgYr)); h.Add(Q(me.LaidUpYr)); h.Add(Q(me.Manpower)); h.Add(string.Join("|", shortages));
        h.Add(me.SlipsWaiting); h.Add(me.SlipsSabotaged); h.Add(slips.Count);
        foreach (var s in slips) { h.Add(s.Hull); h.Add((int)(s.Progress * 100)); h.Add(s.Waiting); }      // per cent, as shown
        foreach (var c in me.Colonies) h.Add(c.YardDownUntil > Sim.Year);
        int sig = h.ToHashCode();
        if (sig == _ecoSig) return;
        _ecoSig = sig;
        Clear(_economy);

        // MOBILISATION
        var lvl = Simulation.Level(me.Mobilisation);
        _economy.AddChild(Gap());
        _economy.AddChild(Ui.Label("MOBILISATION", 14, Ui.Accent, true));
        _economy.AddChild(Ui.Label(lvl.GetProperty("name").GetString(), 15, Ui.Text));
        _economy.AddChild(Note(MobilisationNote(lvl), 13));
        var change = Ui.Button("Change in Policies", "Open the Policies tab, where the level is set");
        change.Pressed += () => Open(TabPolicies);
        var chRow = new HBoxContainer();
        chRow.AddChild(change);
        _economy.AddChild(chRow);

        // CIVILIAN AND MILITARY
        float mil = (float)Math.Clamp(me.MilShare, 0, 1);
        _economy.AddChild(Gap());
        _economy.AddChild(Ui.Label("CIVILIAN AND MILITARY", 14, Ui.Accent, true));
        _economy.AddChild(SplitBar(1 - mil, Ui.Accent, Ui.Danger, SlipBarWidth));
        var split = new HBoxContainer();
        split.AddThemeConstantOverride("separation", 24);
        split.AddChild(Ui.Label($"Civilian {(1 - mil) * 100:0.#}%", 13, Ui.Accent));
        split.AddChild(Ui.Label($"Military {mil * 100:0.#}%", 13, Ui.Danger));
        split.AddChild(Ui.Label($"Military efficiency {me.Efficiency * 100:0}%", 13, Ui.Text));
        _economy.AddChild(split);
        if (me.Efficiency < 0.999) _economy.AddChild(Note("Factories are still being retooled to the new level, so the military share works below full efficiency.", 13));

        // MATERIEL
        _economy.AddChild(Gap());
        _economy.AddChild(Ui.Label("MATERIEL", 14, Ui.Accent, true));
        double net = me.MadeKgYr - me.UpkeepKgYr;
        var g = RowGrid(2);
        g.AddThemeConstantOverride("h_separation", 28);
        void Row(string cap, string val, Color? col = null)
        {
            var c = Ui.Label(cap, 13, Ui.Dim); c.CustomMinimumSize = new Vector2(150, 0);
            // both centred on the row: the value's exponents make it taller than its caption
            g.AddChild(Mid(c)); g.AddChild(Mid(Ui.Label(val, 13, col ?? Ui.Text)));
        }
        Row("In stock", $"{Hud.Sci(me.Materiel)} kg");
        Row("Made per year", $"{Hud.Sci(me.MadeKgYr)} kg");
        Row("Upkeep per year", $"{Hud.Sci(me.UpkeepKgYr)} kg");
        Row("Net per year", (net >= 0 ? "+" : "-") + $"{Hud.Sci(Math.Abs(net))} kg", net >= 0 ? Good : Ui.Danger);
        Row("Manpower", Soldiers(me.Manpower));
        _economy.AddChild(g);

        // SHORTAGES
        _economy.AddChild(Gap());
        _economy.AddChild(Ui.Label("SHORTAGES", 14, Ui.Accent, true));
        if (shortages.Count == 0) _economy.AddChild(Ui.Label("No shortages.", 13, Ui.Dim));
        else foreach (var s in shortages) { var l = Ui.Label(s, 13, Ui.Warm); l.AutowrapMode = TextServer.AutowrapMode.WordSmart; l.SizeFlagsHorizontal = SizeFlags.ExpandFill; _economy.AddChild(l); }

        // SLIPS
        _economy.AddChild(Gap());
        _economy.AddChild(Ui.Label("SLIPS", 14, Ui.Accent, true));
        // each slip counted once: a sabotaged one is sabotaged whether or not it also waits for materiel
        int sabotaged = 0, waiting = 0;
        foreach (var (_, own, down) in SlipsByColony(me)) { if (down) sabotaged += own.Count; else waiting += own.Count(s => s.Waiting); }
        int building = slips.Count - sabotaged - waiting;
        _economy.AddChild(Ui.Label($"{slips.Count:#,0} slips: {building:#,0} building, {waiting:#,0} waiting for materiel, {sabotaged:#,0} sabotaged.", 13, Ui.Text));
        if (slips.Count == 0) return;
        var sg = RowGrid(4);
        foreach (var t in new[] { "System", "Slips", "Progress (mean)", "Held up" }) sg.AddChild(Ui.Label(t, 12, Ui.Dim));
        int shown = 0, hidden = 0;
        foreach (var (c, own, down) in SlipsByColony(me))
        {
            if (shown >= SlipSystemCap) { hidden++; continue; }
            shown++;
            int wait = own.Count(s => s.Waiting);
            float mean = (float)own.Average(s => s.Progress);
            sg.AddChild(Ui.Label(c.Star.Name, 13));
            sg.AddChild(Ui.Label($"{own.Count:#,0}", 13));
            sg.AddChild(ProgressCell(mean));
            var (state, col) = SlipState(c, down, own.Count, wait);
            sg.AddChild(Ui.Label(state, 13, col));
        }
        _economy.AddChild(sg);
        if (hidden > 0) _economy.AddChild(Ui.Label($"and {hidden:#,0} more systems", 13, Ui.Dim));
    }

    /// <summary>One bar split in two: the first colour over the left share, the second over the rest.</summary>
    static Control SplitBar(float left01, Color a, Color b, float width)
    {
        var track = new Panel { CustomMinimumSize = new Vector2(width, 14), MouseFilter = MouseFilterEnum.Ignore };
        var box = new StyleBoxFlat { BgColor = b };
        box.SetBorderWidthAll(1); box.BorderColor = new Color(1, 1, 1, 0.3f);
        track.AddThemeStyleboxOverride("panel", box);
        track.AddChild(new ColorRect { Color = a, Size = new Vector2(width * Math.Clamp(left01, 0, 1), 14), MouseFilter = MouseFilterEnum.Ignore });
        var wrap = new HBoxContainer();
        wrap.AddChild(track);
        return wrap;
    }
}
