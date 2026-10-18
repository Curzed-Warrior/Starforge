using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

/// <summary>
/// Ground war by the numbers, behind a small interface (invasion strength against a garrison, per world) so a full
/// ground-war system can replace it later. Fleets must hold orbit first: no enemy fleets in the sky and the station
/// silenced. Then the stronger landing takes the world, and occupation is how wars are won.
/// </summary>
public partial class Simulation
{
    static int _frigate = -2;
    static int Frigate => _frigate != -2 ? _frigate : _frigate = Math.Max(0, Array.IndexOf(Defs.Hulls, "Frigate"));

    /// <summary>Who holds a world: its occupier, or its owner.</summary>
    public Civ Holder(Colony c) => c.OccupiedBy >= 0 ? Civs[c.OccupiedBy] : c.Civ;

    /// <summary>A system's own defences in frigate-sized hulls: the starbase tier, a capital's fortress, its shields and sentries. An occupied system has none.</summary>
    public double StationHulls(Colony c)
    {
        if (c.OccupiedBy >= 0) return 0;
        int works = (c.Done("shield") ? 1 : 0) + (c.Done("aegis") ? 1 : 0) + (c.Done("sentry") ? 1 : 0);
        return F("defenceHullsBase") * (1 + c.Civ.M.Guard.GetValueOrDefault("base")) * (c.Home ? F("defenceHome") : 1) * (1 + works) * (1 - c.DefenceLoss);
    }

    /// <summary>Does this civ hold the orbit: its fleets there, no enemy fleets, and the station silenced?</summary>
    public bool HoldsOrbit(Civ civ, Colony c)
    {
        int star = c.Star.Id;
        bool ours = false;
        foreach (var f in Fleets)
        {
            if (f.Moving || f.At != star || f.Ships <= 0) continue;
            if (f.Civ == civ.Id) ours = true;
            else if (civ.AtWar.Contains(f.Civ)) return false;
        }
        return ours && StationHulls(c) < 0.5;
    }

    double GroundPower(Civ civ) => Simulation.TierPow(civ.M.Arms.GetValueOrDefault("ground")) * civ.M.Ground;

    public double InvasionStrength(IEnumerable<Army> armies, Civ civ) => armies.Sum(a => a.Troops * a.Quality * (1 + a.Xp)) * GroundPower(civ);

    /// <summary>The holder's armies on the ground there, and (for the owner) the militia its people raise.</summary>
    public double GarrisonStrength(Colony c)
    {
        var holder = Holder(c);
        double s = InvasionStrength(Armies.Where(a => a.Civ == holder.Id && a.Fleet < 0 && a.At == c.Star.Id), holder);
        if (c.OccupiedBy < 0)
            s += c.Pop * WarData.D("armies", "garrisonPerBillion") * WarData.D("armies", "troopsPerArmy") * F("militiaShare") * GroundPower(holder) * (c.Home ? F("garrisonHome") : 1);
        return s;
    }

    /// <summary>
    /// Armies land. Stronger than the garrison: the world is occupied (or freed, when the owner lands), the defenders on
    /// the ground are lost, and the landing force stays as the new garrison. Otherwise half the landing is lost.
    /// </summary>
    public bool Land(Colony c, Civ invader, List<Army> armies)
    {
        double inv = InvasionStrength(armies, invader), gar = GarrisonStrength(c);
        if (inv <= gar * F("landingMargin"))
        {
            foreach (var a in armies) a.Troops *= 0.5;
            return false;
        }
        var holder = Holder(c);
        Armies.RemoveAll(a => a.Civ == holder.Id && a.Fleet < 0 && a.At == c.Star.Id);
        double keep = 1 - Math.Min(0.9, 0.5 * gar / Math.Max(inv, 1e-300));
        foreach (var a in armies)
        {
            a.Troops *= keep; a.Fleet = -1; a.At = c.Star.Id;
            if (!Armies.Contains(a)) Armies.Add(a);
        }
        bool freed = invader == c.Civ;
        c.OccupiedBy = freed ? -1 : invader.Id;
        c.DefenceLoss = Math.Max(c.DefenceLoss, 1f);
        if (invader.Main || c.Civ.Main)
            Log(c, freed ? $"{c.Sys.Name} is free again: {Narrative2.Called(invader, true)} land and drive out {Narrative2.Called(holder)}."
                         : $"{Narrative2.Called(invader, true)} land on {c.Sys.Name} and occupy it.", invader.Main ? "era" : "loss", null);
        return true;
    }

    /// <summary>
    /// After the battles: a side alone at a warring system fights its station, then lands any armies it carries once
    /// it holds the orbit, and otherwise blockades it.
    /// </summary>
    void FieldSieges()
    {
        foreach (var g in Fleets.Where(f => !f.Moving && f.Ships > 0).GroupBy(f => f.At).ToList())
        {
            var c = ColonyAt(g.Key);
            if (c == null) continue;
            var holder = Holder(c);
            var att = g.Select(f => Civs[f.Civ]).FirstOrDefault(x => x.AtWar.Contains(holder.Id) || x == c.Civ && c.OccupiedBy >= 0 && x.AtWar.Contains(c.OccupiedBy));
            if (att == null) continue;
            var w = WarBetween(att, holder);
            if (w == null) continue;
            double full = StationHulls(c);
            if (full >= 0.5)
            {
                var sa = new BattleSide { Civ = att, Fleets = g.Where(f => f.Civ == att.Id).ToList() };
                var sb = new BattleSide { Civ = holder, StationHulls = full, StationHull = Frigate };
                bool attIsA = w.A.Contains(att.Id);
                Fight(w, g.Key, attIsA ? sa : sb, attIsA ? sb : sa);
                double whole = full / Math.Max(1 - c.DefenceLoss, 1e-6);
                c.DefenceLoss = (float)Math.Clamp(1 - sb.StationHulls / Math.Max(whole, 1e-9), 0, 1);
            }
            if (!HoldsOrbit(att, c)) continue;
            var aboard = Armies.Where(a => a.Civ == att.Id && a.Fleet >= 0 && g.Any(f => f.Id == a.Fleet)).ToList();
            // land only with the strength to win; otherwise keep the blockade and wait for more troops
            if (aboard.Count > 0 && InvasionStrength(aboard, att) > GarrisonStrength(c) * F("landingMargin")) Land(c, att, aboard);
            else if (c.OccupiedBy < 0) c.BlockadedUntil = Year + F("orderEvery");
        }
    }

    /// <summary>A colony at war's disposal: neither occupied nor under blockade.</summary>
    public bool Free(Colony c) => c.OccupiedBy < 0 && c.BlockadedUntil <= Year;
}
