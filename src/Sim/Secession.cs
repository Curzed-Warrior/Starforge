using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

/// <summary>
/// Separations (stage 3): systems in open revolt rise together as a bloc and leave, in peace when the council lets
/// them go, or through a secession war when it fights to keep them. Disloyal admirals take their fleets with them.
/// </summary>
public partial class Simulation
{
    /// <summary>
    /// Yearly per civ: when systems are in open revolt, a rising may come (one roll for the civ, so a vast empire does
    /// not shed a system every year); it starts in the most aggrieved system and takes every system in revolt within
    /// supply range of it. Never the capital, and the parent always keeps at least one system.
    /// </summary>
    void RevoltStep(Civ civ, double dy)
    {
        var cap = CapitalOf(civ);
        var revolting = civ.Colonies.Where(c => c != cap && !c.Home && c.OccupiedBy < 0 && c.Unrest >= 4).OrderByDescending(c => c.Grievance).ToList();
        if (revolting.Count == 0 || _r.NextDouble() >= SD("revolt", "chancePerYear") * dy) return;
        var first = revolting[0];
        float range = (float)(F("supplyRangeGu") * civ.M.Range);
        var bloc = revolting.Where(c => c.Star.Pos.DistanceSquaredTo(first.Star.Pos) <= range * range).ToList();
        int systems = civ.Colonies.Count;
        double pop = civ.Pop;
        bool civilWar = bloc.Count >= SD("revolt", "civilWarShare") * systems || bloc.Sum(c => c.Pop) >= SD("revolt", "civilWarPopShare") * pop;
        bool peaceful = !civilWar && bloc.Count <= SD("revolt", "letGoMax") && (civ.Ethic == "Reverent" || civ.Ethic == "Pragmatic" && civ.AtWar.Count == 0);
        string drive = bloc.GroupBy(Leaning).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key;
        StrifeCount("revolts");
        if (civilWar) StrifeCount("civilwars");
        Separate(civ, bloc, drive, peaceful, civilWar: civilWar);
    }

    /// <summary>
    /// A bloc leaves as a new polity. Fleets parked in its systems under a disloyal admiral go with it, with their
    /// embarked armies, and so do the armies garrisoned there. In peace: a truce and some goodwill; otherwise the parent
    /// fights a war of independence (side A the union, side B the rebels). The capital never leaves, and the parent
    /// keeps at least one system. Returns the new civ, or null when nothing could leave.
    /// </summary>
    public Civ Separate(Civ parent, List<Colony> bloc, string drive, bool peaceful, string name = null, bool civilWar = false)
    {
        // never the capital (a splinter's is its first system), and never a system an enemy occupies
        var capital = CapitalOf(parent);
        bloc = bloc.Where(c => c.Civ == parent && c != capital && !c.Home && c.OccupiedBy < 0).Distinct().ToList();
        if (bloc.Count >= parent.Colonies.Count) bloc = bloc.Take(parent.Colonies.Count - 1).ToList();
        if (bloc.Count == 0) return null;
        double share = Math.Min(0.5, bloc.Sum(c => c.Pop) / Math.Max(parent.Pop, 1e-9));
        var s = Splinter(parent, drive, bloc);
        if (name != null) s.Name = name;
        var stars = bloc.Select(c => c.Star.Id).ToHashSet();
        double loyal = SD("revolt", "defectLoyalty");
        foreach (var f in Fleets.Where(f => f.Civ == parent.Id && !f.Moving && stars.Contains(f.At) && (f.Admiral?.Loyalty ?? 1) < loyal).ToList())
        {
            f.Civ = s.Id; f.Target = -1;
            foreach (var a in Armies) if (a.Fleet == f.Id) a.Civ = s.Id;
            if (parent.Main) Log(bloc[0], $"Admiral {f.Admiral.Name} takes the {f.Name} over to {s.Name}.", "loss", null);
        }
        foreach (var a in Armies) if (a.Civ == parent.Id && a.Fleet < 0 && a.To < 0 && stars.Contains(a.At)) a.Civ = s.Id;
        // the crews from the bloc's worlds take their share of every squadron with them (at most half the navy), and a
        // rising strikes first
        var free = new Fleet { Id = NextFleetId++, Civ = s.Id, At = bloc[0].Star.Id, Name = "Free Fleet", Mission = "attack", Admiral = NewAdmiral(s) };
        foreach (var f in Fleets.Where(f => f.Civ == parent.Id))
            foreach (var sq in f.Squadrons)
            {
                double take = sq.Ships * share;
                if (take <= 0) continue;
                sq.Ships -= take; free.Add(sq.Hull, take, sq.Xp);
            }
        if (free.Squadrons.Count > 0) Fleets.Add(free);
        foreach (var c in bloc)
        {
            c.Grievance = 20; c.Unrest = 0; c.Cause = null; c.CauseKey = null; c.Movement = null; c.Autonomy = false; c.InvestedUntil = 0; c.OccupiedBy = -1;
            Withdraw("interior:" + c.Star.Id);
        }
        string where = bloc.Count == 1 ? bloc[0].Sys.Name : $"{bloc.Count} systems";
        if (peaceful)
        {
            StrifeCount("peaceful");
            s.Relations[parent.Id] = parent.Relations[s.Id] = -30 + 20;
            double truce = Year + F("truceYears");
            s.Truce[parent.Id] = truce; parent.Truce[s.Id] = truce;
            Log(bloc[0], $"{where} leave {Narrative2.Called(parent)} in peace as {s.Name}. The council lets them go.", "loss", null);
            if (parent.Main) Raise("Unrest", $"{Cap(s.Name)} leave", $"{where} have left in peace as {s.Name}.", bloc[0]);
        }
        else
        {
            Log(bloc[0], civilWar ? $"Civil war. {where} rise as {s.Name}, and {Narrative2.Called(parent)} is split down the middle."
                                  : $"{where} rise as {s.Name}. {Narrative2.Called(parent, true)} will not let them go.", "loss", null);
            DeclareWar(parent, s, "independence");
        }
        if (parent.Main)
        {
            var old = parent;
            Raise("Decision", $"Lead {s.Name}?", $"{where} have broken away from {Narrative2.Called(old)} as {s.Name}. You can stay with {Narrative2.Called(old)}, or lead the splinter instead.", bloc[0], "lead:" + s.Id,
                new() { ($"Stay with {Narrative2.Called(old)}", () => { }), ($"Lead {s.Name}", () => { if (!s.Dead && !s.Main && s.Colonies.Count > 0) SwitchPlayer(s); }) });
        }
        return s;
    }

    /// <summary>
    /// The player takes over another civ (a splinter, usually). Their policy comes with them; the civ they leave is run
    /// by its council from now on, and every proposal still waiting on the player is closed.
    /// </summary>
    public void SwitchPlayer(Civ to)
    {
        var from = Humanity;
        if (to == null || to == from || to.Dead || to.Colonies.Count == 0) return;
        var p = from.Policy;
        to.Policy = new Policy
        {
            Manual = (bool[])p.Manual.Clone(), Weight = new(p.Weight), WorksShare = p.WorksShare, Mobilisation = p.Mobilisation, HullMix = p.HullMix,
            PauseOn = new(p.PauseOn), Approved = new(to.Policy.Approved), Declined = new(to.Policy.Declined), AskAfter = new(to.Policy.AskAfter),
        };
        for (int i = 0; i < from.Policy.Manual.Length; i++) from.Policy.Manual[i] = false;      // the council runs the old civ now
        foreach (var a in Alerts.Where(a => !a.Done)) { a.Done = true; a.Outcome = "Left behind"; }
        from.Main = false; to.Main = true; Humanity = to;
        HomeColony = CapitalOf(to);
        Focus = HomeColony;
        Log(HomeColony, $"You now lead {to.Name}.", "era", null);
    }
}
