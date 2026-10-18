# War in the Field (Stage 2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Real wars: every war has a reason and claims, fleets move between stars at real drive speeds under supply,
fight auto-resolved battles with weapon/defence families, admirals, experience and morale, blockade systems and land
armies to occupy them, and the war ends through score, exhaustion and `WarModel` terms (surrender, compromise,
ceasefire, collapse), on the same rules for every civ. The coin-flip war is retired.

**Architecture:** A `War` record per conflict (`src/Sim/Wars.cs`) owns sides, reason, claims, score and exhaustion.
`FieldStep(dy)` runs every `StepOnce` (per fleet and per war, never per colony): orders, movement, battles, sieges,
landings. `WarYear()` runs yearly per war: score, exhaustion, peace. Battles are a pure function
(`src/Sim/Battle.cs`); ground war is a small module behind `InvasionStrength`/`GarrisonStrength` (`src/Sim/Ground.cs`)
so it can be replaced later. `GalaxyCheck` keeps relation drift and calls `DeclareWar`; `War.cs`'s `Battle` and the
relation-based peace are deleted.

**Tech Stack:** Godot 4.7.2 .NET, C# (.NET 8), System.Text.Json. Godot binary path in `.godotpath`.

**Spec:** `docs/superpowers/specs/2026-10-18-war-and-strife-design.md` (sections 4.1, 4.3 blockades and supply, 4.4
auto-resolve, 4.5, 4.8 War/Fleets areas, 7). Builds on stage 1 (`docs/superpowers/plans/2026-10-18-war-foundation.md`,
merged at `e623aac`).

## Global Constraints

- Same rules for everyone: humanity, splinters and aliens run through the same code.
- Per-colony work runs at most yearly; per-tick work is per fleet and per war. `--warp=940 --trace` must stay under ~40 s (24 s at the start of this stage).
- Saves round-trip byte-identically (`--warp=300 --savetest` prints `SAVETEST equal=True`); saves from before this stage still load.
- Tunable numbers live in `data/war.json` (new sections `field` and `battle`).
- New `Area` values are appended to the end of the enum (`Fleets` after `Production`).
- Interface and chronicle copy: plain sentences, British spelling, no jargon. Chronicle volume stays ~150–300 lines a century.
- True drive speeds: travel years = `0.15 + ly / ShipSpeed(civ)`, 0.1 through a lane or between two own gateways.
- Build: `dotnet build -v q` prints `Build succeeded.` Tests: `"$(cat .godotpath)" --headless --path . -- --selftest[=filter]` ends `SELFTEST DONE pass=<n> fail=0`, exit 0.
- Lesson from `spike/war-prototype` (reference only, never merge): fleets at real speeds over unreachable borders fought 23 battles in 940 years and wars ended in 25-year stalemates. Wars are only declared between civs that can reach each other, and acceptance measures battles per war and the mix of endings.

## Review Focus

1. **`AtWar` changed outside the war system** (a god power provoking war, `CivActs` empire/assimilate, the `nowar` law, `Extinct`) never leaves a `War` without `AtWar` or the reverse. (Test: `reconcile_atwar`, Task 1.)
2. **A war whose side dies or loses every colony mid-war** ends cleanly: fleets of the dead side are gone, occupied systems of a dead owner are handed to the occupier, nothing throws. (Test: `war_side_dies`, Task 5.)
3. **A colony that changes hands** (ceded, retaken, destroyed) mid-siege leaves no fleet ordered at a star nobody is fighting over, and no army stranded aboard a fleet that no longer exists. (Test: `orders_survive_transfer`, Task 2.)
4. **A battle between huge late-game navies** (1e6 hulls a side) gives finite, non-negative losses and terminates in at most `rounds` rounds. (Test: `huge_battle_finite`, Task 3.)
5. **The player's manual areas** are respected: with Fleets manual the council never retargets the player's fleets (only a broken fleet retreats); with Diplomacy manual peace terms arrive as a Decision instead of being applied. (Tests: `manual_fleets_kept`, Task 2; `manual_peace_asks`, Task 5.)

---

## File Structure

| File | Responsibility |
|---|---|
| `data/war.json` (modify) | New `field` and `battle` sections (values below) |
| `src/Sim/Wars.cs` (create) | `War` record; `DeclareWar`, `ChooseReason`, claims, allies, truce; `Reconcile`; `WarYear` (score, exhaustion, peace, terms); `TransferColony` |
| `src/Sim/Field.cs` (create) | `FieldStep`: orders, travel, supply, morale regen, embarking, battles and sieges where sides meet |
| `src/Sim/Battle.cs` (create) | `BattleSide`, `Battle.Resolve` (pure): families, counters, hull size, experience, admirals, morale, rounds |
| `src/Sim/Ground.cs` (create) | `InvasionStrength`, `GarrisonStrength`, `Land` (occupation), blockade flags |
| `src/Sim/Forces.cs` (modify) | Fleet movement fields; `Army.Fleet` |
| `src/Sim/War.cs` (modify) | Delete `Battle`; keep `TierPow`, `WarStrength`, `AtrocityAllowed`, `BattleFought` |
| `src/Sim/Simulation.cs` (modify) | `GalaxyCheck` declares through `DeclareWar` (relation drift kept, old peace/battle removed); `StepOnce` calls `FieldStep`; yearly `WarYear`; `StartWar`/`EndWar` become wrappers |
| `src/Sim/WarEconomy.cs` (modify) | Occupied and blockaded colonies give no materiel or manpower; council goes total when a home or capital-adjacent system is occupied |
| `src/Sim/Colony.cs`, `Civ.cs`, `Policy.cs`, `SaveGame.cs` (modify) | `OccupiedBy`, `BlockadedUntil`, `DefenceLoss`; `Truce`, `TributeTo`, `TributeUntil`; `Area.Fleets`; saves |
| `src/Main.cs` (modify) | `WARSTATS` line at the end of a `--trace` warp |
| `src/Tests/FieldTests.cs` (create) | Stage 2 tests, registered in `SelfTest.Run` beside `WarTests.All` |

### `data/war.json` additions (builder's calls; tune in Task 8)

```json
"field": {
  "declareRelation": -60, "maxWarTravelYears": 25, "claimsMax": 3, "allyRelation": 40, "truceYears": 20,
  "orderEvery": 0.5, "supplyRangeGu": 3.0, "supplyLossPerYear": 0.3, "supplyGainPerYear": 0.6,
  "moraleRegenPerYear": 0.4, "retreatShipsShare": 0.3, "embarkArmies": 4,
  "defenceHullsBase": 4, "defenceHome": 3, "defenceRegenPerYear": 0.25,
  "garrisonHome": 2.0, "landingMargin": 1.0,
  "scorePerOccupied": 8, "scorePerClaimHeld": 20, "scorePerBattle": 2,
  "exhaustionPerYear": 3, "exhaustionPerLossShare": 40, "exhaustionPerOccupiedShare": 40,
  "bothExhausted": 60, "minWarYears": 2, "reparationsShare": 0.2, "tributeShare": 0.1, "tributeYears": 50,
  "logBattleShare": 0.1
},
"battle": {
  "rounds": 6, "roundCap": 0.3, "moraleLossPerLossShare": 1.5, "moraleBreak": 0.3,
  "counters": { "kinetic": "armor", "energy": "shield", "missile": "pd", "craft": "pd" },
  "secondWeapon": 0.33, "unarmed": 0.55, "exoticPass": 0.5, "xpPerBattle": 0.1,
  "traits": {
    "Aggressive": { "dmg": 1.15, "loss": 1.15 }, "Cautious": { "dmg": 0.95, "loss": 0.85, "break": 0.45 },
    "Tactician": { "dmg": 1.1 }, "Logistician": { "supply": 0.5 }, "Raider": { "raid": 1.3 },
    "Beloved": { "morale": 1.2 }, "Ambitious": {}, "Butcher": { "dmg": 1.1, "pop": 2.0 }
  }
}
```

---

### Task 1: War records, reasons, claims, allies, truce, reconcile

**Files:** Create `src/Sim/Wars.cs`, `src/Tests/FieldTests.cs`; modify `data/war.json`, `src/Sim/Civ.cs`, `src/Sim/Simulation.cs` (`StartWar`/`EndWar` wrap the new calls), `src/Tests/SelfTest.cs` (run `FieldTests.All` too).

**Interfaces:**
- Consumes: `WarModel.ReasonById`, `Civ.Relations`, `Civ.Federation`, `Civ.Overlord`, `TravelYears` (Task 2 — declare it here as `public double TravelYears(Civ civ, StarInfo a, StarInfo b)` on `Simulation`, using the Global Constraints formula and `LaneFrom`).
- Produces:
  - `public class War { public int Id; public List<int> A = new(), B = new(); public string Reason; public List<int> Claims = new(); public double Start; public float Score, ExhA, ExhB; public int Battles, WonA, WonB; public double LostKgA, LostKgB, NavyKgA0, NavyKgB0; public bool Ceasefire; public string Ending; }` — `A[0]`/`B[0]` are the leads (attacker, defender); `Score` is from A's side, −100..100.
  - On `Simulation`: `public readonly List<War> Wars; public int NextWarId = 1; public War WarBetween(Civ a, Civ b); public War DeclareWar(Civ attacker, Civ defender, string reason = null); public string ChooseReason(Civ a, Civ b); public bool CanReach(Civ a, Civ b)` (some pair of their colonies within `maxWarTravelYears`); `void Reconcile()`; `public void EndWarWith(War w, bool aWins, List<Term> terms)` (Task 5 fills the terms; here it closes the war, clears `AtWar`, sets truces).
  - `Civ`: `public Dictionary<int, double> Truce = new(); public int TributeTo = -1; public double TributeUntil;`
  - `ChooseReason` order: `extermination` if Ruthless, alien to the target species and `WarStrength ≥ 2×`; `subjugation` if `WarStrength ≥ 3×` and the attacker is Imperial or Ruthless; `revenge` if the target's Atrocity meter > 10 or it took a system from the attacker in the last 50 years (`LastLoss` of attacker and the target holds a system the attacker founded is not tracked: use `attacker.LastLoss > Year − 50`); `ideology` if ethics differ and either is Reverent; otherwise `border_claim`.
  - Claims: up to `claimsMax` defender colonies, nearest first to the attacker's colonies, never the defender's home unless it has one colony.
  - Allies: defender's federation members and overlord join B; attacker's federation members with relation to the defender < −30 join A; a civ already at war with the other side's lead joins too. `AtWar` is set pairwise between every A and every B member.
  - `Reconcile` (called at the start of `FieldStep`): every `AtWar` pair with no `War` gets one (`reason = "revenge"`, no claims); every `War` whose leads are no longer at war, or a lead dead, closes with `Ending = "status quo"`.

- [ ] **Step 1: Write the failing tests** in `FieldTests.All`:

```csharp
("war_has_reason", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
    var w = sim.DeclareWar(h, o);
    SelfTest.Check(w != null && WarModel.Reasons.Any(r => r.Id == w.Reason) && h.AtWar.Contains(o.Id) && sim.WarBetween(o, h) == w, "a war with a reason, both ways");
    SelfTest.Check(w.Claims.All(id => o.Colonies.Any(c => c.Star.Id == id)), "claims are the defender's systems");
}),
("reconcile_atwar", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
    h.AtWar.Add(o.Id); o.AtWar.Add(h.Id); sim.Warp(0.5);
    SelfTest.Check(sim.WarBetween(h, o) != null, "a war started elsewhere gets a record");
    h.AtWar.Remove(o.Id); o.AtWar.Remove(h.Id); sim.Warp(0.5);
    SelfTest.Check(sim.WarBetween(h, o) == null && sim.Wars.All(w => w.Ending != null || w.A.Concat(w.B).All(id => !sim.Civs[id].Dead)), "and loses it when the war is ended elsewhere");
}),
("truce_blocks", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
    var w = sim.DeclareWar(h, o); sim.EndWarWith(w, true, new());
    SelfTest.Check(sim.DeclareWar(h, o) == null && h.Truce[o.Id] > sim.Year, "no new war during the truce");
}),
```

- [ ] **Step 2: Run** `--selftest=war_has` / `reconcile` / `truce` — expected: build fails (types missing).
- [ ] **Step 3: Implement** the data section, `War`, `Civ` fields, `TravelYears`, `CanReach`, `ChooseReason`, `DeclareWar` (returns null when already at war, in truce, `nowar` law, or either dead), `Reconcile`, `EndWarWith` (closing only). `StartWar(a, b)` becomes `DeclareWar(a, b)`; `EndWar(a, b)` becomes `EndWarWith(WarBetween(a, b), ...)` with `[status_quo]` when a war exists. Keep the existing start/end log lines and alerts in `DeclareWar`/`EndWarWith`, adding the reason's name to them ("War between X and Y over a border claim.").
- [ ] **Step 4: Run** `--selftest` — all PASS.
- [ ] **Step 5: Commit** `git commit -m "War in the field: war records, reasons, claims, allies and truces"`

---

### Task 2: Fleet movement, supply and orders

**Files:** Create `src/Sim/Field.cs`; modify `src/Sim/Forces.cs`, `src/Sim/Policy.cs` (append `Area.Fleets`, `Describe` = "Where fleets go and what they do. Manual: your fleets wait for your orders."), `src/Sim/Simulation.cs` (`StepOnce` calls `FieldStep(dy)` after colonies step); test in `FieldTests.cs`.

**Interfaces:**
- Consumes: `War`, `Wars`, `TravelYears`, `FleetsOf`, `ArmiesOf`, `NavyStep` (stage 1).
- Produces:
  - `Fleet`: `public int To = -1, Target = -1; public double Depart, Arrive; public bool Moving => To >= 0;` `Army`: `public int Fleet = -1;` (embarked when ≥ 0; its `At` follows the fleet).
  - `public void Order(Fleet f, int starId, string mission)` (player and council both use it; mission one of defend/attack/raid/blockade; sets `Target`, sends the fleet).
  - `public Godot.Vector3 FleetPos(Fleet f)` (lerp between stars, for views).
  - `void FieldStep(double dy)`: `Reconcile()`; every `orderEvery` years run `CouncilOrders(civ)` for each civ at war (skipping a civ's fleets when `civ.Policy.IsManual(Area.Fleets)`, except broken fleets); arrivals (`Year >= Arrive` → `At = To; To = -1`); supply (`Supply` −`supplyLossPerYear`·dy when the nearest own unoccupied colony is beyond `supplyRangeGu × civ.M.Range`, +`supplyGainPerYear`·dy otherwise, Logistician halves the loss); `Morale` +`moraleRegenPerYear`·dy at an own system, up to 1; then Task 3's battles and Task 4's sieges where sides meet.
  - Council orders: Home Fleet defends the capital, or the nearest own system with enemy fleets or occupation within `maxWarTravelYears`; attack fleets go to the nearest claimed enemy system, else the nearest enemy system with the weakest defence, and embark up to `embarkArmies` idle armies at their star before leaving; raid fleets go to the enemy system with the most industry within reach, mission `blockade`; a fleet with `Morale < moraleBreak` or `Ships < retreatShipsShare ×` its ships at the war's start returns home. At peace every fleet returns to the home star.
  - When a fleet is removed (destroyed, disbanded) its embarked armies are destroyed; when its target star changes owner to its own civ or stops being an enemy, the next order pass retargets it.

- [ ] **Step 1: Write the failing tests:**

```csharp
("fleet_travels", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var f = sim.FleetsOf(h).First();
    var to = sim.Gal.Stars[sim.Gal.Sol.Near[3]];
    double years = sim.TravelYears(h, sim.Gal.Stars[f.At], to);
    sim.Order(f, to.Id, "attack");
    SelfTest.Check(f.Moving && Math.Abs(f.Arrive - f.Depart - years) < 1e-6, "travel takes the drive's time");
    sim.Warp(years + 0.6);
    SelfTest.Check(!f.Moving && f.At == to.Id, "and arrives");
}),
("supply_drops_far", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var f = sim.FleetsOf(h).First();
    sim.SetManual(h, Area.Fleets, true);
    var far = sim.Gal.Stars.OrderByDescending(s => s.Pos.DistanceTo(sim.Gal.Sol.Pos)).First();
    f.At = far.Id; sim.Warp(2);
    SelfTest.Check(f.Supply < 0.6f, "out of supply far from home");
}),
("manual_fleets_kept", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
    sim.SetManual(h, Area.Fleets, true); sim.DeclareWar(o, h);
    var f = sim.FleetsOf(h).First(); int at = f.At; sim.Warp(3);
    SelfTest.Check(f.At == at && !f.Moving, "the council does not move the player's fleets");
}),
("orders_survive_transfer", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
    var w = sim.DeclareWar(h, o); var f = sim.FleetsOf(h).First();
    var target = o.Colonies.Last(); sim.Order(f, target.Star.Id, "attack");
    sim.TransferColony(target, h); sim.Warp(1);
    SelfTest.Check(f.Target != target.Star.Id || !f.Moving, "a fleet stops chasing a system its own side now holds");
    SelfTest.Check(sim.Armies.All(a => a.Fleet < 0 || sim.Fleets.Any(x => x.Id == a.Fleet)), "no army aboard a missing fleet");
}),
```

(`TransferColony(Colony c, Civ to)` is declared here and implemented in Task 5's file; for this task give it the ownership move from the old `War.cs` `Battle` — remove from old civ, add to new, set `Star.Owner`, clear `OccupiedBy`, call `Extinct` when the old civ has no colonies left.)

- [ ] **Step 2: Run** them — expected FAIL (build).
- [ ] **Step 3: Implement** `Field.cs`, the fields, `Area.Fleets`, `Order`, `FleetPos`, `TransferColony`, the `StepOnce` hook.
- [ ] **Step 4: Run** `--selftest` — all PASS.
- [ ] **Step 5: Commit** `git commit -m "War in the field: fleet movement, supply and council orders"`

---

### Task 3: Battles by the numbers

**Files:** Create `src/Sim/Battle.cs`; modify `src/Sim/Field.cs` (fight where sides meet); test in `FieldTests.cs`.

**Interfaces:**
- Consumes: `TierPow`, `Civ.M.Arms/Guard/Fleet`, `LengthKm`, `Squadron`, `Admiral`, `battle` data.
- Produces:
  - `public class BattleSide { public Civ Civ; public List<Fleet> Fleets = new(); public double StationHulls; public int StationHull; }` (a station counts as `StationHulls` frigates of the side's own tech that never move).
  - `public record BattleResult(bool AWins, double LostA, double LostB, double KgLostA, double KgLostB, int Rounds, bool ARetreats, bool BRetreats);`
  - `public static class Battles { public static double HullDamage(Civ att, Civ def); public static double HullHp(int cls); public static BattleResult Resolve(Simulation sim, BattleSide a, BattleSide b); }`
  - `HullDamage`: best `arms` family tier at full weight plus the second-best at `secondWeapon` (ground and doom excluded; with no arms tech, `unarmed`), each family's value `TierPow(tier) × Block`, `Block = 2·TierPow(t)/(TierPow(t)+TierPow(defender guard tier of the countering family))`; `exotic` takes `exoticPass + (1−exoticPass)·Block` against the defender's average guard tier; times `att.M.Fleet`.
  - `HullHp(cls) = (LengthKm(cls)/frigateLengthKm)²`; a hull's damage output also scales by the same factor (bigger ships carry more guns).
  - Rounds (≤ `rounds`): each side's output = Σ squadrons `Ships × HullHp(hull) × HullDamage × (1+Xp) × admiral dmg × Supply`, plus station; losses on the other side = output / that side's mean hull HP, capped at `roundCap` of its hulls, spread over squadrons by HP share (Aggressive/Cautious `loss` multipliers on the receiving side); morale −= `moraleLossPerLossShare × round loss share` (Beloved divides by `morale`); a side breaks at `moraleBreak` (Cautious `break`); stop when one side breaks or has no hulls.
  - After the battle: survivors' squadrons gain `xpPerBattle` (max 1), dead squadrons removed, admirals' `Wins`/`Losses`, the loser's fleets that still exist are ordered home; `War.Battles`, `WonA/WonB`, `LostKgA/B` updated; `BattleFought` fires (source = the attacker's nearest own colony, target = the colony at the star or the nearest one); chronicle line only when a `Main` civ fights and the loser lost ≥ `logBattleShare` of its navy, at most one per war per year.
  - `Field.cs`: at every star where fleets of two warring sides are present (not moving), `Resolve` once per step.

- [ ] **Step 1: Write the failing tests:**

```csharp
("counters_matter", () => {
    var sim = SelfTest.NewSim(); var a = sim.Humanity; var b = sim.Civs.First(c => c.Alien);
    a.M.Arms.Clear(); a.M.Arms["kinetic"] = 4; b.M.Guard.Clear();
    double open = Battles.HullDamage(a, b); b.M.Guard["armor"] = 4;
    SelfTest.Check(Battles.HullDamage(a, b) < open * 0.75, "armour stops kinetics");
    b.M.Guard.Clear(); b.M.Guard["shield"] = 4;
    SelfTest.Check(Math.Abs(Battles.HullDamage(a, b) - open) < 1e-9, "shields do not");
}),
("bigger_navy_wins", () => {
    var sim = SelfTest.NewSim(); var a = sim.Humanity; var b = sim.Civs.First(c => c.Alien);
    var fa = new Fleet { Civ = a.Id, Admiral = new Admiral { Skill = 1, Trait = "Tactician" } }; fa.Add(2, 300, 0f);
    var fb = new Fleet { Civ = b.Id, Admiral = new Admiral { Skill = 1, Trait = "Tactician" } }; fb.Add(2, 100, 0f);
    a.M.Arms.Clear(); b.M.Arms.Clear(); a.M.Guard.Clear(); b.M.Guard.Clear();
    var r = Battles.Resolve(sim, new BattleSide { Civ = a, Fleets = { fa } }, new BattleSide { Civ = b, Fleets = { fb } });
    SelfTest.Check(r.AWins && r.LostB > r.LostA && fa.Squadrons[0].Xp > 0, "three to one wins, and learns");
}),
("huge_battle_finite", () => {
    var sim = SelfTest.NewSim(); var a = sim.Humanity; var b = sim.Civs.First(c => c.Alien);
    var fa = new Fleet { Civ = a.Id, Admiral = new Admiral { Skill = 1.2f, Trait = "Aggressive" } }; fa.Add(Defs.Hulls.Length - 1, 1e6, 1f);
    var fb = new Fleet { Civ = b.Id, Admiral = new Admiral { Skill = 0.85f, Trait = "Cautious" } }; fb.Add(0, 1e6, 0f);
    var r = Battles.Resolve(sim, new BattleSide { Civ = a, Fleets = { fa } }, new BattleSide { Civ = b, Fleets = { fb } });
    SelfTest.Check(r.Rounds <= 6 && double.IsFinite(r.LostA) && double.IsFinite(r.LostB) && r.LostA >= 0 && r.LostB >= 0, "finite and bounded");
}),
("morale_breaks", () => {
    var sim = SelfTest.NewSim(); var a = sim.Humanity; var b = sim.Civs.First(c => c.Alien);
    var fa = new Fleet { Civ = a.Id, Admiral = new Admiral { Skill = 1, Trait = "Tactician" } }; fa.Add(2, 100, 0f);
    var fb = new Fleet { Civ = b.Id, Admiral = new Admiral { Skill = 1, Trait = "Cautious" } }; fb.Add(2, 100, 0f); fb.Morale = 0.5f;
    var r = Battles.Resolve(sim, new BattleSide { Civ = a, Fleets = { fa } }, new BattleSide { Civ = b, Fleets = { fb } });
    SelfTest.Check(r.BRetreats && fb.Ships > 0, "a shaken, cautious fleet pulls out before it is destroyed");
}),
```

- [ ] **Step 2: Run** — expected FAIL (build).
- [ ] **Step 3: Implement** `Battle.cs` and the `Field.cs` hook.
- [ ] **Step 4: Run** `--selftest` — all PASS.
- [ ] **Step 5: Commit** `git commit -m "War in the field: battles with weapon and defence families, admirals, experience and morale"`

---

### Task 4: Sieges, blockades, landings and occupation

**Files:** Create `src/Sim/Ground.cs`; modify `src/Sim/Colony.cs` (fields), `src/Sim/Field.cs`, `src/Sim/WarEconomy.cs`; test in `FieldTests.cs`.

**Interfaces:**
- Consumes: `Battles.Resolve` (station side), `Army`, `Army.Fleet`, `TransferColony`.
- Produces:
  - `Colony`: `public int OccupiedBy = -1; public double BlockadedUntil; public float DefenceLoss;` (0..1 of the station destroyed; regenerates `defenceRegenPerYear` yearly when not besieged).
  - `public double StationHulls(Colony c) => defenceHullsBase × (1 + guard "base" tier) × (Home ? defenceHome : 1) × (1 + [shield] + [aegis] + [sentry] done) × (1 − DefenceLoss)`.
  - `public bool HoldsOrbit(Civ civ, Colony c)`: `civ`'s fleets at the star, no fleet of the colony's side (owner, or occupier when occupied by someone else) there, and `StationHulls(c) < 0.5`.
  - `public double InvasionStrength(IEnumerable<Army> armies, Civ civ) => Σ Troops × Quality × (1 + Xp) × TierPow(arms "ground") × M.Ground`; `public double GarrisonStrength(Colony c)` = the holder's armies at the star (same formula) + `Pop × garrisonPerBillion × troopsPerArmy × TierPow(holder ground) × (Home ? garrisonHome : 1)`.
  - `public bool Land(Colony c, Civ invader, List<Army> armies)`: when `InvasionStrength > GarrisonStrength × landingMargin`: the colony is occupied by `invader` (or freed: `OccupiedBy = -1` when the invader is the owner), defenders' armies there are destroyed, invaders lose `GarrisonStrength/InvasionStrength × 0.5` of their troops and disembark as the garrison (`Fleet = -1`, `At` = star); else invaders lose half their troops and re-embark.
  - `Field.cs`: an enemy fleet alone at a warring colony's star fights the station (`Resolve` with the defender side's `StationHulls`), raising `DefenceLoss` by the station's losses share; once `HoldsOrbit`, a fleet with embarked armies lands, otherwise the colony is blockaded (`BlockadedUntil = Year + orderEvery`).
  - `WarEconomy`: materiel, manpower and garrison wants skip colonies with `OccupiedBy >= 0` or `BlockadedUntil > Year`; `CouncilMobilisation` returns 3 when the civ's home colony or any colony within `supplyRangeGu` of it is occupied (replaces stage 1's Emergency-only ruling, Emergency still counts).

- [ ] **Step 1: Write the failing tests:**

```csharp
("landing_needs_orbit", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
    var col = o.Colonies.First(); var armies = new List<Army> { new() { Civ = h.Id, Troops = 1e6, Quality = 1 } };
    SelfTest.Check(!sim.HoldsOrbit(h, col), "no orbit without a fleet there");
    SelfTest.Check(sim.Land(col, h, armies) && col.OccupiedBy == h.Id, "an overwhelming landing takes the world");
}),
("occupation_cuts_materiel", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2); var o = sim.Civs.First(c => c.Alien && c.Colonies.Count > 1);
    double before = o.Materiel; sim.Warp(1); double peace = o.Materiel - before;
    foreach (var c in o.Colonies) c.OccupiedBy = sim.Humanity.Id;
    before = o.Materiel; sim.Warp(1);
    SelfTest.Check(o.Materiel - before < peace * 0.1 + 1, "occupied systems make nothing for their owner");
}),
("garrison_holds", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
    var col = o.Colonies.First(); var armies = new List<Army> { new() { Civ = h.Id, Troops = 1e-9, Quality = 0.6f } };
    SelfTest.Check(!sim.Land(col, h, armies) && col.OccupiedBy < 0, "a token force is thrown back");
}),
```

- [ ] **Step 2: Run** — expected FAIL (build).
- [ ] **Step 3: Implement** as above.
- [ ] **Step 4: Run** `--selftest` — all PASS.
- [ ] **Step 5: Commit** `git commit -m "War in the field: sieges, blockades, landings and occupation"`

---

### Task 5: Score, exhaustion and peace

**Files:** Modify `src/Sim/Wars.cs`, `src/Sim/Simulation.cs` (yearly: `WarYear()` once per year, from the first civ's yearly block or a sim-level `_nextWarYear`), `src/Sim/Narrative*.cs` only if a log helper is needed; test in `FieldTests.cs`.

**Interfaces:**
- Consumes: `WarModel.Concede/ChooseTerms/Ending/TermById`, `Cultures.Event`, `TransferColony`, `Policy.IsManual(Area.Diplomacy)`, `Raise`.
- Produces:
  - `void WarYear()`: for each open war, Score = `scorePerOccupied × (B colonies occupied by A side − A colonies occupied by B side) + scorePerClaimHeld × (claims occupied by A) + scorePerBattle × (WonA − WonB)`, clamped ±100; each side's exhaustion += `exhaustionPerYear × (1 − 0.5 × lead Resolve) × (1 + 0.2 × lead WarStrain)` + `exhaustionPerLossShare × (KgLost this year / NavyKg0)` + `exhaustionPerOccupiedShare × (own colonies occupied / own colonies)`, clamped 0..100 (reset the yearly loss counters); then peace (below) once `Year − Start ≥ minWarYears`.
  - Peace: the loser is B if `Score > 0` else A. If the loser is B: `give = Concede(B lead, Reason, −Score, ExhB)`, `terms = ChooseTerms(Reason, give, ExhA ≥ bothExhausted && ExhB ≥ bothExhausted)`. If the loser is A (the attacker failing): `[reparations]` when `Concede(A lead, Reason, Score, ExhA) ≥ 15` (B wins), else `[status_quo]` when `ExhA ≥ bothExhausted`, else fight on. A loser at 100 exhaustion in a survival war gets `[collapse]`. Non-empty terms → `EndWarWith(w, winnerIsA, terms)`; when a `Main` civ is the loser and `Policy.IsManual(Area.Diplomacy)`, raise a `Decision` ("Accept the terms: <names>?" with Accept / Fight on, `AskAfter` +5 years) instead.
  - `EndWarWith` applies terms to the loser side's lead (winner = the other lead): `cede_claims` / `cede_occupied` / `partition` → `TransferColony`; `reparations` → `reparationsShare` of materiel moves to the winner; `tribute` → `TributeTo/TributeUntil` (`tributeShare` of the loser's yearly materiel income moves each year, in `YardStep`); `humiliation` → relation −20; `vassal` / `autonomy` → `Overlord = winner`; `release` → loser's subjects freed; `regime` → loser takes the winner's drive (`SetDrive`) and a new leader; `reunion` → every loser colony transfers; `collapse` → every occupied colony transfers and the loser becomes the winner's subject; `ceasefire` → `Ceasefire = true`, occupation stays; anything else leaves occupation to be returned (`OccupiedBy = -1`). `Cultures.Event` "victory"/"defeat" (or "exhausted" for a side ≥ 100). `w.Ending = WarModel.Ending(terms, Reason)`. Truce `truceYears` both ways. Log line "Peace between X and Y: <ending> (<term names>)." for every war a `Main` civ is in, a shorter one for AI wars (one line per war end; no per-term lines).
  - A side whose lead dies mid-war: the war closes with `[collapse]` for that side, every colony it occupied stays with the occupier.

- [ ] **Step 1: Write the failing tests:**

```csharp
("terms_cede_claims", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien && c.Colonies.Count > 2);
    var w = sim.DeclareWar(h, o, "border_claim"); var claim = o.Colonies.First(c => w.Claims.Contains(c.Star.Id));
    sim.EndWarWith(w, true, new() { WarModel.TermById("cede_claims") });
    SelfTest.Check(claim.Civ == h && w.Ending == "surrender" && !h.AtWar.Contains(o.Id), "the claims change hands");
}),
("ceasefire_keeps_occupation", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien && c.Colonies.Count > 1);
    var w = sim.DeclareWar(h, o); var col = o.Colonies.Last(); col.OccupiedBy = h.Id;
    sim.EndWarWith(w, true, new() { WarModel.TermById("ceasefire") });
    SelfTest.Check(w.Ending == "ceasefire" && col.OccupiedBy == h.Id && col.Civ == o, "the front freezes where it is");
}),
("exhaustion_ends_wars", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
    var w = sim.DeclareWar(h, o, "border_claim"); w.ExhA = 95; w.ExhB = 95; w.Score = 0;
    sim.Warp(3);
    SelfTest.Check(w.Ending != null, $"two exhausted sides stop ({w.Ending})");
}),
("manual_peace_asks", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
    sim.SetManual(h, Area.Diplomacy, true);
    var w = sim.DeclareWar(o, h, "border_claim"); w.Score = 90; w.ExhB = 99; w.ExhA = 10;
    sim.Warp(3);
    SelfTest.Check(w.Ending == null && sim.Alerts.Any(a => a.Cat == "Decision" && !a.Done && a.Title.Contains("terms")), "the player is asked");
}),
("war_side_dies", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien && c.Colonies.Count > 1);
    var w = sim.DeclareWar(h, o); o.Colonies.Last().OccupiedBy = h.Id;
    foreach (var c in o.Colonies.Where(c => c.OccupiedBy < 0).ToList()) sim.DestroyColony(c, "test");
    sim.Warp(1);
    SelfTest.Check(w.Ending != null && !sim.FleetsOf(o).Any() && sim.Colonies.All(c => c.OccupiedBy < 0 || !sim.Civs[c.OccupiedBy].Dead), "a dead side's war ends cleanly");
}),
```

- [ ] **Step 2: Run** — expected FAIL (assertions; the types exist).
- [ ] **Step 3: Implement** `WarYear`, the peace rules, `EndWarWith` terms, tribute in `YardStep`, the yearly hook.
- [ ] **Step 4: Run** `--selftest` — all PASS.
- [ ] **Step 5: Commit** `git commit -m "War in the field: score, exhaustion, terms and peace"`

---

### Task 6: Retire the coin-flip war

**Files:** Modify `src/Sim/Simulation.cs` (`GalaxyCheck`), `src/Sim/War.cs`; test in `FieldTests.cs`.

**Interfaces:**
- Consumes: `DeclareWar`, `ChooseReason`, `CanReach`, `Truce`.
- Produces: `GalaxyCheck` keeps contact and relation drift; the declaration test becomes `!war && rel < declareRelation && CanReach(a, b) && !allied && Year ≥ truce && !Laws.Contains("nowar")` with the existing stealth roll and the existing player Decision (its text names the chosen reason); the `war && rel > -20` peace branch and the `war && border` `Battle` call are deleted; `War.cs` loses `Battle`. `border` stays as is for relation drift only.

- [ ] **Step 1: Write the failing test:**

```csharp
("wars_are_fought", () => {
    var sim = SelfTest.NewSim(); sim.Warp(300);
    SelfTest.Check(sim.Wars.Count > 0, "wars are declared");
    var ended = sim.Wars.Where(w => w.Ending != null).ToList();
    SelfTest.Check(sim.Wars.Sum(w => w.Battles) >= sim.Wars.Count, $"battles in the wars ({sim.Wars.Sum(w => w.Battles)} in {sim.Wars.Count})");
    SelfTest.Check(ended.All(w => w.Ending != "none"), "every ended war has an ending");
}),
```

- [ ] **Step 2: Run** `--selftest=wars_are` — expected FAIL (the old path still declares through the coin flip, and no battles are recorded on `War`).
- [ ] **Step 3: Implement** as above.
- [ ] **Step 4: Run** `--selftest` — all PASS.
- [ ] **Step 5: Commit** `git commit -m "War in the field: wars declared for reasons, the coin-flip battle retired"`

---

### Task 7: Saves

**Files:** Modify `src/Sim/SaveGame.cs`; test in `FieldTests.cs`.

**Interfaces:**
- Produces: `SaveDto.Wars` (List<War>, serialised as-is), `NextWarId`; `CivDto.Truce`, `TributeTo`, `TributeUntil`; `ColonyDto.OccupiedBy` (`int?`, missing = −1), `BlockadedUntil`, `DefenceLoss`. Fleet movement fields and `Army.Fleet` ride on the stage-1 serialisation (check defaults: `To = -1`, `Fleet = -1` when missing — use `[JsonInclude]` initialisers that survive a missing field). A save from before this stage with `AtWar` pairs gets `War` records from `Reconcile` on the first step.

- [ ] **Step 1: Write the failing test:**

```csharp
("field_state_saved", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
    var w = sim.DeclareWar(h, o); var f = sim.FleetsOf(h).First(); sim.Order(f, o.Colonies[0].Star.Id, "attack");
    o.Colonies.Last().OccupiedBy = h.Id; h.Truce[5] = 2500;
    SaveGame.Save(sim, 20610, "user://selftest_field.json");
    var (s2, _) = SaveGame.Load("user://selftest_field.json");
    var f2 = s2.FleetsOf(s2.Humanity).First(x => x.Id == f.Id);
    SelfTest.Check(s2.Wars.Count == sim.Wars.Count && s2.WarBetween(s2.Humanity, s2.Civs[o.Id]).Reason == w.Reason, "wars round-trip");
    SelfTest.Check(f2.To == f.To && f2.Arrive == f.Arrive && s2.Civs[o.Id].Colonies.Last().OccupiedBy == h.Id && s2.Humanity.Truce[5] == 2500, "movement, occupation and truces round-trip");
}),
```

- [ ] **Step 2: Run** — expected FAIL.
- [ ] **Step 3: Implement** the DTO fields and defaults.
- [ ] **Step 4: Run** `--selftest` and `--warp=300 --savetest` — all PASS and `SAVETEST equal=True`.
- [ ] **Step 5: Commit** `git commit -m "War in the field: save wars, movement, occupation and truces"`

---

### Task 8: Acceptance runs and tuning

**Files:** Modify `src/Main.cs` (trace summary), `data/war.json` (tuning only).

**Interfaces:**
- Produces: at the end of a `--trace` warp, one line
  `WARSTATS wars=<n> ended=<n> battles=<n> perwar=<mean> nobattle=<wars with 0 battles> endings=surrender:<n>,compromise:<n>,ceasefire:<n>,status quo:<n>,collapse:<n> civsfought=<civs in ≥1 war>/<civs alive at any time> chronicle=<log lines per century>`.

- [ ] **Step 1: Add** the `WARSTATS` line.
- [ ] **Step 2: Run** `time "$(cat .godotpath)" --headless --path . -- --warp=940 --trace --seed=<s> | grep -E "WARSTATS"` for seeds 20610, 7, 4242. Expected (acceptance, spec §7): battles in the hundreds per game; `nobattle` under a quarter of ended wars; at least three different endings across the three runs; `civsfought` most civs; chronicle ~150–300 lines a century; real time under ~40 s.
- [ ] **Step 3: Tune** only `data/war.json` values until Step 2 holds; record each change and its effect in the ledger.
- [ ] **Step 4: Run** `--selftest` and `--warp=300 --savetest` — all PASS, `SAVETEST equal=True`.
- [ ] **Step 5: Commit** `git commit -m "War in the field: trace war statistics and tuning"`

---

## Not in this stage

Infighting, grievance, secession and coups (stage 3, which uses the `independence`/`autonomy`/`reunion`/`partition`
terms built here); special forces (stage 4); the Wars and Military screens, map markers and battle graphics (stage 5:
`FleetPos` and `BattleFought` are their hooks); Total War-style command (stage 6).
