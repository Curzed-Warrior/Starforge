using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

/// <summary>
/// Movements (stage 3): a system that reaches insurgency gets a movement with a name and a demand, and the council (or
/// the player, holding the Interior area) answers it: grant self-rule, invest, crack down, or wait.
/// </summary>
public partial class Simulation
{
    public enum Answer { SelfRule, Invest, Crackdown, Wait }

    static string AnswerLabel(Answer a) => a switch
    {
        Answer.SelfRule => "Grant self-rule", Answer.Invest => "Invest", Answer.Crackdown => "Crack down", _ => "Wait"
    };

    /// <summary>A movement rises in a system at insurgency: it is named for what drives it, and it wants an answer. One
    /// broken up by troops, or laid down within strife.movementMemoryYears, that climbs back is the same movement: it is
    /// not counted again, and it is not news.</summary>
    public void Rise(Colony c)
    {
        bool fresh = c.Movement == null && Year - c.QuietSince >= SD("movementMemoryYears");
        if (fresh) StrifeCount("movements");
        // the yearly step has just worked out what drives it; a movement raised any other way works it out now
        if (c.CauseKey == null && GrievanceTarget(c, out string key, MeanIndustry(c.Civ), WarExhaustionOf(c.Civ), CapitalOf(c.Civ)) >= 0 && key != null)
        { c.CauseKey = key; c.Cause = CausePhrase[key]; }
        string sys = c.Sys.Name;
        c.Movement = c.CauseKey switch
        {
            "distance" or "distanceSlow" or "occupied" => $"the Free Worlds of {sys}",
            "hive" => $"the Flesh of {sys}",
            "levy" or "deposits" or "poverty" or "crowding" => $"the {sys} Compact",
            _ => $"the {c.Civ.Faction(Leaning(c))} of {sys}",
        };
        if (c.Civ.Main && fresh && c.Crackdowns == 0 && !c.Holdout       // a movement broken up and back again, or a holdout in arms, is no longer news
            && News("risings", "movementNewsYears") is var untold && untold >= 0)
        {
            string why = c.Cause ?? "nobody listens to it";
            Log(c, $"{Cap(c.Movement)} take up arms at {sys}: {why}.{Untold(untold, "other movement has", "other movements have", "taken up arms")}", "loss", null);
        }
        AnswerMovement(c);
    }

    /// <summary>How long an answered movement waits before it is put to anyone again (strife.answers.askAgainYears).</summary>
    public static double AskAgainYears => SD("answers", "askAgainYears");

    /// <summary>
    /// The player answers a movement from the Interior tab: through the inbox's open proposal for it when there is one
    /// (so the proposal closes with that answer), otherwise directly, withdrawing any stale proposal. Returns false when
    /// the system is no longer the player's or has no movement any more.
    /// </summary>
    public bool AnswerFromInterior(Colony c, Answer a)
    {
        if (c.Civ != Humanity || c.Movement == null) return false;
        string key = "interior:" + c.Star.Id;
        var open = Alerts.FirstOrDefault(x => x.Key == key && !x.Done && x.Decision);
        int idx = open?.Choices.FindIndex(ch => ch.Label == AnswerLabel(a)) ?? -1;
        if (idx >= 0) Decide(open, idx);
        else { Respond(c, a); Withdraw(key); }
        return true;
    }

    /// <summary>
    /// The Interior tab's rows: the player's systems but the capital, sorted ("Grievance", "Name" or "Stage") and cut
    /// into pages of `perPage`. The page asked for is clamped to the pages there are.
    /// </summary>
    public static (List<Colony> Rows, int Page, int Pages, int Count) InteriorPage(Civ civ, string sort, int page, int perPage)
    {
        var rest = civ.Colonies.Where(c => !c.Home);
        var sorted = sort switch
        {
            "Name" => rest.OrderBy(c => c.Star.Name, StringComparer.OrdinalIgnoreCase),
            "Stage" => rest.OrderByDescending(c => c.Unrest).ThenByDescending(c => c.Grievance),
            _ => rest.OrderByDescending(c => c.Grievance),
        };
        var list = sorted.ToList();
        perPage = Math.Max(1, perPage);
        int pages = Math.Max(1, (list.Count + perPage - 1) / perPage);
        page = Math.Clamp(page, 0, pages - 1);
        return (list.Skip(page * perPage).Take(perPage).ToList(), page, pages, list.Count);
    }

    /// <summary>Put the movement to whoever answers it: the player when they hold Interior, otherwise the council.</summary>
    void AnswerMovement(Colony c)
    {
        var civ = c.Civ;
        string key = "interior:" + c.Star.Id;
        if (Year < civ.Policy.AskAfter.GetValueOrDefault(key)) return;          // answered within AskAgainYears
        if (civ.Main && civ.Policy.IsManual(Area.Interior))
        {
            if (Waiting(key)) return;
            var pick = CouncilAnswer(c);
            var order = new List<Answer> { pick };
            order.AddRange(Enum.GetValues<Answer>().Where(a => a != pick));
            var choices = order.Select(a => (AnswerLabel(a), (Action)(() => { if (c.Civ == civ) Respond(c, a); }))).ToList();
            Raise("Decision", $"{Cap(c.Movement)} demand an answer", $"{c.Sys.Name} is in arms: {c.Cause ?? "nobody listens to it"}. The council would {AnswerLabel(pick).ToLower()}.", c, key, choices);
            return;
        }
        Respond(c, CouncilAnswer(c));
    }

    /// <summary>A movement left waiting comes back for an answer once its time is up (AskAgainYears).</summary>
    void AnswerAgain(Colony c)
    {
        if (c.Movement == null || c.Autonomy || Year < c.InvestedUntil) return;
        AnswerMovement(c);
    }

    /// <summary>The council's answer, from its ethic and what drives the movement.</summary>
    public Answer CouncilAnswer(Colony c)
    {
        var civ = c.Civ;
        if (civ.Ethic == "Ruthless" || civ.Junta(Year)) return Answer.Crackdown;
        if (civ.Ethic == "Reverent") return CanInvest(civ) ? Answer.Invest : Answer.SelfRule;
        return c.CauseKey switch
        {
            // what self-rule ends (the levy, the deposits taken) or eases (distance, an empire too vast to hear it)
            "distance" or "distanceSlow" or "deposits" or "levy" or "sprawl" => Answer.SelfRule,
            "poverty" or "crowding" => CanInvest(civ) ? Answer.Invest : Answer.Wait,
            _ => Answer.Wait,
        };
    }

    /// <summary>The grievance every other system gains from a crackdown: strife.answers.crackdownSpread up to
    /// crackdownSpreadFull systems, thinning as the square root of the size beyond (word carries less far in a vast empire).</summary>
    public double CrackdownSpread(Civ civ) =>
        SD("answers", "crackdownSpread") * Math.Sqrt(Math.Min(1, SD("answers", "crackdownSpreadFull") / Math.Max(1, civ.Colonies.Count)));

    static Colony CapitalOf(Civ civ) => civ.Colonies.FirstOrDefault(x => x.Home) ?? civ.Colonies.FirstOrDefault();
    bool CanInvest(Civ civ) => civ.Materiel > 0 || CapitalOf(civ)?.Stock > 0;

    /// <summary>Answer a movement. Each answer has its cost: self-rule keeps its industry home, investment is paid from
    /// materiel (or the capital's stockpile), a crackdown angers every other system and stains a Reverent people.</summary>
    public void Respond(Colony c, Answer a)
    {
        var civ = c.Civ;
        string key = "interior:" + c.Star.Id;
        switch (a)
        {
            case Answer.SelfRule:
                c.Autonomy = true;
                if (civ.Main) Log(c, $"{c.Sys.Name} is granted self-rule. Its industry stays at home now.", "council", null);
                break;
            case Answer.Invest:
                double k = SD("answers", "investCost");
                if (civ.Materiel > 0) civ.Materiel -= civ.Materiel * k;
                else if (CapitalOf(civ) is { } cap) cap.Stock -= cap.Stock * k;
                c.InvestedUntil = Year + SD("answers", "investYears");
                if (civ.Main) Log(c, $"The council pours money into {c.Sys.Name}: new yards, new homes, a seat at the table.", "council", null);
                break;
            case Answer.Crackdown:
                // troops can break a movement up once or twice; after that they only harden it
                c.Grievance = (float)Math.Max(0, c.Grievance - SD("answers", "crackdown") * Math.Pow(SD("answers", "crackdownFade"), c.Crackdowns++));
                c.Unrest = Stage(c.Grievance, c.Unrest);
                float spread = (float)CrackdownSpread(civ);
                // every other system hears of it, once a year however many crackdowns there are
                foreach (var o in civ.Colonies)
                    if (o != c && !o.Home && o.HeardCrackdown < Year) { o.HeardCrackdown = Year + 1; o.Grievance = Math.Min(100, o.Grievance + spread); }
                StrifeCount("crackdowns");
                if (civ.Ethic == "Reverent") civ.AddMeter("Atrocity", 5);
                if (civ.Main && c.Crackdowns == 1 && News("crackdowns", "movementNewsYears") is var untold && untold >= 0)
                    Log(c, $"Troops land at {c.Sys.Name}. {Cap(c.Movement ?? "the movement")} are broken up, and every other world hears about it.{Untold(untold, "other movement has", "other movements have", "been broken up by troops")}", "loss", null);
                break;
        }
        // whatever the answer, the movement is not taken up again for a while
        civ.Policy.AskAfter[key] = Year + AskAgainYears;
    }
}
