using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// The Wars tab (F6): one card per open war the player's polity is in, seen from its side — score, exhaustion,
/// battles, fronts, claims and the peace on offer, with the buttons to propose terms or a ceasefire — then the last
/// few wars that ended. Rebuilt at most every 0.5 s, and only when something on it has changed.
/// </summary>
public partial class EmpireScreen
{
    int _warsSig = -1;
    int _warFocus = -1;
    readonly HashSet<int> _askOpen = new();
    readonly Dictionary<int, HashSet<string>> _ask = new();
    static readonly Color Good = new(0.45f, 0.9f, 0.55f);

    /// <summary>Open the Wars tab scrolled to one war.</summary>
    public void ShowWar(int warId) { _warFocus = warId; Open(TabWars); }

    int WarsSig(Civ me, List<War> open)
    {
        var h = new HashCode();
        h.Add(me.Id);
        h.Add((int)(Sim.Year * 2));
        h.Add(Sim.Wars.Count(w => !w.Open));
        foreach (var w in open)
        {
            h.Add(w.Id); h.Add((int)w.Score); h.Add((int)w.ExhA); h.Add((int)w.ExhB); h.Add(w.Battles);
            h.Add(_askOpen.Contains(w.Id));
        }
        return h.ToHashCode();
    }

    void RefreshWars()
    {
        var me = Sim.Humanity;
        var open = Sim.Wars.Where(w => w.Open && Simulation.Side(w, me) >= 0).ToList();
        int sig = WarsSig(me, open);
        if (sig == _warsSig) return;
        _warsSig = sig;
        Clear(_wars);
        _wars.AddChild(Ui.Label("WARS", 14, Ui.Accent, true));
        if (open.Count == 0) _wars.AddChild(Note($"{Narrative2.Called(me, true)} is at peace."));
        foreach (var w in open) WarCard(w, me);

        var past = Sim.Wars.Where(w => !w.Open && Simulation.Side(w, me) >= 0).Reverse().Take(5).ToList();
        if (past.Count > 0)
        {
            _wars.AddChild(Ui.Label("PAST WARS", 14, Ui.Accent, true));
            foreach (var w in past) _wars.AddChild(EndingLine(w, me));
        }

        if (_warFocus >= 0)
        {
            int f = _warFocus; _warFocus = -1;
            Callable.From(() =>
            {
                foreach (var n in _wars.GetChildren())
                    if (n is Control c && c.HasMeta("war") && (int)c.GetMeta("war") == f) { (_wars.GetParent() as ScrollContainer)?.EnsureControlVisible(c); break; }
            }).CallDeferred();
        }
    }

    string CivName(Civ c, bool cap = false) => Narrative2.Called(c, cap) + (c.Dead ? " (gone)" : "");
    string StarName(int id) => id >= 0 && id < Sim.Gal.Stars.Count ? Sim.Gal.Stars[id].Name : "?";

    static string Stakes(string tier) => tier switch
    {
        "limited" => "limited stakes", "principle" => "a war of principle", "survival" => "a war of survival", _ => tier
    };

    Label EndingLine(War w, Civ me)
    {
        int s = Simulation.Side(w, me);
        var enemy = Sim.Lead(w, 1 - s);
        var l = Ui.Label($"{w.Start:0}  ·  {CivName(enemy, true)}  ·  {WarModel.ReasonById(w.Reason).Name}: {w.Ending}.", 13, Ui.Dim);
        l.SetMeta("war", w.Id);
        return l;
    }

    void WarCard(War w, Civ me)
    {
        int s = Simulation.Side(w, me);
        var enemy = Sim.Lead(w, 1 - s);
        var reason = WarModel.ReasonById(w.Reason);
        var p = new PanelContainer();
        p.SetMeta("war", w.Id);
        p.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.05f, 0.07f, 0.1f, 0.9f), Ui.Danger * new Color(1, 1, 1, 0.45f), 3, 8));
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 6);
        p.AddChild(v);

        // header: who, why, how long
        var head = new HBoxContainer();
        head.AddChild(Ui.Label(CivName(enemy, true), 17, enemy.Color, true));
        var allies = (s == 0 ? w.B : w.A).Skip(1).Select(id => CivName(Sim.Civs[id])).ToList();
        if (allies.Count > 0) head.AddChild(Ui.Label($"with {string.Join(", ", allies)}", 13, Ui.Text));
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        int years = (int)Math.Max(0, Sim.Year - w.Start);
        head.AddChild(Ui.Label($"{reason.Name} · {Stakes(reason.Tier)} · {years} {(years == 1 ? "year" : "years")} at war", 13, Ui.Warm));
        v.AddChild(head);
        var ours = (s == 0 ? w.A : w.B).Skip(1).Select(id => CivName(Sim.Civs[id])).ToList();
        if (ours.Count > 0) v.AddChild(Ui.Label($"On our side: {string.Join(", ", ours)}", 12, Ui.Dim));

        // score and exhaustion
        float score = s == 0 ? w.Score : -w.Score;
        float exhOurs = s == 0 ? w.ExhA : w.ExhB, exhTheirs = s == 0 ? w.ExhB : w.ExhA;
        var g = RowGrid(3);
        g.AddChild(Caption("War score"));
        g.AddChild(ScoreBar(score, 320));
        g.AddChild(Ui.Label($"{score:+0;-0;0}", 13, score > 0 ? Good : score < 0 ? Ui.Danger : Ui.Text, true));
        g.AddChild(Caption("Our exhaustion"));
        g.AddChild(Bar(exhOurs / 100f, Ui.Warm, 320));
        g.AddChild(Ui.Label($"{exhOurs:0}", 13));
        g.AddChild(Caption("Their exhaustion"));
        g.AddChild(Bar(exhTheirs / 100f, Ui.Warm, 320));
        g.AddChild(Ui.Label($"{exhTheirs:0}", 13));
        v.AddChild(g);

        // battles: hulls lost from the records of this war (the records keep only recent battles)
        int won = s == 0 ? w.WonA : w.WonB, lost = s == 0 ? w.WonB : w.WonA;
        double hullsOurs = 0, hullsTheirs = 0; int counted = 0;
        BattleRecord last = null;
        foreach (var r in Sim.Battles)
        {
            if (r.WarId != w.Id) continue;
            counted++; last = r;
            bool aIsUs = r.CivA < Sim.Civs.Count && Simulation.Side(w, Sim.Civs[r.CivA]) == s;
            hullsOurs += aIsUs ? r.LostA : r.LostB;
            hullsTheirs += aIsUs ? r.LostB : r.LostA;
        }
        double kgOurs = s == 0 ? w.LostKgA : w.LostKgB, kgTheirs = s == 0 ? w.LostKgB : w.LostKgA;
        var brow = new HFlowContainer();
        brow.AddChild(Caption("Battles"));
        string recent = counted < w.Battles ? $" (hulls from the last {counted})" : "";
        brow.AddChild(Mid(Ui.Label($"{w.Battles}: won {won}, lost {lost}  ·  ships lost: ours {hullsOurs:0} hulls ({Hud.Sci(kgOurs)} kg), theirs {hullsTheirs:0} hulls ({Hud.Sci(kgTheirs)} kg){recent}", 13)));
        if (last != null)
        {
            var star = last.Star;
            var go = Ui.Button($"Last battle: {StarName(star)}, {last.Year:0}", "Fly there");
            go.Pressed += () => { Visible = false; if (star >= 0 && star < Sim.Gal.Stars.Count) _main.TravelTo(Sim.Gal.Stars[star]); };
            brow.AddChild(go);
        }
        v.AddChild(brow);

        // fronts (one scan per war per rebuild) and the systems occupied in this war
        var fronts = Sim.Fronts(w);
        var frow = new HFlowContainer();
        frow.AddChild(Caption("Fronts"));
        if (fronts.Count == 0) frow.AddChild(Mid(Ui.Label("none yet", 13, Ui.Dim)));
        foreach (var (from, to) in fronts)
        {
            int target = to;
            var b = Ui.Button($"{StarName(from)} → {StarName(to)}", "Fly to the target");
            b.Pressed += () => { Visible = false; _main.TravelTo(Sim.Gal.Stars[target]); };
            frow.AddChild(b);
        }
        v.AddChild(frow);
        var occupied = new List<(Colony C, Civ Holder)>();
        foreach (var id in w.A.Concat(w.B))
            foreach (var c in Sim.Civs[id].Colonies)
                if (c.OccupiedBy >= 0 && c.OccupiedBy < Sim.Civs.Count && Simulation.Side(w, Sim.Civs[c.OccupiedBy]) is int hs && hs >= 0 && hs != Simulation.Side(w, c.Civ))
                    occupied.Add((c, Sim.Civs[c.OccupiedBy]));
        if (occupied.Count > 0)
        {
            var orow = new HFlowContainer();
            orow.AddChild(Caption("Occupied"));
            foreach (var (c, holder) in occupied.Take(40))
                orow.AddChild(Mid(Ui.Label($"{c.Sys.Name} ({Narrative2.Called(holder)})", 13, holder.Color)));
            if (occupied.Count > 40) orow.AddChild(Mid(Ui.Label($"and {occupied.Count - 40} more", 13, Ui.Dim)));
            v.AddChild(orow);
        }

        // the attacker's claims, held by our side or lost to theirs
        if (w.Claims.Count > 0)
        {
            var crow = new HFlowContainer();
            crow.AddChild(Caption(s == 0 ? "Our claims" : "Their claims"));
            foreach (var id in w.Claims)
            {
                var col = Sim.ColonyAt(id);
                bool held = col != null && Simulation.Side(w, Sim.Holder(col)) == s;
                crow.AddChild(Mid(Ui.Label($"{StarName(id)}: {(held ? "held" : "lost")}", 13, held ? Good : Ui.Danger)));
            }
            v.AddChild(crow);
        }

        // the peace on offer, and the buttons
        var (weWin, terms) = Sim.TermsOnOffer(w, me);
        string names = string.Join(", ", terms.Select(t => t.Name.ToLower()));
        v.AddChild(Ui.Label(terms.Count == 0 ? "No peace is on offer yet." : weWin ? $"They would give: {names}." : $"They demand: {names}.", 14, weWin ? Good : Ui.Warm));

        bool can = Sim.CanPropose(w, me, out string why);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var propose = Ui.Button("Propose these terms", can ? (terms.Count == 0 ? "Nothing is on offer yet." : "Put the peace on offer to them") : why);
        propose.Disabled = !can || terms.Count == 0;
        propose.Pressed += () => Proposed(w, me, Sim.ProposeTerms(w, me, terms, out string r), r);
        row.AddChild(propose);
        bool hasTerms = reason.Terms.Length > 0;
        var askB = Ui.Button(_askOpen.Contains(w.Id) ? "Ask for… ▴" : "Ask for…", can ? (hasTerms ? "Choose the terms to ask for" : "This war has no terms: it ends when one side collapses.") : why);
        askB.Disabled = !can || !hasTerms;
        askB.Pressed += () => { if (!_askOpen.Remove(w.Id)) _askOpen.Add(w.Id); _warsSig = -1; RefreshWars(); };
        row.AddChild(askB);
        bool truce = WarData.P("stakes", reason.Tier, "ceasefire").GetBoolean();
        var cease = Ui.Button("Propose a ceasefire", !can ? why : truce ? "Offer to stop fighting where the lines are" : "This war is fought to the end: there is no ceasefire.");
        cease.Disabled = !can || !truce;
        cease.Pressed += () => Proposed(w, me, Sim.ProposeCeasefire(w, me, out string r), r);
        row.AddChild(cease);
        v.AddChild(row);

        if (can && hasTerms && _askOpen.Contains(w.Id))
        {
            var chosen = _ask.TryGetValue(w.Id, out var set) ? set : _ask[w.Id] = new HashSet<string>();
            var arow = new HFlowContainer();
            foreach (var t in reason.Terms.Select(WarModel.TermById))
            {
                string id = t.Id;
                var cb = new CheckBox { Text = t.Name, ButtonPressed = chosen.Contains(id), FocusMode = FocusModeEnum.None };
                cb.Toggled += on => { if (on) chosen.Add(id); else chosen.Remove(id); };
                arow.AddChild(cb);
            }
            var send = Ui.Button("Propose", "Put these terms to them");
            send.Pressed += () =>
            {
                var ask = reason.Terms.Where(chosen.Contains).Select(WarModel.TermById).ToList();
                Proposed(w, me, Sim.ProposeTerms(w, me, ask, out string r), r);
            };
            arow.AddChild(send);
            v.AddChild(arow);
        }
        _wars.AddChild(p);
    }

    void Proposed(War w, Civ me, bool ok, string why)
    {
        if (ok)
        {
            int s = Simulation.Side(w, me);
            _main.Hud.Toast($"Peace with {Narrative2.Called(Sim.Lead(w, 1 - s))}: {w.Ending}.");
            _askOpen.Remove(w.Id); _ask.Remove(w.Id);
        }
        else _main.Hud.Toast(why ?? "They refuse.");
        _warsSig = -1;
        RefreshWars();
    }

    static Label Caption(string text)
    {
        var l = Mid(Ui.Label(text, 13, Ui.Dim));
        l.CustomMinimumSize = new Vector2(130, 0);
        return l;
    }

    /// <summary>Centre a label on its row, level with the buttons beside it.</summary>
    static Label Mid(Label l) { l.SizeFlagsVertical = SizeFlags.ShrinkCenter; return l; }

    /// <summary>A bar centred on zero for a score of −100..100: green to the right when winning, red to the left when losing.</summary>
    static Control ScoreBar(float score, float width)
    {
        float half = width / 2, v = Math.Clamp(score / 100f, -1, 1) * half;
        var track = new ColorRect { Color = new Color(1, 1, 1, 0.08f), CustomMinimumSize = new Vector2(width, 10), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        track.AddChild(new ColorRect { Color = v >= 0 ? Good : Ui.Danger, Position = new Vector2(v >= 0 ? half : half + v, 0), Size = new Vector2(Math.Abs(v), 10), MouseFilter = MouseFilterEnum.Ignore });
        track.AddChild(new ColorRect { Color = Ui.Dim, Position = new Vector2(half - 1, -2), Size = new Vector2(2, 14), MouseFilter = MouseFilterEnum.Ignore });
        return track;
    }
}
