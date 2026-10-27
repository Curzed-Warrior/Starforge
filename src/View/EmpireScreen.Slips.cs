using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>The shipyard slips as the Military and Economy tabs both show them: grouped by the colony that owns them.</summary>
public partial class EmpireScreen
{
    /// <summary>Slips are owned by colonies in order: colony i holds the next SlipsOf(i) entries of the yard. Skips colonies with none.</summary>
    IEnumerable<(Colony C, List<Slip> Slips, bool Down)> SlipsByColony(Civ me)
    {
        int at = 0;
        foreach (var c in me.Colonies)
        {
            int n = Math.Max(0, Math.Min(Sim.SlipsOf(c), me.Yard.Count - at));
            var own = me.Yard.Skip(at).Take(n).ToList();
            at += n;
            if (own.Count > 0) yield return (c, own, c.YardDownUntil > Sim.Year);
        }
    }

    /// <summary>What holds a group of slips up, or nothing when they are simply building.</summary>
    (string Text, Color Col) SlipState(Colony c, bool down, int cnt, int waiting) =>
        down ? ($"sabotaged until {c.YardDownUntil:0}", Ui.Danger)
        : waiting > 0 ? (cnt > 1 ? $"{waiting} of {cnt} waiting for materiel" : "waiting for materiel", Ui.Warm)
        : ("", Ui.Dim);          // building, as most slips are: only a hold-up is worth a word

    static Control ProgressCell(float mean)
    {
        var pb = new HBoxContainer();
        pb.AddThemeConstantOverride("separation", 6);
        pb.AddChild(Bar(mean, Ui.Accent, SlipBarWidth));
        pb.AddChild(Ui.Label($"{mean * 100:0}%", 13, Ui.Text));
        return pb;
    }
}
