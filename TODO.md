# To do

Re-measured 2026-10-28 on branch `fracture-followup` (six acceptance seeds 20610, 7, 4242, 1, 99, 2026, `--warp=940
--trace`, run one at a time in Godot; fracture rate from 24 seeds with `tools/fracrate.sh`). Before/after table:
`python tools/seedtable.py BEFORE_DIR AFTER_DIR` on the output of `tools/seeds.sh DIR`.

## War and Strife §7: fracture

- [x] **Humanity always became a hive.** Fixed twice over: the vote with holdouts (follow-up session, 47f881d), and now
      the research fix this file proposed: Hive Consensus's exclusive rivals open with the spine and only the member the
      council leans to is forced into the hand (`Simulation.SpineLike`). Humanity is a hive in half of games
      (12 of 24 sampled).
- [x] **The revolt ladder never split humanity.** It does now, colonies against the core: `SPLITSTATS` shows
      `HRevoltStep` splits in 14 of 24 seeds (holdouts in hive games, sprawl and levy in the others).
- [x] **`movements` was inflated** (16k–29k a game). A movement broken up by troops, or laid down within
      `strife.movementMemoryYears` (25), is the same movement when it climbs back. Now 2.9k–9.4k.
- [x] **The chronicle hid secession lines.** `Separate` logs for the parent (`Log(..., about: parent)`).
- [x] **Runaway empires without fracture.** New cause `sprawl` (`strife.sprawl.from` 150 → `full` 600 systems, up to
      +40 grievance, not damped by government) and overstretched hives fray even after a clean vote. Every civ that
      peaks above 400 systems now splits at least once (`EMPIRESTATS`).
- [x] **Seeds with no revolt / too few battles.** Every seed has 42–107 revolts and 1,391–9,077 battles.
- [x] **Run time.** 19–37 s per seed in Godot (seed 7 was 42.9 s). Self-tests 180/180 (Godot), `--savetest` equal at
      300, 700 and 939.
- [x] **Election schisms split humanity in nearly every non-hive game** (found this session: 24 of 24 seeds fractured).
      With the owner's agreement a schism now splits off only frontier colonies already at strikes or worse, and
      respects the split cooldown; otherwise the losing faction leaves as emigrants.

Still open:

- [ ] **Five of the six acceptance seeds fracture** (only 99 stays whole; 2 of 6 before). Across 24 seeds, 8 stay
      whole, all of them non-hive. In a hive game humanity always ends split: the vote's holdouts or the sprawl fray
      give it at least one small splinter. Fracture is still left to chance (no steering), but if "some seeds whole"
      should hold for the six, the hive side is the lever (`strife.hive.sprawlFray`, `frayPerYear`, `splitShare`).
- [x] **Self-rule answers the levy and sprawl.** A council (not Ruthless, not a junta) now answers a movement over the
      levy or the empire's size with self-rule (it was "wait", so self-rule was never granted), and a self-ruled system
      feels sprawl at `strife.relief.autonomySprawl` (0.5).
- [ ] **A Ruthless humanity smoulders.** The 20610 strikes were not sprawl: humanity there is Ruthless, cracks down
      on every movement, and each crackdown adds `crackdownSpread` (6) to every other system. From ~2800 the levy
      puts half the empire on strike, crackdowns cascade, and by 2951 343 systems are in insurgency and 72 in open
      revolt, with 4% of the levy shipped. It splits only twice (`revolt.cooldownYears` 50). Options: scale the
      spread by empire size, or let the revolt cooldown shorten with the number in open revolt. Owner's call.
- [x] **Chronicle per century was 148–230** (target 100–200). An enemy's own losses (its revolutions, its other wars)
      now share one slot a year (`interface.foreignNewsYears`), as foreign wonders do; humanity's part in them is always
      told. Now 142–198: 20610 168, 7 146, 4242 188, 1 198, 99 142, 2026 184 (simulation unchanged).
- [x] **Review leftovers.** `hand_forces_the_leaned_member` tests the hand builder (fails against the old `t.Spine`
      rule); tests that cannot run on a seed call `SelfTest.Skip`, reported as `SELFTEST SKIP` and counted in
      `SELFTEST DONE ... skip=K`.
- [ ] **Hive splinters are tiny.** Many human splits in hive games are 1–3 systems (holdouts rising alone). They count
      as fracture in `STRIFESTATS`.

## docs/AUDIT.md

- [x] Finding 6, autosave hitch: `SaveGame.Snapshot` (main thread, owns its data) + `Write` (worker), autosave via
      `SaveInBackground`, never during a cinematic; atomic write and `.bak` kept. Main-thread cost late game 19–30 ms
      (was 120–245 ms whole save). `--savetime` prints both halves.
- [x] Finding 7, history lost on reload: turning points (`Simulation.Lasting`: era, break, omen, god) are kept for the
      whole game in memory and in saves; only routine lines are capped (800 in a save, oldest routine first in memory).
- [x] Finding 8: `CameraRig.FreeFlight`; the three `Hide` statics are `Works.HiddenParts` (`--hide=closeup` and
      `mega` now work too); `TechScreen.TechName`; `Extinct` text; `regime` terms keep the loser's conscience. The
      outliner's all-pairs war distance was already gone (ebef67f). Game build: 0 warnings.

## Stage 5 deferred interface minors (docs/superpowers/notes/interface/ledger.md)

Fixed in 0bc7b98 (final review) or by this branch, except these, judged no change:

- [ ] "Bursts on both sides" (Task 7): the ledger already calls it the accepted reading of the brief.
- [ ] Hulls at the size floor read as noise from the default Earth view (Task 7): the floor is the zoomed-out stand-in
      by design; culling them would also hide distant battles.
- [ ] battle_close framing, battle_alien overlap and the grey smudge (Task 7) did not show in this branch's battle
      shots (`--stagebattle --focus=battle`); re-check with alien hull models built.
- [ ] 27 other files are CRLF in the index (only Main.cs and WorksFleets.cs were named and converted).

Measured, no action: fireball fill-rate at 1600×900, vsync off, is about 15% (80–94 fps during the blasts against
~97 paused and ~110 after).
