# To do

## Humanity never ends a game fractured (War and Strife §7)

Found 2026-10-18 on master @ 2b2fe50. Not fixed yet.

**Cause.** Humanity always researches S8 "Hive Consensus" between 2328 and 2437, at 6–34 systems. A hive shuts off
every fracture channel: `StrifeStep` zeroes grievance (Strife.cs, `civ.M.Hive` branch), `Coup`/`Revolution` return
(Coups.cs), `Election` skips the schism (Simulation.cs). Humanity never splinters before that either. Every revolt in
STRIFESTATS is an alien civ's, because the counters cover all civs.

**Why S8 is certain.** `ascS` = {S8, ASC-S3b, ASC-S3c}. `Lean` picks one at random, so hive should be 1 in 3. But S8 is
a spine tech: `PickResearch` forces it into every hand, and `TierOk` exempts it. ASC-S3b and ASC-S3c need 3 known tier-7
S techs, which humanity never has. So S8 wins even when `Lean` picks another member.

- [ ] **Fix (no steering):** in `src/Sim/Research.cs`, make a tech that shares an exclusive group with a spine tech count
      as spine in `TierOk`, and force only the member `Lean` picked into the hand. Tried in a throwaway build: 4 of 6
      seeds fractured (20610, 7, 1, 99), 2 did not (4242, 2026, where `Lean` rolled S8). Still to check: run time
      under ~40 s and `--warp=300 --savetest`.
- [ ] The revolt ladder (`Secession.RevoltStep`) produced 0 human splinters even without the hive. Every human split
      in the test came from an election schism. Spec §4.7's colonies-against-the-core split does not break humanity yet.
- [ ] The `movements` counter is probably inflated (16k–107k per game). `Rise()` seems to fire again every time a
      system climbs back to insurgency.
- [ ] The chronicle hides secession lines: `Separate` logs at `bloc[0]` after the system has moved to the new splinter,
      which has not met humanity and is not at war with it yet, so `Log`'s relevance filter drops the line.

Full diagnosis, instrumentation diff and seed table: `fracture-diagnosis.md` in the Claude side-session notes
folder (outside the repo, may not persist; everything needed to act is above).

### Other stage 3 acceptance gaps (plan 2026-10-19-infighting.md Task 7, spec §7)

Measured on the baseline runs of 2026-10-18 (master @ 2b2fe50, six seeds, `--warp=940 --trace`).

- [ ] **No revolt in two seeds.** "Every seed has movements and at least one revolt somewhere" fails: 20610 and 7 have
      `revolts=0`.
- [ ] **Runaway empires that never fracture.** "No runaway empires without fracture" fails: in 20610 the Hegemony ends
      with 999 systems and Hask with 1024, with 0 revolts; in 7 the Concord ends with 1374 with 0 revolts. Humanity
      itself reaches 868–968 systems as a hive. In seed 99 the Vessh sat at strikes for centuries (levy the top cause)
      without reaching open revolt.
- [ ] **Too few battles in two seeds.** Stage 2's bar (hundreds of battles) fails: 20610 has 5 wars and 65 battles,
      7 has 19 wars and 173 battles. Seed 20610 endings are only compromise and collapse.
- [ ] **Not yet checked:** real time under ~40 s per seed (the runs were in parallel, so not timed), `--selftest`,
      `--warp=300 --savetest`, `--warp=700 --savetest`.
- [ ] Record every tuning change and its effect in the ledger (Task 7 Step 3), then commit (Step 5).

Already met: Sol uprising before 2300 (seed 7: 2107, seed 99: 2102); chronicle at most ~300 lines a century
(91–234); `nobattle` under a quarter of ended wars in every seed.
