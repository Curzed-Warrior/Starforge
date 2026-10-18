using System;
using System.Collections.Generic;
using System.Linq;

namespace Starforge.Sim;

/// <summary>
/// The war in the field, every step: fleets travel at their drive's speed, run short of supply far from home, recover
/// at their own systems, and take the council's orders (unless the player holds the Fleets area). Per fleet and per war,
/// never per colony.
/// </summary>
public partial class Simulation
{
    double _nextOrders;
    readonly Dictionary<int, Colony> _colonyAt = new();

    /// <summary>The colony at a star this step, or null.</summary>
    public Colony ColonyAt(int starId) => _colonyAt.TryGetValue(starId, out var c) ? c : null;

    /// <summary>Send a fleet somewhere with a mission (defend, attack, raid, blockade). A fleet in flight goes on from where it lands.</summary>
    public void Order(Fleet f, int starId, string mission)
    {
        f.Mission = mission; f.Target = starId;
        if (!f.Moving) Send(f);
    }

    void Send(Fleet f)
    {
        if (f.Target < 0 || f.Target == f.At) { f.To = -1; return; }
        var civ = Civs[f.Civ];
        f.To = f.Target; f.Depart = Year;
        f.Arrive = Year + TravelYears(civ, Gal.Stars[f.At], Gal.Stars[f.To]);
    }

    /// <summary>Where a fleet is, in galaxy units (between two stars while it travels).</summary>
    public Godot.Vector3 FleetPos(Fleet f)
    {
        var a = Gal.Stars[f.At].Pos;
        if (!f.Moving) return a;
        float u = (float)Math.Clamp((Year - f.Depart) / Math.Max(f.Arrive - f.Depart, 1e-6), 0, 1);
        return a.Lerp(Gal.Stars[f.To].Pos, u);
    }

    /// <summary>A system changes hands: its people, stock and works go with it.</summary>
    public void TransferColony(Colony c, Civ to)
    {
        var from = c.Civ;
        c.OccupiedBy = -1;
        if (from == to) return;
        from.Colonies.Remove(c);
        to.Colonies.Add(c);
        c.Civ = to;
        c.Star.Owner = to.Id;
        from.LastLoss = Year;
        if (from.Colonies.Count == 0) Extinct(from);
    }

    void FieldStep(double dy)
    {
        Reconcile();
        _colonyAt.Clear();
        foreach (var c in Colonies) _colonyAt[c.Star.Id] = c;

        // arrivals, and onward legs ordered while in flight
        foreach (var f in Fleets)
            if (f.Moving && Year >= f.Arrive) { f.At = f.To; f.To = -1; if (f.Target >= 0 && f.Target != f.At) Send(f); }
        // embarked armies ride with their fleet; a fleet that is gone takes them with it
        var fleetById = Fleets.ToDictionary(f => f.Id);
        Armies.RemoveAll(a => a.Fleet >= 0 && !fleetById.ContainsKey(a.Fleet));
        foreach (var a in Armies) if (a.Fleet >= 0) a.At = fleetById[a.Fleet].At;

        // supply and morale
        double range = F("supplyRangeGu"), loss = F("supplyLossPerYear"), gain = F("supplyGainPerYear"), regen = F("moraleRegenPerYear");
        foreach (var f in Fleets)
        {
            if (f.Moving) continue;
            var civ = Civs[f.Civ];
            var here = ColonyAt(f.At);
            bool home = here != null && here.Civ == civ && here.OccupiedBy < 0;
            var pos = Gal.Stars[f.At].Pos; float r2 = (float)(range * civ.M.Range); r2 *= r2;
            bool supplied = home || civ.Colonies.Any(c => c.OccupiedBy < 0 && c.Star.Pos.DistanceSquaredTo(pos) <= r2);
            if (supplied) f.Supply = (float)Math.Min(1, f.Supply + gain * dy);
            else f.Supply = (float)Math.Max(0, f.Supply - loss * dy * (f.Admiral?.Trait == "Logistician" ? TraitD("Logistician", "supply", 1) : 1));
            if (home) f.Morale = (float)Math.Min(1, f.Morale + regen * dy);
        }

        FieldBattles();
        FieldSieges();

        if (Year >= _nextOrders)
        {
            _nextOrders = Year + F("orderEvery");
            foreach (var civ in Civs) if (!civ.Dead && civ.Colonies.Count > 0) CouncilOrders(civ);
        }
    }

    /// <summary>Wherever fleets of two sides at war share a star, they fight (one battle per star per step).</summary>
    void FieldBattles()
    {
        foreach (var g in Fleets.Where(f => !f.Moving && f.Ships > 0).GroupBy(f => f.At).ToList())
        {
            var ids = g.Select(f => f.Civ).Distinct().ToList();
            if (ids.Count < 2) continue;
            int x = -1, y = -1;
            for (int i = 0; i < ids.Count && x < 0; i++)
                for (int j = i + 1; j < ids.Count; j++)
                    if (Civs[ids[i]].AtWar.Contains(ids[j])) { x = ids[i]; y = ids[j]; break; }
            if (x < 0) continue;
            var cx = Civs[x]; var cy = Civs[y];
            var w = WarBetween(cx, cy);
            if (w == null) continue;
            if (w.B.Contains(x)) (cx, cy) = (cy, cx);                 // cx on side A
            var sa = new BattleSide { Civ = cx, Fleets = g.Where(f => f.Civ == cx.Id).ToList() };
            var sb = new BattleSide { Civ = cy, Fleets = g.Where(f => f.Civ == cy.Id).ToList() };
            Fight(w, g.Key, sa, sb);
        }
    }

    /// <summary>Resolve a battle at a star and book it to its war: score, losses, the chronicle and the cinematic hook.</summary>
    BattleResult Fight(War w, int star, BattleSide sa, BattleSide sb)
    {
        double navyA = NavyStrength(sa.Civ), navyB = NavyStrength(sb.Civ);
        var r = Battles.Resolve(this, sa, sb);
        w.Battles++;
        if (r.AWins) w.WonA++; else w.WonB++;
        w.LostKgA += r.KgLostA; w.LostKgB += r.KgLostB; w.YearKgA += r.KgLostA; w.YearKgB += r.KgLostB;
        var here = ColonyAt(star);
        var target = here ?? sb.Civ.Colonies.OrderBy(c => c.Star.Pos.DistanceSquaredTo(Gal.Stars[star].Pos)).FirstOrDefault();
        var source = sa.Civ.Colonies.OrderBy(c => c.Star.Pos.DistanceSquaredTo(Gal.Stars[star].Pos)).FirstOrDefault();
        if (source != null && target != null) BattleFought?.Invoke(source, target, sa.Civ);
        var (win, lose) = r.AWins ? (sa.Civ, sb.Civ) : (sb.Civ, sa.Civ);
        double lostKg = r.AWins ? r.KgLostB : r.KgLostA, navy = r.AWins ? navyB : navyA;
        if ((win.Main || lose.Main) && Year - w.LastLog >= 1 && lostKg >= F("logBattleShare") * Math.Max(navy, 1))
        {
            w.LastLog = Year;
            double hulls = r.AWins ? r.LostB : r.LostA;
            Log(target ?? HomeColony, $"Battle at {Gal.Stars[star].Name}: {Narrative2.Called(win, true)} beat {Narrative2.Called(lose)}, who lose {hulls:#,0} ships.", win.Main ? "era" : "loss", null);
        }
        return r;
    }

    static double TraitD(string trait, string key, double dflt) =>
        WarData.P("battle", "traits", trait).TryGetProperty(key, out var v) ? v.GetDouble() : dflt;

    /// <summary>
    /// The admirals' orders: the home fleet guards what is threatened, attack fleets go for the claims and then the
    /// weakest system in reach (carrying armies), raiders blockade the richest. A broken fleet always goes home; with
    /// the Fleets area in the player's hands nothing else is ordered. At peace every fleet comes home.
    /// </summary>
    void CouncilOrders(Civ civ)
    {
        var mine = FleetsOf(civ).ToList();
        if (mine.Count == 0) return;
        var home = civ.Colonies.FirstOrDefault(c => c.Home) ?? civ.Colonies[0];
        bool manual = civ.Policy.IsManual(Area.Fleets);
        double breakAt = WarData.D("battle", "moraleBreak"), reach = F("maxWarTravelYears");
        var enemies = civ.AtWar.Where(id => id < Civs.Count && !Civs[id].Dead).Select(id => Civs[id]).ToList();
        var claims = Wars.Where(w => w.Open && w.A.Contains(civ.Id)).SelectMany(w => w.Claims).ToHashSet();

        foreach (var f in mine)
        {
            if (f.Morale < breakAt) { if (f.Target != home.Star.Id) Order(f, home.Star.Id, "defend"); continue; }
            if (manual) continue;
            if (enemies.Count == 0) { if (f.Target != home.Star.Id) Order(f, home.Star.Id, f.Mission == "blockade" ? "raid" : f.Mission); continue; }

            var from = Gal.Stars[f.Moving ? f.To : f.At];
            bool InReach(Colony c) => TravelYears(civ, from, c.Star) <= reach;
            Colony pick;
            if (f.Mission == "defend")
            {
                // an own system with enemies in its sky or under occupation, nearest first; otherwise the capital
                pick = civ.Colonies.Where(c => c.OccupiedBy >= 0 || Fleets.Any(e => !e.Moving && e.At == c.Star.Id && civ.AtWar.Contains(e.Civ)))
                    .Where(InReach).OrderBy(c => c.Star.Pos.DistanceSquaredTo(from.Pos)).FirstOrDefault() ?? home;
                if (f.Target != pick.Star.Id) Order(f, pick.Star.Id, "defend");
                continue;
            }
            var targets = enemies.SelectMany(e => e.Colonies).Where(InReach).ToList();
            if (f.Mission == "raid" || f.Mission == "blockade")
                pick = targets.OrderByDescending(c => c.IndustryKg).FirstOrDefault();
            else
                pick = targets.Where(c => claims.Contains(c.Star.Id)).OrderBy(c => c.Star.Pos.DistanceSquaredTo(from.Pos)).FirstOrDefault()
                    ?? targets.OrderBy(c => c.Star.Pos.DistanceSquaredTo(from.Pos)).FirstOrDefault();
            if (pick == null) { if (f.Target != home.Star.Id) Order(f, home.Star.Id, f.Mission); continue; }
            if (f.Target == pick.Star.Id) continue;
            string mission = f.Mission == "raid" ? "blockade" : f.Mission;
            // an attack carries armies from wherever it sets out
            if (mission == "attack" && !f.Moving)
                foreach (var a in Armies.Where(a => a.Civ == civ.Id && a.Fleet < 0 && a.At == f.At).Take((int)F("embarkArmies")).ToList())
                    a.Fleet = f.Id;
            Order(f, pick.Star.Id, mission);
        }
    }
}
