# Battle command (Stage 6) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development or superpowers:executing-plans. Before each View/UI/input/camera task invoke the matching godot-prompter skill (`camera-system`, `input-handling`, `godot-ui`), name the pattern chosen and the one rejected, then build.

**Goal:** Spec §4.4 optional Total War-style command. When a battle starts the player may pause, give squadron-level orders (any squadron splittable to single ships): move, focus fire, formation, screen, retreat. Uncommanded squadrons follow the admiral's doctrine. A commanded battle resolves with the same numbers as auto-resolve, shown at true scale in the system view. Per-player setting: always auto / ask each time / always command.

**Spec:** `docs/superpowers/specs/2026-10-18-war-and-strife-design.md` §4.4, §4.8, §5 ("System view: the command mode's controls"). Builds on stages 2 (`Battle.cs`, `Field.cs`) and 5 (`WorksFleets.cs`, `Hud`, `EmpireScreen`).

## Decisions (and what was rejected)

1. **One engine, two drivers.** `Battles.Resolve` becomes `Begin` / `Step` / `Finish` on a `BattleState`; `Resolve` is just the loop over them. Command mode steps the same object one round at a time. *Rejected:* a second real-time combat sim (two sets of numbers, drift, and "same numbers" could never be proven).
2. **Orders are bounded multipliers that default to exactly 1.0.** No orders = `Resolve` bit-for-bit (`Battles` has no RNG, so equality is exact). Allocation orders (focus, screen, formation exposure) redistribute the hit points a round removes, they never create or destroy damage; overkill on a small target is wasted, which is the cost of focusing. Output/loss multipliers from formation, flanking and screening are capped (`battle.command.*`, about ±15%).
3. **Doctrine is already in the numbers.** The admiral's trait multipliers (`dmg`, `loss`, `break`) plus stance are the uncommanded default; `Doctrine(unit)` only decides each unit's default order (press / hold / screen) from them and is neutral.
4. **Sim holds while the player commands.** A `CommandSession` stops `Main` advancing the sim year; the battle runs on its own clock (round = `roundSeconds`, speeds pause/1x/2x/4x). *Rejected:* letting the galaxy run during a battle (fleets arriving mid-fight, saves mid-fight).
5. **Units, not new classes.** A `CmdUnit` = a slice of one `Squadron` (fleet, hull, ships, xp, position, orders). Split/merge move ships between units of the same squadron; `Finish` writes survivors back, averaging xp by count. Cap `maxUnitsPerSide` (64) so a million-ship squadron costs the same as a ten-ship one; draw count stays capped by `fleetDrawMax`.
6. **Camera: reuse `CameraRig`** (orbit + `FlyTo` + `Adopt` + free flight already do the camera-system skill's orbit/follow patterns). Add a follow-selection target func and a log zoom clamp to battle radius. *Rejected:* a second Camera3D/SpringArm rig (fights `Frame`'s floating origin).
7. **Input: Input Map actions + `_UnhandledInput`** so the UI consumes first and keys are rebindable; project's raw `Key` switch in `Main` stays for old keys. LMB select / box-select over unit markers (screen-space, not 3D picking of 600 hulls), RMB contextual (ground = move, enemy unit = focus). No gamepad in this stage.
8. **UI: `BattleCommandPanel`** on its own `CanvasLayer` in `Hud`, built from `Ui.Panel/Label/Button` in containers (top bar, left force tree, bottom order bar, right unit card); order buttons `FocusMode.None` like the rest of the HUD so Space/keys reach input.
9. **Not saved:** a session is transient; saving is blocked mid-battle with a toast; loading re-detects the battle. Only `Policy.BattleMode` is saved (default Auto, so old saves load unchanged).

## Global Constraints

- Branch `stage6-command` from `master`; merge `--no-ff`.
- Tunables in `data/war.json` `battle.command` (round seconds, `maxUnitsPerSide`, formation table, flank/screen/focus caps, retreat exposure) — no literals in code.
- Aliens/AI civs never command; only `Sim.Humanity` (read fresh, `SwitchPlayer` can change it).
- Tests: `"$(cat .godotpath)" --headless --path . -- --selftest[=filter]` ends `SELFTEST DONE pass=<n> fail=0`. `--warp=300 --savetest` and `--warp=700 --savetest` still print `SAVETEST equal=True`. Screens by `tools/shot.sh` + render-qa.
- Performance: stepping 64 units a side with 1e6 ships each under 1 ms; per-frame view work per unit, never per ship beyond the existing draw cap.

## File Structure

| File | Responsibility |
|---|---|
| `data/war.json` (modify) | `battle.command` section |
| `src/Sim/Battle.cs` (modify) | `BattleState` (`Begin/Step/Finish`), `Resolve` = loop; exposure weights in `Hit` |
| `src/Sim/BattleCommand.cs` (create) | `CmdUnit`, `Order`, `Split/Merge`, `Doctrine`, order modifiers, `CommandSession` |
| `src/Sim/Policy.cs` (modify) | `BattleMode` enum + field |
| `src/Sim/Field.cs` (modify) | `FieldBattles` queues sessions per mode; `Fight` split into `Fight` + `BookBattle(result)` |
| `src/Tests/CommandTests.cs` (create), `SelfTest.cs` | Tests below |
| `src/View/WorksCommand.cs` (create) | Draws units from a session; focus-fire targeting; loss blasts |
| `src/View/BattleCamera.cs` (create) | Follow-selection / zoom clamp helpers over `CameraRig` |
| `src/View/BattleInput.cs` (create) | Actions, selection, box select, contextual orders |
| `src/View/BattleCommandPanel.cs` (create) | The panel |
| `src/View/EmpireScreen.cs` (modify) | Policies tab: Battles setting |
| `src/Main.cs`, `src/View/Hud.cs`, `Works.cs` (modify) | Hold sim, save block, wiring, `--stagecommand`, `--battlemode=` |

---

### Task 1: Stepper core and parity (no behaviour change)

**Files:** `src/Sim/Battle.cs`, `src/Tests/CommandTests.cs`, `src/Tests/SelfTest.cs`

- [ ] Extract `Resolve`'s body into `BattleState` (fields: sides, `relA/relB`, `hp0`, `kg0`, `ships0`, round, break flags). `Step()` runs one exchange and returns whether the battle is over; `Finish(sim)` does xp, admiral wins/losses, loser's fleets home, and returns `BattleResult`. `Resolve` = `Begin; while(!Step()); Finish`.
- [ ] Tests: `stepped_equals_resolve` — 200 generated scenarios (mixed traits, stations, supply, morale, families) compare every field of `BattleResult` and all ship counts exactly, via two cloned sims. Existing `FieldTests` battle tests unchanged and green.

### Task 2: Units, split/merge, orders

**Files:** `src/Sim/BattleCommand.cs`, `src/Sim/Battle.cs` (`Hit` takes per-unit exposure), `data/war.json`

- [ ] `CmdUnit` (Id, Fleet, Hull, Ships, Xp, Pos, `Order`). `Split(unit, ships)` (1 up to Ships−ε) and `Merge(a, b)`; `maxUnitsPerSide` enforced; ships and ship×xp conserved.
- [ ] `Order` = Hold | Move(pos) | Focus(unitId) | Screen(unitId) | Formation(kind) | Retreat. Modifiers: engaged flag (range band; a unit out of range neither fires nor can be hit), flank bonus, formation table, screen cover, focus share.
- [ ] `Hit` distributes `share` by hp-weighted exposure `w/W̄`; when every `w == 1` take the old path unchanged. Retreating units leave after `retreatRounds` at fixed exposure, write back survivors, and count toward the side's break test.
- [ ] Tests: `split_neutral` (split/merge with no orders = auto result), `doctrine_default_neutral` (explicit doctrine orders = auto result), `orders_conserve` (hp removed per round never exceeds uniform), `focus_waste` (focus on a tiny unit leaves the rest healthier but wastes fire), `screen_absorbs`, `retreat_keeps_ships`, `unit_cap`, `million_ship_perf`.

### Task 3: Settings and battle flow

**Files:** `src/Sim/Policy.cs`, `src/Sim/Field.cs`, `src/Sim/BattleCommand.cs`

- [ ] `Policy.BattleMode { Auto, Ask, Command }` (saved, default Auto; resize/default handling like `Manual`).
- [ ] `FieldBattles`: if a side's civ is `Humanity` and mode ≠ Auto, queue a `CommandSession` (Ask raises a decision `Alert`, cat "Battle", choices "Take command" / "Leave it to the admirals", default = auto on timeout-less decline); otherwise `Fight` as today. Other stars' battles in the same step resolve auto. `Fight` = `Resolve` + `BookBattle(w, star, sa, sb, result)`; a session ends in the same `BookBattle`, so war score, records, chronicle and cinematic hooks match.
- [ ] `Sim.Command` (active session or null); `Main` stops advancing the year while it is non-null.
- [ ] Tests: `mode_auto_never_commands`, `mode_ask_holds_and_resumes`, `mode_command_opens`, `ai_never_commands`, `session_result_books_war` (score, `w.Battles`, `BattleRecord`), `policy_roundtrip_old_save_defaults_auto`.

### Task 4: View — the commanded battle at true scale

**Files:** `src/View/WorksCommand.cs`, `src/View/WorksFleets.cs` (hooks only)

- [ ] Skill: none new (rendering); keep `Frame`/`TrueView` rules from `CLAUDE.md`.
- [ ] `LayOut`: when `Sim.Command` is at this star, build `_entries` per unit (unit id in `Entry.Fleet`), place each at `Pos` on the existing battle axis with the existing gap/hull-size rules; replace the stateless record-driven path for this battle only.
- [ ] `Fire`: focus-ordered units draw their shots at the target unit's hulls (`PickLive` over that unit's indices); retreating units show drives and leave; screening units sit between.
- [ ] Losses: when a unit's alive draw count drops across a round, the dropped slots become Dying (existing `Explosions`), timed across the round.
- [ ] Verify: `--stagecommand` screenshots at small and million-ship counts; render-qa.

### Task 5: Camera

**Files:** `src/View/BattleCamera.cs`, `src/Main.cs`

- [ ] Skill: invoke `godot-prompter:camera-system` first. Entry via `GoToBattle`-style `FlyTo(BattleTarget)`; `Follow(unit)` sets `Target` to the unit centroid; Tab cycles units; Home frames the whole battle; zoom clamped between a hull and ~1.2 battle radii; free flight (existing) allowed.
- [ ] Verify with screenshots at three distances; the camera never jumps when the selection changes (`Adopt`).

### Task 6: Input and selection

**Files:** `src/View/BattleInput.cs`, `project.godot` (or code-registered actions guarded by `InputMap.HasAction`)

- [ ] Skill: invoke `godot-prompter:input-handling` first. Actions: `cmd_select`, `cmd_order`, `cmd_focus`, `cmd_formation`, `cmd_screen`, `cmd_retreat`, `cmd_split`, `cmd_merge`, `cmd_group_1..9`; Space pause reuses Main's pause; Esc cancels a pending order.
- [ ] `_UnhandledInput`, active only while a session is open; mouse stays `VISIBLE`; box select over projected unit centroids; double-click selects the whole fleet; Ctrl+1..9 groups.
- [ ] Tests (headless): synthetic `InputEventAction`s produce the right `Order` on the selected units.

### Task 7: Panel and settings UI

**Files:** `src/View/BattleCommandPanel.cs`, `src/View/EmpireScreen.cs`, `src/View/Hud.cs`

- [ ] Skills: invoke `godot-prompter:godot-ui` first. Containers only (no manual positions); one `Ui` theme; section headings caps 14; plain British copy.
- [ ] Top bar: year, round n/6, pause / 1x / 2x / 4x, "Leave it to the admirals" (auto-resolves the rest from the current state), Retreat all.
- [ ] Left: force tree side → fleet → squadron/unit rows (hull, count, health bar, order chip). Bottom: Move, Focus, Formation▾, Screen, Retreat, Split, Merge. Right: unit card. Split dialog: 1 ship / half / log slider.
- [ ] Policies tab: "Battles" three-way row (Auto / Ask / Command) with a one-line description.

### Task 8: Verification and docs

- [ ] Full self-test, both savetests, `--warp=940 --trace` time unchanged, galaxy fps unchanged.
- [ ] Game-level parity: stage the same battle twice (`--stagebattle` auto vs `--stagecommand` with no orders): identical `STAGEBATTLE lostA/lostB`.
- [ ] Screens via `tools/shot.sh` + render-qa; update `TODO.md`, `docs/GAMEPLAY_QUESTIONS.md` (#23), `README.md`.

## Review Focus

1. **Parity:** any path where "no orders" differs from auto by even one ulp (`stepped_equals_resolve`).
2. **Orders creating damage or immortality:** allocation must conserve hp; a retreat must never delete ships.
3. **Late game:** millions of ships and 64 units a side — unit cap, draw cap, per-frame cost.
4. **Player becomes a non-party mid-session** (peace signed by council, civ extinct, `SwitchPlayer`): the session closes cleanly and books nothing twice.
5. **Save/load and skip paths:** save blocked mid-battle; `--warp` and fast speeds never open a session (warping resolves auto).
