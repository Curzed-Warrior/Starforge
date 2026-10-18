# SDD ledger — plan: docs/superpowers/plans/2026-10-21-interface.md

Spec: docs/superpowers/specs/2026-10-18-war-and-strife-design.md (read). Branch stage5-interface from master 182f93c; plan commit 16579b5. Baseline: build ok, SELFTEST DONE pass=112 fail=0.

## Pre-flight scan

| Pair / task | Produces vs consumes | Finding |
|---|---|---|
| T1 → T3 | TermsOnOffer, ProposeTerms, ProposeCeasefire, CanPropose, Fronts, Side, Lead | match |
| T1 → T7 | BattlesAt, BattleRecord, BestFamily, TestFight (internal; Main same assembly) | match |
| T1 → T8 | Battles list, Fronts | match |
| T2 → T4 | T2 gives per-civ SlipsWaiting/SlipsSabotaged counts; T4 wants per-slip "waiting"/"sabotaged" | gap → R2 |
| T2 → T5 | MadeKgYr, UpkeepKgYr, LaidUpYr, SlipsWaiting, SlipsSabotaged, Shortages | match |
| T2 → T6 | GrievanceCauses | match |
| T2 → T7 | DrawCount, HasModel, Named | match |
| T3 → T9 | ShowWar, tab constants | match |
| T7 → T3/T9 | GoToBattle (T3 uses TravelTo until T7, T9 uses GoToBattle) | match, T3 must be patched to GoToBattle in T7 or T9 → R5 |
| T7 → T8 | --stagebattle used by T8 shot | match (order ok) |
| T1 self | ProposeTerms winner flag wording ambiguous | R1 |
| T2 self | grievance_causes_sum test formula subtracts relief before GovFactor; code applies GovFactor then relief | R3 |
| T3–T6 self | screens verified by shots only, no self-tests (plan Global Constraints) | ok |
| T7 self | files consistent | ok |
| T8 self | `--dist=<value>` left open | R4 |
| T9 self | ok |

Ruling R1: ProposeTerms: when `by` asks terms of the other (by winning), aWins = (Side(w,by)==0); when `by` offers back its own on-offer concessions (by losing), aWins = (Side(w,by)==1) — winner is whoever receives the terms — wrong would flip peace outcomes, caught by propose_accept_and_refuse.
Ruling R2: Task 2 adds transient `[JsonIgnore] public bool Waiting` on `Slip` (set each YardStep; Slip is serialized directly in CivDto.Yard) and T4 reads `slip.Waiting` and colony `YardDownUntil > Year` for sabotaged — keeps save byte-identical; cost if wrong: a save field change breaking savetest.
Ruling R3: grievance_causes_sum asserts Clamp(sumPositive * GovFactor + reliefPoints, 0, 100) == GrievanceTarget, matching the code's order — cost if wrong: a false test failure only.
Ruling R4: Task 8 implementer chooses the galaxy --dist by trial so the home region fills the frame and records it in the report — cost: a less useful screenshot.
Ruling R5: Task 7 also switches the Wars tab's "last battle" click from TravelTo to GoToBattle — small, same feature — cost: none.

## Task 1
Task 1: implemented 86155b0 (DONE_WITH_CONCERNS; 120/120, savetest equal at 300/700)
Ruling: zero-price asks (status quo / ceasefire) use the ceasefire acceptance rule and an empty ask is refused — stricter than the brief, closes an "end any war for free" hole — cost if wrong: player can't end a war at status quo when the AI would have.
Ruling: BestFamily excludes ground and doom families, matching HullDamage's Excluded list — they are not ship-vs-ship weapons — cost if wrong: battle effects never show doom weapons.
Task 1: minor (deferred): Simulation.Battles field shadows static class Battles (needs a comment).
Task 1: minor (deferred): Fronts scans all war colonies — UI must cache it (carry into Task 3/8 briefs).
Task 1: minor (deferred): inline collapse term duplicates WarModel.Collapse.
Task 1: minor (deferred): helper name UI(key) in Wars.cs vague.
Task 1: minor (deferred): CanPropose does not check by.Dead.
Task 1: minor (deferred): BestFamily _families static cache never invalidated.
Ruling: a side offering its own surrender may accept collapse when TermsOnOffer demands exactly [collapse] of it — otherwise a fully occupied side could never surrender — cost if wrong: player can surrender to annihilation by choice (intended).
Task 1: fix round 1/5 (1 addressed, 0 open — ProposeTerms term validation; commits 86155b0..052b01a)
Task 1: minor (deferred): ProposeTerms sums caller-supplied Term prices; look terms up by id inside.
Task 1: minor (deferred): winner asking [collapse] of a Gone enemy falls to WouldStop and may be refused (PeaceCheck ends such wars yearly anyway).
Task 1: complete (commits 16579b5..052b01a, review clean)
Task 2: implemented 5e9b4ad (DONE_WITH_CONCERNS; 124/124)
Task 2: fix round 1/5 pre-review (CRLF restored to LF; b90a206)
Task 2: review ⚠️ squadronDrawMax key — confirmed present from Task 1 (war.json interface section).
Task 2: minor (deferred): Sim references View.Megastructures.Catalogue in Shortages (layering inversion).
Task 2: minor (deferred): barracks shortage line ignores army cap / materiel guards.
Task 2: minor (deferred): grievance_causes_sum "biggest first" check trivially true (compare non-relief entries).
Task 2: minor (deferred): needless $ prefix; hard-coded 0.25 quarter pace in shortage sentence.
Task 2: complete (commits 052b01a..b90a206, review clean)
Ruling R6: screen tasks (3-6, 7, 8) — implementers take the tools/shot.sh screenshots and list paths in their report; the controller dispatches render-qa on them as part of the task review (implementers may not spawn subagents) — cost: none.
Task 3: implemented ab52e88 (DONE_WITH_CONCERNS; 124/124; shots wars_tab.png, wars_tab_past.png)
Ruling: enabled Wars-tab buttons and "They would give/demand" line get their screenshot in Task 7 (--stagebattle stages a limited war) — default seed's only war is a survival war — cost: buttons unseen on screen until Task 7.
Ruling: Hud.ShowWar(int) one-line addition accepted — needed so Outliner (Task 9) can reach EmpireScreen.ShowWar.
Task 3: code review Approved (0 Critical/Important). Visual QA findings promoted to Important: score bar scale/centre tick, exhaustion bar contrast + /100, reversed duplicate fronts, disabled button contrast, tiny peace message, headers flush to tab bar.
Task 3: minor (deferred): claim with destroyed colony reads "lost" (should be "gone"); card signature lacks occupation/claims/allies; _askOpen/_ask leak for wars ended by sim; Ask "Propose" enabled with nothing ticked; allies extraction duplicated; hulls vs kg mismatch in battles line; header fields crammed in one line; claims lack separators; battles text tight to Last battle button.
Task 3: minor (deferred, outside task): HUD Earth panel / Saturn label overlap in system view (pre-existing).
Task 3: fix round 1/5 (6 addressed, 0 open — visual readability; commits ab52e88..fd8602b; scoped re-review = render-qa re-check, all FIXED)
Task 3: minor (deferred): "No peace is on offer yet." not aligned to value column.
Task 3: concern for whole-stage check: shot fps 60/43 vs 78/86 earlier — compare with master in Task 9.
Task 3: complete (commits b90a206..fd8602b, review clean)
Task 4: first implementer hit API session limit mid-task; partial uncommitted src/View/EmpireScreen.Military.cs left; re-dispatched fresh.
Task 4: implemented d698d43 (DONE_WITH_CONCERNS; build ok; shot military_tab.png). Ruling: Troops shown as million/billion (troops stored in billions) instead of #,0 — #,0 printed 0 — cost: none.
Task 4: review Needs fixes — Important: rebuild can free an open OptionButton popup; army troop signature always 0 (billions cast to int). Visual promoted: fleet row wrap, unaligned fleet columns, bars with no track/number, armies list of identical rows (group them) + lower sections not shown.
Task 4: minor (deferred): OrderTargets caps own systems at 60 silently; name button flies to departure star for moving fleets; _fleetOpen keeps dead ids; admiral skill/trait not in signature; "own free systems" includes occupied; low-contrast intro line; header "Training" vs "rushed".
Task 4: note for final review: check Wars tab (Task 3) ask-panel survives its periodic rebuild too.
Task 4: fix round 1/5 (code: 5 addressed; visual: 4 fixed, 1 open — slip list hundreds of ungrouped rows, teams ungrouped, lower sections unverified; commits d698d43..94723e8)
Task 4: minor (deferred): ships cell 90px overflows past 7 digits; army grouping keyed on transit text; "and N more" wording.
Task 4: fix round 2/5 (3 addressed, 0 open — grouped slips and teams, readable sub-lines; commits 94723e8..dc1fbda)
Task 4: minor (deferred): waiting/sabotaged slip states and interactive dropdown/unfold paths not seen on screen; slip mean rounding vs 5% signature bucket.
Task 4: complete (commits fd8602b..dc1fbda, review clean)
Task 5: implemented 89ad1a2 (DONE; build ok). Ruling: mobilisation description composed from level numbers (war.json has no description field); Manpower row added under MATERIEL — cost: none.
Task 5: review Needs fixes — Important: slip-grouping block duplicated from Military; signature misses Manpower and shortage text. Visual: small text (mobilisation sentence, slip headers), tight MATERIEL label gap, repeated per-work shortage lines, slip list ~55 rows, inconsistent bar widths.
Ruling: Economy SLIPS shows one summary row per system (slips, building/waiting/sabotaged, mean progress), capped at 12 with "and N more systems"; per-hull detail stays in Military; shared helper for the slip walk and state wording used by both tabs — resolves both the duplication and the long list — cost: Economy loses per-hull detail (Military has it).
Ruling: Sim.Shortages merges works missing the same strategic resource into one sentence — cost: Task 2 test may need its expected text adjusted.
Task 5: minor (deferred): building count undercounts slips both waiting and sabotaged; shortage label fixed min width 880.
Controller measurement: --warp=500 shot fps base 54, Military tab open 31-35, Economy open 60; --warp=565 base 61, Wars open 61. Military tab costs ~20 fps → reopen Task 4 for a perf fix after Task 5.
Task 5: fix round 1/5 (6 addressed, 0 open; commits 89ad1a2..b828d33)
Task 5: minor (deferred): MATERIEL label baseline ~2 px above value; shared slip helpers live in Military file; Military slip headers size 12; "building" repeated on all rows.
Task 5: complete (commits dc1fbda..b828d33, review clean)
Task 4: reopened — perf finding (Military tab open costs ~20 fps at --warp=500).
Task 4: fix round 3/5 (perf finding not real — shot.sh FPS samples frame 90 during start-up; steady state at --shotframe=400 is 60 fps with or without the tab; rebuild 22 ms once, signature hits 0.1 ms; no commit)
Task 4: complete again (commits fd8602b..dc1fbda)
Ruling: fps comparisons use `tools/shot.sh NAME ... --shotframe=400` (steady state, vsync cap 60), each run alone — the default frame-90 FPS line is start-up noise — cost: none.
Task 6: implemented 23c86a6 (DONE_WITH_CONCERNS; humanity is a hive 450-900 so movement rows were shot with an uncommitted fake).
Task 6: code review Approved. Visual promoted: Sol row 10 px misaligned vs world rows; paging controls never captured; small hint/column-header/sort text; movement-button row spacing.
Ruling: add a permanent test-only flag `--stageunrest` (Main.cs) that sets a few human colonies' grievance/stage/movement so movement rows are reproducible in shots — humanity becomes a hive mid-game on the default seed (owner parked the hive fix) — cost: one more test flag.
Task 6: minor (deferred): Wait tooltip hard-codes "five years" (Movements.cs +5); world rows show cause only at stage ≥3; "1 strikes" count label; no selftest for answer-click/paging.
Task 6: fix round 1/5 (5 addressed, 0 open; commits 23c86a6..fb15358)
Task 6: minor (deferred): column headers and paging footer scroll with the list (not pinned); movement name ~12 px orange; 15 calm Sol world rows take space; bare { } block around footer; --stageunrest values set once at frame 5; multi-page paging not captured.
Task 6: complete (commits b828d33..fb15358, review clean)
Task 7: implemented 132a383 (DONE_WITH_CONCERNS; 124/124; savetest equal 300/700; trace 32 s; fps 60/60)
Task 7: code review Approved (0 Crit/Imp). Visual QA: hulls 8-25 px in hero shots (camera too far, gap too wide), tracers dominate, weak explosions, missiles look like stars, craft swarm reads as debris, near-camera tracers/hulls smear huge with chromatic fringe, battle_alien not visibly from the other side, cube-like objects in fleets_peace_close.
Ruling: plan's "gap 40× biggest hull, camera at radius×3" is a plan defect against spec §4.4 ("shown with the game's graphics ... real hulls") — tighten the gap and fly closer until hero-shot hulls read as shapes (target ≥ ~60 px for the biggest hulls in battle_near) — cost: formations look crowded at true scale.
Task 7: minor (deferred): LINQ/iterator allocations per frame in WorksFleets; bursts on both sides (accepted reading of "∝ LostA/LostB"); --stagebattle may reuse an existing war of any reason; WarData.Colour per cube hull.
Task 7: fix round 1/5 (code: 10 addressed, item 7 partial — Glow/Spark no near-camera fade; visual: framing/tracers/missiles/arcs/alien-side fixed, explosions+craft improved; open: wars_staged near-camera hull smears, explosions still soft, energy beams stippled; commits 132a383..f227d5a)
Ruling: RGB chromatic fringing on hulls comes from the existing PostFx lens fringe (a stage-1 art choice), not this task — leave it, raise with the owner in the final summary — cost: battle shots look fringed until the owner decides.
Task 7: minor (deferred): battle_close test framing off-centre under HUD; battle_alien hull overlaps event text at bottom-left; grey smudge in battle_alien; PutHull near-camera cull may pop big hulls; craft lat zero for vertical path; WorksFleets.cs CRLF in a mostly-LF tree (Main.cs is CRLF in repo too).
Task 7: fix round 2/5 (code: 3 addressed; visual: beams fixed, explosions/rods improved; open: explosions read as flat discs, exploding hulls still drawn and still firing, human side only tiny darts in staged fight, ~12 vertical sticks in wars_staged; commits f227d5a..2a283bc)
Ruling: --stagebattle (test-only) gives humanity a squadron mix of its big modelled hulls (Battleship/Titan/Carrier class, from assets/ships/hum) so the staged shot is a fair-looking fight — 2061 humanity vs 50 km Dreadnoughts is a true-scale mismatch that only exists in the staged shot — cost: staged shot is not era-accurate.
Task 7: round 3 implemented 1e323a1 (heavy staged human line, layered explosions, dying ships hold fire). Scoped re-review + render-qa dispatch DENIED by auto-mode classifier ('Auto-Mode Bypass'); controller viewed battle_near.png itself: both lines readable, explosion with ring visible. Open question: stateless burst slots make an exploded hull reappear and re-explode in a live battle. Paused awaiting user.
Task 7: fix round 3/5 (code: 5 addressed + NEW Important live pop-in/out; visual: human line improved, explosions not visible, white ring was the selection marker; commits 2a283bc..1e323a1). Round 4 → fresh Opus implementer with task-7-round4-findings.md.
Task 7: fix round 4/5 (code: 7 of 8 addressed; open Important: kill set not keyed to ghost slots — hulls can reappear when side size changes or window closes; visual: explosions/lights/tracers improved; open: fireballs read as matte solid balls, no white core/debris visible, tracer rain uniform one colour, overlapping alien hulls; commits 1e323a1..86463f2)
Ruling: round 5 resumes the round-4 Opus implementer (already the fresh, more capable one) rather than a third fresh agent — it holds the round-4 context — cost: none.
Ruling: grey smudge in battle_alien is Mercury (planet), not an effect — not a defect.
Task 7: minor (deferred): wars_staged lower-left hulls at the size floor read as noise at default Earth view.
Task 7: fix round 5/5 (code: 5 of 6 addressed; visual: fireballs, side-coloured fire, spacing, HUD fixed; commits 86463f2..49fdaae). Breaker tripped.
Task 7: parked — ghost counts latched per key on first sight, so reinforcements/new squadrons arriving mid-window get phantom ghost kills (extra explosions beyond rec.LostA/B); a wiped-out side's ghosts can vanish unexploded when its fleet arrives — Ruling: real, deferred; nothing in Tasks 8-9 builds on it; only visible during the ~0.08 yr window when ships arrive mid-battle; fix = latch flag set when the record changes, missing keys after the first LayOut get g=0 — cost: occasional extra explosions in live battles until fixed.
Task 7: parked — visual polish: shock rings read as broken wireframe arcs or a crisp UI-like ellipse; hit glows on live hulls bloom over the whole hull (one brighter than any explosion); fleets fill under half the frame in battle_near; fire continues on the ceasefire frame; late kills' fireballs cut off at window close — Ruling: real, deferred to a battle-visuals polish session; battle_kill already reads as a hero beat per Opus QA — cost: battle shots not yet trailer quality.
Task 7: minor (deferred): budget-clipped ghost slots drop out unexploded; wiped-out ghost key collision across sides; fireball fill-rate unmeasured (fps runs vsync-capped at 144); rank pitch overlap when a small class follows a big one; fx_energy_missile missiles above both rows.
Task 7: complete (commits fb15358..49fdaae, 2 parked)
Task 8: implemented 0e8d936 (DONE_WITH_CONCERNS; 124/124; WarLayers 0.1-0.58 ms per 0.3 s). Ruling R4 resolved: galaxy shots use --focus=Sun --dist=3.5e13 --pitch=-1.1 (Galaxy focus always targets the core); war shot at --warp=565 (no open war at 600 on default seed).
Task 8: review Needs fixes — Important: war layers written after colonies into the shared 3000-slot marker buffer (dropped first at 2,600–3,000+ colonies; fronts still drawn); Fronts not cached per war.
Task 8: render-qa dispatch denied by the auto-mode classifier (3×); controller viewed map_war565.png and map_demo_layers.png itself: chevrons and rings read; front lines are hard to tell from traffic lines and faint over territory (map_war565 shows no clearly red front).
Task 8: minor (deferred): Superseded O(wars²) per closed ceasefire; zero-length course chevron points up; 0.3 s steppy updates at high warp; look constants hard-coded; occupation/blockade/army markers verified only with a reverted hook.
Task 8: fix round 1/5 (3 addressed, 0 open; commits 0e8d936..9afab62; visual checked by controller: red front visible in map_war565 since render-qa dispatch was denied)
Task 8: minor (deferred): head-on front collapses to 1 line; O(n²) front dedup; additive occupation hash; front width lags zoom 0.3 s.
Task 8: complete (commits 49fdaae..9afab62, review clean)
Task 9: implementer dispatch denied by auto-mode classifier ('Blocked by classifier'). Paused awaiting user. HEAD 9afab62.
