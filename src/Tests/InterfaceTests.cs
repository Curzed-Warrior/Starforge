using System;
using System.Collections.Generic;
using System.Linq;
using Starforge.Sim;

namespace Starforge.Tests;

/// <summary>Stage 5 self-tests: the war read models the screens show (docs/superpowers/plans/2026-10-21-interface.md).</summary>
public static class InterfaceTests
{
    /// <summary>A fresh war between humanity and the first alien civ, over a border (limited stakes, priced terms).</summary>
    static (Simulation Sim, Civ H, Civ O, War W) BorderWar()
    {
        var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
        return (sim, h, o, sim.DeclareWar(h, o, "border_claim"));
    }

    static double I(string key) => WarData.D("interface", key);

    public static readonly List<(string Name, Action Body)> All = new()
    {
        ("battle_records_bounded", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
            var w = sim.DeclareWar(h, o); int star = h.Colonies[0].Star.Id;
            sim.TestFight(w, star); sim.TestFight(w, star);
            SelfTest.Check(sim.BattlesAt(star, sim.Year - 1).Count() == 2, "both battles recorded at the star");
            var r = sim.Battles[^1];
            SelfTest.Check(r.WarId == w.Id && r.CivA == h.Id && r.CivB == o.Id && r.FamA == Battles.BestFamily(h) && r.FamB == Battles.BestFamily(o), "the record names the war, the sides and their weapons");
            for (int i = 0; i < 400; i++) sim.TestFight(w, star);
            SelfTest.Check(sim.Battles.Count == (int)WarData.D("interface", "battleRecordsMax"), "ring stays bounded");
            SelfTest.Check(!sim.BattlesAt(star, sim.Year + 1).Any(), "nothing after now");
            h.M.Arms.Clear();
            SelfTest.Check(Battles.BestFamily(h) == "kinetic", "unarmed fights with kinetics");
            h.M.Arms["missile"] = 3; h.M.Arms["energy"] = 3; h.M.Arms["craft"] = 2; h.M.Arms["ground"] = 9;
            SelfTest.Check(Battles.BestFamily(h) == "energy", "the highest space weapon, ties in family order");
        }),
        ("fronts_join_the_sides", () => {
            var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
            var w = sim.DeclareWar(h, o); var f = sim.Fronts(w);
            bool owns(int side, int star) => sim.ColonyAt(star) is { } c && (side == 0 ? w.A : w.B).Contains(c.Civ.Id);
            int sideA = 0, sideB = 1;
            SelfTest.Check(f.Count > 0 && f.All(p => owns(sideA, p.From) && owns(sideB, p.To) || owns(sideB, p.From) && owns(sideA, p.To)), "fronts run between the sides");
            SelfTest.Check(f.Distinct().Count() == f.Count, "each front once");
        }),
        ("terms_on_offer_follow_score", () => {
            var (sim, h, o, w) = BorderWar();
            var reason = WarModel.ReasonById(w.Reason);
            w.Score = 60; w.ExhB = 70;
            var (wins, terms) = sim.TermsOnOffer(w, h);
            SelfTest.Check(wins && terms.Count > 0 && terms.All(t => reason.Terms.Contains(t.Id)), "winning: terms from the reason's list");
            SelfTest.Check(!sim.TermsOnOffer(w, o).ViewerWins, "and the other side sees itself losing");
            w.Score = -60; w.ExhB = 0; w.ExhA = 70;
            SelfTest.Check(!sim.TermsOnOffer(w, h).ViewerWins, "losing");
            w.Score = 0;
            var st = sim.TermsOnOffer(w, h).Terms;
            SelfTest.Check(st.Count == 1 && st[0].Id == "ceasefire", "a stalemate on limited stakes offers a ceasefire");
        }),
        ("propose_accept_and_refuse", () => {
            var (sim, h, o, w) = BorderWar();
            w.Score = 60; w.ExhB = 70;
            SelfTest.Check(sim.ProposeTerms(w, h, sim.TermsOnOffer(w, h).Terms, out _) && !w.Open, "the loser takes the terms on offer");
            var (sim2, h2, o2, w2) = BorderWar();
            w2.Score = 0; w2.ExhB = 0;
            var all = WarModel.ReasonById(w2.Reason).Terms.Select(WarModel.TermById).ToList();
            SelfTest.Check(!sim2.ProposeTerms(w2, h2, all, out var why) && !string.IsNullOrEmpty(why) && w2.Open, "asking everything of a fresh enemy is refused");
            SelfTest.Check(!sim2.ProposeTerms(w2, h2, new(), out _), "nothing asked is no offer");
            var (sim3, h3, o3, w3) = BorderWar();
            w3.Score = -60; w3.ExhA = 70;
            var (wins3, terms3) = sim3.TermsOnOffer(w3, h3);
            SelfTest.Check(!wins3 && terms3.Count > 0 && sim3.ProposeTerms(w3, h3, terms3, out _) && !w3.Open, "a loser giving what is demanded ends the war");
        }),
        ("refusal_cooldown", () => {
            var (sim, h, o, w) = BorderWar();
            sim.SetManual(h, Area.Fleets, true); sim.SetManual(o, Area.Fleets, true);
            var all = WarModel.ReasonById(w.Reason).Terms.Select(WarModel.TermById).ToList();
            SelfTest.Check(!sim.ProposeTerms(w, h, all, out _), "refused");
            SelfTest.Check(!sim.ProposeTerms(w, h, all, out var why) && why.Contains("years"), $"no second offer at once ({why})");
            sim.Warp(I("proposeCooldownYears") + 0.1);
            bool again = sim.CanPropose(w, h, out var why2);
            SelfTest.Check(w.Open && again, $"heard again after the cooldown ({why2})");
            sim.EndWarWith(w, true, new());
            SelfTest.Check(!sim.ProposeCeasefire(w, h, out _), "nothing to propose in a closed war");
        }),
        ("ceasefire_needs_tired_enemy", () => {
            var (sim, h, o, w) = BorderWar();
            w.Score = 30;                                      // not a stalemate: only weariness makes them stop
            w.ExhB = 10;
            SelfTest.Check(!sim.ProposeCeasefire(w, h, out var why) && w.Open && !string.IsNullOrEmpty(why), "a fresh enemy fights on");
            w.ExhB = (float)I("ceasefireAcceptExhaustion") + 5;
            SelfTest.Check(WarData.P("stakes", WarModel.ReasonById(w.Reason).Tier, "ceasefire").GetBoolean(), "border stakes allow a ceasefire");
            SelfTest.Check(sim.ProposeCeasefire(w, h, out _) && w.Ceasefire && !w.Open, "a tired one stops");
        }),
        ("ally_cannot_propose", () => {
            var (sim, h, o, w) = BorderWar();
            var x = sim.Civs.First(c => c != h && c != o && !c.Dead && c.Colonies.Count > 0);
            w.A.Add(x.Id);
            SelfTest.Check(!sim.CanPropose(w, x, out var why) && !string.IsNullOrEmpty(why), "only the lead speaks for a side");
            SelfTest.Check(sim.CanPropose(w, h, out _), "the lead can");
        }),
        ("terms_dead_enemy", () => {
            var (sim, h, o, w) = BorderWar();
            o.Dead = true;
            var (wins, terms) = sim.TermsOnOffer(w, h);
            SelfTest.Check(wins && terms.Count == 0, "nothing to ask of the dead");
            SelfTest.Check(!sim.CanPropose(w, h, out _), "and nobody to ask");
        }),
    };
}
