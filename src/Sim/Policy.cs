using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

/// <summary>The areas of government a player can hand to the council or take into their own hands.</summary>
public enum Area { Economy, Research, Construction, Colonisation, Military, Diplomacy, Production, Fleets, Interior, Intelligence }

/// <summary>
/// How a civilisation is run. Every area is either left to the council (automatic, leaning on these priorities) or held
/// by the player (manual: the council still does the legwork, but each of its decisions arrives as a proposal to approve
/// or turn down). Every civ has one, so alien empires play by the same rules; only the player's is ever set to manual.
/// </summary>
public class Policy
{
    public bool[] Manual = new bool[Enum.GetValues<Area>().Length];
    public bool IsManual(Area a) => Manual[(int)a];

    /// <summary>
    /// Priorities, 0..2 (1 is neutral): kinds of great work (Industry, Stellar, Mind, Transit, Habitat, War) and research
    /// areas (Physics, Engineering, Society). The council follows them whether an area is automatic or manual.
    /// </summary>
    public Dictionary<string, float> Weight = new();
    public float W(string key) => Weight.TryGetValue(key, out var v) ? v : 1f;

    /// <summary>Economy in manual: the share of industry spent on great works (otherwise the government decides).</summary>
    public float WorksShare = 0.25f;

    /// <summary>Production in manual: the mobilisation level the player has chosen (0 peace .. 3 total war).</summary>
    public int Mobilisation;
    /// <summary>Production in manual: the hull class every slip builds, or -1 to leave the mix to the council.</summary>
    public int HullMix = -1;

    /// <summary>Construction in manual: great works the player has approved, and ones turned down (never proposed again).</summary>
    public HashSet<string> Approved = new(), Declined = new();

    /// <summary>Battles the player fights: leave them to the admirals, be asked each time, or always take command (spec 4.4).</summary>
    public BattleMode Battle = BattleMode.Auto;

    /// <summary>Alert categories that pause the game when they arrive.</summary>
    public HashSet<string> PauseOn = new() { "War", "Contact", "Decision", "Crisis" };

    /// <summary>Earliest year each kind of proposal may be put to the player again after "not now" (key -> year).</summary>
    public Dictionary<string, double> AskAfter = new();

    public static readonly string[] WorkKinds = { "Industry", "Stellar", "Mind", "Transit", "Habitat", "War" };
    public static readonly string[] Categories = { "War", "Battle", "Contact", "Decision", "Milestone", "Crisis", "Unrest", "Incident" };

    public static string Describe(Area a) => a switch
    {
        Area.Economy => "How much industry goes to great works. Manual: you set the share.",
        Area.Research => "What the three research slots work on. Manual: you pick each card from the hand.",
        Area.Construction => "Which great works are begun. Manual: each new one waits for your approval.",
        Area.Colonisation => "Where seed ships go. Manual: each launch waits for your approval.",
        Area.Military => "When to go to war. Manual: the council asks before declaring war.",
        Area.Diplomacy => "When to make peace. Manual: peace offers come to you.",
        Area.Fleets => "Where fleets go and what they do. Manual: your fleets wait for your orders.",
        Area.Production => "How much of the economy goes to war. Manual: you set the mobilisation level and mandatory service.",
        Area.Interior => "How the council answers unrest. Manual: each movement's demands come to you.",
        Area.Intelligence => "Where special forces go and what they do. Manual: each ready team waits for your orders.",
        _ => ""
    };
}

/// <summary>Something the player should know about, or decide: a war, a first contact, a proposal from the council.</summary>
public class Alert
{
    public double Year;
    public string Cat, Title, Text, Key;
    public Colony Colony;
    public StarInfo Star;
    /// <summary>Choices for a decision (null for news). The council's own pick is first.</summary>
    public List<(string Label, Action Do)> Choices;
    public bool Done, Seen;
    public string Outcome;
    public bool Decision => Choices != null;
}

public partial class Simulation
{
    public readonly List<Alert> Alerts = new();
    public event Action<Alert> Alerted;
    /// <summary>Display names for great works (the catalogue lives with the views).</summary>
    public static Func<string, string> WorkName = id => id;

    /// <summary>
    /// Tell the player. A decision with a key is raised once while it waits; asking again returns the waiting one.
    /// Only the player's own affairs are alerted.
    /// </summary>
    public Alert Raise(string cat, string title, string text, Colony c = null, string key = null, List<(string, Action)> choices = null)
    {
        if (key != null && Alerts.FirstOrDefault(a => a.Key == key && !a.Done) is { } waiting) return waiting;
        var a = new Alert { Year = Year, Cat = cat, Title = title, Text = text, Colony = c, Star = c?.Star, Key = key, Choices = choices };
        Alerts.Add(a);
        if (Alerts.Count > 300) Alerts.RemoveAll(x => (x.Done || !x.Decision) && Alerts.IndexOf(x) < 100);
        Alerted?.Invoke(a);
        return a;
    }

    public bool Waiting(string key) => Alerts.Any(a => a.Key == key && !a.Done);

    public void Decide(Alert a, int choice)
    {
        if (a.Done || a.Choices == null || choice < 0 || choice >= a.Choices.Count) return;
        a.Done = a.Seen = true;
        a.Outcome = a.Choices[choice].Label;
        a.Choices[choice].Do();
    }

    /// <summary>Withdraw a proposal that no longer applies (the target was taken, the work started anyway).</summary>
    public void Withdraw(string key)
    {
        foreach (var a in Alerts.Where(a => a.Key == key && !a.Done)) { a.Done = true; a.Outcome = "Withdrawn"; }
    }

    /// <summary>Hand an area to the council or take it back. Proposals waiting in an area handed back are withdrawn: the council decides them now.</summary>
    public void SetManual(Civ civ, Area a, bool manual)
    {
        civ.Policy.Manual[(int)a] = manual;
        if (manual && a == Area.Production) civ.Policy.Mobilisation = civ.Mobilisation;   // start from the level in force
        if (manual) return;
        string prefix = a switch { Area.Research => "research:", Area.Construction => "build:", Area.Colonisation => "seed:", Area.Military => "war:", Area.Diplomacy => "peace:", Area.Interior => "interior:", Area.Intelligence => "intel:", _ => null };
        if (prefix != null) foreach (var x in Alerts.Where(x => x.Key != null && x.Key.StartsWith(prefix) && !x.Done)) { x.Done = true; x.Outcome = "Left to the council"; }
    }
}
