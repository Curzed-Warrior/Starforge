# Starforge audit, October 2026

*An audit of the game after stage 3 (infighting), with the fixes and additions on branch `claude/youthful-carson-7z08y3`.
It follows on from `docs/GAME_ANALYSIS.html` (18 October), which covered the player's stakes, first-contact pacing, the
flat late game, the Vessh snowball and the interface. Those findings still stand and are not repeated here. Everything
below is new: problems that analysis did not cover, and additions that are not in any plan or spec yet.*

## How this was tested

The cloud session had no Godot, so the first job was a way to run the simulation without it. `tools/headless` builds
`src/Sim` and `src/Tests` as a plain .NET 8 console app, with a small stand-in for the Godot types they use. The game
and the runner now share the warp, trace, stats and save-test code (`src/Sim/Runs.cs`), so nothing is duplicated.

With it:

- **All 89 existing self-tests pass** outside the engine. With the new ones there are 92.
- **Full games** of 2061 to 3000 (`--warp=939 --trace`) ran for the six acceptance seeds in the infighting plan
  (20610, 7, 4242, 1, 99, 2026), plus `--savetest` on each.
- **Determinism:** the same seed run twice gives a byte-identical trace.
- **Profiling:** the runner times the main parts of a step.

The shim's single-precision maths follows Godot's but is not guaranteed bit-identical, so one seed can play out a little
differently here than in the engine. Compare statistics across seeds, not single events.

## Results across six seeds (2061 to 3000)

| Seed | Wars | Battles | No-battle wars / ended | Revolts | Civil wars | Coups | Revolutions | First Sol uprising | Humanity fractured | Chronicle lines per century | Save round trip |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 20610 | 58 | 811 | 6/55 | 14 | 0 | 4 | 30 | none | no | 114 | ok |
| 7 | 45 | 407 | 6/42 | 4 | 0 | 3 | 14 | 2107 | no | 105 | ok |
| 4242 | 102 | 927 | 20/97 | 20 | 0 | 9 | 47 | none | no | 146 | ok |
| 1 | 158 | 1000 | 24/149 | 31 | 0 | 10 | 84 | none | no | 179 | **failed** (passes after fix 1) |
| 99 | 92 | 655 | 7/75 | 26 | 0 | 4 | 43 | 2102 | no | 128 | ok |
| 2026 | 22 | 143 | 0/21 | 2 | 0 | 1 | 6 | none | no | 105 | ok |

War in the field meets its bar: hundreds of battles per game, few wars without a battle, a real mix of endings, and a
chronicle well under 300 lines a century. **Infighting does not meet its own acceptance** in
`docs/superpowers/plans/2026-10-19-infighting.md` Task 7, which needs at least one fractured humanity across these
seeds. None fractured, and nothing anywhere ever became a civil war. Findings 3 and 4 explain why.

## Findings

Most serious first. Each says whether this branch fixes it.

### 1. Loading a game moved the Shard, the Vault and the Gates (fixed)

A save records the galaxy's star count, and loading regenerates that many stars. The count included stars kindled
during play. These come from the voice's *Kindle* power, and also from the `kindle_star` act, so AI civs kindle stars
without the player doing anything. Loading therefore generated one extra random star per kindled star. That changed the
random draws in `Galaxy.PlaceSpecials`, so the dormant Shard, the Vault and the rim Gates landed on different stars.
The stars they had really been on were never saved, because the save only records stars that were edited.

What the player saw: on seed 1, a colony at Ethzen reloaded as "the Shard b". It lost all its hubs and settlements, and
a live star went dark. The Vault moved in the regression test. **Any game in which anyone kindled a star was damaged on
its first reload, and the damage then carried on in every later save.**

Fix: regenerate only the original stars (`SaveGame.LoadFile`). Older saves load correctly with the fix too, because
their kindled stars are flagged. Regression test: `kindled_stars_keep_specials`.

### 2. One crash during an autosave could destroy the only save (fixed)

`SaveGame.Save` opened the save file for writing and streamed about 6 MB of JSON into it. The autosave does this every
five minutes over a single file. A crash, a power cut or a full disk during that write left a truncated file, and
loading a truncated file threw inside `Main._Ready`, which left a broken scene.

Fix: the save is written to a `.tmp` file beside the real one and then moved into place, and the previous save is kept
as `.bak`. Loading an unreadable save falls back to its `.bak`. If neither can be read, a new game starts with a message
instead of a broken scene. Test: `corrupt_save_falls_back`.

### 3. Humanity can never fall apart: Hive Consensus ends infighting in every game (fixed: a contested vote and holdouts)

*Fixed after the merge (src/Sim/Fracture.cs): researching Hive Consensus puts it to a vote of every system, with a
random swing for the civ. Enough refusers leave at once (in peace, by war, or as a civil war); fewer stay as holdouts,
unlinked and resentful, and while the swing was strong far systems keep falling out of the link. Holdouts can rise and
leave; a hive lets them go half the time. Across the six seeds humanity fractures in three and stays whole in three.*

In all six seeds, humanity's own council researches **S8 Hive Consensus** between 2327 and 2436:

| Seed | 20610 | 7 | 4242 | 1 | 99 | 2026 |
|---|---|---|---|---|---|---|
| Hive Consensus | 2365 | 2412 | 2436 | 2327 | 2417 | 2345 |

A hive ends elections and schisms (as designed in the tech bible), coups and revolutions (`Coups.cs`), and every
system's grievance (`StrifeStep` set it to zero). That leaves about 600 years in which humanity cannot fracture. The
spec's anti-goals name this outcome: *"Humanity that can never fall apart."* The human splinters that do appear in these
runs are earlier schisms coming back as rivals, and they are fought down. Not one human system raised a movement after
the hive formed.

The hive is not the only shield. Humanity is also about 97% digital by then, and digital minds scale grievance by
`gov.digital`. Two experiments with `strife.gov.hive` above 0:

| `gov.hive` | Humanity's mean grievance at 3000 | Highest step on the ladder |
|---|---|---|
| 0 (shipped) | 0 | calm |
| 0.3 | 8 to 14 | calm |
| 0.6 | 22 to 36 | discontent, and a few strikes in two seeds |

At either value nothing reached insurgency, and humanity fractured in no seed. Feeling grievance is not enough on its
own: nothing in the late game pushes a hive's systems hard enough to rise.

What this branch does: `strife.gov.hive` now works as a knob. Before, the hive check returned before the value was ever
read, so changing it did nothing. At 0, the shipped value, behaviour is exactly as before. The design choice is yours.
Options, smallest change first:

- **Holdouts.** When a civ becomes a hive, systems more than a lifetime away (`DistanceFactor` ≥ 1) or on the far side
  of a war front are not linked in. They keep full grievance and get a "flesh against the hive" movement. The spec
  already names "uploaded minds against flesh" as a fault line.
- **Make Hive Consensus contested.** Adopting it is a decision the Society faction fights, with a revolution check like
  the one for a pushed-too-far council. It should not be a quiet tech pick.
- **Research weights.** Lower the S path's weight for the council's autopilot, so a hive is one outcome among several.
  Today it is the outcome every time.

### 4. Civil wars cannot happen in practice (fixed: risings pull in insurgents, crackdowns fade)

*Fixed after the merge (src/Sim/Secession.cs, Movements.cs): a rising takes insurgent systems that share its cause
within ten years' travel; it is a civil war when it holds most of the restless systems and 12% of the empire; each
crackdown in the same system relieves half as much as the last; a new polity has 60 years' grace and any civ 50
years between risings.*

There were zero civil wars in all six seeds, including alien empires with 900 to 1,100 systems, half of them in
insurgency. `RevoltStep` makes a bloc only from systems *in open revolt within supply range* (3 galaxy units) of the most
aggrieved one. A civil war needs that bloc to hold 20% of the empire's systems or 25% of its people. A local cluster of
open-revolt systems never comes near that in a large empire. Small empires rarely reach open revolt at all.

There is a second pattern behind this. In large empires grievance is almost uniform: Vessh and Hask systems sit within
about 15 points of each other, and the cause in nearly every system is the levy. Ruthless councils crack down every five
years, which knocks grievance down by 30 and stops it from ever reaching open revolt. The result is permanent mass
insurgency that never comes to anything. It also means 450 to 700 systems on strike stop shipping their levy, so the
levy economy of the largest empires is quietly switched off.

Proposals:

- Let a rising pull in *insurgent* systems that share its cause, not only systems already in open revolt. Measure its
  reach by the same lane and fold travel time that `CanReach` uses, not the supply radius.
- Count civil-war size against the systems in insurgency or worse, not the whole empire.
- Make crackdowns stop working after a few repeats: each one in the same system relieves less and spreads more anger.

### 5. A full game runs slower than the spec allows (part fixed)

The spec's budget is about 40 s for a 940-year headless run, and it notes "~25 s today" from before the war stages. The
headless runner took **52.6 s** for seed 20610. The breakdown, before the fixes:

| Part of the step | Seconds |
|---|---|
| `Colony.Step` (all colonies) | 25.1 |
| of which `BuildStep` (great-works planning, every colony every step) | 15.0 |
| of which planning sites and levies (yearly) | 4.2 |
| of which `Decide` | 6.7 |
| `YardStep` (shipyards) | **14.6** |
| `StrifeStep` | 5.7 |
| everything else | about 5 |

Two causes were plain waste:

- `SlipsOf` searched the empire for its capital once per colony. That is quadratic in a year for empires with a
  thousand systems.
- `WarData.D` walked the JSON document on every one of its thousands of calls a year.

Both are fixed. The shipyard step dropped from 14.6 s to 3.5 s, and the war and strife statistics on seed 20610 are
identical before and after. A run of seed 20610 now takes **35.9 s**, inside the budget again. What remains is `BuildStep` running for every complete colony every
step. Caching the ordered `want` list per colony until its civ's `Megas`, `Sites` or emergency state changes is the
obvious next step. It touches the construction rules, so it is left for a reviewed pass.

### 6. An autosave stalls a late game for 120 to 210 ms (proposal)

A late-game save is about 6 MB of JSON and takes 120 to 210 ms on the main thread, every five minutes. Loading takes
650 to 750 ms. The design brief cares about camera glides and cinematics, and this is a visible hitch in the middle of
one. The DTO shares live lists (fleets, wars, yards and so on) with the simulation, so it cannot simply be serialised on
another thread. The fix is to build the DTO with copied lists on the main thread, which is cheap, then serialise and
write it on a worker. Skip the autosave while a cinematic is running.

### 7. History is lost on reload (part addressed)

Memory holds the last 4,000 chronicle lines, but a save keeps only the last **800**. A long game reloaded keeps about
five centuries at most, and in a busy one only a few decades. The new **Shift+F6 history export** (below) writes the whole
in-memory chronicle out. A real fix would keep milestones and era lines indefinitely in the save, and cap only the
routine lines.

### 8. Smaller items

- `Simulation.Extinct` logs `{(civ.Main ? "are" : "are")}`, a leftover branch with the same text on both sides.
- `regime` peace terms call `SetDrive`, which resets the loser's conscience to its ethic's default. Its drift history is
  lost along with its government.
- `CameraRig.Free`, `CloseUp.Hide`, `Megastructures.Hide`, `Works.Hide` and `TechScreen.Name` hide Godot members
  (warning CS0108). `Free` hiding `GodotObject.Free()` is the dangerous one: code that means to free the rig would read
  a bool instead. Renaming it to `FreeFlight` would be safer.
- Movement counts run to 24,000 to 108,000 a game. Most are the same systems climbing back to insurgency after each
  crackdown. The count is only a statistic, but it shows the loop in finding 4.
- The outliner's war rows measure every enemy system against every one of ours, twice a second. That is about a
  million distance checks a second in a late war. It is cheap enough today, but should use `Frontier` instead.
- The headless and engine builds share one folder. `Starforge.csproj` now excludes `tools/**`, because Godot's SDK
  globbed the runner's generated files into the game build.

## Added on this branch (not in any plan)

| Addition | What it does | Where |
|---|---|---|
| **Headless runner** | Self-tests, full traces, save tests, profiling and tuning without Godot | `tools/headless`, `src/Sim/Runs.cs` |
| **CI** | Self-tests and a 300-year save round trip on every push | `.github/workflows/sim.yml` |
| **Safe saves** | Atomic writes, a `.bak` of the previous save, fallback on load, no broken scene | `SaveGame.Save` / `Load` |
| **Crash guard** | If a sim step throws, time stops and `user://crash_<time>.txt` plus an emergency save are written, with a message | `Main.SimCrashed` |
| **Screenshots** | **F12** saves the frame, **Shift+F12** saves it without the interface, to `user://screenshots` (a graphics-first game had no player screenshot key) | `Main.Screenshot` |
| **History book** | **Shift+F6** writes the chronicle as Markdown, a chapter a century, with a closing table, to `user://histories` | `src/Sim/HistoryExport.cs` |
| **Mods folder** | `user://mods/data/*.json` replaces the built-in rules. An exported game packs `res://` into its .pck, where players cannot edit it | `src/Sim/DataFiles.cs` |
| **Tuning tools** | `--strifedump` (every civ's grievance, ladder and causes), `--savetime`, `--seeds=a,b,c`, and `SF_RES_OVERLAY` to try a changed `war.json` without editing the repository | `tools/headless/Program.cs` |
| **Speed** | Cached rules lookups and a linear shipyard step: 52.6 s down to 35.9 s for a full game | `WarData.D`, `WarEconomy` |

The game-side additions (F12, Shift+F6, the crash guard, and the broken-save message) compile but could not be run in this
session, because there is no Godot here. Try them once in the editor.

## Further ideas (not built)

These are things that would help the game and are not in the spec, the plans or the 18 October roadmap.

1. **Nightly balance check in CI.** Run the six acceptance seeds headless every night and fail when a spec §7 number
   leaves its range: battles per game, no-battle share, chronicle density, run time, and fracture across seeds. Tuning
   would then stop going stale between stages, which is how findings 3 and 4 went unnoticed.
2. **New-game settings.** Seed entry and display (so a game can be shared), number of alien empires, and "distance to
   the nearest empire". The last is a direct handle on the five-centuries-to-contact problem, without a rule change.
3. **Statistics screen.** `Simulation.Timeline` already samples Kardashev, population and systems every two years.
   Graph those, plus wars fought and systems lost and gained per century. A self-playing game needs a way to see what
   happened while you weren't looking.
4. **Scenario starts.** Start as a splinter (stage 3 already supports it), "hold Sol together" with a Ruthless council,
   or start already at war in 2400. Each sets up the systems from stages 1 to 3 that a normal game takes centuries to
   reach.
5. **Bug-report bundle.** The crash guard already writes the seed, the year and an emergency save. Add a menu entry that
   zips the latest save, the crash log and the Godot log, so a player can send a reproducible case. With the headless
   runner, any save can then be replayed in CI.
6. **Photo mode.** It builds on F12 and on Tab's free flight: a paused camera with field of view, depth of field,
   exposure and time-of-day controls. For a game whose selling point is its graphics, players' shots are the marketing.
7. **Accessibility.** Interface scale and text size, and a colour-blind-safe set of empire colours. Empire colours are
   currently random hues (`Color.FromHsv`), so two neighbouring empires can be indistinguishable.
8. **Milestone achievements.** `Simulation.Milestone` already fires for every turning point, and they map one to one
   onto Steam achievements when the time comes.
