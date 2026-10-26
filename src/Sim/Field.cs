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
    /// <summary>ColonyAt, falling back to a search before the first field step has built the index.</summary>
    Colony ColonyAtAny(int starId) => _colonyAt.Count > 0 ? ColonyAt(starId) : Colonies.FirstOrDefault(c => c.Star.Id == starId);

    /// <summary>Send a fleet somewhere with a mission (defend, attack, raid, blockade). A fleet in flight goes on from where it lands.</summary>
    public void Order(Fleet f, int starId, string mission)
    {
        f.Mission = mission; f.Target = starId;
        if (f.Moving && starId != f.To)
        {
            // turn around in flight: the fleet comes about at the star nearest where it is now
            var p = FleetPos(f);
            f.At = Gal.Stars.OrderBy(s => s.Pos.DistanceSquaredTo(p)).First().Id;
            f.To = -1;
        }
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
        // a new owner starts the books afresh: grievance is judged by them from the next year
        c.Grievance = 0; c.Unrest = 0; c.Cause = null; c.Movement = null; c.Autonomy = false; c.InvestedUntil = 0; c.Holdout = false; c.Crackdowns = 0;
        c.YardDownUntil = c.DepositsDownUntil = c.IncitedUntil = c.PreparedUntil = 0; c.PreparedBy = -1;
        from.Colonies.Remove(c);
        to.Colonies.Add(c);
        c.Home = false;                       // a conquered capital is not the conqueror's capital
        c.Civ = to;
        c.Star.Owner = to.Id;
        from.LastLoss = Year;
        if (c == HomeColony && from.Colonies.Count > 0) HomeColony = CapitalOf(from);     // the player's seat moves to what they still hold
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
        foreach (var a in Armies)
        {
            if (a.Fleet >= 0)
            {
                var f = fleetById[a.Fleet];
                a.At = f.At;
                // troops come ashore at their own systems unless the fleet is about to attack, and always at peace
                var own = f.Moving ? null : ColonyAt(f.At);
                if (own != null && own.Civ.Id == a.Civ && own.OccupiedBy < 0 && (f.Mission != "attack" || Civs[a.Civ].AtWar.Count == 0)) a.Fleet = -1;
            }
            else if (a.To >= 0 && Year >= a.Arrive) { a.At = a.To; a.To = -1; }
        }

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
        WarYear();

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
    BattleResult Fight(War w, int star, BattleSide sa, BattleSide sb) => Dispatch(w, star, sa, sb);

    /// <summary>Book a fought battle (auto-resolved or commanded) to its war: records, score, losses, chronicle, cinematic hook.</summary>
    BattleResult Book(War w, int star, BattleSide sa, BattleSide sb, BattleResult r, double navyA, double navyB)
    {
        Record(new BattleRecord(star, Year, w.Id, sa.Civ.Id, sb.Civ.Id, r.AWins, r.LostA, r.LostB,
            Starforge.Sim.Battles.BestFamily(sa.Civ), Starforge.Sim.Battles.BestFamily(sb.Civ)));
        w.Battles++;
        if (r.AWins) w.WonA++; else w.WonB++;
        w.LostKgA += r.KgLostA; w.LostKgB += r.KgLostB; w.YearKgA += r.KgLostA; w.YearKgB += r.KgLostB;
        var here = ColonyAt(star);
        var target = here ?? sb.Civ.Colonies.OrderBy(c => c.Star.Pos.DistanceSquaredTo(Gal.Stars[star].Pos)).FirstOrDefault();
        var source = sa.Civ.Colonies.OrderBy(c => c.Star.Pos.DistanceSquaredTo(Gal.Stars[star].Pos)).FirstOrDefault();
        if (source != null && target != null) BattleFought?.Invoke(source, target, sa.Civ);
        var (win, lose) = r.AWins ? (sa.Civ, sb.Civ) : (sb.Civ, sa.Civ);
        AfterBattle(win, lose, target);
        double lostKg = r.AWins ? r.KgLostB : r.KgLostA, navy = r.AWins ? navyB : navyA;
        if ((win.Main || lose.Main) && Year - w.LastLog >= 1 && lostKg >= F("logBattleShare") * Math.Max(navy, 1))
        {
            w.LastLog = Year;
            double hulls = r.AWins ? r.LostB : r.LostA;
            Log(target ?? HomeColony, $"Battle at {Gal.Stars[star].Name}: {Narrative2.Called(win, true)} beat {Narrative2.Called(lose)}, who lose {hulls:#,0} ships.", win.Main ? "era" : "loss", null);
        }
        return r;
    }

    /// <summary>
    /// Where fleets gather before a strike: the enemy system nearest our capital, then our free system nearest that
    /// (two linear passes, not every pair: big empires have a thousand systems).
    /// </summary>
    Colony Frontier(Civ civ, List<Civ> enemies) => Frontier(civ, enemies, out _);

    /// <summary>Frontier, also giving the enemy system nearest our capital (the strike's target).</summary>
    Colony Frontier(Civ civ, List<Civ> enemies, out Colony near)
    {
        near = null;
        var home = civ.Colonies.FirstOrDefault(c => c.Home) ?? civ.Colonies.FirstOrDefault();
        if (home == null) return null;
        float bd = float.MaxValue;
        foreach (var e in enemies)
            foreach (var x in e.Colonies)
            {
                float d = home.Star.Pos.DistanceSquaredTo(x.Star.Pos);
                if (d < bd) { bd = d; near = x; }
            }
        if (near == null) return null;
        Colony best = null; bd = float.MaxValue;
        foreach (var c in civ.Colonies)
        {
            if (c.OccupiedBy >= 0) continue;
            float d = c.Star.Pos.DistanceSquaredTo(near.Star.Pos);
            if (d < bd) { bd = d; best = c; }
        }
        return best;
    }

    /// <summary>
    /// A war's fronts, as (from, to) star ids: each lead's staging system to the enemy system nearest its capital, and
    /// for every system occupied in this war, the holder's nearest free system to it. Each pair once.
    /// </summary>
    public List<(int From, int To)> Fronts(War w)
    {
        var fronts = new List<(int From, int To)>();
        for (int s = 0; s < 2; s++)
        {
            var lead = Lead(w, s);
            if (lead.Dead) continue;
            var enemies = (s == 0 ? w.B : w.A).Select(id => Civs[id]).Where(c => !c.Dead).ToList();
            var stage = Frontier(lead, enemies, out var target);
            if (stage != null && target != null) fronts.Add((stage.Star.Id, target.Star.Id));
        }
        foreach (var id in w.A.Concat(w.B))
            foreach (var c in Civs[id].Colonies)
            {
                if (c.OccupiedBy < 0) continue;
                var holder = Civs[c.OccupiedBy];
                int hs = Side(w, holder);
                if (hs < 0 || hs == Side(w, c.Civ)) continue;
                Colony best = null; float bd = float.MaxValue;
                foreach (var x in holder.Colonies)
                {
                    if (x.OccupiedBy >= 0) continue;
                    float d = x.Star.Pos.DistanceSquaredTo(c.Star.Pos);
                    if (d < bd) { bd = d; best = x; }
                }
                if (best != null) fronts.Add((best.Star.Id, c.Star.Id));
            }
        return fronts.Distinct().ToList();
    }

    /// <summary>Test hook: the first fleet of each lead jumps to the star and the two fight there.</summary>
    internal BattleResult TestFight(War w, int star)
    {
        BattleSide SideAt(Civ lead)
        {
            var f = FleetsOf(lead).FirstOrDefault();
            if (f != null) { f.At = star; f.To = -1; f.Target = -1; }
            return new BattleSide { Civ = lead, Fleets = FleetsOf(lead).Where(x => !x.Moving && x.At == star).ToList() };
        }
        return Fight(w, star, SideAt(Lead(w, 0)), SideAt(Lead(w, 1)));
    }

    /// <summary>Wrecks teach: salvage toward the Umbral and Vault gates, and the salvage act's stolen techs.</summary>
    public void AfterBattle(Civ att, Civ def, Colony at)
    {
        if (def.Knows("UMB-3") && att.Gates.GetValueOrDefault("GE6") == 1) att.GateP["GE6:salvage"] = att.GateP.GetValueOrDefault("GE6:salvage") + 1;
        if (def.Knows("GLY-1") && att.Gates.GetValueOrDefault("GE8") == 1) att.GateP["GE8:wrecks"] = 1;
        if (att.M.Acts.Contains("salvage_tech") && _r.NextDouble() < 0.3)
        {
            var t = def.Techs.Select(Defs.TechById).Where(x => Available(att, x)).OrderBy(_ => _r.Next()).FirstOrDefault();
            if (t != null) { CompleteTech(att, t, true); if (att.Main && at != null) Log(at, $"Salvage teams pick over the wrecks at {at.Sys.Name}. What they find becomes {t.Name}.", "event", null); }
        }
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

        Colony frontier = null, watch = null; Civ rival = null; HashSet<int> hostile = null; List<Colony> lost = null;
        var inReach = new Dictionary<int, List<Colony>>();
        // at war, armies beyond the capital's own garrison ship out to the frontier, where the attack fleets gather
        if (enemies.Count > 0 && !manual && (frontier = Frontier(civ, enemies)) != null && frontier != home)
        {
            bool keepOne = true;
            foreach (var a in Armies.Where(a => a.Civ == civ.Id && a.Fleet < 0 && a.To < 0 && a.At != frontier.Star.Id).ToList())
            {
                if (a.At == home.Star.Id && keepOne) { keepOne = false; continue; }
                a.To = frontier.Star.Id; a.Arrive = Year + TravelYears(civ, Gal.Stars[a.At], frontier.Star);
            }
        }
        foreach (var f in mine)
        {
            if (f.Morale < breakAt) { if (f.Target != home.Star.Id) Order(f, home.Star.Id, "defend"); continue; }
            if (manual) continue;
            if (enemies.Count == 0)
            {
                // peace: the home fleet guards the capital; the others hold the border facing the most hostile
                // neighbour, or stay at the nearest of our own systems (a capital can be decades from the border)
                string m = f.Mission == "blockade" ? "raid" : f.Mission;
                Colony post;
                if (m == "defend") post = home;
                else
                {
                    rival ??= civ.Relations.Where(r => r.Value < -40 && r.Key < Civs.Count && !Civs[r.Key].Dead && Civs[r.Key].Colonies.Count > 0)
                        .OrderBy(r => r.Value).Select(r => Civs[r.Key]).FirstOrDefault() ?? civ;
                    post = rival != civ ? (watch ??= Frontier(civ, new() { rival }) ?? home)
                        : ColonyAt(f.Moving ? f.To : f.At) is { } c0 && c0.Civ == civ ? c0
                        : civ.Colonies.OrderBy(c => c.Star.Pos.DistanceSquaredTo(Gal.Stars[f.Moving ? f.To : f.At].Pos)).First();
                }
                if (f.Target != post.Star.Id && !(f.Target < 0 && f.At == post.Star.Id)) Order(f, post.Star.Id, m);
                continue;
            }

            var from = Gal.Stars[f.Moving ? f.To : f.At];
            bool InReach(Colony c) => TravelYears(civ, from, c.Star) <= reach;
            Colony pick;
            if (f.Mission == "defend")
            {
                // an own system with enemies in its sky or under occupation, nearest first; otherwise the capital
                hostile ??= Fleets.Where(e => !e.Moving && civ.AtWar.Contains(e.Civ)).Select(e => e.At).ToHashSet();
                pick = civ.Colonies.Where(c => c.OccupiedBy >= 0 || hostile.Contains(c.Star.Id))
                    .Where(InReach).OrderBy(c => c.Star.Pos.DistanceSquaredTo(from.Pos)).FirstOrDefault() ?? home;
                if (f.Target != pick.Star.Id) Order(f, pick.Star.Id, "defend");
                continue;
            }
            // our own systems in enemy hands come first
            if (f.Mission == "attack")
            {
                lost ??= civ.Colonies.Where(c => c.OccupiedBy >= 0 && enemies.Contains(Holder(c))).ToList();
                var lib = lost.Count == 0 ? null : lost.Where(InReach).OrderBy(c => c.Star.Pos.DistanceSquaredTo(from.Pos)).FirstOrDefault();
                if (lib != null)
                {
                    if (f.Target != lib.Star.Id)
                    {
                        if (!f.Moving)
                            foreach (var a in Armies.Where(a => a.Civ == civ.Id && a.Fleet < 0 && a.To < 0 && a.At == f.At).Skip(f.At == home.Star.Id ? 1 : 0).Take((int)F("embarkArmies")).ToList())
                                a.Fleet = f.Id;
                        Order(f, lib.Star.Id, "attack");
                    }
                    continue;
                }
            }
            // keep a target that is still the enemy's (and not already ours by occupation)
            if (f.Target >= 0 && ColonyAt(f.Target) is { } cur && enemies.Contains(Holder(cur)) && (f.Mission == "blockade" || cur.OccupiedBy != civ.Id)) continue;
            // the enemy's systems not already in friendly hands, and our own systems the enemy holds
            // (fleets setting out from the same star share one list)
            lost ??= civ.Colonies.Where(c => c.OccupiedBy >= 0 && enemies.Contains(Holder(c))).ToList();
            if (!inReach.TryGetValue(from.Id, out var targets))
                inReach[from.Id] = targets = enemies.SelectMany(e => e.Colonies).Where(c => enemies.Contains(Holder(c))).Concat(lost).Where(InReach).ToList();
            if (f.Mission == "raid" || f.Mission == "blockade")
                pick = targets.OrderByDescending(c => c.IndustryKg).FirstOrDefault();
            else
                pick = targets.Where(c => c.Civ == civ).OrderBy(c => c.Star.Pos.DistanceSquaredTo(from.Pos)).FirstOrDefault()
                    ?? targets.Where(c => claims.Contains(c.Star.Id)).OrderBy(c => c.Star.Pos.DistanceSquaredTo(from.Pos)).FirstOrDefault()
                    ?? targets.OrderBy(c => c.Star.Pos.DistanceSquaredTo(from.Pos)).FirstOrDefault();
            if (pick == null)
            {
                // nothing in reach from here: stage at our system nearest the enemy, and strike from there
                frontier ??= Frontier(civ, enemies) ?? home;
                if (f.Target != frontier.Star.Id) Order(f, frontier.Star.Id, f.Mission);
                continue;
            }
            if (f.Target == pick.Star.Id) continue;
            string mission = f.Mission == "raid" ? "blockade" : f.Mission;
            // an attack carries armies from wherever it sets out
            if (mission == "attack" && !f.Moving)
                foreach (var a in Armies.Where(a => a.Civ == civ.Id && a.Fleet < 0 && a.To < 0 && a.At == f.At).Skip(f.At == home.Star.Id ? 1 : 0).Take((int)F("embarkArmies")).ToList())
                    a.Fleet = f.Id;
            Order(f, pick.Star.Id, mission);
        }
    }
}
