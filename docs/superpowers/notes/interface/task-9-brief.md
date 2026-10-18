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
