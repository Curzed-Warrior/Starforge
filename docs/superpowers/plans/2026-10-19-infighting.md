# Infighting (Stage 3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Humanity (and every civ) can fall apart: systems build grievance from unequal access, climb a ladder from calm
to open revolt, form movements the council (or player) answers, and leave peacefully or through a secession war; the
home system's own worlds can rise; victorious admirals take power in coups, pushed peoples overturn their councils,
juntas rule; and the player can switch to a splinter. Whether humanity fractures is left to chance.

**Architecture:** A yearly `StrifeStep(civ)` (`src/Sim/Strife.cs`) computes each system's grievance as a pull toward
a target built from named causes (not a running sum fighting a flat relief term: that is why the spike never left
calm), climbs the ladder with hysteresis, and raises movements. Answers live in `src/Sim/Movements.cs`, separations
and secession wars in `src/Sim/Secession.cs` (reusing `Splinter()` and stage 2's `DeclareWar`/`WarYear`), the home
system's worlds in `src/Sim/HomeWorlds.cs` (reusing `Colony.Rival` and `Narrative.StartCrisis(c, "war")`), coups
and revolutions in `src/Sim/Coups.cs`. The old election schism routes through the new separation code.

**Tech Stack:** Godot 4.7.2 .NET, C# (.NET 8), System.Text.Json. Godot binary path in `.godotpath`.

**Spec:** `docs/superpowers/specs/2026-10-18-war-and-strife-design.md` (sections 1, 4.7, 4.8 Interior and switching
sides, 7). Builds on stages 1–2 (`docs/superpowers/plans/2026-10-18-war-foundation.md`,
`docs/superpowers/plans/2026-10-19-war-in-the-field.md`, merged on `master` at `9172daa`).

## Global Constraints

- Same rules for everyone: humanity, splinters and aliens run through the same code (a hive feels no grievance; a mostly digital people little).
- Per-colony work runs at most yearly. `--warp=940 --trace` must stay under ~40 s (25–37 s at the start of this stage, seed-dependent).
- Saves round-trip byte-identically (`--warp=300 --savetest` and `--warp=700 --savetest` print `SAVETEST equal=True`); saves from before this stage still load.
- Tunable numbers live in `data/war.json` (new section `strife`).
- New `Area` values are appended to the end of the enum (`Interior` after `Fleets`).
- Copy: plain sentences, British spelling, no jargon. Chronicle ~150–300 lines a century at most; movements and revolts are news, yearly grievance is not.
- **No steering** (owner's decision): nothing pushes a game toward or away from fracture. Acceptance only checks that both outcomes happen across seeds and that a Sol uprising is possible before 2300.
- Build: `dotnet build -v q` prints `Build succeeded.` Tests: `"$(cat .godotpath)" --headless --path . -- --selftest[=filter]` ends `SELFTEST DONE pass=<n> fail=0`, exit 0 (read Godot's exit code, not grep's).
- Lesson from `spike/war-prototype` `Unrest.cs` (reference only, never merge): grievance as `+causes − relief(3.5 + works)` per year never left calm for humanity. Use a target-and-pull model and measure the ladder in acceptance.

## Review Focus

1. **A system on the ladder that changes hands** (ceded, occupied, destroyed, splintered) keeps no stale movement, and its grievance is judged by its new owner from the next year. (Test: `transfer_resets_movement`, Task 1.)
2. **A rising that includes the capital, or every system**, never makes a civ with no colonies or two capitals; the parent keeps at least its capital. (Test: `capital_never_leaves`, Task 3.)
3. **The player switching to a splinter mid-war or with alerts waiting**: open Decisions belong to the old civ and are closed; manual areas and policy carry over; nothing still reads the old civ as `Main`. (Test: `switch_player_clean`, Task 6.)
4. **A coup or revolution in a civ that is already a junta, a hive or an overmind** does not stack or throw. (Test: `no_double_junta`, Task 5.)
5. **Civil-war terms are side-aware**: the union winning gets reunion, autonomy or partition; the rebels winning get independence; an overlord–vassal cycle never forms. (Test: `civil_war_terms`, Task 3.)

---

## File Structure

| File | Responsibility |
|---|---|
| `data/war.json` (modify) | New `strife` section (values below) |
| `src/Sim/Strife.cs` (create) | Grievance causes and target, the ladder, strikes, `StrifeStep`, `Leaning`, government factor |
| `src/Sim/Movements.cs` (create) | Movements at insurgency; the four answers; council choice; Decision for manual Interior |
| `src/Sim/Secession.cs` (create) | Revolting blocs; peaceful separation vs secession war; defecting fleets and armies; civil-war terms |
| `src/Sim/HomeWorlds.cs` (create) | Per-world grievance inside the home system; uprisings as `Rival` + war crisis; movement names |
| `src/Sim/Coups.cs` (create) | Coups by ambitious victorious admirals, revolutions, juntas |
| `src/Sim/Simulation.cs` (modify) | Yearly hook; `Election` schism routes through `Separate`; `SwitchPlayer` |
| `src/Sim/Wars.cs` (modify) | Side-aware terms for `independence` wars; `independence` case; no overlord cycle |
| `src/Sim/Colony.cs`, `Civ.cs`, `Policy.cs`, `SaveGame.cs`, `Construction.cs` (modify) | Fields; `Area.Interior`; strikes stop levies; saves |
| `src/View/EmpireScreen.cs` (modify) | Overview line (unrest summary); "Lead <splinter>" buttons on the Policies tab |
| `src/Main.cs` (modify) | `STRIFESTATS` line at the end of a `--trace` warp |
| `src/Tests/StrifeTests.cs` (create) | Stage 3 tests, run beside `WarTests` and `FieldTests` |

### `data/war.json` additions (builder's calls; tune in Task 7)

```json
"strife": {
  "ladder": { "up": [25, 45, 65, 85], "down": [18, 38, 58, 78], "names": ["calm", "discontent", "strikes", "insurgency", "open revolt"] },
  "pull": 0.25,
  "causes": {
    "levy": 60, "distance": 12, "distanceSlow": 18, "crowding": 10, "emergency": 15, "warStrain": 4, "warExhaustion": 0.25,
    "outOfPower": 8, "deposits": 6, "occupied": 30, "poverty": 15, "dread": 10, "mandatory": 8
  },
  "relief": { "autonomy": 20, "invested": 25, "megaOrder": 100, "faith": 10, "base": 5 },
  "gov": { "hive": 0, "digital": 0.6, "Synthetic Commonwealth": 0.6, "Directorate": 1.1, "junta": 1.3 },
  "answers": { "investCost": 0.05, "investYears": 25, "crackdown": 30, "crackdownSpread": 6, "selfRuleIndustry": 0.5 },
  "revolt": { "chancePerYear": 0.25, "civilWarShare": 0.2, "civilWarPopShare": 0.25, "letGoMax": 3, "defectLoyalty": 0.6 },
  "coup": { "exhaustion": 50, "wins": 3, "loyalty": 0.5, "chancePerYear": 0.1, "juntaYears": 25 },
  "revolution": { "insurgentShare": 0.33, "chancePerYear": 0.2 },
  "homeWorlds": { "far": 6, "hub": 10, "crowding": 6, "emergency": 10, "ruthless": 5, "base": 3, "riseChance": 0.3 }
}
```

The `causes` values are the grievance target contributed by a full-strength cause (target 0..100); `relief` values
subtract from the target. Grievance moves `pull × dy` of the way to the target each year.

---

### Task 1: Grievance and the ladder

**Files:** Create `src/Sim/Strife.cs`, `src/Tests/StrifeTests.cs`; modify `data/war.json`, `src/Sim/Colony.cs`, `src/Sim/Simulation.cs` (yearly `StrifeStep(civ, yd)` after `BarracksStep`), `src/Sim/Construction.cs` (a colony on strike sends no levy), `src/Sim/Field.cs` (`TransferColony` resets strife fields), `src/Tests/SelfTest.cs`.

**Interfaces:**
- Consumes: `Civ.LevyOut`, `Civ.Emergency`, `Civ.WarStrain`, `Wars` (exhaustion via the civ's open wars), `Colony.OccupiedBy`, `Colony.MegaOrder` (make it `internal`), `Civ.Dread`, `Civ.Faith`, `Civ.MandatoryService`, `Civ.Culture.Duty`, `Government.Of(civ)`, `Folds`.
- Produces:
  - `Colony`: `public float Grievance; public int Unrest; public string Cause, Movement; public bool Autonomy; public double InvestedUntil;`
  - `public static string[] UnrestNames` (from data); `public static int Stage(float g, int cur)` (hysteresis: up at `up[s]`, down below `down[s-1]`).
  - `public static string Leaning(Colony c)` — the home colony leans to the civ's drive; a colony with strategic deposits or metal-rich leans `Industrious`, otherwise by `(c.Star.Seed & 3) == 0 ? "Contemplative" : "Expansionist"`.
  - `public double GrievanceTarget(Colony c, out string cause)` — Σ causes (each scaled 0..1 by its condition: levy = `LevyOut` share; distance = min(dist to capital / 25 gu, 3)/3, `distanceSlow` when the civ cannot fold; crowding when `Pop > 0.92 PopCap`; emergency; `warStrain × WarStrain`; `warExhaustion ×` the civ's highest open-war exhaustion; out of power when `Leaning ≠ Drive`; deposits when it holds strategic deposits and no autonomy; occupied; poverty when its `IndustryKg` < 0.2 × the civ's mean; dread × `Dread`; mandatory when `MandatoryService && Duty < 0`) × the government factor, minus relief (autonomy, invested while `Year < InvestedUntil`, `megaOrder × MegaOrder`, `faith × Faith`, `base`), clamped 0..100; `cause` is the largest contributor's phrase ("its industry is shipped off to the capital's great works", "the capital is a lifetime away", …).
  - `void StrifeStep(Civ civ, double dy)`: for every non-home colony, `Grievance += (target − Grievance) × min(1, pull × dy)`; `Unrest = Stage(...)`; stage ≥ 2 (strikes) stops its levy; crossing into 3 calls `Movements.Rise` (Task 2); crossing below 3 clears `Movement` with one chronicle line for `Main`. Hive civs skip everything (all zero). The home colony is handled by Task 4.
  - `TransferColony` resets `Grievance`, `Unrest`, `Movement`, `Autonomy`, `InvestedUntil`.

- [ ] **Step 1: Write the failing tests** in `StrifeTests.All`:

```csharp
("ladder_hysteresis", () => {
    SelfTest.Check(Simulation.Stage(30, 0) == 1 && Simulation.Stage(20, 1) == 1 && Simulation.Stage(17, 1) == 0, "up at 25, down below 18");
    SelfTest.Check(Simulation.Stage(90, 0) == 4, "straight to open revolt");
}),
("levy_breeds_grievance", () => {
    var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
    var c = h.Colonies.First(x => !x.Home); h.LevyOut[c] = 1; c.Autonomy = false;
    double t0 = sim.GrievanceTarget(c, out _); h.LevyOut[c] = 0;
    SelfTest.Check(t0 > sim.GrievanceTarget(c, out _) + 30, "shipping its industry away is resented");
}),
("hive_feels_none", () => {
    var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; h.M.Hive = true;
    foreach (var c in h.Colonies) c.Grievance = 80;
    sim.Warp(2);
    SelfTest.Check(h.Colonies.All(c => c.Grievance < 1 && c.Unrest == 0), "a hive has no grievance");
}),
("strikes_stop_levy", () => {
    var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
    var c = h.Colonies.First(x => !x.Home); c.Unrest = 2; c.Grievance = 60;
    SelfTest.Check(c.LocalWorksShipped() == 0, "a striking system ships nothing");
}),
("transfer_resets_movement", () => {
    var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; var o = sim.Civs.First(x => x.Alien && !x.Dead);
    var c = h.Colonies.First(x => !x.Home); c.Grievance = 90; c.Unrest = 4; c.Movement = "Free Worlds";
    sim.TransferColony(c, o);
    SelfTest.Check(c.Grievance == 0 && c.Unrest == 0 && c.Movement == null, "a new owner starts the books afresh");
}),
```

(`Colony.LocalWorksShipped()` is the share of `LocalWorks` this colony may ship to other systems' levies: 0 when `Unrest >= 2`, else `LocalWorks`; Construction's levy code uses it.)

- [ ] **Step 2: Run** `--selftest=ladder` etc. — expected FAIL (build: types missing).
- [ ] **Step 3: Implement** as above.
- [ ] **Step 4: Run** `--selftest` — all PASS (55 earlier + 5).
- [ ] **Step 5: Commit** `git commit -m "Infighting: grievance, the unrest ladder and strikes"`

---

### Task 2: Movements and the council's answers

**Files:** Create `src/Sim/Movements.cs`; modify `src/Sim/Policy.cs` (append `Area.Interior`; `Describe` = "How the council answers unrest. Manual: each movement's demands come to you."); test in `StrifeTests.cs`.

**Interfaces:**
- Consumes: `Colony.Grievance/Unrest/Cause/Movement/Autonomy/InvestedUntil`, `Raise`, `Waiting`, `Policy.AskAfter`.
- Produces:
  - `public void Rise(Colony c)`: names the movement (`"the Free Worlds of {sys}"`, `"the {sys} Compact"`, `"the {Faction(Leaning)} of {sys}"` by cause), logs one line, and calls `Answer`.
  - `public enum Answer { SelfRule, Invest, Crackdown, Wait }`; `public void Respond(Colony c, Answer a)`:
    - SelfRule: `Autonomy = true` (relief; its industry no longer ships, `selfRuleIndustry` of its industry stays home);
    - Invest: costs `investCost × civ.Materiel` (or the capital's `Stock` when the civ has no materiel), `InvestedUntil = Year + investYears`;
    - Crackdown: `Grievance −= crackdown`, every other non-home colony of the civ `+= crackdownSpread`, `Atrocity` meter +5 when the civ is Reverent (it costs them);
    - Wait: nothing.
  - `public Answer CouncilAnswer(Colony c)`: Reverent → Invest if affordable else SelfRule; Ruthless or a junta → Crackdown; Pragmatic → SelfRule when the cause is distance or deposits, Invest when it is poverty or crowding, else Wait; a hive never gets here.
  - When the civ is `Main` and `Policy.IsManual(Area.Interior)`: a Decision "The {movement} demand an answer" with four choices (Grant self-rule / Invest / Crack down / Wait), key `"interior:" + star id`, `AskAfter` 5 years on Wait.

- [ ] **Step 1: Write the failing tests:**

```csharp
("council_answers", () => {
    var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; h.SetDrive(h.Drive, "Reverent");
    var c = h.Colonies.First(x => !x.Home); h.Materiel = 1e30;
    sim.Rise(c);
    SelfTest.Check(c.Movement != null && c.InvestedUntil > sim.Year, "a Reverent council invests");
}),
("manual_interior_asks", () => {
    var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; sim.SetManual(h, Area.Interior, true);
    var c = h.Colonies.First(x => !x.Home); sim.Rise(c);
    SelfTest.Check(sim.Alerts.Any(a => a.Cat == "Decision" && !a.Done && a.Key == "interior:" + c.Star.Id), "the player is asked");
}),
("self_rule_relieves", () => {
    var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
    var c = h.Colonies.First(x => !x.Home); h.LevyOut[c] = 1;
    double before = sim.GrievanceTarget(c, out _); sim.Respond(c, Simulation.Answer.SelfRule);
    SelfTest.Check(sim.GrievanceTarget(c, out _) < before - 15 && c.LocalWorksShipped() == 0, "self-rule calms and keeps its industry home");
}),
```

- [ ] **Step 2: Run** — expected FAIL (build).
- [ ] **Step 3: Implement**; wire `StrifeStep`'s crossing into stage 3 to `Rise`.
- [ ] **Step 4: Run** `--selftest` — all PASS.
- [ ] **Step 5: Commit** `git commit -m "Infighting: movements and the council's answers"`

---

### Task 3: Revolts, separations and secession wars

**Files:** Create `src/Sim/Secession.cs`; modify `src/Sim/Wars.cs` (side-aware civil-war terms, `independence` case, no overlord cycle), `src/Sim/Simulation.cs` (`Election`'s frontier schism calls `Separate`); test in `StrifeTests.cs`.

**Interfaces:**
- Consumes: `Splinter(parent, drive, cols)`, `DeclareWar(a, b, "independence")`, `WarYear`/`PeaceCheck`/`ApplyTerms`, `Fleets`, `Admiral.Loyalty`, `Armies`.
- Produces:
  - Yearly in `StrifeStep`: systems at stage 4 roll `revolt.chancePerYear × dy`; a rising takes every stage-4 system within `supplyRangeGu` of the first (the bloc); never the capital; the parent always keeps ≥ 1 colony.
  - `public Civ Separate(Civ parent, List<Colony> bloc, string drive, bool peaceful)`: `Splinter` the bloc (drive = the bloc's most common `Leaning`); fleets parked at bloc stars whose admiral `Loyalty < defectLoyalty` change sides (and their embarked armies); armies garrisoned in the bloc change sides; bloc colonies start at grievance 20, unrest 0. Peaceful: relations +20 both ways, truce `truceYears`. Otherwise `DeclareWar(parent, splinter, "independence")`.
  - Peaceful when the bloc has ≤ `letGoMax` systems and the parent is Reverent, or Pragmatic and not at war; a civil war when the bloc is ≥ `civilWarShare` of the systems or `civilWarPopShare` of the people; otherwise the parent fights.
  - Civil-war peace (reason `independence`, side A = the union, B = the rebels): the union winning offers the rebels, in order, `reunion` (give ≥ 50), `autonomy` (≥ 25), `partition` (≥ 30, cede occupied); the rebels winning get `[independence]` (the war simply ends, the splinter stays free). `ApplyTerms`: `independence` → nothing beyond peace; `autonomy` → loser becomes the winner's subject **unless** the winner is already the loser's subject (then the winner's `Overlord` is cleared instead); `vassal` gets the same guard.
  - `Election`'s "far colonies break away" path calls `Separate(civ, frontier, leaving, peaceful: civ.Ethic != "Ruthless")` instead of `Splinter` directly; its home-colony exodus path is unchanged. The `AI` meter's Machine Concord split stays a direct `Splinter` (a rising of machines, not a movement) but goes through `Separate(..., peaceful: false)` so it is a war.

- [ ] **Step 1: Write the failing tests:**

```csharp
("peaceful_separation", () => {
    var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; h.SetDrive(h.Drive, "Reverent");
    var bloc = h.Colonies.Where(c => !c.Home).Take(2).ToList();
    var s = sim.Separate(h, bloc, h.Drive, peaceful: true);
    SelfTest.Check(!h.AtWar.Contains(s.Id) && bloc.All(c => c.Civ == s) && h.Truce[s.Id] > sim.Year, "let go in peace");
}),
("secession_war", () => {
    var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
    var bloc = h.Colonies.Where(c => !c.Home).Take(3).ToList();
    var s = sim.Separate(h, bloc, h.Drive, peaceful: false);
    var w = sim.WarBetween(h, s);
    SelfTest.Check(w != null && w.Reason == "independence" && w.A[0] == h.Id, "the union fights to keep them");
}),
("capital_never_leaves", () => {
    var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
    foreach (var c in h.Colonies) { c.Grievance = 99; c.Unrest = 4; }
    sim.Warp(3);
    SelfTest.Check(h.Colonies.Any(c => c.Home) && !h.Dead, "the capital stays with the union");
    SelfTest.Check(sim.Civs.All(c => c.Colonies.Count(x => x.Home) <= 1), "never two capitals");
}),
("civil_war_terms", () => {
    var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
    var s = sim.Separate(h, h.Colonies.Where(c => !c.Home).Take(2).ToList(), h.Drive, peaceful: false);
    var w = sim.WarBetween(h, s);
    sim.EndWarWith(w, true, new() { WarModel.TermById("autonomy") });
    SelfTest.Check(s.Overlord == h && h.Overlord != s, "the union wins: autonomy within it");
    var s2 = sim.Separate(h, h.Colonies.Where(c => !c.Home).Take(2).ToList(), h.Drive, peaceful: false);
    var w2 = sim.WarBetween(h, s2);
    sim.EndWarWith(w2, false, new() { WarModel.TermById("independence") });
    SelfTest.Check(s2.Overlord == null && !s2.Dead && w2.Ending != null, "the rebels win: free");
}),
("fleets_defect", () => {
    var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity;
    var bloc = h.Colonies.Where(c => !c.Home).Take(2).ToList(); var f = sim.FleetsOf(h).First();
    f.At = bloc[0].Star.Id; f.To = -1; f.Admiral.Loyalty = 0.3f;
    var s = sim.Separate(h, bloc, h.Drive, peaceful: false);
    SelfTest.Check(f.Civ == s.Id, "a disloyal admiral takes the fleet over");
}),
```

- [ ] **Step 2: Run** — expected FAIL (build).
- [ ] **Step 3: Implement** as above.
- [ ] **Step 4: Run** `--selftest` — all PASS.
- [ ] **Step 5: Commit** `git commit -m "Infighting: revolts, peaceful separations and secession wars"`

---

### Task 4: The home system's worlds

**Files:** Create `src/Sim/HomeWorlds.cs`; modify `src/Sim/Colony.cs` (fields), `src/Sim/Strife.cs` (home colony branch); test in `StrifeTests.cs`.

**Interfaces:**
- Consumes: `Colony.Settled`, `Colony.Hubs`, `Colony.Seat`, `Colony.Rival`, `Narrative.StartCrisis(c, "war")`, `homeWorlds` data.
- Produces:
  - `Colony`: `public Dictionary<Body, float> WorldGrievance = new(); public Dictionary<Body, int> WorldUnrest = new();` (saved by body name).
  - `void HomeWorlds(Colony c, double dy)`: for each settled body except the seat with settlement ≥ 0.4: target = `far × min(|log2(orbit / seatOrbit)|, 3)/3` (Moon counts 0.3) + `hub ×` its hub level/3 + crowding + emergency + `ruthless` when the civ is Ruthless + `base`, × government factor − relief (`megaOrder × MegaOrder`, `faith × Faith`, elevator/ring finished −4 each); pull and ladder as Task 1; at stage 3 a movement (`MovementName(body)`: Mars → "Free Mars movement", Moon → "Lunar Independence League", Main Belt → "Belt Compact", Venus → "Cloud Cities' Assembly", Jovian moons → "Jovian Accord", Saturnian moons → "Saturnian Union", else "{world} Self-Rule movement") answered by `Respond` semantics on the world; at stage 4, with no rival or crisis already, `riseChance × dy` → `Uprising`: `c.Rival = new Rival { Name, Pop = c.Pop × 0.04, Ships = 3 + settlement × 6 + grievance/25, Planet = b, T0 = Year, Stance = "war" }`, `Narrative.StartCrisis(c, "war")`, world grievance reset to 60, a Raise("Unrest") for `Main` and a one-time milestone "The First Uprising".

- [ ] **Step 1: Write the failing tests:**

```csharp
("mars_can_rise", () => {
    var sim = SelfTest.NewSim(); sim.Warp(150); var h = sim.Humanity; var home = h.Colonies.First(c => c.Home);
    var mars = home.Settled.Keys.FirstOrDefault(b => b.Name == "Mars");
    if (mars == null) { home.Settled[home.Sys.Find("Mars")] = 1f; mars = home.Sys.Find("Mars"); }
    home.WorldGrievance[mars] = 95; home.WorldUnrest[mars] = 4; home.Rival = null; home.Crisis = null;
    for (int i = 0; i < 40 && home.Rival == null; i++) sim.Warp(1);
    SelfTest.Check(home.Rival != null && home.Rival.Planet == mars, "a Mars that feels used can rise");
}),
("far_worlds_resent_more", () => {
    var sim = SelfTest.NewSim(); sim.Warp(300); var home = sim.Humanity.Colonies.First(c => c.Home);
    var moon = home.Sys.Find("Moon"); var titan = home.Sys.Find("Titan");
    SelfTest.Check(sim.WorldTarget(home, titan) > sim.WorldTarget(home, moon), "Titan feels further from Earth than the Moon");
}),
```

(`public double WorldTarget(Colony c, Body b)` exposes the target for the test.)

- [ ] **Step 2: Run** — expected FAIL (build).
- [ ] **Step 3: Implement** as above.
- [ ] **Step 4: Run** `--selftest` — all PASS.
- [ ] **Step 5: Commit** `git commit -m "Infighting: the home system's worlds and their uprisings"`

---

### Task 5: Coups, revolutions and juntas

**Files:** Create `src/Sim/Coups.cs`; modify `src/Sim/Civ.cs` (`public double JuntaUntil; public bool Junta(double year) => JuntaUntil > year;`), `src/Sim/Strife.cs` (call `Coup` and `Revolution` yearly; junta in the government factor), `src/Sim/Movements.cs` (juntas crack down); test in `StrifeTests.cs`.

**Interfaces:**
- Produces:
  - `void Coup(Civ civ, double dy)`: when the civ is in an open war with its exhaustion ≥ `coup.exhaustion`, an admiral with `Wins ≥ coup.wins`, `Loyalty < coup.loyalty` or trait Ambitious takes the council with chance `coup.chancePerYear × dy`: `JuntaUntil = Year + juntaYears`, the admiral becomes `Leader` (Drive kept), Raise("Unrest") for `Main`, one chronicle line. Never when already a junta, a hive, or `OvermindUntil > Year`.
  - `void Revolution(Civ civ, double dy)`: when ≥ `revolution.insurgentShare` of its colonies are at stage ≥ 3 (or any open war has its exhaustion at 100), chance `revolution.chancePerYear × dy`: the most common `Leaning` among the unrest takes the council (`SetDrive`), a new leader, `JuntaUntil = 0`, every colony's grievance halved; Raise for `Main`.
  - Juntas: government factor ×`gov.junta`, `CouncilAnswer` always Crackdown, `CouncilMobilisation` +1 level (capped by comfort+1); when `JuntaUntil` passes, a new elected leader and one chronicle line.

- [ ] **Step 1: Write the failing tests:**

```csharp
("admiral_coup", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
    var w = sim.DeclareWar(h, o); w.ExhA = 80;
    var f = sim.FleetsOf(h).First(); f.Admiral.Wins = 5; f.Admiral.Loyalty = 0.2f; f.Admiral.Trait = "Ambitious";
    for (int i = 0; i < 60 && h.JuntaUntil <= sim.Year; i++) { w.ExhA = 80; sim.Warp(1); }
    SelfTest.Check(h.JuntaUntil > sim.Year && h.Leader.Name == f.Admiral.Name, "a victorious, ambitious admiral takes the council");
}),
("no_double_junta", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; h.JuntaUntil = sim.Year + 10; string lead = h.Leader.Name;
    var o = sim.Civs.First(c => c.Alien); var w = sim.DeclareWar(h, o); w.ExhA = 80;
    foreach (var f in sim.FleetsOf(h)) { f.Admiral.Wins = 9; f.Admiral.Loyalty = 0; }
    sim.Warp(3);
    SelfTest.Check(h.Leader.Name == lead, "a junta is not overthrown by its own admirals");
}),
("revolution", () => {
    var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; string d0 = h.Drive;
    foreach (var c in h.Colonies.Where(c => !c.Home)) { c.Grievance = 70; c.Unrest = 3; c.Movement = "x"; }
    for (int i = 0; i < 40 && h.Drive == d0; i++) sim.Warp(1);
    SelfTest.Check(h.Drive != d0 || h.Colonies.All(c => c.Grievance < 70), "a people pushed too far overturns its council");
}),
```

- [ ] **Step 2: Run** — expected FAIL (build).
- [ ] **Step 3: Implement** as above.
- [ ] **Step 4: Run** `--selftest` — all PASS.
- [ ] **Step 5: Commit** `git commit -m "Infighting: coups, revolutions and juntas"`

---

### Task 6: Switching to a splinter, and saves

**Files:** Modify `src/Sim/Simulation.cs` (`SwitchPlayer`), `src/Sim/Secession.cs` (offer on a human splinter's birth), `src/View/EmpireScreen.cs` (Overview unrest line; "Lead {name}" buttons for living human splinters on the Policies tab), `src/Sim/SaveGame.cs`; test in `StrifeTests.cs`.

**Interfaces:**
- Produces:
  - `public void SwitchPlayer(Civ to)`: `Humanity.Main = false; to.Main = true; Humanity = to;` copies the old civ's `Policy` (manual areas, weights, pause rules) onto `to`, closes every open alert (`Done = true`, `Outcome = "Left behind"`), refocuses `Focus` on `to`'s first colony, logs "You now lead {name}." — the old civ keeps running on the council.
  - When `Separate` creates a splinter of the `Main` civ: Raise("Decision", "Lead {name}?", …, choices "Stay with {old}" / "Lead {name}" → `SwitchPlayer`).
  - Overview line: `Unrest: {n} systems discontent · {n} on strike · {n} in insurgency · {n} in revolt{ · junta until Y}`.
  - Saves: `ColonyDto` gains `Grievance, Unrest, Cause, Movement, Autonomy, InvestedUntil`, `WorldGrievance`/`WorldUnrest` keyed by body name; `CivDto` gains `JuntaUntil`; old saves default to zero.

- [ ] **Step 1: Write the failing tests:**

```csharp
("switch_player_clean", () => {
    var sim = SelfTest.NewSim(); sim.Warp(400); var h = sim.Humanity; sim.SetManual(h, Area.Research, true);
    var s = sim.Separate(h, h.Colonies.Where(c => !c.Home).Take(2).ToList(), h.Drive, peaceful: true);
    sim.Raise("Decision", "test", "test", h.Colonies[0], "k", new() { ("ok", () => { }) });
    sim.SwitchPlayer(s);
    SelfTest.Check(sim.Humanity == s && s.Main && !h.Main && s.Policy.IsManual(Area.Research), "the player leads the splinter, with their policies");
    SelfTest.Check(sim.Alerts.All(a => a.Done), "the old civ's decisions are closed");
    sim.Warp(2);
    SelfTest.Check(sim.Civs.Count(c => c.Main) == 1, "one player");
}),
("strife_saved", () => {
    var sim = SelfTest.NewSim(); sim.Warp(150); var h = sim.Humanity; var home = h.Colonies.First(c => c.Home);
    var b = home.Sys.Find("Mars"); home.WorldGrievance[b] = 42; h.JuntaUntil = 2300;
    var c = h.Colonies.FirstOrDefault(x => !x.Home) ?? home; c.Grievance = 33; c.Movement = "m"; c.Autonomy = true;
    SaveGame.Save(sim, 20610, "user://selftest_strife.json");
    var (s2, _) = SaveGame.Load("user://selftest_strife.json");
    var h2 = s2.Humanity; var home2 = h2.Colonies.First(x => x.Home); var c2 = h2.Colonies.First(x => x.Star.Id == c.Star.Id);
    SelfTest.Check(home2.WorldGrievance[home2.Sys.Find("Mars")] == 42 && h2.JuntaUntil == 2300 && c2.Grievance == 33 && c2.Movement == "m" && c2.Autonomy, "strife round-trips");
}),
```

- [ ] **Step 2: Run** — expected FAIL.
- [ ] **Step 3: Implement** as above.
- [ ] **Step 4: Run** `--selftest`, `--warp=300 --savetest` and `--warp=700 --savetest` — all PASS, `SAVETEST equal=True` twice.
- [ ] **Step 5: Commit** `git commit -m "Infighting: switching to a splinter, the unrest overview, saves"`

---

### Task 7: Acceptance runs and tuning

**Files:** Modify `src/Main.cs` (trace summary), `data/war.json` (tuning).

**Interfaces:**
- Produces: at the end of a `--trace` warp, one line
  `STRIFESTATS movements=<n> revolts=<n> peaceful=<n> civilwars=<n> coups=<n> revolutions=<n> uprisings=<n> firstSolUprising=<year or none> humanity=<number of living human civs> fractured=<yes|no> chronicle=<lines per century>` (count events with counters on `Simulation`, not by parsing the log).

- [ ] **Step 1: Add** `STRIFESTATS` (the existing `WARSTATS` line stays).
- [ ] **Step 2: Run** `"$(cat .godotpath)" --headless --path . -- --warp=940 --trace --seed=<s>` for seeds 20610, 7, 4242, 1, 99, 2026 and record both lines per seed. Acceptance (spec §7): across the six, at least one seed with `fractured=yes` (≥ 2 living human civs at the end) and at least one with `fractured=no`; `firstSolUprising` before 2300 in at least one seed; every seed has movements and at least one revolt somewhere; `WARSTATS` still meets stage 2's bar (hundreds of battles, `nobattle` under a quarter of ended wars, a mix of endings); chronicle at most ~300 lines a century; real time under ~40 s per seed.
- [ ] **Step 3: Tune** `data/war.json` `strife` values until Step 2 holds — without adding anything that steers toward or away from fracture. Record each change and its effect in the ledger. If a code defect blocks acceptance, fix it with a failing test first and ledger it.
- [ ] **Step 4: Run** `--selftest`, `--warp=300 --savetest`, `--warp=700 --savetest` — all PASS.
- [ ] **Step 5: Commit** `git commit -m "Infighting: trace strife statistics and tuning"`

---

## Not in this stage

Special forces and their "incite a revolt" mission feeding grievance (stage 4); the Interior screen with every
system's grievance, causes and the four answers, map overlays for unrest (stage 5); battle command (stage 6).
Orbital bombardment (spec 4.5, deferred from stage 2) goes with stage 5's battle graphics or a later pass.
