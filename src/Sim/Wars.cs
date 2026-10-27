using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

/// <summary>
/// One war: the two sides (leads first, then allies), the reason it is fought, the systems the attacker claims, and how
/// it is going (score from side A's view, each side's exhaustion, battles). A closed war keeps its record and its ending.
/// </summary>
public class War
{
    public int Id;
    public List<int> A = new(), B = new();
    public string Reason;
    public List<int> Claims = new();
    public double Start;
    public float Score, ExhA, ExhB;
    public int Battles, WonA, WonB;
    public double LostKgA, LostKgB, NavyKgA0, NavyKgB0, YearKgA, YearKgB, LastLog = -1e9, LastYear = -1e9;
    public bool Ceasefire;
    public string Ending;
    public bool Open => Ending == null;
    public bool Has(int civ) => A.Contains(civ) || B.Contains(civ);
}

public partial class Simulation
{
    public readonly List<War> Wars = new();
    public int NextWarId = 1;
    static double F(string key) => WarData.D("field", key);

    /// <summary>The open war with these two on opposite sides, or null.</summary>
    public War WarBetween(Civ a, Civ b) =>
        Wars.FirstOrDefault(w => w.Open && (w.A.Contains(a.Id) && w.B.Contains(b.Id) || w.B.Contains(a.Id) && w.A.Contains(b.Id)));

    /// <summary>Years for a ship of this civ to cross between two stars: a lane or a pair of own gateways is quick, otherwise the drive's speed.</summary>
    public double TravelYears(Civ civ, StarInfo a, StarInfo b)
    {
        if (a == b) return 0;
        if (LaneFrom(a, b) != null) return 0.1;
        var ca = ColonyAtAny(a.Id); var cb = ca == null ? null : ColonyAtAny(b.Id);
        if (ca != null && cb != null && ca.Civ == civ && cb.Civ == civ && ca.Done("gateway") && cb.Done("gateway")) return 0.1;
        return 0.15 + a.Pos.DistanceTo(b.Pos) * Galaxy.LyPerUnit / Math.Max(ShipSpeed(civ), 0.01f);
    }

    /// <summary>Can either side's ships reach the other within a war's lifetime? Wars between unreachable empires never fight.</summary>
    public bool CanReach(Civ a, Civ b)
    {
        double years = F("maxWarTravelYears");
        double gu = years * Math.Max(Math.Max(ShipSpeed(a), ShipSpeed(b)), 0.01f) / Galaxy.LyPerUnit;
        float g2 = (float)(gu * gu);
        foreach (var x in a.Colonies)
            foreach (var y in b.Colonies)
                if (x.Star.Pos.DistanceSquaredTo(y.Star.Pos) <= g2 || LaneFrom(x.Star, y.Star) != null) return true;
        return false;
    }

    /// <summary>What a council fights for, from its ethic, its strength against the target and their history.</summary>
    public string ChooseReason(Civ a, Civ b)
    {
        double sa = WarStrength(a), sb = Math.Max(WarStrength(b), 1e-9);
        if (a.Ethic == "Ruthless" && a.Species != b.Species && sa >= 2 * sb) return "extermination";
        if (sa >= 3 * sb && (a.Imperial || a.Ethic == "Ruthless")) return "subjugation";
        if (b.Meter("Atrocity") > 10 || a.LastLoss > Year - 50) return "revenge";
        if (a.Ethic != b.Ethic && (a.Ethic == "Reverent" || b.Ethic == "Reverent")) return "ideology";
        return "border_claim";
    }

    /// <summary>
    /// Go to war, for a reason (the council's choice when none is given). Allies join: the defender's federation and
    /// overlord, the attacker's federation members who dislike the target too, and anyone already fighting the other lead.
    /// Returns null when no war can start (already at war, a truce, the law forbids it, or a side is gone).
    /// </summary>
    public War DeclareWar(Civ attacker, Civ defender, string reason = null)
    {
        if (attacker == defender || attacker.Dead || defender.Dead || attacker.Colonies.Count == 0 || defender.Colonies.Count == 0) return null;
        if (WarBetween(attacker, defender) != null || attacker.AtWar.Contains(defender.Id) || Laws.Contains("nowar")) return null;
        if (attacker.Truce.GetValueOrDefault(defender.Id) > Year || defender.Truce.GetValueOrDefault(attacker.Id) > Year) return null;
        var w = Open(attacker, defender, reason ?? ChooseReason(attacker, defender));
        // claims: the defender's systems nearest the attacker, never its homeworld while it has others
        w.Claims = defender.Colonies.Where(c => !c.Home || defender.Colonies.Count == 1)
            .OrderBy(c => attacker.Colonies.Min(x => x.Star.Pos.DistanceSquaredTo(c.Star.Pos)))
            .Take((int)F("claimsMax")).Select(c => c.Star.Id).ToList();
        // allies
        foreach (var o in Civs)
        {
            if (o.Dead || o.Colonies.Count == 0 || w.Has(o.Id)) continue;
            bool defends = defender.Federation >= 0 && o.Federation == defender.Federation || defender.Overlord == o || o.AtWar.Contains(attacker.Id);
            bool joins = !defends && (attacker.Federation >= 0 && o.Federation == attacker.Federation && o.Relations.GetValueOrDefault(defender.Id) < -30
                                      || o.AtWar.Contains(defender.Id));
            if (defends) w.B.Add(o.Id); else if (joins) w.A.Add(o.Id);
        }
        foreach (var x in w.A) foreach (var y in w.B) { Civs[x].AtWar.Add(y); Civs[y].AtWar.Add(x); }
        var col = (attacker.Main ? attacker : defender.Main ? defender : attacker).Colonies[0];
        string why = WarModel.ReasonById(w.Reason).Name.ToLower();
        Log(col, $"War between {Narrative2.Called(attacker)} and {Narrative2.Called(defender)}, over {why}.", "loss", null);
        if (attacker.Main || defender.Main)
            Raise("War", $"War with {Narrative2.Called(attacker.Main ? defender : attacker)}", $"{Narrative2.Called(attacker, true)} and {Narrative2.Called(defender)} are at war, over {why}.", col);
        return w;
    }

    War Open(Civ a, Civ b, string reason)
    {
        var w = new War { Id = NextWarId++, Reason = reason, Start = Year, NavyKgA0 = Math.Max(NavyStrength(a), 1), NavyKgB0 = Math.Max(NavyStrength(b), 1) };
        w.A.Add(a.Id); w.B.Add(b.Id);
        a.AtWar.Add(b.Id); b.AtWar.Add(a.Id);
        Wars.Add(w);
        return w;
    }

    /// <summary>
    /// Keep war records and AtWar in step when something else starts or stops a war (a god power, an act, the law, a
    /// side dying): a war with no record gets one, a record whose leads are no longer at war closes.
    /// </summary>
    void Reconcile()
    {
        foreach (var w in Wars.Where(w => w.Open).ToList())
        {
            var a = Civs[w.A[0]]; var b = Civs[w.B[0]];
            if (a.Dead || b.Dead)
            {
                // a side is gone: the survivors keep what they hold of it, and nobody occupies for the dead
                ReturnOccupied(w);
                Close(w, "collapse");
            }
            else if (!a.AtWar.Contains(b.Id)) { ReturnOccupied(w); Close(w, "status quo"); }
        }
        // nobody occupies for the dead, or for a side no longer at war (a ceasefire's frozen front excepted)
        foreach (var c in Colonies)
        {
            if (c.OccupiedBy < 0) continue;
            var occ = Civs[c.OccupiedBy];
            if (occ.Dead || occ == c.Civ || !occ.AtWar.Contains(c.Civ.Id) && !Wars.Any(x => x.Ceasefire && x.Has(occ.Id) && x.Has(c.Civ.Id))) c.OccupiedBy = -1;
        }
        foreach (var c in Civs)
        {
            if (c.Dead) continue;
            foreach (var id in c.AtWar.ToList())
            {
                if (id >= Civs.Count || id < c.Id) continue;
                var o = Civs[id];
                if (o.Dead) { c.AtWar.Remove(id); continue; }
                if (WarBetween(c, o) == null) Open(c, o, "revenge");
            }
        }
    }

    /// <summary>Close a war: the sides stop fighting, and a truce holds for a generation.</summary>
    void Close(War w, string ending)
    {
        w.Ending = ending;
        double truce = Year + F("truceYears");
        foreach (var x in w.A)
            foreach (var y in w.B)
            {
                var cx = Civs[x]; var cy = Civs[y];
                if (Wars.Any(o => o != w && o.Open && (o.A.Contains(x) && o.B.Contains(y) || o.B.Contains(x) && o.A.Contains(y)))) continue;
                cx.AtWar.Remove(y); cy.AtWar.Remove(x);
                cx.Truce[y] = truce; cy.Truce[x] = truce;
            }
    }

    /// <summary>
    /// Yearly per war: the score (systems held, claims, battles), each side's exhaustion (time, losses, occupation,
    /// eased by resolve), and whether it can end. A side with nothing left unoccupied collapses; otherwise the loser
    /// concedes what its stakes, score and weariness allow, and the terms are bought with that.
    /// </summary>
    void WarYear()
    {
        foreach (var w in Wars.Where(w => w.Open && Year >= w.Start + 1 && Year >= w.LastYear + 1).ToList())
        {
            w.LastYear = Year;
            var a = Civs[w.A[0]]; var b = Civs[w.B[0]];
            int occB = 0, occA = 0, claims = 0, ownA = 0, ownB = 0, lostA = 0, lostB = 0;
            foreach (var c in Colonies)
            {
                bool inA = w.A.Contains(c.Civ.Id), inB = w.B.Contains(c.Civ.Id);
                if (!inA && !inB) continue;
                if (inA) ownA++; else ownB++;
                if (c.OccupiedBy < 0) continue;
                if (inB && w.A.Contains(c.OccupiedBy)) { occB++; lostB++; if (w.Claims.Contains(c.Star.Id)) claims++; }
                if (inA && w.B.Contains(c.OccupiedBy)) { occA++; lostA++; }
            }
            w.Score = (float)Math.Clamp(F("scorePerOccupied") * (occB - occA) + F("scorePerClaimHeld") * claims + F("scorePerBattle") * (w.WonA - w.WonB), -100, 100);
            float Exh(Civ lead, double yearKg, double navy0, int lost, int own) => (float)(
                F("exhaustionPerYear") * (1 - 0.5 * lead.Culture.Resolve) * (1 + 0.2 * lead.WarStrain)
                + F("exhaustionPerLossShare") * yearKg / Math.Max(navy0, 1)
                + F("exhaustionPerOccupiedShare") * lost / Math.Max(own, 1));
            w.ExhA = Math.Clamp(w.ExhA + Exh(a, w.YearKgA, w.NavyKgA0, lostA, ownA), 0, 100);
            w.ExhB = Math.Clamp(w.ExhB + Exh(b, w.YearKgB, w.NavyKgB0, lostB, ownB), 0, 100);
            w.YearKgA = w.YearKgB = 0;
            PeaceCheck(w, a, b);
        }
    }

    void PeaceCheck(War w, Civ a, Civ b)
    {
        var collapse = new List<Term> { WarModel.Collapse };
        bool Gone(Civ c) => c.Colonies.All(x => x.OccupiedBy >= 0);
        if (Gone(b)) { Offer(w, true, collapse); return; }
        if (Gone(a)) { Offer(w, false, collapse); return; }
        if (Year - w.Start < F("minWarYears")) return;
        double be = F("bothExhausted");
        bool both = w.ExhA >= be && w.ExhB >= be;
        // a stalemate: nobody is losing, so it ends only when both are worn out
        if (Math.Abs(w.Score) < F("stalemateScore"))
        {
            if (!both) return;
            bool truce = WarData.P("stakes", WarModel.ReasonById(w.Reason).Tier, "ceasefire").GetBoolean();
            Offer(w, w.Score >= 0, new() { WarModel.TermById(truce ? "ceasefire" : "status_quo") });
            return;
        }
        if (w.Reason == "independence") { IndependencePeace(w, both); return; }
        if (w.Score > 0)
        {
            double give = WarModel.Concede(b, w.Reason, -w.Score, w.ExhB);
            var terms = WarModel.ChooseTerms(w.Reason, give, both);
            // a winner holds out for what it went to war for, and settles for less only once it is tired too
            if (terms.Count > 0 && (WarModel.Ending(terms, w.Reason) != "compromise" || w.ExhA >= F("settleExhaustion"))) Offer(w, true, terms);
        }
        else
        {
            double give = WarModel.Concede(a, w.Reason, w.Score, w.ExhA);
            if (double.IsPositiveInfinity(give)) Offer(w, false, collapse);
            else if (give >= WarModel.TermById("reparations").Price) Offer(w, false, new() { WarModel.TermById("reparations") });
            else if (w.ExhA >= be) Offer(w, false, new() { WarModel.TermById("status_quo") });
        }
    }

    /// <summary>
    /// A war of independence is side-aware (side A the union, side B the rebels). The union winning offers the rebels
    /// reunion if they give enough, partition when it holds some of their systems, autonomy otherwise; the rebels
    /// winning simply end the war and stay free.
    /// </summary>
    void IndependencePeace(War w, bool both)
    {
        var (aWins, terms) = IndependenceTerms(w, w.Score, w.ExhA, w.ExhB, both);
        if (terms.Count > 0) Offer(w, aWins, terms);
    }

    /// <summary>The terms a war of independence would end on at this score and exhaustion (none: fight on).</summary>
    public (bool AWins, List<Term> Terms) IndependenceTerms(War w, double score, double exhA, double exhB, bool both)
    {
        var union = Civs[w.A[0]]; var rebels = Civs[w.B[0]];
        if (score > 0)
        {
            double give = WarModel.Concede(rebels, w.Reason, -score, exhB);
            bool holds = Colonies.Any(c => c.Civ == rebels && c.OccupiedBy >= 0 && w.A.Contains(c.OccupiedBy));
            string t = give >= WarModel.TermById("reunion").Price ? "reunion"
                     : holds && give >= WarModel.TermById("partition").Price ? "partition"
                     : give >= WarModel.TermById("autonomy").Price ? "autonomy" : both ? "ceasefire" : null;
            return (true, t == null ? new() : new() { WarModel.TermById(t) });
        }
        double g = WarModel.Concede(union, w.Reason, score, exhA);
        return (false, g >= WarModel.TermById("independence").Price || exhA >= F("bothExhausted") ? new() { WarModel.TermById("independence") } : new());
    }

    /// <summary>Make one civ the other's subject, unless that would close a loop: a subject that beats its overlord is simply free.</summary>
    static void Subject(Civ loser, Civ winner)
    {
        if (winner.Overlord == loser) winner.Overlord = null;
        else loser.Overlord = winner;
    }

    /// <summary>What the terms do to the loser (and give the winner). Unless the front is frozen by a ceasefire, every
    /// system still occupied is handed back, and the occupying armies go home.</summary>
    void ApplyTerms(War w, Civ winner, Civ loser, List<Term> terms)
    {
        var side = w.A.Contains(winner.Id) ? w.A : w.B;
        var held = Colonies.Where(c => c.Civ == loser && c.OccupiedBy >= 0 && side.Contains(c.OccupiedBy)).ToList();
        foreach (var t in terms)
            switch (t.Id)
            {
                case "cede_claims": foreach (var c in loser.Colonies.Where(c => w.Claims.Contains(c.Star.Id)).ToList()) TransferColony(c, winner); break;
                case "cede_occupied": case "partition": case "collapse":
                    foreach (var c in held) if (c.Civ == loser && c.OccupiedBy >= 0 && !Civs[c.OccupiedBy].Dead) TransferColony(c, Civs[c.OccupiedBy]);
                    if (t.Id == "collapse" && !loser.Dead) Subject(loser, winner);
                    break;
                case "reunion": foreach (var c in loser.Colonies.ToList()) TransferColony(c, winner); break;
                case "reparations": { double amt = loser.Materiel * F("reparationsShare"); loser.Materiel -= amt; winner.Materiel += amt; break; }
                case "tribute": loser.TributeTo = winner.Id; loser.TributeUntil = Year + F("tributeYears"); break;
                case "humiliation":
                    loser.Relations[winner.Id] = loser.Relations.GetValueOrDefault(winner.Id) - 20; winner.Relations[loser.Id] = winner.Relations.GetValueOrDefault(loser.Id) - 20; break;
                case "vassal": case "autonomy": Subject(loser, winner); break;
                case "release": foreach (var c in Civs) if (c.Overlord == loser) c.Overlord = null; break;
                case "regime": { float conscience = loser.Conscience; loser.SetDrive(winner.Drive, loser.Ethic); loser.Conscience = conscience; }      // a new government, the same people: its conscience keeps its drift
                    loser.Leader = loser.NewLeader(winner.Drive, Year); break;
                case "ceasefire": w.Ceasefire = true; break;
            }
        if (!w.Ceasefire) ReturnOccupied(w);
        if (!winner.Dead) Cultures.Event(winner, "victory");
        if (!loser.Dead) Cultures.Event(loser, "defeat");
        foreach (var (lead, exh) in new[] { (Civs[w.A[0]], w.ExhA), (Civs[w.B[0]], w.ExhB) })
            if (exh >= 100 && !lead.Dead) Cultures.Event(lead, "exhausted");
    }

    /// <summary>Hand back what either side still occupies of the other's, and send the occupying armies home.</summary>
    void ReturnOccupied(War w)
    {
        foreach (var c in Colonies)
        {
            if (c.OccupiedBy < 0 || !(w.A.Contains(c.Civ.Id) && w.B.Contains(c.OccupiedBy) || w.B.Contains(c.Civ.Id) && w.A.Contains(c.OccupiedBy))) continue;
            var occ = Civs[c.OccupiedBy];
            var home = occ.Colonies.FirstOrDefault(x => x.Home) ?? occ.Colonies.FirstOrDefault();
            foreach (var a in Armies) if (a.Civ == occ.Id && a.Fleet < 0 && a.At == c.Star.Id && home != null) a.At = home.Star.Id;
            c.OccupiedBy = -1;
        }
    }

    /// <summary>Put terms to the loser: applied at once, or (when the player holds Diplomacy and loses) asked as a decision.</summary>
    void Offer(War w, bool aWins, List<Term> terms)
    {
        var loser = Civs[aWins ? w.B[0] : w.A[0]]; var winner = Civs[aWins ? w.A[0] : w.B[0]];
        if (loser.Main && loser.Policy.IsManual(Area.Diplomacy) && loser.Colonies.Count > 0)
        {
            string key = "terms:" + w.Id;
            if (Waiting(key) || Year < loser.Policy.AskAfter.GetValueOrDefault(key)) return;
            string names = string.Join(", ", terms.Select(t => t.Name.ToLower()));
            Raise("Decision", $"Accept the terms of {Narrative2.Called(winner)}?", $"{Narrative2.Called(winner, true)} will make peace for: {names}.", loser.Colonies[0], key,
                new() { ("Accept", () => { if (w.Open) EndWarWith(w, aWins, terms); }), ("Fight on", () => loser.Policy.AskAfter[key] = Year + 5) });
            return;
        }
        EndWarWith(w, aWins, terms);
    }

    /// <summary>A number from war.json's interface section (proposal cooldowns, ceasefire thresholds).</summary>
    static double InterfaceD(string key) => WarData.D("interface", key);

    /// <summary>Which side of a war a civ is on: 0 side A, 1 side B, -1 not in it.</summary>
    public static int Side(War w, Civ c) => w.A.Contains(c.Id) ? 0 : w.B.Contains(c.Id) ? 1 : -1;
    /// <summary>The civ that leads a side (and speaks for it in peace talks); null for side -1.</summary>
    public Civ Lead(War w, int side) => side == 0 ? Civs[w.A[0]] : side == 1 ? Civs[w.B[0]] : null;

    /// <summary>
    /// The peace the war would end on now, seen from one side: whether that side is winning, and the terms the loser
    /// would give (the winner's ask, or what the viewer would be asked to give). PeaceCheck's decision without its
    /// waiting (minimum years, a winner holding out) and without doing anything. Nothing to ask of a dead enemy.
    /// </summary>
    public (bool ViewerWins, List<Term> Terms) TermsOnOffer(War w, Civ viewer)
    {
        int s = Side(w, viewer);
        if (s < 0) return (false, new());
        var enemy = Lead(w, 1 - s);
        if (enemy.Dead || enemy.Colonies.Count == 0) return (true, new());
        var mine = Lead(w, s);
        bool Gone(Civ c) => c.Colonies.All(x => x.OccupiedBy >= 0);
        if (Gone(enemy)) return (true, new() { WarModel.Collapse });
        if (Gone(mine)) return (false, new() { WarModel.Collapse });
        double be = F("bothExhausted");
        bool both = w.ExhA >= be && w.ExhB >= be;
        bool aWins; List<Term> terms;
        if (Math.Abs(w.Score) < F("stalemateScore"))
        {
            bool truce = WarData.P("stakes", WarModel.ReasonById(w.Reason).Tier, "ceasefire").GetBoolean();
            aWins = w.Score >= 0; terms = new() { WarModel.TermById(truce ? "ceasefire" : "status_quo") };
        }
        else if (w.Reason == "independence") (aWins, terms) = IndependenceTerms(w, w.Score, w.ExhA, w.ExhB, both);
        else
        {
            aWins = w.Score > 0;
            var loser = Lead(w, aWins ? 1 : 0);
            terms = WarModel.ChooseTerms(w.Reason, WarModel.Concede(loser, w.Reason, aWins ? -w.Score : w.Score, aWins ? w.ExhB : w.ExhA), both);
        }
        return (aWins == (s == 0), terms);
    }

    /// <summary>Can this civ put an offer of peace to the other side now? Only a side's lead may, and not again soon after a refusal.</summary>
    public bool CanPropose(War w, Civ by, out string why)
    {
        why = null;
        int s = Side(w, by);
        if (!w.Open) why = "This war is over.";
        else if (by.Dead) why = "There is nobody left to speak for this side.";
        else if (s < 0 || Lead(w, s) != by) why = "Only the leader of a side can make peace for it.";
        else if (Lead(w, 1 - s) is var other && (other.Dead || other.Colonies.Count == 0)) why = "There is nobody left to make peace with.";
        else if (Year < by.ProposeAfter.GetValueOrDefault(w.Id))
            why = $"They will not hear another offer for {Math.Max(1, Math.Ceiling(by.ProposeAfter[w.Id] - Year)):0} years.";
        return why == null;
    }

    /// <summary>Would the other lead stop fighting for nothing: worn out, or nobody is winning?</summary>
    bool WouldStop(War w, int otherSide) =>
        (otherSide == 0 ? w.ExhA : w.ExhB) >= InterfaceD("ceasefireAcceptExhaustion") || Math.Abs(w.Score) < F("stalemateScore");

    /// <summary>
    /// Put terms to the other side. A side asking terms gets them when they cost no more than the other would concede
    /// (asking nothing priced needs an enemy willing to stop); a losing side gets peace by giving everything demanded of
    /// it. A refusal closes the talks for a while.
    /// </summary>
    public bool ProposeTerms(War w, Civ by, List<Term> terms, out string why)
    {
        if (!CanPropose(w, by, out why)) return false;
        if (terms == null || terms.Count == 0) { why = "Choose the terms to offer."; return false; }
        int s = Side(w, by);
        var other = Lead(w, 1 - s);
        var (byWins, offer) = TermsOnOffer(w, by);
        // only the reason's own terms; collapse only alone, and only when it is the peace on offer
        var allowed = WarModel.ReasonById(w.Reason).Terms;
        bool collapseOnOffer = offer.Count == 1 && offer[0].Id == "collapse";
        if (terms.Any(t => t.Id == "collapse" ? !(collapseOnOffer && terms.Count == 1) : !allowed.Contains(t.Id)))
        {
            why = "Those terms are not on the table.";
            return false;
        }
        // a civ that holds nothing any more cannot refuse its own collapse
        if (byWins && collapseOnOffer && terms.Count == 1)
        {
            EndWarWith(w, s == 0, terms);
            return true;
        }
        if (!byWins && offer.Count > 0 && offer.All(t => terms.Any(x => x.Id == t.Id)))
        {
            EndWarWith(w, s == 1, terms);
            return true;
        }
        double price = terms.Sum(t => t.Id == "collapse" ? 0 : WarModel.TermById(t.Id).Price);   // the table's prices, not the caller's
        double otherScore = s == 0 ? -w.Score : w.Score, otherExh = s == 0 ? w.ExhB : w.ExhA;
        bool accept = price > 0 ? price <= WarModel.Concede(other, w.Reason, otherScore, otherExh) : WouldStop(w, 1 - s);
        if (accept)
        {
            EndWarWith(w, s == 0, terms);
            return true;
        }
        by.ProposeAfter[w.Id] = Year + InterfaceD("proposeCooldownYears");
        why = $"{Narrative2.Called(other, true)} refuses.";
        return false;
    }

    /// <summary>Offer a ceasefire on the front: taken when the stakes allow one and the other side is worn out or nobody is winning.</summary>
    public bool ProposeCeasefire(War w, Civ by, out string why)
    {
        if (!CanPropose(w, by, out why)) return false;
        int s = Side(w, by);
        if (!WarData.P("stakes", WarModel.ReasonById(w.Reason).Tier, "ceasefire").GetBoolean()) { why = "This war is fought to the end: there is no ceasefire."; return false; }
        if (!WouldStop(w, 1 - s)) { why = $"{Narrative2.Called(Lead(w, 1 - s), true)} will not stop fighting yet."; return false; }
        EndWarWith(w, w.Score >= 0, new() { WarModel.TermById("ceasefire") });
        return true;
    }

    /// <summary>End a war on these terms (aWins: side A is the winner).</summary>
    public void EndWarWith(War w, bool aWins, List<Term> terms)
    {
        if (w == null || !w.Open) return;
        Close(w, terms.Count == 0 ? "status quo" : WarModel.Ending(terms, w.Reason));
        var a = Civs[w.A[0]]; var b = Civs[w.B[0]];
        ApplyTerms(w, aWins ? a : b, aWins ? b : a, terms);
        var col = (a.Main ? a : b.Main ? b : a).Colonies.FirstOrDefault() ?? HomeColony;
        Log(col, $"Peace between {Narrative2.Called(a)} and {Narrative2.Called(b)}: {w.Ending}.", "era", null, party: a.Main || b.Main);
        if (a.Main || b.Main) Raise("Contact", $"Peace with {Narrative2.Called(a.Main ? b : a)}", $"The war between {Narrative2.Called(a)} and {Narrative2.Called(b)} is over: {w.Ending}.", col);
    }
}
