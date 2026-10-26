using System;
using System.Collections.Generic;
using System.Linq;
using Starforge.Sim;
using static Starforge.Sim.Battles;

namespace Starforge.Tests;

/// <summary>Stage 6 self-tests: battle command (docs/superpowers/plans/2026-10-26-battle-command.md).</summary>
public static class CommandTests
{
    static readonly string[] Fams = { "kinetic", "energy", "missile", "craft", "exotic" };

    /// <summary>A reproducible pair of sides from a seed: mixed hulls, counts, experience, morale, supply, admirals, stations.</summary>
    internal static (BattleSide A, BattleSide B) Scenario(Civ ca, Civ cb, int seed)
    {
        var r = new Random(seed);
        foreach (var c in new[] { ca, cb })
        {
            c.M.Arms.Clear(); c.M.Guard.Clear(); c.M.Fleet = 1;
            c.M.Arms[Fams[r.Next(Fams.Length)]] = r.Next(1, 6);
            if (r.Next(2) == 0) c.M.Arms[Fams[r.Next(Fams.Length)]] = r.Next(1, 6);
            foreach (var g in new[] { "armor", "shield", "pd" }) if (r.Next(2) == 0) c.M.Guard[g] = r.Next(1, 6);
        }
        BattleSide Side(Civ c)
        {
            var s = new BattleSide { Civ = c };
            for (int i = 0, n = r.Next(1, 4); i < n; i++)
            {
                var f = new Fleet { Civ = c.Id, Morale = (float)(0.4 + 0.6 * r.NextDouble()), Supply = (float)(0.5 + 0.5 * r.NextDouble()),
                    Admiral = new Admiral { Skill = (float)(0.85 + 0.35 * r.NextDouble()), Trait = Forces.Traits[r.Next(Forces.Traits.Length)] } };
                for (int k = 0, m = r.Next(1, 4); k < m; k++) f.Add(r.Next(0, 6), Math.Round(Math.Pow(10, r.NextDouble() * 4)), (float)r.NextDouble());
                s.Fleets.Add(f);
            }
            if (r.Next(3) == 0) s.StationHulls = r.Next(1, 50);
            return s;
        }
        return (Side(ca), Side(cb));
    }

    internal static string Dump(BattleState st, BattleResult res)
    {
        string Fl(BattleSide s) => string.Join("|", s.Fleets.Select(f => $"{f.Morale:R}/{f.Admiral?.Wins}/{f.Admiral?.Losses}/" + string.Join(",", f.Squadrons.Select(q => $"{q.Hull}:{q.Ships:R}:{q.Xp:R}"))));
        return $"{res} {Fl(st.A)} {Fl(st.B)} {st.A.StationHulls:R} {st.B.StationHulls:R}";
    }

    static BattleSide Plain(Civ c, params (int Hull, double N)[] sq)
    {
        var f = new Fleet { Civ = c.Id, Admiral = new Admiral { Skill = 1, Trait = "Tactician" } };
        foreach (var (h, n) in sq) f.Add(h, n, 0f);
        return new BattleSide { Civ = c, Fleets = { f } };
    }

    static void Even(Civ a, Civ b) { foreach (var c in new[] { a, b }) { c.M.Arms.Clear(); c.M.Guard.Clear(); c.M.Fleet = 1; c.M.Arms["kinetic"] = 2; } }

    static void Close(double x, double y, string what)
    {
        SelfTest.Check(Math.Abs(x - y) <= 1e-9 * Math.Max(1, Math.Max(Math.Abs(x), Math.Abs(y))), $"{what}: {x:R} vs {y:R}");
    }

    static string Shape(BattleSide a, BattleSide b, BattleResult r) =>
        $"{r.AWins} {r.Rounds} {r.ARetreats} {r.BRetreats} {string.Join("|", a.Fleets.Concat(b.Fleets).Select(f => $"{f.Morale:R}/" + string.Join(",", f.Squadrons.Select(q => $"{q.Hull}:{q.Ships:R}:{q.Xp:R}"))))}";

    static double Hp(BattleSide s) => s.Hp(Battles.HullHp);

    public static readonly List<(string Name, Action Body)> All = new()
    {
        ("units_neutral_exact", () => {
            var sim = SelfTest.NewSim(); var ca = sim.Humanity; var cb = sim.Civs.First(c => c.Alien);
            for (int seed = 0; seed < 100; seed++)
            {
                var (a1, b1) = Scenario(ca, cb, seed);
                var r1 = Battles.Resolve(sim, a1, b1);
                var (a2, b2) = Scenario(ca, cb, seed);
                int id = 1; Units.Build(a2, ref id); Units.Build(b2, ref id);
                // explicit doctrine orders (hold the line, line formation) change nothing
                foreach (var u in a2.Units.Concat(b2.Units)) { u.HasMove = true; u.Formation = "line"; }
                var r2 = Battles.Resolve(sim, a2, b2);
                SelfTest.Check(Shape(a1, b1, r1) == Shape(a2, b2, r2), $"scenario {seed}: units with doctrine orders differ from auto-resolve");
            }
        }),
        ("split_neutral", () => {
            var sim = SelfTest.NewSim(); var ca = sim.Humanity; var cb = sim.Civs.First(c => c.Alien);
            var rng = new Random(5);
            for (int seed = 0; seed < 60; seed++)
            {
                var (a1, b1) = Scenario(ca, cb, seed);
                var r1 = Battles.Resolve(sim, a1, b1);
                var (a2, b2) = Scenario(ca, cb, seed);
                int id = 1; Units.Build(a2, ref id); Units.Build(b2, ref id);
                foreach (var side in new[] { a2, b2 })
                    foreach (var u in side.Units.ToList())
                        if (u.Ships > 2) { var n = Units.Split(side, u, Math.Floor(u.Ships * (0.1 + 0.8 * rng.NextDouble())), ref id); if (n != null && n.Ships > 2 && rng.Next(2) == 0) Units.Split(side, n, Math.Floor(n.Ships / 2), ref id); }
                var r2 = Battles.Resolve(sim, a2, b2);
                SelfTest.Check(r1.AWins == r2.AWins && r1.Rounds == r2.Rounds, $"scenario {seed}: split changes the winner or rounds");
                Close(r1.LostA, r2.LostA, "lost A"); Close(r1.LostB, r2.LostB, "lost B");
            }
        }),
        ("split_merge_roundtrip", () => {
            var sim = SelfTest.NewSim(); var ca = sim.Humanity;
            var s = Plain(ca, (2, 100), (3, 7)); int id = 1; Units.Build(s, ref id);
            var u = s.Units[0]; var n = Units.Split(s, u, 1, ref id);
            SelfTest.Check(n != null && n.Ships == 1 && u.Ships == 99 && s.Units.Count == 3 && s.Units[1] == n, "one ship split off, right after its unit");
            SelfTest.Check(Units.Split(s, u, 99, ref id) == null && Units.Split(s, u, 0, ref id) == null, "cannot split off all or none");
            SelfTest.Check(Units.Merge(s, u, n) && u.Ships == 100 && s.Units.Count == 2, "and merge back");
            SelfTest.Check(!Units.Merge(s, s.Units[0], s.Units[1]), "different squadrons never merge");
        }),
        ("unit_cap", () => {
            var sim = SelfTest.NewSim(); var s = Plain(sim.Humanity, (2, 1000)); int id = 1; Units.Build(s, ref id);
            while (Units.Split(s, s.Units[0], 1, ref id) != null) { }
            SelfTest.Check(s.Units.Count == Units.MaxPerSide, $"units stop at the cap ({s.Units.Count})");
        }),
        ("orders_conserve", () => {
            var sim = SelfTest.NewSim(); var ca = sim.Humanity; var cb = sim.Civs.First(c => c.Alien);
            Even(ca, cb);
            double Lost(Action<BattleSide, BattleSide> orders)
            {
                var a = Plain(ca, (3, 10000), (4, 6000)); var b = Plain(cb, (3, 9000), (4, 5000), (5, 3000));
                int id = 1; Units.Build(a, ref id); Units.Build(b, ref id);
                orders(a, b);
                double before = Hp(b); new BattleState(a, b).Step();
                return before - Hp(b);
            }
            double plain = Lost((a, b) => { });
            double focus = Lost((a, b) => { a.Units[0].FocusId = b.Units[0].Id; });
            double screen = Lost((a, b) => { b.Units[1].ScreenId = b.Units[0].Id; });
            double sphere = Lost((a, b) => { foreach (var u in b.Units) u.Formation = "sphere"; });
            Close(plain, focus, "focus fire redistributes, it does not add"); Close(plain, screen, "screening redistributes"); Close(plain, sphere, "a formation alone redistributes");
        }),
        ("focus_waste", () => {
            var sim = SelfTest.NewSim(); var ca = sim.Humanity; var cb = sim.Civs.First(c => c.Alien);
            Even(ca, cb);
            double Lost(bool focus)
            {
                var a = Plain(ca, (3, 10000)); var b = Plain(cb, (3, 9000)); int id = 1; Units.Build(a, ref id); Units.Build(b, ref id);
                var tiny = Units.Split(b, b.Units[0], 1, ref id);
                if (focus) a.Units[0].FocusId = tiny.Id;
                double before = Hp(b); new BattleState(a, b).Step(); return before - Hp(b);
            }
            SelfTest.Check(Lost(true) < Lost(false) * 0.9, "fire poured into a one-ship unit is wasted");
        }),
        ("screen_absorbs", () => {
            var sim = SelfTest.NewSim(); var ca = sim.Humanity; var cb = sim.Civs.First(c => c.Alien);
            Even(ca, cb);
            double[] Frac(bool screen)
            {
                var a = Plain(ca, (3, 10000)); var b = Plain(cb, (3, 5000), (4, 5000)); int id = 1; Units.Build(a, ref id); Units.Build(b, ref id);
                if (screen) b.Units[1].ScreenId = b.Units[0].Id;
                new BattleState(a, b).Step(); return new[] { 1 - b.Units[0].Ships / 5000, 1 - b.Units[1].Ships / 5000 };
            }
            var plain = Frac(false); var scr = Frac(true);
            SelfTest.Check(scr[0] < plain[0] && scr[1] > plain[1], "the covered unit loses less, the screen more");
        }),
        ("retreat_keeps_ships", () => {
            var sim = SelfTest.NewSim(); var ca = sim.Humanity; var cb = sim.Civs.First(c => c.Alien);
            Even(ca, cb);
            var a = Plain(ca, (3, 100), (4, 50)); var b = Plain(cb, (3, 100)); int id = 1; Units.Build(a, ref id); Units.Build(b, ref id);
            a.Units[1].Retreat = true;
            var st = new BattleState(a, b); st.Step(); double left = a.Units[1].Ships;
            while (st.Step()) { }
            st.Finish(sim);
            SelfTest.Check(a.Units[1].Gone && left > 0 && a.Units[1].Ships == left && a.Fleets[0].Squadrons.Any(q => q.Hull == 4 && q.Ships == left), "a retreating unit is exposed for its leaving round, then keeps every ship it has");
            var a2 = Plain(ca, (3, 100)); var b2 = Plain(cb, (3, 100)); id = 1; Units.Build(a2, ref id); Units.Build(b2, ref id);
            a2.Units[0].Retreat = true;
            var r2 = Battles.Resolve(sim, a2, b2);
            SelfTest.Check(r2.ARetreats && !r2.AWins && r2.LostA < 100, $"the whole side retreating: B holds the field, A keeps what survives its leaving round ({r2})");
        }),
        ("reserve_not_hit", () => {
            var sim = SelfTest.NewSim(); var ca = sim.Humanity; var cb = sim.Civs.First(c => c.Alien);
            Even(ca, cb);
            var a = Plain(ca, (3, 100), (4, 100)); var b = Plain(cb, (3, 200)); int id = 1; Units.Build(a, ref id); Units.Build(b, ref id);
            a.Units[1].HasMove = true; a.Units[1].MoveDepth = 1;
            var st = new BattleState(a, b); st.Step(); st.Step(); double held = a.Units[1].Ships; st.Step(); st.Step();
            SelfTest.Check(a.Units[1].Ships == held && a.Units[1].Depth == 1 && held < 100, "a unit pulled out of range takes fire only until it is clear (a round late)");
        }),
        ("million_ship_perf", () => {
            var sim = SelfTest.NewSim(); var ca = sim.Humanity; var cb = sim.Civs.First(c => c.Alien);
            Even(ca, cb);
            var a = Plain(ca, Enumerable.Range(0, 8).Select(i => (i, 1e6)).ToArray()); var b = Plain(cb, Enumerable.Range(0, 8).Select(i => (i, 1e6)).ToArray());
            int id = 1; Units.Build(a, ref id); Units.Build(b, ref id);
            foreach (var side in new[] { a, b }) foreach (var u in side.Units.ToList()) for (int k = 0; k < 7; k++) Units.Split(side, u, 1000, ref id);
            foreach (var u in a.Units) u.Formation = "wedge";
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var r = Battles.Resolve(sim, a, b);
            SelfTest.Check(sw.ElapsedMilliseconds < 100 && double.IsFinite(r.LostA) && double.IsFinite(r.LostB), $"64 units a side, millions of ships: {sw.ElapsedMilliseconds} ms");
        }),
        ("stepped_equals_resolve", () => {
            var sim = SelfTest.NewSim(); var ca = sim.Humanity; var cb = sim.Civs.First(c => c.Alien);
            for (int seed = 0; seed < 200; seed++)
            {
                var (a1, b1) = Scenario(ca, cb, seed);
                var r1 = Battles.Resolve(sim, a1, b1);
                var (a2, b2) = Scenario(ca, cb, seed);
                var st = new BattleState(a2, b2);
                while (st.Step()) { }
                var r2 = st.Finish(sim);
                string d1 = $"{r1} {string.Join("|", a1.Fleets.Concat(b1.Fleets).Select(f => $"{f.Morale:R}/{string.Join(",", f.Squadrons.Select(q => $"{q.Hull}:{q.Ships:R}:{q.Xp:R}"))}"))} {a1.StationHulls:R} {b1.StationHulls:R}";
                string d2 = $"{r2} {string.Join("|", a2.Fleets.Concat(b2.Fleets).Select(f => $"{f.Morale:R}/{string.Join(",", f.Squadrons.Select(q => $"{q.Hull}:{q.Ships:R}:{q.Xp:R}"))}"))} {a2.StationHulls:R} {b2.StationHulls:R}";
                SelfTest.Check(d1 == d2, $"scenario {seed}: stepping differs from Resolve\n{d1}\n{d2}");
                SelfTest.Check(r1.Rounds <= 6, "at most the data's rounds");
            }
        }),
    };
}
