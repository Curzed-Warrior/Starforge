using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// The Interior tab (F10): how content each system is, what drives its grievance, and the four answers to a movement.
/// Built on opening and on its own Refresh button, never on a timer. Rows are sorted and paged: a late empire has about
/// a thousand systems.
/// </summary>
public partial class EmpireScreen
{
    string _intSort = "Grievance";
    int _intPage;
    const float NameW = 190, BarW = 150, StageW = 100, CauseW = 330;

    static readonly Color[] StageColors =
    {
        new(0.45f, 0.9f, 0.55f), new(0.85f, 0.9f, 0.45f), new(1f, 0.75f, 0.35f), new(1f, 0.5f, 0.3f), new(1f, 0.3f, 0.3f)
    };
    static Color StageColor(int s) => StageColors[Math.Clamp(s, 0, StageColors.Length - 1)];
    static string StageName(int s) => Simulation.UnrestNames[Math.Clamp(s, 0, Simulation.UnrestNames.Length - 1)];

    static Label Cell(string text, float width, Color? col = null, int size = 13)
    {
        var l = Ui.Label(text, size, col ?? Ui.Text);
        l.CustomMinimumSize = new Vector2(width, 0);
        l.ClipText = true;
        return l;
    }

    static HBoxContainer Line(float indent = 0)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 10);
        if (indent > 0) h.AddChild(new Control { CustomMinimumSize = new Vector2(indent, 0), MouseFilter = MouseFilterEnum.Ignore });
        return h;
    }

    string CauseTip(Colony c)
    {
        var lines = Sim.GrievanceCauses(c).Select(x => $"{x.Phrase}: {(x.Points >= 0 ? "+" : "")}{x.Points:0.#}");
        return $"Grievance {c.Grievance:0}. What drives it:\n" + string.Join("\n", lines);
    }

    partial void BuildInterior()
    {
        Clear(_interior);
        var me = Sim.Humanity;
        _interior.AddChild(Gap());
        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 12);
        top.AddChild(Ui.Label("INTERIOR", 14, Ui.Accent, true));
        var refresh = Ui.Button("Refresh", "Rebuild this list from the latest figures");
        refresh.Pressed += BuildInterior;
        top.AddChild(refresh);
        _interior.AddChild(top);

        if (me.M.Hive)
        {
            _interior.AddChild(Note("A hive has no grievances to answer: there is no dissent in it to put down or to heed.", 13));
            return;
        }
        var cols = me.Colonies;
        if (cols.Count == 0) { _interior.AddChild(Note("No systems yet.", 13)); return; }

        var counts = new int[Simulation.UnrestNames.Length];
        foreach (var c in cols) counts[Math.Clamp(c.Unrest, 0, counts.Length - 1)]++;
        var sum = new HBoxContainer();
        sum.AddThemeConstantOverride("separation", 20);
        for (int i = 0; i < counts.Length; i++) sum.AddChild(Ui.Label($"{counts[i]:#,0} {StageName(i)}", 13, counts[i] > 0 ? StageColor(i) : Ui.Dim));
        _interior.AddChild(sum);
        _interior.AddChild(Note("Hover a system to see what drives its grievance. Answer a movement with one of the four buttons; the council's own pick is marked.", 12));

        var sortRow = new HBoxContainer();
        sortRow.AddThemeConstantOverride("separation", 8);
        sortRow.AddChild(Ui.Label("Sort by", 12, Ui.Dim));
        foreach (var k in new[] { "Grievance", "Name", "Stage" })
        {
            var b = Ui.Button(k, "Sort the systems by " + k.ToLower());
            if (k == _intSort) b.Modulate = Ui.Accent;
            string key = k;
            b.Pressed += () => { _intSort = key; _intPage = 0; BuildInterior(); };
            sortRow.AddChild(b);
        }
        _interior.AddChild(sortRow);

        var head = Line();
        head.AddChild(Cell("SYSTEM", NameW, Ui.Dim, 12)); head.AddChild(Cell("GRIEVANCE", BarW, Ui.Dim, 12));
        head.AddChild(Cell("STAGE", StageW, Ui.Dim, 12)); head.AddChild(Cell("MAIN CAUSE", CauseW, Ui.Dim, 12));
        head.AddChild(Cell("FLAGS", 150, Ui.Dim, 12));
        _interior.AddChild(head);

        // home system first, its worlds below it
        var home = cols.FirstOrDefault(c => c.Home);
        if (home != null)
        {
            SystemRow(home);
            foreach (var (b, g) in home.WorldGrievance.OrderByDescending(kv => kv.Value).Select(kv => (kv.Key, kv.Value)))
            {
                int st = home.WorldUnrest.GetValueOrDefault(b);
                var row = Line(24);
                row.AddChild(Cell(b.Name, NameW - 34, Ui.Text)); row.AddChild(Bar(g / 100f, StageColor(st), BarW));
                row.AddChild(Cell(StageName(st), StageW, StageColor(st)));
                row.AddChild(Cell(st >= 3 ? Simulation.MovementName(b) : "", CauseW, Ui.Dim));
                _interior.AddChild(row);
            }
        }

        var rest = cols.Where(c => !c.Home);
        var sorted = _intSort switch
        {
            "Name" => rest.OrderBy(c => c.Star.Name, StringComparer.OrdinalIgnoreCase),
            "Stage" => rest.OrderByDescending(c => c.Unrest).ThenByDescending(c => c.Grievance),
            _ => rest.OrderByDescending(c => c.Grievance),
        };
        var list = sorted.ToList();
        int page = Math.Max(1, (int)WarData.D("interface", "interiorPage"));
        int pages = Math.Max(1, (list.Count + page - 1) / page);
        _intPage = Math.Clamp(_intPage, 0, pages - 1);
        foreach (var c in list.Skip(_intPage * page).Take(page)) SystemRow(c);

        if (pages > 1)
        {
            var nav = new HBoxContainer();
            nav.AddThemeConstantOverride("separation", 12);
            var prev = Ui.Button("Prev", "Previous page"); prev.Disabled = _intPage == 0;
            prev.Pressed += () => { _intPage--; BuildInterior(); };
            var next = Ui.Button("Next", "Next page"); next.Disabled = _intPage >= pages - 1;
            next.Pressed += () => { _intPage++; BuildInterior(); };
            nav.AddChild(prev);
            nav.AddChild(Ui.Label($"Page {_intPage + 1} of {pages}  ({list.Count:#,0} systems)", 13, Ui.Dim));
            nav.AddChild(next);
            _interior.AddChild(nav);
        }
    }

    void SystemRow(Colony c)
    {
        var holder = new VBoxContainer();
        holder.AddThemeConstantOverride("separation", 2);
        var row = Line();
        row.MouseFilter = MouseFilterEnum.Stop;
        row.TooltipText = CauseTip(c);
        var col = StageColor(c.Unrest);
        row.AddChild(Cell(c.Star.Name, NameW, Colors.White));
        row.AddChild(Bar(c.Grievance / 100f, col, BarW));
        row.AddChild(Cell(StageName(c.Unrest), StageW, col));
        row.AddChild(Cell(c.Grievance < 1 ? "" : (c.Cause ?? Sim.GrievanceCauses(c).FirstOrDefault().Phrase ?? ""), CauseW, Ui.Dim));
        var flags = new List<string>();
        if (c.Autonomy) flags.Add("self-rule");
        if (Sim.Year < c.InvestedUntil) flags.Add($"invested until {(int)c.InvestedUntil}");
        row.AddChild(Cell(string.Join(", ", flags), 150, Ui.Dim));
        holder.AddChild(row);
        if (c.Movement != null) holder.AddChild(MovementRow(c));
        _interior.AddChild(holder);
    }

    Control MovementRow(Colony c)
    {
        var row = Line(24);
        row.AddChild(Cell(UpFirst(c.Movement), NameW + BarW, Ui.Warm));
        var pick = Sim.CouncilAnswer(c);
        double invest = WarData.D("strife", "answers", "investCost") * 100, yrs = WarData.D("strife", "answers", "investYears");
        double crack = WarData.D("strife", "answers", "crackdown"), spread = WarData.D("strife", "answers", "crackdownSpread");
        var opts = new (Simulation.Answer A, string Label, string Tip)[]
        {
            (Simulation.Answer.SelfRule, "Grant self-rule", "Its industry stays at home from now on, and the grievance eases. No other cost."),
            (Simulation.Answer.Invest, "Invest", $"Costs {invest:0.#}% of the materiel stock (or of the capital's stockpile). Eases the grievance for {yrs:0} years."),
            (Simulation.Answer.Crackdown, "Crack down", $"Cuts its grievance by {crack:0} now, but every other system gains {spread:0.#} grievance. A Reverent people count it as an atrocity."),
            (Simulation.Answer.Wait, "Wait", "Costs nothing. The movement comes back for an answer in five years."),
        };
        foreach (var (a, label, tip) in opts)
        {
            bool isPick = a == pick;
            var b = Ui.Button(isPick ? label + " (council)" : label, (isPick ? "The council's pick. " : "") + tip);
            if (isPick) b.Modulate = Ui.Accent;
            var colony = c; var ans = a; string lab = label;
            b.Pressed += () => Answer(colony, ans, lab);
            row.AddChild(b);
        }
        return row;
    }

    static string UpFirst(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];

    /// <summary>Answer a movement the way the inbox does when it holds an open proposal for it, then rebuild.</summary>
    void Answer(Colony c, Simulation.Answer a, string label)
    {
        if (c.Civ != Sim.Humanity || c.Movement == null) { BuildInterior(); return; }
        string key = "interior:" + c.Star.Id;
        var open = Sim.Alerts.FirstOrDefault(x => x.Key == key && !x.Done && x.Decision);
        int idx = open?.Choices.FindIndex(ch => ch.Label == label) ?? -1;
        if (idx >= 0) Sim.Decide(open, idx);
        else { Sim.Respond(c, a); Sim.Withdraw(key); }
        _inboxSig = -1;
        BuildInterior();
    }
}
