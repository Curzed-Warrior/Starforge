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

    /// <summary>A movement rises in a system at insurgency: it is named for what drives it, and it wants an answer.</summary>
    public void Rise(Colony c)
    {
        GrievanceTarget(c, out string key, MeanIndustry(c.Civ), WarExhaustionOf(c.Civ));
        if (key != null) { c.CauseKey = key; c.Cause = CausePhrase[key]; }
        string sys = c.Sys.Name;
        c.Movement = c.CauseKey switch
        {
            "distance" or "distanceSlow" or "occupied" => $"the Free Worlds of {sys}",
            "levy" or "deposits" or "poverty" or "crowding" => $"the {sys} Compact",
            _ => $"the {c.Civ.Faction(Leaning(c))} of {sys}",
        };
        StrifeCount("movements");
        if (c.Civ.Main)
        {
            string why = c.Cause ?? "nobody listens to it";
            Log(c, $"{Cap(c.Movement)} take up arms at {sys}: {why}.", "loss", null);
        }
        AnswerMovement(c);
    }

    /// <summary>Put the movement to whoever answers it: the player when they hold Interior, otherwise the council.</summary>
    void AnswerMovement(Colony c)
    {
        var civ = c.Civ;
        string key = "interior:" + c.Star.Id;
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

    /// <summary>A movement left waiting comes back for an answer once its time is up (five years).</summary>
    void AnswerAgain(Colony c)
    {
        if (c.Movement == null || c.Autonomy || Year < c.InvestedUntil) return;
        if (Year < c.Civ.Policy.AskAfter.GetValueOrDefault("interior:" + c.Star.Id)) return;
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
            "distance" or "distanceSlow" or "deposits" => Answer.SelfRule,
            "poverty" or "crowding" => CanInvest(civ) ? Answer.Invest : Answer.Wait,
            _ => Answer.Wait,
        };
    }

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
                c.Grievance = (float)Math.Max(0, c.Grievance - SD("answers", "crackdown"));
                c.Unrest = Stage(c.Grievance, c.Unrest);
                float spread = (float)SD("answers", "crackdownSpread");
                foreach (var o in civ.Colonies) if (o != c && !o.Home) o.Grievance = Math.Min(100, o.Grievance + spread);
                if (civ.Ethic == "Reverent") civ.AddMeter("Atrocity", 5);
                if (civ.Main) Log(c, $"Troops land at {c.Sys.Name}. {Cap(c.Movement ?? "the movement")} are broken up, and every other world hears about it.", "loss", null);
                break;
            case Answer.Wait:
                civ.Policy.AskAfter[key] = Year + 5;
                break;
        }
    }
}
