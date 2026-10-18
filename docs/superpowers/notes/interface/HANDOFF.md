# Stage 5 "Interface": handoff

Branch `stage5-interface`, written on 19 October 2026 after Task 8.

- Plan: `docs/superpowers/plans/2026-10-21-interface.md`
- Spec: `docs/superpowers/specs/2026-10-18-war-and-strife-design.md`, section 5

## State

- Tasks 1–8 are done. Each was reviewed for spec compliance and code quality. The screen tasks also had a visual QA pass on screenshots.
  - The run is recorded in `ledger.md`, in this folder: every ruling, each fix round, and the deferred minors.
- Task 9 has not started: the outliner entries and the whole-stage check. Its brief is `task-9-brief.md`, in this folder.
- The final whole-branch review has not run.
- Last verified state, at `9afab62`:
  - `dotnet build` succeeded.
  - `--selftest` gave `SELFTEST DONE pass=124 fail=0`.
  - `--savetest` gave equal at both `--warp=300` and `--warp=700`.
  - `--warp=940 --trace` took about 32 s.
- The branch is not merged into `master`. Merging is the owner's call once they have seen the screenshots.

## Rules for working on this branch

The rules are in `contract.md`, in this folder: the project's global constraints, how to work, and the report format. Also follow `CLAUDE.md` in the repo root. The important ones:

- Same rules for every civ. Never cache a `Civ`: read `Sim.Humanity` fresh on every refresh.
- Per-frame work runs only per fleet and per war. Per-colony work runs at most every 0.5 s, and only while its tab is open. No LINQ allocations in per-frame paths.
- Add no new saved fields. `--savetest` must stay `equal=True`.
- Tunable numbers go in `data/war.json`, section `interface`.
- Keep each file's existing line endings. Some files are CRLF, for example `src/View/Outliner.cs`; others are LF.
- Outliner rows are pooled. Never rebuild them under the mouse pointer.

## Test flags added in this stage

| Flag | What it does |
|---|---|
| `--open=wars\|military\|economy\|interior` | Opens that empire-screen tab. Hotkeys: F6 Wars, F7 Military, F8 Economy, F10 Interior. |
| `--scroll=N` | Scrolls the open tab. |
| `--stagebattle` | Stages a limited war at Sol with a heavy human line, then pauses. Run it with `--warp=300` for an alien empire to exist. |
| `--battlerun=S` | Lets the staged battle run for S seconds. |
| `--battlefam=A,B` | Sets each side's weapon family in the staged battle. |
| `--stagelayers` | Stages occupation, blockade and transport markers for the galaxy map. Use with `--stagebattle`. |
| `--stageunrest` | Stages grievance and movements in human colonies, so Interior rows appear. Humanity becomes a hive mind mid-game on the default seed. |
| `--focus=battle` | Points the camera at the battle. |
| `--hide=fleets\|warmap` | Hides the new fleet hulls or the galaxy war layers. |

`--shotframe=400` gives a steady-state FPS reading. The default reading at frame 90 is start-up noise.

## APIs Task 9 needs

- `Hud.ShowWar(int warId)` opens the Wars tab at a war.
- `Hud.ToggleEmpire(EmpireScreen.TabInterior)` opens the Interior tab.
- `Main.GoToBattle(int star)` and `Main.TravelTo(StarInfo)` move the camera.
- `Simulation.Side(War, Civ)` returns 0, 1, or −1 when the civ is not in the war.
- `War.Score` is from side A's point of view.
- `Sim.Battles` is a list of `BattleRecord(Star, Year, WarId, …)`, newest last.
- `WarData.D("interface", "battleMapYears")` reads the battle age limit.
- `Sim.FleetsOf(civ)`, `Simulation.UnrestNames`, and colonies' `Movement` and `Unrest`.
- `Outliner` currently exposes only the events `OpenAlert` and `OpenTech`, which `Hud` wires up around line 183. Add events for opening a war and the Interior tab the same way.

## Parked after five fix rounds (Task 7; see the ledger for the reasons)

1. Ghost counts are latched per key the first time a key is seen. So fleets or squadrons that arrive in the middle of a battle can show extra explosions beyond the battle's real losses.
   - Fix: set a latch flag when the battle record changes. After the first layout, treat any key missing from the latch as having zero ghosts. The code is `src/View/WorksFleets.cs`, `ScheduleBursts` and `LayOut`.
2. Battle look polish:
   - Shock rings read as broken arcs, or as a crisp ellipse that looks like UI.
   - The glow from hits blooms over whole hulls.
   - The fleets fill less than half of the `battle_near` frame.

The global lens chromatic fringe is left as it is, deliberately. It is the owner's decision.
