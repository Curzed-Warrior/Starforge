using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

public partial class Colony
{
    /// <summary>The home system's own worlds (stage 3): grievance and ladder step per settled world, saved by body name.</summary>
    public Dictionary<Body, float> WorldGrievance = new();
    public Dictionary<Body, int> WorldUnrest = new();
}

/// <summary>
/// The home system's worlds (stage 3): a Mars or a Belt that feels used by the capital climbs the same ladder as a
/// colony, raises a movement, and in open revolt can rise as a rival with a fleet (a war crisis in the system).
/// </summary>
public partial class Simulation
{
    /// <summary>The year a human home system's world first rose (0 until one does), for the trace summary.</summary>
    public double FirstSolUprising;

    static double HW(string key) => SD("homeWorlds", key);

    /// <summary>A world's movement, by name.</summary>
    public static string MovementName(Body b) => b.Name switch
    {
        "Mars" => "Free Mars movement", "Moon" => "Lunar Independence League", "Main Belt" => "Belt Compact", "Venus" => "Cloud Cities' Assembly",
        _ => b.Parent?.Name switch { "Jupiter" => "Jovian Accord", "Saturn" => "Saturnian Union", _ => $"{b.Name} Self-Rule movement" },
    };

    static float StarOrbit(Body b) => b.Parent == null ? b.Orbit : b.Parent.Orbit;

    /// <summary>Where a world's grievance is heading: distance from the seat, the mining that feeds the capital,
    /// crowding, emergencies and a ruthless council, less what makes people feel safe and heard.</summary>
    public double WorldTarget(Colony c, Body b)
    {
        var civ = c.Civ;
        double far = b.Parent == c.Seat ? 0.3 : Math.Min(Math.Abs(Math.Log2(Math.Max(StarOrbit(b), 1e-6f) / Math.Max(StarOrbit(c.Seat), 1e-6f))), 3);
        double sum = HW("far") * far / 3 + HW("hub") * c.Hubs.GetValueOrDefault(b) / 3.0
                     + (c.PopCap > 0 && c.Pop > 0.92 * c.PopCap ? HW("crowding") : 0) + (civ.Emergency ? HW("emergency") : 0)
                     + (civ.Ethic == "Ruthless" ? HW("ruthless") : 0) + HW("base");
        double relief = SD("relief", "megaOrder") * c.MegaOrder + SD("relief", "faith") * civ.Faith + (c.ElevatorP >= 1 ? 4 : 0) + (c.RingP >= 1 ? 4 : 0);
        return Math.Clamp(sum * GovFactor(civ) - relief, 0, 100);
    }

    /// <summary>The biggest of WorldTarget's causes, as a phrase for the Interior tab (null when nothing weighs on it).</summary>
    public string WorldCause(Colony c, Body b)
    {
        var civ = c.Civ;
        double far = b.Parent == c.Seat ? 0.3 : Math.Min(Math.Abs(Math.Log2(Math.Max(StarOrbit(b), 1e-6f) / Math.Max(StarOrbit(c.Seat), 1e-6f))), 3);
        var causes = new (double Points, string Phrase)[]
        {
            (HW("far") * far / 3, $"{c.Seat.Name} is far away"),
            (HW("hub") * c.Hubs.GetValueOrDefault(b) / 3.0, $"its mines and yards work for {c.Seat.Name}"),
            (c.PopCap > 0 && c.Pop > 0.92 * c.PopCap ? HW("crowding") : 0, "there is no room left to live"),
            (civ.Emergency ? HW("emergency") : 0, "the emergency takes everything it makes"),
            (civ.Ethic == "Ruthless" ? HW("ruthless") : 0, "the council rules it harshly"),
        };
        var top = causes.OrderByDescending(x => x.Points).First();
        return top.Points > 0 ? top.Phrase : null;
    }

    /// <summary>Yearly for a home system: each settled world (not the seat) climbs or descends the ladder; at insurgency
    /// a movement is answered; in open revolt it may rise.</summary>
    void HomeWorlds(Colony c, double dy)
    {
        double pull = Math.Min(1, SD("pull") * dy);
        foreach (var (b, settled) in c.Settled.ToList())
        {
            if (b == c.Seat || settled < 0.4f) continue;
            float g = c.WorldGrievance.GetValueOrDefault(b);
            g = (float)Math.Clamp(g + (WorldTarget(c, b) - g) * pull, 0, 100);
            c.WorldGrievance[b] = g;
            int old = c.WorldUnrest.GetValueOrDefault(b), now = Stage(g, old);
            c.WorldUnrest[b] = now;
            if (old < 3 && now >= 3) WorldMovement(c, b);
            // a world that entered the year in open revolt (or reached it now) may rise
            if ((old >= 4 || now >= 4) && c.Rival == null && c.Crisis == null && _r.NextDouble() < HW("riseChance") * dy) Uprising(c, b);
        }
        foreach (var b in c.WorldGrievance.Keys.Where(b => !c.Settled.ContainsKey(b)).ToList()) { c.WorldGrievance.Remove(b); c.WorldUnrest.Remove(b); }
    }

    /// <summary>A world's movement takes up arms; the council answers it as it would a colony's (once: a world has no
    /// standing self-rule or investment of its own, so each answer moves its grievance once).</summary>
    void WorldMovement(Colony c, Body b)
    {
        var civ = c.Civ;
        string name = MovementName(b), key = $"interior:{c.Star.Id}:{b.Name}";
        if (Year < civ.Policy.AskAfter.GetValueOrDefault(key)) return;           // answered within AskAgainYears
        civ.Policy.AskAfter[key] = Year + AskAgainYears;
        StrifeCount("movements");
        if (civ.Main) Log(c, $"The {name} takes up arms on {b.Name}: it is tired of working for {c.Seat.Name}.", "loss", b);
        var a = civ.Ethic == "Ruthless" ? Answer.Crackdown : civ.Ethic == "Reverent" ? (CanInvest(civ) ? Answer.Invest : Answer.SelfRule) : Answer.SelfRule;
        float g = c.WorldGrievance[b];
        switch (a)
        {
            case Answer.SelfRule: g -= (float)SD("relief", "autonomy"); break;
            case Answer.Invest:
                double k = SD("answers", "investCost");
                if (civ.Materiel > 0) civ.Materiel -= civ.Materiel * k; else c.Stock -= c.Stock * k;
                g -= (float)SD("relief", "invested");
                break;
            case Answer.Crackdown:
                g -= (float)SD("answers", "crackdown");
                if (civ.Ethic == "Reverent") civ.AddMeter("Atrocity", 5);
                break;
        }
        c.WorldGrievance[b] = Math.Max(0, g);
        c.WorldUnrest[b] = Stage(c.WorldGrievance[b], c.WorldUnrest[b]);
    }

    /// <summary>A world rises: its people and some of the system's hulls become a rival fleet at war with the capital.</summary>
    void Uprising(Colony c, Body b)
    {
        var civ = c.Civ;
        float g = c.WorldGrievance[b];
        string name = MovementName(b);
        double pop = c.Pop * 0.04;
        c.Pop -= pop;
        c.Rival = new Rival { Name = name, Pop = pop, Ships = (int)(3 + c.Settled.GetValueOrDefault(b) * 6 + g / 25), Planet = b, T0 = Year, Stance = "war" };
        Narrative.StartCrisis(c, "war");
        c.WorldGrievance[b] = 60;
        StrifeCount("uprisings");
        if (civ.Human && c.Star == Gal.Sol && FirstSolUprising == 0) FirstSolUprising = Year;
        if (civ.Main)
        {
            Raise("Unrest", $"{b.Name} rises", $"The {name} has seized {b.Name} and armed its ships.", c);
            if (!civ.Used.Contains("ms_uprising"))
            {
                civ.Used.Add("ms_uprising");
                Milestone(c, "The First Uprising", $"Year {(int)Year}. {b.Name} rises against {c.Seat.Name}. The {name} would rather fight than keep working for someone else.", "body:" + b.Name);
            }
        }
    }
}
