# Interface (Stage 5) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Everything stages 1–4 simulate becomes visible and steerable: Wars, Military, Economy and Interior tabs in
the empire screen; fleets, armies, fronts, occupation, blockades and battles on the galaxy map; fleets, wars and
movements in the outliner; and battles in the system view drawn with the real Blender hulls (`Ships.Mesh`), with
colour-coded cubes only for things that have no model.

**Architecture:** The screens only read and call the simulation; every number they show that the sim does not already
hold comes from a small, tested read model added to the sim first (Task 1–2: battle records, fronts, terms on offer,
proposals, grievance causes, yearly war-economy figures). The four new tabs are `partial class EmpireScreen` files,
one per tab, like `Simulation`'s partial files. Fleets at the viewed star (at peace or in battle) are drawn by a new
`partial class Works` file (`WorksFleets.cs`) next to `WorksWar.cs`, one `MultiMesh` per (hull class, style) using
`Ships.Mesh(cls, style)`, so every hull the project has modelled is used; the galaxy map layers go into
`GalaxyOverlay` beside its existing markers and lines.

**Tech Stack:** Godot 4.7.2 .NET, C# (.NET 8). Godot binary path in `.godotpath`.

**Spec:** `docs/superpowers/specs/2026-10-18-war-and-strife-design.md` (sections 3, 4.4, 4.8, **5**, 5 "Placeholder
assets", 7). Builds on stages 1–4 (`2026-10-18-war-foundation.md`, `2026-10-19-war-in-the-field.md`,
`2026-10-19-infighting.md`, `2026-10-20-special-forces.md`; stage 4 merged on `master` at `2b2fe50`). Stage 6
(Total War-style command) is **out of scope**: no battle orders, no pausing inside a battle.

## Global Constraints

- Branch `stage5-interface` from `master`; merge `--no-ff` when done, like stage 3 and 4.
- Same rules for everyone: the screens show `Sim.Humanity` (the civ the player is on, which `SwitchPlayer` can change); read it fresh on every refresh, never cache a `Civ`.
- Performance: per-frame view work is per fleet at the viewed star and per war, never per colony; per-colony lists rebuild at most every 0.5 s and only while their tab is open. Galaxy-scale frame rate unchanged (`tools/shot.sh` prints fps; compare with `master` at the same flags). `--warp=940 --trace` stays under ~40 s.
- Saves: no new saved fields. Battle records and yearly economy figures are transient (rebuilt as the sim runs); `--warp=300 --savetest` and `--warp=700 --savetest` still print `SAVETEST equal=True`.
- Tunable numbers go in `data/war.json` under a new `interface` section (values in Task 1); placeholder colours stay in `placeholders`.
- Placeholders (spec §5, owner's note of 18 Oct: "use the ships we designed"): every fleet hull is drawn with `Ships.Mesh(cls, style)` — its own model, or its stand-in model through `hulls.fallbackUntilModelled`. Army transports use the `Transport` hull (`assets/ships/hum/Transport.glb`). A grey `hull` cube is drawn only when no `.glb` exists at all for that class (fresh clone; `.glb` is gitignored). Factories, slips, barracks, ground armies and special forces stay cubes (`WorksWar.cs`).
- UI look: `Ui.Panel/Label/Button/Box`, `Ui.Accent/Warm/Danger/Dim`; section headings in caps at size 14 like `EmpireScreen`. Copy: plain sentences, British spelling, no jargon.
- Hotkeys (free keys checked in `Main.cs` and `CameraRig.cs`): F6 Wars, F7 Military, F8 Economy, F10 Interior. Test flag `--open=` gains `wars|military|economy|interior`.
- Build: `dotnet build -v q` prints `Build succeeded.` Tests: `"$(cat .godotpath)" --headless --path . -- --selftest[=filter]` ends `SELFTEST DONE pass=<n> fail=0`, exit 0. Screens are checked by screenshot (`tools/shot.sh`) and the render-qa subagent, not by self-tests.

## Review Focus

1. **The other side of a war dies, splinters or is absorbed while the Wars tab is open** (index still in `w.A`/`w.B`, `Civs[i].Dead`): rows show "(gone)", buttons are disabled, nothing throws. (Test: `terms_dead_enemy`, Task 1.)
2. **The player's civ is an ally, not the lead, of its side**: it sees the war but cannot propose; `ProposeTerms`/`ProposeCeasefire` return false with a reason. (Test: `ally_cannot_propose`, Task 1.)
3. **Proposal spam and closed wars**: a refused proposal sets a cooldown (`interface.proposeCooldownYears`); proposing on a closed war returns false. (Test: `refusal_cooldown`, Task 1.)
4. **Late-game numbers** (squadrons of millions of ships, empires of a thousand systems): the system view draws a capped count per squadron and in total; labels show the true count; the Interior tab pages instead of building a thousand rows. (Test: `draw_cap`, Task 2; Interior paging, Task 6.)
5. **Arriving at a star after its battle** (or camera elsewhere when it happens): the battle shows only inside its window (`interface.battleShowYears`), the map marker fades by age, the record list stays bounded. (Test: `battle_records_bounded`, Task 1.)

---

## File Structure

| File | Responsibility |
|---|---|
| `data/war.json` (modify) | New `interface` section |
| `src/Sim/Battle.cs` (modify) | `BattleRecord`; `Simulation.Battles` ring, `BattlesAt`, `BestFamily` |
| `src/Sim/Field.cs` (modify) | `Fight` pushes a `BattleRecord`; `Fronts(War)` |
| `src/Sim/Wars.cs` (modify) | `TermsOnOffer`, `ProposeTerms`, `ProposeCeasefire`, `CanPropose`, `Side`, `Lead` |
| `src/Sim/Strife.cs` (modify) | `GrievanceCauses(Colony)` (the `Add` loop collects every cause) |
| `src/Sim/WarEconomy.cs`, `Civ.cs` (modify) | Yearly transient figures on `Civ`; `Shortages(Civ)` |
| `src/Tests/InterfaceTests.cs` (create), `SelfTest.cs` (modify) | Read-model tests |
| `src/View/EmpireScreen.cs` (modify) | Tab constants, `Refresh` switch, `RowGrid`/`Bar` helpers |
| `src/View/EmpireScreen.Wars.cs` (create) | Wars tab |
| `src/View/EmpireScreen.Military.cs` (create) | Military tab |
| `src/View/EmpireScreen.Economy.cs` (create) | Economy tab |
| `src/View/EmpireScreen.Interior.cs` (create) | Interior tab |
| `src/View/Outliner.cs` (modify) | Fleets and Movements sections; Wars rows open the Wars tab |
| `src/View/GalaxyOverlay.cs`, `shaders/marker.gdshader` (modify) | Fleet/army markers, movement lines, fronts, occupation, blockades, battles; new marker styles |
| `src/View/Ships.cs` (modify) | `HasModel(cls, style)`, `Named(string)` |
| `src/View/WorksFleets.cs` (create) | Fleets at the viewed star with real hulls; battle formations and effects |
| `src/View/WorksWar.cs` (modify) | Parked-squadron and carried-army cubes removed (now real hulls); legend unchanged |
| `src/View/Works.cs`, `src/Main.cs`, `src/View/Hud.cs` (modify) | Build/update hooks; `GoToBattle`; hotkeys; `--open=`, `--stagebattle`, `--focus=battle` |

---

### Task 1: War read models (battle records, fronts, terms, proposals)

**Files:**
- Modify: `data/war.json`, `src/Sim/Battle.cs`, `src/Sim/Field.cs`, `src/Sim/Wars.cs`
- Create: `src/Tests/InterfaceTests.cs`; Modify: `src/Tests/SelfTest.cs` (add `InterfaceTests.All` to the `Concat` chain)

**Interfaces:**
- Produces:
  - `public record BattleRecord(int Star, double Year, int WarId, int CivA, int CivB, bool AWins, double LostA, double LostB, string FamA, string FamB);`
  - `Simulation.Battles` : `List<BattleRecord>`, newest last, capped at `interface.battleRecordsMax`; `IEnumerable<BattleRecord> BattlesAt(int star, double since)`
  - `public static string Battles.BestFamily(Civ c)` — the key of `c.M.Arms` with the highest tier (ties: first in `WarData` weapon-family order), `"kinetic"` when empty
  - `public List<(int From, int To)> Fronts(War w)` — star ids
  - `public static int Side(War w, Civ c)` → `0` side A, `1` side B, `-1` not in it; `public Civ Lead(War w, int side)`
  - `public (bool ViewerWins, List<Term> Terms) TermsOnOffer(War w, Civ viewer)`
  - `public bool CanPropose(War w, Civ by, out string why)`; `public bool ProposeTerms(War w, Civ by, List<Term> terms, out string why)`; `public bool ProposeCeasefire(War w, Civ by, out string why)`
  - `Civ.ProposeAfter` : `Dictionary<int,double>` (war id → year), transient

- [ ] **Step 1: Add `interface` to `data/war.json`**

```json
"interface": {
  "battleRecordsMax": 256, "battleShowYears": 0.08, "battleMapYears": 3.0,
  "proposeCooldownYears": 2.0, "ceasefireAcceptExhaustion": 40,
  "squadronDrawMax": 24, "fleetDrawMax": 600, "interiorPage": 60
}
```

- [ ] **Step 2: Write the failing tests** in `InterfaceTests.cs` (same shape as `FieldTests`):

```csharp
("battle_records_bounded", () => {   // Fight twice at one star, then flood
    var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
    var w = sim.DeclareWar(h, o); int star = h.Colonies[0].Star.Id;
    sim.TestFight(w, star);  sim.TestFight(w, star);
    SelfTest.Check(sim.BattlesAt(star, sim.Year - 1).Count() == 2, "both battles recorded at the star");
    for (int i = 0; i < 400; i++) sim.TestFight(w, star);
    SelfTest.Check(sim.Battles.Count == (int)WarData.D("interface", "battleRecordsMax"), "ring stays bounded");
    SelfTest.Check(!sim.BattlesAt(star, sim.Year + 1).Any(), "nothing after now");
}),
("fronts_join_the_sides", () => {
    var sim = SelfTest.NewSim(); sim.Warp(2); var h = sim.Humanity; var o = sim.Civs.First(c => c.Alien);
    var w = sim.DeclareWar(h, o); var f = sim.Fronts(w);
    bool owns(int side, int star) => sim.ColonyAt(star) is { } c && (side == 0 ? w.A : w.B).Contains(c.Civ.Id);
    int sideA = 0, sideB = 1;
    SelfTest.Check(f.Count > 0 && f.All(p => owns(sideA, p.From) && owns(sideB, p.To) || owns(sideB, p.From) && owns(sideA, p.To)), "fronts run between the sides");
}),
("terms_on_offer_follow_score", () => {
    // w.Score = +60, w.ExhB = 70 → TermsOnOffer(w, h).ViewerWins == true and Terms non-empty, all in the reason's Terms
    // w.Score = -60, w.ExhA = 70 → ViewerWins == false
}),
("propose_accept_and_refuse", () => {
    // winning (Score +60, ExhB 70): ProposeTerms(w, h, TermsOnOffer(w,h).Terms) == true and !w.Open
    // new war, Score 0, ExhB 0: ProposeTerms(w, h, all reason terms) == false, why non-empty
}),
("refusal_cooldown", () => {
    // after a refusal, a second ProposeTerms at the same Year returns false with why containing "years";
    // after sim.Warp(proposeCooldownYears + 0.1) CanPropose is true again; EndWarWith then ProposeCeasefire == false
}),
("ceasefire_needs_tired_enemy", () => {
    // ExhB = 10: ProposeCeasefire false; ExhB = 45 (≥ ceasefireAcceptExhaustion) and reason tier allows ceasefire: true, w.Ceasefire
}),
("ally_cannot_propose", () => {
    // a third civ x joins side A (w.A.Add(x.Id)); CanPropose(w, x, out why) == false
}),
("terms_dead_enemy", () => {
    // o.Dead = true; TermsOnOffer(w, h) returns (true, empty) without throwing; CanPropose == false
}),
```

`sim.TestFight(War w, int star)` is an `internal` test hook in `Field.cs` that builds both `BattleSide`s from each lead's
fleets (moving the first fleet of each to `star`) and calls `Fight`.

- [ ] **Step 3: Run** `"$(cat .godotpath)" --headless --path . -- --selftest=propose` — Expected: build error / FAIL (members missing).

- [ ] **Step 4: Implement**
  - `Fight` appends the `BattleRecord` (families from `Battles.BestFamily` of each side's civ) and trims the front of the list past the cap.
  - `Fronts`: for each pair (lead of side s, lead of the other side), take `Frontier(lead, enemies)` (staging) and the enemy colony nearest the lead's capital (target), one pair per lead; then add `(holder's nearest free colony, occupied star)` for every colony occupied in this war. Distinct pairs. No per-pair search across all colonies (keep `Frontier`'s two linear passes).
  - `TermsOnOffer`: the `PeaceCheck` decision without its time gates (`minWarYears`, `settleExhaustion`) and without side effects: viewer's side winning → `WarModel.ChooseTerms(reason, Concede(enemyLead, …), both)`; losing → what the enemy would demand from the viewer the same way; stalemate → `ceasefire` or `status_quo` per the reason's stakes tier; independence → `IndependenceTerms`. Dead or colony-less enemy lead → `(true, empty)`.
  - `CanPropose`: false when war closed, `by` not a lead (`Lead(w, Side(w, by)) != by`), other lead dead, or `Year < by.ProposeAfter[w.Id]` (why: `"They will not hear another offer for N years."`).
  - `ProposeTerms`: accepted when the summed `Price` of the asked terms ≤ `Concede(other, reason, otherScore, otherExh)`, or when `by` is losing and offers its own `TermsOnOffer` terms back; accepted → `EndWarWith(w, byIsA == byWins, terms)`; refused → set cooldown, `why = "<Name> refuses."`.
  - `ProposeCeasefire`: accepted when the reason's stakes tier allows a ceasefire and the other lead's exhaustion ≥ `ceasefireAcceptExhaustion` or `|Score| < stalemateScore`; accepted → `EndWarWith(w, w.Score >= 0, [ceasefire])`.

- [ ] **Step 5: Run** `--selftest` (all) — Expected: `SELFTEST DONE pass=<old+8> fail=0`. Then `--warp=300 --savetest` → `SAVETEST equal=True`.

- [ ] **Step 6: Commit** `git commit -m "Interface: war read models (battle records, fronts, terms, proposals)"`

### Task 2: Economy and interior read models; ship model lookup

**Files:**
- Modify: `src/Sim/Civ.cs`, `src/Sim/WarEconomy.cs`, `src/Sim/Strife.cs`, `src/View/Ships.cs`, `src/Tests/InterfaceTests.cs`

**Interfaces:**
- Produces:
  - Transient `Civ` fields set by `YardStep` each year: `double MadeKgYr, UpkeepKgYr, LaidUpYr; int SlipsWaiting, SlipsSabotaged;`
  - `public List<string> Shortages(Civ c)` — plain sentences: slips waiting for materiel, ships laid up, each strategic resource a work in progress `Needs` but the civ lacks (`"No Neutronium: the Kugelblitz goes at a quarter pace."`), manpower below the barracks' next intake
  - `public List<(string Key, string Phrase, double Points)> GrievanceCauses(Colony c)` — every positive cause, biggest first, plus a `("relief", "...", -relief)` entry; `GrievanceTarget` is unchanged in value (refactor: both call one private method that fills an optional list)
  - `public static int DrawCount(double ships, int left)` in `Ships.cs` → `min(ceil(ships), squadronDrawMax, left)`, ≥ 1 when ships > 0
  - `public static bool Ships.HasModel(int cls, int style)` — any `.glb` resolves for it (own, named, or stand-in); `public static ArrayMesh Ships.Named(string name)` — `TrueScale(name)` cached, null if no file

- [ ] **Step 1: Write the failing tests**

```csharp
("grievance_causes_sum", () => {   // a colony far from the capital with levy 1: causes sum (minus relief) times GovFactor == GrievanceTarget, clamped
    SelfTest.Check(Math.Abs(Math.Clamp(sum * sim.GovFactor(c.Civ), 0, 100) - target) < 1e-6, "the breakdown adds up");
    SelfTest.Check(causes[0].Points >= causes[^1].Points, "biggest first");
}),
("shortage_when_materiel_runs_out", () => {   // h.Materiel = 0, MilShare tiny, slips present → after Warp(1.1): SlipsWaiting > 0 and Shortages(h) has a line containing "materiel"
}),
("economy_figures_yearly", () => {   // after Warp(1.1) with MilShare 0.3: h.MadeKgYr > 0 and h.UpkeepKgYr >= 0
}),
("draw_cap", () => {
    SelfTest.Check(Ships.DrawCount(3e6, 1000) == (int)WarData.D("interface", "squadronDrawMax"), "a million-ship squadron is drawn capped");
    SelfTest.Check(Ships.DrawCount(0.4, 10) == 1 && Ships.DrawCount(5, 2) == 2 && Ships.DrawCount(0, 10) == 0, "partial, budget-limited, empty");
}),
```

- [ ] **Step 2: Run** `--selftest=draw_cap` — Expected: FAIL / build error.
- [ ] **Step 3: Implement** the members above (`YardStep` already computes `made`, `due`, the deficit and the step limit; record them, scaled to per year by `dy`).
- [ ] **Step 4: Run** `--selftest` — Expected: `fail=0`. `--warp=700 --savetest` → `SAVETEST equal=True`.
- [ ] **Step 5: Commit** `"Interface: economy and interior read models, ship model lookup"`

### Task 3: Empire screen tabs scaffold and the Wars tab

**Files:**
- Modify: `src/View/EmpireScreen.cs`, `src/Main.cs` (hotkeys, `--open=`), `src/View/Hud.cs` (nothing new if `ToggleEmpire` suffices)
- Create: `src/View/EmpireScreen.Wars.cs`

**Interfaces:**
- Consumes: Task 1 (`TermsOnOffer`, `ProposeTerms`, `ProposeCeasefire`, `CanPropose`, `Fronts`, `Side`, `Lead`).
- Produces: `EmpireScreen.TabWars = 4, TabMilitary = 5, TabEconomy = 6, TabInterior = 7`; `Scroll(name)` reused for each; helpers in `EmpireScreen.cs`: `static Control Bar(float v01, Color col, float width = 160)` and `static GridContainer RowGrid(int columns)`; `public void ShowWar(int warId)` (opens TabWars scrolled to it).

- [ ] **Step 1: Scaffold** the four tabs (`Scroll("Wars")`, `"Military"`, `"Economy"`, `"Interior"`), `Refresh` and `_Process` cases (Wars and Military refresh every 0.5 s while open; Economy every 1 s; Interior rebuilt on open and on its own "Refresh" button), hotkeys in `Main._UnhandledInput` (F6 → TabWars, F7 → TabMilitary, F8 → TabEconomy, F10 → TabInterior, via `_hud.ToggleEmpire`), and `--open=wars|military|economy|interior` in the `--open=` array.
- [ ] **Step 2: Wars tab** — one card per open war the player is in (then "PAST WARS", last 5 closed, ending only):
  - Header: `Narrative2.Called(enemy lead, true)` + allies, reason name (`WarModel.ReasonById(w.Reason).Name`) and stakes tier, years at war.
  - Score: a centred bar −100..100 from the player's side (`Side`), green right / red left, with the number.
  - Exhaustion: two bars (ours, theirs), 0..100, `Ui.Warm`.
  - Battles: won/lost, ships lost each side (`LostKgA/B` → hulls via the records), last battle place and year (`Sim.Battles` last for this war) — click flies to it (`Main.GoToBattle`, Task 7; until then `TravelTo`).
  - Fronts: each `Fronts(w)` pair as "Staging → Target" buttons (`TravelTo(target)`); occupied systems listed with holder colour.
  - Claims: the attacker's claimed systems, each marked held/lost.
  - Terms on offer: `TermsOnOffer` — "They would give: …" or "They demand: …".
  - Buttons: **Propose these terms** (`ProposeTerms` with the on-offer list), **Ask for…** (checkbox per term of the reason, then Propose), **Propose a ceasefire**; disabled with `CanPropose`'s `why` as tooltip; result shown with `Hud.Toast`. When the war is closed, the card turns into its ending line.
- [ ] **Step 3: Verify**: `dotnet build -v q` → `Build succeeded.`; `tools/shot.sh wars_tab --warp=500 --open=wars` (pick a `--warp` from a `--trace` run where humanity is at war; if none, add `--stagebattle`, Task 7, and re-shoot then). Send `shots/wars_tab.png` to render-qa: text legible, no overflow past the 960×660 panel, bars drawn.
- [ ] **Step 4: Commit** `"Interface: Wars tab"`

### Task 4: Military tab

**Files:** Create `src/View/EmpireScreen.Military.cs`.

**Interfaces:** Consumes `FleetsOf`, `ArmiesOf`, `TeamsOf`, `Civ.Yard`, `Civ.Barracks`, `Civ.Academy`, `Forces.Level`, `Simulation.Order`, `Policy.IsManual(Area.Fleets)`, `Ships.ClassNames`.

- [ ] **Step 1: Fleets** — one foldable row per fleet: name, admiral (name, skill, trait, wins/losses), ships total, morale and supply bars, stance, mission and target star (or "in transit to X, arrives in N y"); unfolded: a squadron grid (hull class, ships `#,0`, experience `Forces.Level(xp)`). Click name → `TravelTo(f.At)`. When `Area.Fleets` is manual: a mission `OptionButton` (`defend/attack/raid/blockade/escort`) and a target `OptionButton` (own free systems + claims + front targets from Task 1) calling `Sim.Order(f, star, mission)`; when automatic the two show the council's choice, disabled, tooltip "The council runs Fleets. Take it in Policies."
- [ ] **Step 2: Armies** — grid: where (star or "aboard <fleet>"), troops `#,0`, quality, experience level, rushed. **Special forces** — teams: name, mission, target, quality, rushed, status (travelling/on site, due year). **Queues** — yard slips (colony, hull class, progress bar, "waiting for materiel" / "sabotaged" from Task 2 flags), barracks courses (troops, years left, rushed), academy courses.
- [ ] **Step 3: Verify** `tools/shot.sh military_tab --warp=500 --open=military` → render-qa (legible, folds work, nothing clipped). Build succeeds.
- [ ] **Step 4: Commit** `"Interface: Military tab"`

### Task 5: Economy tab

**Files:** Create `src/View/EmpireScreen.Economy.cs`.

**Interfaces:** Consumes Task 2 (`MadeKgYr`, `UpkeepKgYr`, `LaidUpYr`, `SlipsWaiting`, `SlipsSabotaged`, `Shortages`), `Simulation.Level(civ.Mobilisation)`, `Civ.MilShare`, `Efficiency`, `Materiel`, `Manpower`, `Slips(civ)`.

- [ ] **Step 1: Build** sections: **MOBILISATION** (level name and its description from `war.json`, a "Change in Policies" button → `Open(TabPolicies)`); **CIVILIAN AND MILITARY** (one split bar: civilian `1−MilShare` in `Ui.Accent`, military in `Ui.Danger`, efficiency %); **MATERIEL** (stock, made per year, upkeep per year, net, all via `Hud.Sci`); **SHORTAGES** (`Shortages(civ)` lines in `Ui.Warm`, or "No shortages." in `Ui.Dim`); **SLIPS** (count, building, waiting, sabotaged; one compact row per slip grouped by colony, max 40 rows then "and N more").
- [ ] **Step 2: Verify** `tools/shot.sh economy_tab --warp=500 --open=economy` → render-qa. Build succeeds.
- [ ] **Step 3: Commit** `"Interface: Economy tab"`

### Task 6: Interior tab

**Files:** Create `src/View/EmpireScreen.Interior.cs`.

**Interfaces:** Consumes `GrievanceCauses` (Task 2), `Colony.Grievance/Unrest/Movement/Autonomy/InvestedUntil`, `Simulation.UnrestNames`, `Simulation.Respond(c, Answer)`, `Simulation.CouncilAnswer(c)`, `Colony.WorldGrievance/WorldUnrest`, `HomeWorlds.MovementName(Body)`.

- [ ] **Step 1: Build** a sortable table (sort buttons: grievance, name, stage), paged by `interface.interiorPage` (Prev/Next): system, grievance bar coloured by stage, stage name, top cause phrase; hover tooltip lists `GrievanceCauses` with points; flags (self-rule, invested until Y). Home system first, its worlds indented below it (world grievance and stage). Rows with a `Movement` show the movement's name and four buttons **Grant self-rule / Invest / Crack down / Wait**, each with its cost as tooltip (from `strife.answers`), the council's pick (`CouncilAnswer`) marked; a click calls `Respond(c, a)` and closes any open `"interior:<star>"` alert (same path the inbox uses). Header line: systems at each stage (counts).
- [ ] **Step 2: Verify** `tools/shot.sh interior_tab --warp=700 --open=interior` → render-qa. Build succeeds. Check frame time stays flat with the tab open at `--warp=900` (fps line from `shot.sh`).
- [ ] **Step 3: Commit** `"Interface: Interior tab"`

### Task 7: Fleets and battles in the system view with real hulls

**Files:**
- Create: `src/View/WorksFleets.cs` (`partial class Works`)
- Modify: `src/View/Works.cs` (call `BuildFleets()` beside `BuildWarProps()`, `UpdateFleets(c, year, vdt)` beside `UpdateWarProps`), `src/View/WorksWar.cs` (drop the parked-squadron and carried-army cubes), `src/Main.cs` (`GoToBattle`, `--stagebattle`, `--focus=battle`)

**Interfaces:**
- Consumes: Task 1 (`BattlesAt`, `BattleRecord`, `BestFamily`), Task 2 (`DrawCount`, `HasModel`, `Named`), `Ships.Mesh(cls, style)`, `Ships.Style(civ)`, `Ships.Size(cls)`, `Works.Drawn`, `Works.PosOfD`, `Spectacle.Additive`.
- Produces: `public bool Works.BattleHere(out D3 centre, out float radius)` (true position, for the camera); `public void Main.GoToBattle(int star)` (travel, then `_rig.FlyTo(() => centre, radius * 3, …)` as `FlyToPendingMega` does for works).

- [ ] **Step 1: Fleet drawing** — a pool of `MultiMeshInstance3D`, one per (hull class, style) actually present, mesh `Ships.Mesh(cls, style)` (or a grey `hull` cube from `WorksWar`'s shader when `!HasModel`); transports carrying armies drawn with `Ships.Named("Transport")`, one per 50 000 troops (`DrawCount`). Each civ's fleets at the star sit in their own formation: a wedge per fleet, squadrons in ranks by class (biggest at the back), spacing 3× hull length, at the civ's station 2.5 seat radii out (angle from civ id). Size `max(true size, camera distance × 0.004)` so they read at any range (the beam-width rule in `Spectacle.Battle`). Budget `fleetDrawMax` instances per frame across all civs, `DrawCount` per squadron.
- [ ] **Step 2: Battle** — when fleets of two civs at war share the star, or a `BattleRecord` here is younger than `battleShowYears`: the two formations face each other across a gap of 40× the biggest hull length, centred between them; effects by each side's `FamA/FamB`: `energy` beams (additive quads, side colour), `kinetic` tracer streaks, `missile` sprites flying between formations with point-defence flashes, `strike craft` swarms of dots, `exotic` violet arcs; explosions (additive sprite bursts) on random ships of the losing side, count ∝ `LostA/LostB` share; ships of the losing side fade out over the window. Effects are pooled (≤ 64 beams, ≤ 32 bursts), positioned through `Drawn` like `WorksWar`, all motion by `vdt` (0 when paused).
- [ ] **Step 3: Test flags** — `--stagebattle`: at frame 5, declare war between humanity and the nearest alien that has a fleet (spawn one of its best hull if none), put Humanity's Home Fleet and that fleet at the view star, set Fleets manual for both so they stay, run one `TestFight`, then pause. `--focus=battle` flies the camera to `BattleHere`'s centre.
- [ ] **Step 4: Verify** with screenshots, each to render-qa (ships are real hulls, not cubes; both formations visible; effects visible; no z-fighting or giant hulls):
  - `tools/shot.sh battle_near --stagebattle --focus=battle`
  - `tools/shot.sh battle_far --stagebattle --focus=Earth --dist=40`
  - `tools/shot.sh fleets_peace --warp=300 --focus=Earth --dist=12` (parked fleets in formation, real hulls)
  - `tools/shot.sh battle_alien --stagebattle --focus=battle --yaw=140` (alien style hulls on the far side)
  Check the fps line against `master` at `--focus=Earth --dist=12`.
- [ ] **Step 5: Commit** `"Interface: fleets and battles in the system view with real hulls"`

### Task 8: Galaxy map layers

**Files:** Modify `src/View/GalaxyOverlay.cs`, `shaders/marker.gdshader`.

**Interfaces:** Consumes `Sim.Fleets`, `FleetPos(f)`, `Sim.Armies`, `Fronts(w)`, `Colony.OccupiedBy/BlockadedUntil`, `Sim.Battles`, `Main.StarWorld`.

- [ ] **Step 1: Marker styles** in `marker.gdshader` (documented in its header comment): `3` fleet (chevron), `4` army transport (small square), `5` occupied (ring in the occupier's colour around the owner's dot), `6` blockade (dashed ring), `7` battle (cross flare, alpha from custom `.w` = 1 − age / `battleMapYears`).
- [ ] **Step 2: Layers** in `UpdateMarkers` / `UpdateShips` (same 0.3 s cadence, same buffers; raise `MaxMarkers` if the count shows they are reached): a marker per fleet with ships at `FleetPos` in its civ colour (size by `log10(ships)`), a line from it to its `To` star while moving (bright near the fleet, fading out, like ship lines); armies aboard moving fleets as style 4 beside them; fronts of every open war as lines (player's wars `Ui.Danger` at 0.8 alpha, others at 0.25; a ceasefire's front dim and dashed by segments); occupied and blockaded systems as styles 5/6; battles younger than `battleMapYears` as style 7.
- [ ] **Step 3: Verify** `tools/shot.sh map_war --warp=600 --focus=Galaxy --dist=<value that frames the home region, as used in earlier map shots>` and `tools/shot.sh map_stage --stagebattle --focus=Galaxy --dist=<same>` → render-qa (markers legible, lines not cluttering the map, battles visible). fps line within a few % of `master` at the same flags.
- [ ] **Step 4: Commit** `"Interface: fleets, fronts, occupation, blockades and battles on the galaxy map"`

### Task 9: Outliner entries, then the whole-stage check

**Files:** Modify `src/View/Outliner.cs`.

**Interfaces:** Consumes `EmpireScreen.ShowWar`, `Main.GoToBattle`, `Hud.ToggleEmpire`.

- [ ] **Step 1: Outliner** — **Wars** rows now show score (from the player's side) and our exhaustion, click → `ShowWar(w.Id)`; a sub-row "Battle at X, N days ago" when a battle of this war is younger than `battleMapYears`, click → `GoToBattle`. New **Fleets** section (player's fleets, max 8 + "and N more"): name, ships, mission or ETA, click → `TravelTo`. New **Movements** section (colonies with a `Movement`, worst first, max 6): system, stage, click → Interior tab. Rows are pooled and never rebuilt under the pointer (existing rule).
- [ ] **Step 2: Whole-stage check**
  - `dotnet build -v q` → `Build succeeded.`
  - `--selftest` → `fail=0`
  - `--warp=300 --savetest`, `--warp=700 --savetest` → `SAVETEST equal=True`
  - `--warp=940 --trace` → under ~40 s, no exceptions
  - Shots: `outliner_war --warp=500` plus all earlier shots re-taken; render-qa pass on the set.
- [ ] **Step 3: Commit** `"Interface: outliner entries for fleets, wars and movements"`; then merge `stage5-interface` into `master` with `--no-ff` (`"Merge stage 5: interface"`) once the owner has seen the screenshots.
