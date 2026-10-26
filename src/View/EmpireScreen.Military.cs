using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// The Military tab (F7): the player's fleets (folded to one row each; unfold for squadrons and, under manual control,
/// orders), armies, special forces, and the queues that feed them. Rebuilt at most every 0.5 s, and only when
/// something shown on it has changed.
/// </summary>
public partial class EmpireScreen
{
    int _milSig = -1;
    readonly HashSet<int> _fleetOpen = new();
    static readonly string[] MissionNames = { "defend", "attack", "raid", "blockade", "escort" };
    const int ArmyCap = 40, OwnSystemCap = 60;
    public const float SlipBarWidth = 240;

    int MilitarySig(Civ me, List<Fleet> fleets, List<Army> armies, List<Team> teams)
    {
        var h = new HashCode();
        h.Add(me.Id); h.Add(me.Policy.IsManual(Area.Fleets));
        foreach (var f in fleets)
        {
            h.Add(f.Id); h.Add(f.At); h.Add(f.To); h.Add(f.Target); h.Add(f.Mission); h.Add(f.Stance);
            h.Add((int)(f.Ships)); h.Add((int)(f.Morale * 20)); h.Add((int)(f.Supply * 20));
            h.Add(f.Moving ? (int)Math.Ceiling(f.Arrive - Sim.Year) : 0);
            h.Add(f.Admiral?.Name); h.Add(f.Admiral?.Wins ?? 0); h.Add(f.Admiral?.Losses ?? 0);
            h.Add(_fleetOpen.Contains(f.Id));
            if (_fleetOpen.Contains(f.Id)) foreach (var s in f.Squadrons) { h.Add(s.Hull); h.Add((int)s.Ships); h.Add(s.Level); }
        }
        h.Add(armies.Count);
        foreach (var a in armies) { h.Add(a.Id); h.Add(a.At); h.Add(a.Fleet); h.Add(a.To); h.Add((long)(a.Troops * 1e6)); h.Add(Forces.Level(a.Xp)); h.Add(a.To >= 0 ? (int)Math.Ceiling((a.Arrive - Sim.Year) * 10) : 0); }
        foreach (var t in teams) { h.Add(t.Id); h.Add(t.At); h.Add(t.To); h.Add(t.Mission); h.Add((int)t.Due); h.Add(t.To >= 0 ? (int)Math.Ceiling((t.Arrive - Sim.Year) * 10) : 0); }
        foreach (var s in me.Yard) { h.Add(s.Hull); h.Add((int)(s.Progress * 20)); h.Add(s.Waiting); }
        foreach (var c in me.Colonies) h.Add(c.YardDownUntil > Sim.Year);
        foreach (var b in me.Barracks) { h.Add((long)(b.Troops * 1e6)); h.Add((int)(b.Left * 4)); }
        foreach (var b in me.Academy) h.Add((int)(b.Left * 4));
        return h.ToHashCode();
    }

    readonly List<OptionButton> _milOpts = new();

    partial void RefreshMilitary()
    {
        // never free a dropdown while the player is choosing from it; try again next tick
        foreach (var ob in _milOpts) if (IsInstanceValid(ob) && ob.GetPopup().Visible) return;
        var me = Sim.Humanity;
        var fleets = Sim.FleetsOf(me).ToList();
        var armies = Sim.ArmiesOf(me).ToList();
        var teams = Sim.TeamsOf(me).ToList();
        int sig = MilitarySig(me, fleets, armies, teams);
        if (sig == _milSig) return;
        _milSig = sig;
        _milOpts.Clear();
        Clear(_military);

        // FLEETS
        _military.AddChild(Gap());
        _military.AddChild(Ui.Label("FLEETS", 14, Ui.Accent, true));
        if (fleets.Count == 0) _military.AddChild(Ui.Label("No fleets.", 15, Ui.Text));
        else
        {
            bool manual = me.Policy.IsManual(Area.Fleets);
            _military.AddChild(Note(manual
                ? "You command the fleets. Unfold one to give it a mission and a target."
                : "The council runs the fleets. Unfold one to see its squadrons."));
            List<(int Id, string Name)> targets = null;
            foreach (var f in fleets)
            {
                bool open = _fleetOpen.Contains(f.Id);
                _military.AddChild(FleetHeader(f, open));
                if (!open) continue;
                targets ??= manual ? OrderTargets(me) : new();
                _military.AddChild(FleetDetail(f, manual, targets));
            }
        }

        // ARMIES
        _military.AddChild(Gap());
        _military.AddChild(Ui.Label("ARMIES", 14, Ui.Accent, true));
        if (armies.Count == 0) _military.AddChild(Ui.Label("No armies.", 15, Ui.Text));
        else
        {
            var g = RowGrid(5);
            foreach (var t in new[] { "Where", "Troops", "Quality", "Experience", "Training" }) g.AddChild(Ui.Label(t, 12, Ui.Dim));
            var groups = armies.GroupBy(a => (Where: ArmyWhere(a), Q: $"{a.Quality:0.00}", Lv: Forces.Level(a.Xp), a.Rushed)).ToList();
            foreach (var grp in groups.Take(ArmyCap))
            {
                int n = grp.Count();
                g.AddChild(Ui.Label(n > 1 ? $"{grp.Key.Where}  ×{n}" : grp.Key.Where, 13));
                g.AddChild(Ui.Label(Soldiers(grp.Sum(a => a.Troops)), 13));
                g.AddChild(Ui.Label(grp.Key.Q, 13));
                g.AddChild(Ui.Label(grp.Key.Lv, 13));
                g.AddChild(Ui.Label(grp.Key.Rushed ? "rushed" : "full", 13, grp.Key.Rushed ? Ui.Warm : Ui.Dim));
            }
            _military.AddChild(g);
            if (groups.Count > ArmyCap) _military.AddChild(Ui.Label($"and {groups.Skip(ArmyCap).Sum(x => x.Count()):#,0} more armies", 13, Ui.Dim));
        }

        // SPECIAL FORCES
        _military.AddChild(Gap());
        _military.AddChild(Ui.Label("SPECIAL FORCES", 14, Ui.Accent, true));
        if (teams.Count == 0) _military.AddChild(Ui.Label("No teams.", 15, Ui.Text));
        else
        {
            var g = RowGrid(6);
            foreach (var t in new[] { "Team", "Mission", "Target", "Quality", "Training", "Status" }) g.AddChild(Ui.Label(t, 12, Ui.Dim));
            var groups = teams.GroupBy(t => (t.Mission, Target: t.Target >= 0 ? StarName(t.Target) : "none", Q: $"{t.Quality:0.00}", t.Rushed,
                Status: t.To >= 0 ? $"travelling to {StarName(t.To)}, arrives in {Years(t.Arrive - Sim.Year)}"
                    : t.Due > 0 ? $"on site at {StarName(t.At)}, due {t.Due:0}" : $"at {StarName(t.At)}")).ToList();
            foreach (var grp in groups.Take(ArmyCap))
            {
                var k = grp.Key; int n = grp.Count();
                g.AddChild(Ui.Label(n > 1 ? $"{grp.First().Name ?? "Team"}  ×{n}" : grp.First().Name ?? $"Team {grp.First().Id}", 13));
                g.AddChild(Ui.Label(k.Mission, 13));
                g.AddChild(Ui.Label(k.Target, 13));
                g.AddChild(Ui.Label(k.Q, 13));
                g.AddChild(Ui.Label(k.Rushed ? "rushed" : "full", 13, k.Rushed ? Ui.Warm : Ui.Dim));
                g.AddChild(Ui.Label(k.Status, 13));
            }
            _military.AddChild(g);
            if (groups.Count > ArmyCap) _military.AddChild(Ui.Label($"and {groups.Skip(ArmyCap).Sum(x => x.Count()):#,0} more teams", 13, Ui.Dim));
        }

        // QUEUES
        _military.AddChild(Gap());
        _military.AddChild(Ui.Label("SHIPYARDS", 14, Ui.Accent, true));
        if (me.Yard.Count == 0) _military.AddChild(Ui.Label("No ships are being built.", 15, Ui.Text));
        else
        {
            _military.AddChild(Note("Slips grouped by system and hull class."));
            var g = RowGrid(3);
            foreach (var t in new[] { "System · hull class", "Progress (mean)", "State" }) g.AddChild(Ui.Label(t, 12, Ui.Dim));
            const int SlipRows = 30;
            int rows = 0, hiddenSlips = 0, hiddenSys = 0;
            foreach (var (c, own, down) in SlipsByColony(me))
            {
                var groups = own.GroupBy(s => s.Hull).ToList();
                if (rows + groups.Count > SlipRows) { hiddenSlips += own.Count; hiddenSys++; continue; }
                foreach (var grp in groups)
                {
                    rows++;
                    int cnt = grp.Count(), wait = grp.Count(s => s.Waiting);
                    float mean = (float)grp.Average(s => s.Progress);
                    g.AddChild(Ui.Label($"{c.Star.Name} · {Ships.ClassNames[grp.Key]}" + (cnt > 1 ? $"  ×{cnt}" : ""), 13));
                    g.AddChild(ProgressCell(mean));
                    var (state, col) = SlipState(c, down, cnt, wait);
                    g.AddChild(Ui.Label(state, 13, col));
                }
            }
            _military.AddChild(g);
            if (hiddenSlips > 0) _military.AddChild(Ui.Label($"and {hiddenSlips:#,0} more slips in {hiddenSys:#,0} systems", 13, Ui.Dim));
        }

        _military.AddChild(Gap());
        _military.AddChild(Ui.Label("BARRACKS", 14, Ui.Accent, true));
        if (me.Barracks.Count == 0) _military.AddChild(Ui.Label("No recruits in training.", 15, Ui.Text));
        else
        {
            var g = RowGrid(4);
            foreach (var t in new[] { "At", "Troops", "Years left", "Training" }) g.AddChild(Ui.Label(t, 12, Ui.Dim));
            foreach (var b in me.Barracks.Take(ArmyCap))
            {
                g.AddChild(Ui.Label(StarName(b.At), 13));
                g.AddChild(Ui.Label($"{Soldiers(b.Troops)}", 13));
                g.AddChild(Ui.Label($"{b.Left:0.0}", 13));
                g.AddChild(Ui.Label(b.Rushed ? "rushed" : "full", 13, b.Rushed ? Ui.Warm : Ui.Dim));
            }
            _military.AddChild(g);
            if (me.Barracks.Count > ArmyCap) _military.AddChild(Ui.Label($"and {me.Barracks.Count - ArmyCap:#,0} more", 13, Ui.Dim));
        }

        _military.AddChild(Gap());
        _military.AddChild(Ui.Label("ACADEMY", 14, Ui.Accent, true));
        if (me.Academy.Count == 0) _military.AddChild(Ui.Label("No special-forces courses under way.", 15, Ui.Text));
        else
        {
            var g = RowGrid(3);
            foreach (var t in new[] { "At", "Years left", "Training" }) g.AddChild(Ui.Label(t, 12, Ui.Dim));
            foreach (var b in me.Academy)
            {
                g.AddChild(Ui.Label(StarName(b.At), 13));
                g.AddChild(Ui.Label($"{b.Left:0.0}", 13));
                g.AddChild(Ui.Label(b.Rushed ? "rushed" : "full", 13, b.Rushed ? Ui.Warm : Ui.Dim));
            }
            _military.AddChild(g);
        }
    }

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

    (string Text, Color Col) SlipState(Colony c, bool down, int cnt, int waiting) =>
        down ? ($"sabotaged until {c.YardDownUntil:0}", Ui.Danger)
        : waiting > 0 ? (cnt > 1 ? $"{waiting} of {cnt} waiting for materiel" : "waiting for materiel", Ui.Warm)
        : ("building", Ui.Dim);

    static Control ProgressCell(float mean)
    {
        var pb = new HBoxContainer();
        pb.AddThemeConstantOverride("separation", 6);
        pb.AddChild(Bar(mean, Ui.Accent, SlipBarWidth));
        pb.AddChild(Ui.Label($"{mean * 100:0}%", 13, Ui.Text));
        return pb;
    }

    /// <summary>Troops are held in billions of people; show them as people.</summary>
    static string Soldiers(double billions) => billions >= 1 ? $"{billions:0.0} billion" : billions >= 0.001 ? $"{billions * 1000:#,0} million" : $"{billions * 1e9:#,0}";

    string ArmyWhere(Army a) => a.Fleet >= 0 ? $"aboard {Sim.Fleets.FirstOrDefault(x => x.Id == a.Fleet)?.Name ?? "a fleet"}"
        : a.To >= 0 ? $"{StarName(a.At)} to {StarName(a.To)}, lands in {Years(a.Arrive - Sim.Year)}" : StarName(a.At);

    static string Years(double y) => $"{Math.Max(0, y):0.0} y";

    /// <summary>Two lines per fleet: fixed-width cells (fold, name, ships, morale, supply, stance), then admiral and orders.</summary>
    Control FleetHeader(Fleet f, bool open)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 0);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var fold = Ui.Button(open ? "▾" : "▸", open ? "Fold this fleet" : "Show squadrons");
        fold.CustomMinimumSize = new Vector2(34, 0);
        int id = f.Id;
        fold.Pressed += () => { if (!_fleetOpen.Remove(id)) _fleetOpen.Add(id); _milSig = -1; RefreshMilitary(); };
        row.AddChild(fold);
        var name = Ui.Button(f.Name, "Fly to this fleet");
        name.CustomMinimumSize = new Vector2(150, 0);
        int at = f.At;
        name.Pressed += () => { Visible = false; _main.TravelTo(Sim.Gal.Stars[at]); };
        row.AddChild(name);
        row.AddChild(Cell(Ui.Label($"{f.Ships:#,0} ships", 13, Ui.Warm), 90));
        row.AddChild(Meter("morale", f.Morale, Good));
        row.AddChild(Meter("supply", f.Supply, Ui.Warm.Lightened(0.15f)));
        row.AddChild(Cell(Ui.Label(f.Stance, 13, Ui.Dim), 80));
        box.AddChild(row);
        var a = f.Admiral;
        string who = a == null ? "no admiral" : $"Adm. {a.Name} · skill {a.Skill:0.00} · {a.Trait} · {a.Wins}W {a.Losses}L";
        string where = f.Moving ? $"{f.Mission}: in transit to {StarName(f.To)}, arrives in {Years(f.Arrive - Sim.Year)}"
            : f.Target >= 0 && f.Target != f.At ? $"{f.Mission} at {StarName(f.Target)}" : $"{f.Mission}, at {StarName(f.At)}";
        var line = Ui.Label($"{who}   ·   {where}", 13, Ui.Text);
        var m = new MarginContainer();
        m.AddThemeConstantOverride("margin_left", 44);
        m.AddThemeConstantOverride("margin_bottom", 4);
        m.AddChild(line);
        box.AddChild(m);
        return box;
    }

    static Control Cell(Label l, float w) { l.CustomMinimumSize = new Vector2(w, 0); l.SizeFlagsVertical = SizeFlags.ShrinkCenter; return l; }

    static Control Meter(string caption, float v01, Color col)
    {
        var h = new HBoxContainer { CustomMinimumSize = new Vector2(150, 0) };
        h.AddThemeConstantOverride("separation", 4);
        h.AddChild(Cell(Ui.Label(caption, 12, Ui.Dim), 44));
        h.AddChild(Bar(v01, col, 60));
        h.AddChild(Cell(Ui.Label($"{Math.Clamp(v01, 0, 1) * 100:0}%", 12, Ui.Text), 34));
        return h;
    }

    /// <summary>Squadron grid, and the order buttons (disabled with the council's choice when the Fleets area is automatic).</summary>
    Control FleetDetail(Fleet f, bool manual, List<(int Id, string Name)> targets)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        var g = RowGrid(3);
        foreach (var t in new[] { "Hull class", "Ships", "Experience" }) g.AddChild(Ui.Label(t, 12, Ui.Dim));
        foreach (var s in f.Squadrons)
        {
            g.AddChild(Ui.Label(Ships.ClassNames[s.Hull], 13));
            g.AddChild(Ui.Label($"{s.Ships:#,0}", 13));
            g.AddChild(Ui.Label(Forces.Level(s.Xp), 13));
        }
        box.AddChild(g);

        var orders = new HBoxContainer();
        orders.AddThemeConstantOverride("separation", 8);
        string tip = manual ? "" : "The council runs Fleets. Take it in Policies.";
        var mission = new OptionButton { TooltipText = tip, Disabled = !manual };
        foreach (var m in MissionNames) mission.AddItem(m);
        int mi = Array.IndexOf(MissionNames, f.Mission);
        if (mi < 0) { mission.AddItem(f.Mission); mi = MissionNames.Length; }
        mission.Selected = mi;
        _milOpts.Add(mission);
        var target = new OptionButton { TooltipText = tip, Disabled = !manual };
        _milOpts.Add(target);
        var list = new List<(int Id, string Name)>(targets);
        int want = f.Target >= 0 ? f.Target : f.At;
        if (!list.Any(t => t.Id == want)) list.Insert(0, (want, StarName(want)));
        for (int i = 0; i < list.Count; i++) { target.AddItem(list[i].Name, list[i].Id); if (list[i].Id == want) target.Selected = i; }
        if (manual)
        {
            mission.ItemSelected += i => Sim.Order(f, f.Target >= 0 ? f.Target : f.At, mission.GetItemText((int)i));
            target.ItemSelected += i => Sim.Order(f, target.GetItemId((int)i), f.Mission);
        }
        orders.AddChild(Mid(Ui.Label("Mission", 13, Ui.Dim))); orders.AddChild(mission);
        orders.AddChild(Mid(Ui.Label("Target", 13, Ui.Dim))); orders.AddChild(target);
        box.AddChild(orders);
        var p = new MarginContainer();
        p.AddThemeConstantOverride("margin_left", 28);
        p.AddThemeConstantOverride("margin_bottom", 6);
        p.AddChild(box);
        return p;
    }

    /// <summary>Where a fleet can be sent: own systems, the claims and the front targets of open wars.</summary>
    List<(int Id, string Name)> OrderTargets(Civ me)
    {
        var ids = new List<int>();
        foreach (var c in me.Colonies.Take(OwnSystemCap)) ids.Add(c.Star.Id);
        foreach (var w in Sim.Wars.Where(w => w.Open && w.Has(me.Id)))
        {
            ids.AddRange(w.Claims);
            foreach (var (_, to) in Sim.Fronts(w)) ids.Add(to);
        }
        return ids.Distinct().Select(i => (i, StarName(i))).ToList();
    }
}
