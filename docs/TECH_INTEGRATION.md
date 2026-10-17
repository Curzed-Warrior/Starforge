# Tech Bible → Starforge: Integration Plan

*October 2026. Turns the 40-tree tech bible into Starforge's sim. Pairs with:*
- `data/tech_catalogue.txt`: the source catalogue (493 techs, one per line, human-editable)
- `data/techs.json`: built from the catalogue by `tools/build_techs.py`
- `data/progression.json`: research rules, exotic pools, gate events, danger meters, hulls, great works, apotheosis, god acts

Run `python tools/build_techs.py data/tech_catalogue.txt data/techs.json` after any catalogue edit. It fails on unknown requirements, tier inversions, cycles, bad effect syntax, or any tech that no combination of choices can reach. It also prints what each T7 fork path gives up.

---

## 1. What changes, in one screen

| Today | After |
|---|---|
| 38 techs in 4 branches, one per tier per branch, each a C# lambda | 493 techs in 8 branches and 39 lines, data-driven, effects in a small effect language |
| One research slot, highest weighted score wins | Three slots (Physics, Engineering, Society), each drawing a weighted hand of 3 like Stellaris |
| Strict chain: tier N needs tier N−1 in the same branch | Explicit prerequisites, plus a soft tier gate for non-spine techs |
| Battles: `power^0.25 × 1.2 if Ruthless` | Same, multiplied by a War factor built from weapon, defence, fleet and hull techs |
| Danger is three one-off crises | 11 danger meters that rise with risky techs and acts, decay slowly, and set off repeatable crises |
| Tops out at T10 with hard-SF techs | Hard-SF spine unchanged; an exotic layer (Umbral, Abyssal/Chrono, Glyph, Stellarine, Lucent) opens from T5 via gate events |
| Transcendence is the only end of the S branch | Five ascension paths plus a Luminary hybrid; any capstone feeds an **apotheosis** roll that can lift a civ into the divine realm |
| God acts cost Will; Will regrows with Faith | No Will, no costs, no cooldowns. Faith and Dread stay as consequences. 16 new acts |

**The existing 38 techs are the spine.** They keep their IDs, requirements, costs and effects, so current pacing and old saves survive. Bible techs that duplicate a spine tech are merged into it rather than added (§4).

---

## 2. Decisions taken (change them here before building)

1. **Sublight stays sublight.** The bible gives FTL at T2. Starforge is hard-SF until the Fold Drive (X6, T6), so the bible's three FTL doctrines become a fork at T6: Lane Folding, Metric Bubble, or Gate Anchoring.
2. **Weapons don't become ship components.** Battles are resolved in the sim, so weapon and defence techs set a *family tier* (energy, kinetic, missile, craft, exotic, doom, ground; armour, shield, PD, base). The best tiers become a War factor. The family tiers and `vis:` tags are stored per civ, so visible battles can later show lasers vs plasma vs lances.
3. **Strategic resources T2–T5 are not separate stockpiles.** Volatiles, crystals and neutronium fold into `Stock` and mods. Only the five exotic pools (Umbral, Abyssal, Anima, Stellarine, Lumen) are tracked, because each gates a tier.
4. **The god is unlimited.** The bible's Understanding rule and Will costs are dropped. Its divine tiers become (a) new player acts and (b) the apotheosis path for civs.
5. **Exclusive forks:** FTL doctrine (T6), Abyssal vs Chronogravitics (T7), Thought- vs Life-Siphon (T8), Custodian vs Imperial mandate (T5), and ascension path (stage 1, plus a stage-3 choice inside each path). The validator shows the T7 fork is balanced: the Abyssal road gives up 10 techs and the Chrono road gives up 12.
6. **Atrocity techs are ethic-gated by weight**: Reverent ×0, Pragmatic ×0.2, Ruthless ×1.5. A Reverent civ never researches the Soul Harvester unless the god gifts it.

---

## 3. Data model

### 3.1 `Tech` (Defs.cs)
Replace the `T(...)` factory list with a loader for `data/techs.json`. New fields:

```csharp
public class Tech
{
    public string Id, Name, Fx, Log, Line;          // Line: "SPINE", "PWR", "EWP", ...
    public char B;                                   // X I E S L M W Q
    public int Tier;
    public double Cost;
    public string[][] Req;                           // AND of OR-groups: [["PWR-12","CHR-3"],["GLY-2"]]
    public bool Start, Rare, Danger, Keystone, Event, Atrocity;
    public string Exclusive;                         // group id or null
    public List<Effect> Effects;                     // parsed effect language
    public Action<Mods> Apply;                       // built from Effects at load
}
```
- `Civ.PrereqDone(t)` becomes `t.Req.All(group => group.Any(Techs.Contains))`.
- Keep `Log` (chronicle text). Spine techs keep their current lines; bible techs default to `Fx` until the lore pass writes them.
- `Defs.Branches` grows to 8: Starflight X, Industry I, Energy E, Society S, **Life L, Mind M, War W, Beyond Q**.

### 3.2 Effect language → `Mods`
`ComputeMods()` walks known techs in tier order and applies each effect. One parser handles all of them:

| Effect | Meaning | Lands in |
|---|---|---|
| `mod:F*x` / `mod:F>=x` / `mod:F<=x` | multiply / raise to at least / lower to at most | `Mods` field `F` |
| `flag:Name` | set bool | `Mods` bool, or `Mods.Flags` set for new ones |
| `arms:family>=t` | best weapon tier in family | `Mods.Arms[family]` |
| `guard:family>=t` | best defence tier | `Mods.Guard[family]` |
| `hull:Name` | unlock hull class | `Mods.Hulls` set |
| `mega:id` | unlock a great work | `Mods.Megas` set (replaces the hard-coded `Tech` column in `Megastructures.Catalogue`) |
| `res:Pool+n` | produce an exotic pool | `Mods.Produce[pool]` |
| `use:Pool` | this tech's effects need the pool fed | skipped in ComputeMods while pool is empty |
| `meter:Name±n` | danger meter on completion | `Civ.Meters[name]` (applied once in CompleteTech, not in ComputeMods) |
| `act:id` | unlock an autonomous council action | `Mods.Acts` set |
| `vis:id` | visual tag for future battles and hulls | `Mods.Vis` set |
| `gate:GEx` | can't be drawn until that gate event is open for this civ | checked in the draw |
| `event:id` | fires a chronicle event or situation on completion | `CompleteTech` |
| `cond:...` | the existing special gates (core colony, K≥3, digital≥0.9, ≥40 colonies) | `Simulation.Gate()` |
| `path:G/C/S/P/D` | marks ascension path entry | `Civ.Path` |
| `digital:x`, `conscience:x` | existing S6/S7 side effects | `CompleteTech` |
| `apotheosis:+n` | adds to apotheosis score | `Civ.ApoScore` |

**Acceptance check for this step:** for every prefix of the old research order, Mods computed from the JSON spine equal Mods from the old lambdas, field for field. Then delete the lambdas.

### 3.3 New `Mods` fields and where each one hooks in

| Field | Default | Hook |
|---|---|---|
| `Fleet` | 1 | War factor (§5) |
| `Ground` | 1 | War factor; chance to *take* rather than burn a system |
| `WarLoss` | 1 | pop and frigate losses in war crises |
| `Stealth` | 1 | chance of being chosen as a war target (÷ Stealth) |
| `Sense` | 1 | first-contact distance in `GalaxyCheck` (× Sense) |
| `Range` | 1 | `MaxRangeGu` (× Range) |
| `Thrust` | 1 | interplanetary mission duration in `Colony` (÷ Thrust) |
| `Diplo` | 1 | relation drift per check: `+0.3 × (Diplo − 1)` |
| `Unity` | 1 | schism threshold in `Election`: `0.05 / Unity` |
| `Admin` | 1 | breakaway share: `Colonies.Count / (3 × Admin)` |
| `Trade` | 1 | Stock income `× (1 + 0.5 × (Trade − 1))` |
| `Terra` | 1 | Mars terraform time `260 / Terra`; act:terraform_any duration |
| `Uplift` | 1 | uplift success and population bonus |
| `Habs` | 1 | habitat cap and habitat housing in `Colony.Step` |
| `Settle` | 1 | settlement maturing speed (`/40`, `/60` divisors) |
| `Build` | 1 | elevator (22 y), ring (45 y) and great-project durations |
| `Spy` | 1 | espionage act success and defence |
| `Base` | 0 | starbase level = `max(era level, Base)` in `Megastructures` |

---

## 4. Merges into the spine

Merged bible techs don't exist separately; the spine tech's description notes what it absorbed. Bible techs that required a merged ID now require the spine ID.

| Spine | Absorbs |
|---|---|
| X1 Nuclear Thermal Rockets | FTL-1 Ion Drives |
| X2 Fusion Torch Drives | FTL-2 Fusion Torches |
| X6 Fold Drive | FTL-11 Spatial Folding |
| X7 Interstellar Deterrent | MEG-11 Nicoll-Dyson Beam |
| X9 Galactic Gate Network | MEG-19 Galactic Ring Network |
| I2 Asteroid Mining | MIN-2 Asteroid Harvesters |
| I3 Self-Replicating Foundries | IND-11 Von Neumann Fabricators (so I3 now adds Goo +10) |
| I6 Stellar Lifting | MIN-10 Star Lifting, MEG-12 Star Lifter Array |
| E1 Commercial Fusion | PWR-2 Tokamak Fusion |
| E2 Orbital Solar Arrays | PWR-3 Orbital Solar Mirrors, MEG-4 Dyson Swarm |
| E5 Statite Sails | MEG-8 Dyson Sphere (capture ≥ 0.9 already *is* the sphere) |
| E6 Matrioshka Layers | MEG-10 Matrioshka Brain |
| E7 Shkadov Steering | MEG-14 Stellar Engine |
| E8 Kugelblitz Engines | MEG-17 Kugelblitz Forge |
| E9 Galactic Core Tap | MEG-20 Core Lattice |
| S1 Arcologies | IND-8 Arcology Prefabs, COL-7 Arcology Living |
| S2 Closed Ecologies | COL-4 Dome Cities |
| S3 Radical Longevity | BIO-11 Telomere Mastery |
| S4 Habitat Rings | COL-6 Void Habitats |
| S6 Mind Uploading | CMP-11 Synthetic Citizens; becomes Synthetic path stage 1 |
| MIN-9 Singularity Decompression | MEG-7 Singularity Drill |
| COL-10 Ringworlds | MEG-6 Ringworld |

**Not merged on purpose** (tiers would invert): PWR-5 stays a separate T3 "Antimatter Containment Reactors" (X5 is T5); MIN-4 stays a T2 "He-3 Scoops" (I4 is T4); TER-3 Full Terraforming stays separate from S5 (S5 is the Mars project).

**Dropped:** PWR-16 Wellspring of Will and FTL-16 Omnipresence (the god has no Will to refill), MEG-9 Fleet Catapult (fold travel already covers it).

**Spine S6–S10 is now the Synthetic ascension path.** S6 is in the `asc` exclusive group. S8 Hive Consensus is one of three stage-3 choices (with Modular Bodies and Nanite Becoming). S9 accepts any of them. S10 Transcendence keeps its condition and adds +3 apotheosis.

---

## 5. War

Replace the strength line in `Simulation.Battle` (~line 540):

```
strength = GalacticPower^0.25
         × (Ruthless ? 1.2 : 1)
         × TierPow(max(Arms))                         // best weapon family
         × sqrt(TierPow(mean(Guard.armor, shield, pd)))
         × Fleet
         × (1 + 0.04 × best hull class index)
         × (1 + 0.05 × Arms.ground)                    // also raises take-vs-burn odds
```
`TierPow(t)` is the product of `tierPower.steps[1..t]`: ×1.3 per tier to T7, then ×1.4, ×1.5, ×3. A tier is only counted while the pool its tech `use:`s is above zero, so a civ that runs out of Umbral fights at its best fed tier.

**Hulls.** `Ships.ClassNames` grows to the 17 in `progression.json → hulls.order`. `ClassTech` goes away; `MaxClass` reads `civ.M.Hulls`. New classes use the listed fallback mesh until their Blender model exists. **Pacing note:** HUL-7 Titan is now T5 rather than I8 (T8), so titans arrive earlier than today; push HUL-7's cost up if that reads wrong.

**Bonus:** BIO-10 Grown Starships switches a civ to the organic hull family your aliens already use.

---

## 6. Research model

1. **Three slots.** Each civ researches one tech in each area at once: Physics (E, M, Q), Engineering (I, X, W), Society (S, L). `Colony` RP accrual stays; RP is split across slots by the drive's branch weights summed per area.
2. **Hand draw.** When a slot is free, draw a hand of 3 from that area's available techs. Weight each tech:
   `driveWeight[branch] × (rare ? 0.125 : 1) × (keystone ? 2 : 1) × dangerByEthic × atrocityByEthic × (0.5 if in the last hand) × (2 if a met civ knows it) / (tier + 1)^0.8`.
   The council takes the top-weighted card with the existing ±40% noise. The old "restless" ×4 on Starflight stays.
3. **Pity.** Any keystone that has been available for 8 draws without being picked is forced into the next hand.
4. **Tier gate (non-spine only).** A tier-t tech in branch b needs `min(3, count)` known tier-(t−1) techs in b, where `count` is how many exist at that tier. The spine is exempt so its pacing doesn't change.
5. **Gate events.** Techs with `gate:` can't be drawn until that gate is open for the civ (§7).
6. **Exclusive groups.** Once one member is known, the rest are removed. AI preferences per drive and ethic are in `research.aiExclusivePrefs`.
7. **Cost.** Spine techs keep `TierCost[tier]`. Non-spine techs cost `TierCost[tier] × 0.35`. This is the main pacing knob (§12).
8. **Aliens.** At spawn they get every tech at or below their age's max tier, *including* bible techs, so the Vessh (T6) arrive already holding the Umbral line. That makes them a source for the GE6 keys.
9. **Dreams.** `Inspire(civ, branch)` keeps working; it now biases the draw of the area that contains the branch.

---

## 7. Exotic pools and gate events

**Pools** live on the civ: `Dictionary<string,double> Pools`. Each `res:` effect adds `0.01 × n` per year per colony, scaled by `max(0.1, colony.Capture)`. Each known `use:` tech drains 0.002 per year per colony. An empty pool switches off its techs' effects in `ComputeMods`.

**Gate events** are a small per-civ state machine in a new `Sim/GateEvents.cs`: `Closed → Open → Done`. Each event checks its trigger yearly, then waits for any one key. The AI picks keys by drive. Definitions and keys are in `progression.json → gateEvents`:

| Event | Opens | Keys |
|---|---|---|
| GE6 Umbral Breakthrough | UMB-2 | own research project · salvage 3 wins vs an Umbral civ · be taught by a friendly civ · buy from an ancient civ |
| GE7 Abyssal Drawing | UMB-5 | research; then the ancients demand a sample or war |
| GE7b Archive Uplink | ARC-7 | friendly ancients · the Visitor (Talk/Study) · the Signal answered · self-build roll 45/45/10 |
| GE8 The Vault | GLY-1 | wrecks · contract · 4 raids on the Vault system |
| GE9 Rekindled Star | STL-1 | a colony near the Shard star, or the Archive |
| GE10 / GE10b Sealed Gates | LUX-1 / LUX-3 | trials at the rim Gates; wrong riddle answers still pay something |

**New galaxy content** (`Galaxy.cs`): add `StarInfo.Special` and place one Vault, one dormant Shard and four rim Gates at generation. The Visitor and the Signal become gate keys instead of standalone events. *The Quiet Lattice* (defined but never spawned) is a candidate for the Vault's holder; that's a lore call for later.

The "teach" key reuses ACOT's six-way choice (Charity / Gift / Teach / Refuse / Insult / Trade), picked by the teaching civ's ethic and relations.

---

## 8. Danger meters

`Civ.Meters` is a dictionary of 11 meters. `meter:` effects add on tech completion; acts and divine events add too; everything decays 0.5 per year. Crossing a threshold starts the linked crisis, which can now recur with a 50-year cooldown per meter (today each crisis fires once per civ via `Used`). The three existing crises are reused:

| Meter | Fed by | Crisis |
|---|---|---|
| Goo | I3, MAT-9, ASC-S3c, nanite acts | existing **runaway** replicators |
| AI | CMP-9/10, GOV-8 | existing **secession**, reworked as rogue machines breaking away as a splinter civ |
| Flux | STL-2, MIN-13, PWR-14 | existing **storms**; above 80, a Star-Wyrm (roaming hostile) |
| Mind | CMP-13, CMP-16 | new **overmind** takes the council for a decade |
| Strange | MAT-10 | new: a world converts to strange matter |
| Breach | FTL-8, UMB-7 | new **incursion**: hostile fleet at the colony (reuse the Rival fleet code) |
| Veil | PSI-5, FTL-12, XWP-8 | new **possession**: drive flips at random |
| Elders | UMB-4/5, PWR-12, exotic gifts | ancients demand tribute, then war |
| Atrocity | atrocity techs and acts | met civs sour; at 60, ancients or divine civs declare war |
| Spy | ESP-7 | exposure: every met civ −30 |
| Egress | CHR-7 | a civ leaves the universe; every civ loses some capture |

Negative meter effects (PSI-4, SHD-11, CMP-14, STL-5, CHR-5, ASC-P3c) are the counter-techs. Your **Intervene** still ends any crisis.

---

## 9. Civ acts: ripple effects from mortals

`act:` effects unlock 57 council actions. The big idea is to **refactor each god power into an actor-agnostic core**, e.g. `DoIgnite(actor, colony, body)`, called by both the player's button and a council decision. Civs that research Gas-Giant Ignition then ignite giants themselves, the chronicle says who did it, and Faith and Dread only move when the voice was the actor.

| Group | Acts | Work |
|---|---|---|
| Reuse a god power | ignite, shift_orbit, seed_life, seed_life_far, gaia, garden, restore, living_world, awaken_world, crack_world, collapse_world, nova, kindle_star, move_star, forge_star, eat_star, build_planet, terraform_any, uplift | refactor + council rule |
| War and diplomacy | bombard, kkv, planet_killer, beam_strike, purge, harvest, seal_world, nanite_world, erase_cluster, assimilate, federation, assembly, custodian, empire, ancient_accord, memetic, spy, sabotage, incite, steal_tech, block_research, dreamwalk | new rules in war and relations code; treaties are already on your "not yet built" list |
| Flavour (chronicle event + small effect) | dig, find_ruins, salvage_tech, foresee, migrate, nomad, tame, veil_pact, veil_patron, hear_voice, avatar, stasis, star_weapons, vault_raid, gate_riddle, egress | event text plus a modifier |

Atrocity acts (purge, harvest, nova, erase_cluster, assimilate, cracking inhabited worlds) are Ruthless only, or Pragmatic when at war and losing.

---

## 10. Ascension paths and apotheosis

**Paths** (`asc` group, one stage-1 per civ; each has a stage-3 choice):

| Path | Stages | Capstone |
|---|---|---|
| Synthetic (spine) | S6 → S7 → S8 / Modular / Nanite → S9 | S10 Transcendence (+3) |
| Genetic | ASC-G1 → G2 → Exemplar / Multiplicity / Adaptive | ASC-G4 Living Godflesh (+2) |
| Cybernetic | ASC-C1 → C2 → Augmenters / Link-Hive / Assimilators | ASC-C4 Unified Chassis (+2) |
| Psionic | ASC-P1 → P2 → Covenant / Chosen / Severed | ASC-P4 Veil Ascendancy (+2) |
| Departure | ASC-D1 → D2 (needs the Chrono road) | CHR-7 Horizon Egress (+2) |
| Luminary (hybrid, on top of any) | Purity of Form → Essence → Will | ASC-L3 (+4) |

The +n values are apotheosis score. Other sources: CMP-16, BIO-17, GOV-11, DIP-11, PSI-10 (+1 each), LUX-4 Lucent Covenant (+3).

**Apotheosis.** Every 10 years a civ with score ≥ 3 rolls `0.004 × score × luck` (capped at 25%). Luck is `(0.5 + Faith) × ethic (Reverent 1.2 / Ruthless 0.8) × (1 − Atrocity/100)`. A civ with score 10 and middling faith rolls about 4% a decade, so it's rare but real.
- **Success → `Civ.Divine`.** The civ keeps its worlds as sacred worlds and starts acting like a lesser god: every 15–40 years it seeds life, ignites giants, kindles stars, uplifts natives, gifts a tech to a favoured civ, or smites a hated one, picked by drive and ethic. It has relations with the voice, so if you harm it, it can answer. Gold glow on territory and hulls. Maximum 3 divine civs.
- **High-score failure (score ≥ 6) → 20% chance of becoming Fallen:** vigour ×0.3, research ×0.5, no more seeding. This is how new Vessh-like empires appear.
- **Your overrides:** RaiseDivine and CastDown (§11).

---

## 11. The god: unlimited

1. **Remove Will.** Delete `Will`, `WillMax`, `WillPerYear`, `WillCost`, `Afford`, `Spend`, `WillRate`, and the cooldown check for every act (`Ready`/`Use`). Remove Will from the HUD (`Hud.cs` ~lines 51–138 and 626) and from saves (`SaveGame.cs` 44/86/124; tolerate the old field on load).
2. **Keep `Witness`.** Faith and Dread still rise, aliens still blame whoever they suspect, and Faith now feeds apotheosis luck rather than Will.
3. **New acts** (`progression.json → godActs.new`): GiftTech, EraseTech (one civ or everyone), KindleStar, SnuffStar, MoveStar, ForgeWorld, UpliftNow, SeedSpecies, BendFate, Possess, OpenLane, Quarantine, RaiseDivine, CastDown, RewriteLaw, and Raise extended to every new great work.
   - **Ripples:** a gifted exotic tech adds Elders to the ancients; snuffing an inhabited star is a horror; SeedSpecies and UpliftNow create real civs that enter diplomacy.
4. **Deferred:** TimeTurn (needs colony snapshots) and Genesis (needs a second galaxy instance).

---

## 12. Pacing and balance

1. **Baseline first.** Before changing research, add `--techlog` to `tools/shot.sh` (with `--warp`). It prints the year each civ first reaches each tier in each branch. Record a baseline for 3 seeds.
2. After §6, rerun and tune `costScaleNonSpine` (default 0.35) until spine milestones (Seed Ships, Fold Drive, Type II, Core Tap, Transcendence) land within ±10% of baseline.
3. **Targets:** the exotic layer opens around the Fold era (GE6 ~ year 2350–2450 for humanity), T8+ is reached by only 1–3 civs per galaxy, and apotheosis happens in maybe 1 in 3 long games.
4. Watch `GalaxyCheck`. War strength now varies more (×1 to ×~100 at T10). If ancients become unbeatable, lower the late `tierPower` steps.

---

## 13. Tech screen (Hud.cs `RefreshTech`)

The 4×11 grid can't hold 493 techs.
- **Tabs** for the 8 branches.
- **Tiers** as columns 0–10 in a horizontal scroll, with rows grouped by line (PWR, MEG, …).
- **Top strip:** the three area slots with progress, plus the current hand of 3 for the viewed civ.
- **Tech cards:** known / researching / available / locked colours, with badges for Rare, Dangerous, Keystone, Event, Atrocity and Exclusive.
- **Tooltips:** requirements (OR groups shown as "one of"), effects in words, and the chronicle line.
- **Beyond (Q)** stays hidden until the civ's first gate event opens.
- **Header:** a meter panel and pool readout.

---

## 14. Saves

Old IDs are unchanged, so `Techs` sets load as-is. On load:
- Map `ResearchId` to the slot of its area.
- Missing pools and meters default to 0.
- Drop Will.
- Start-known techs (flag `S`) are added if missing, so an old save gains the T0 baseline.

---

## 15. Build order for Claude Code

Each phase is shippable and testable on its own.

1. **Data and loader.** Load `techs.json`, build `Apply` from effects, add the 8 branches. *Check:* spine Mods equal the old lambdas, field for field (§3.2).
2. **Research model.** Slots, hand draw, tier gate, exclusives, cost scale, AI prefs, save migration. *Check:* `--techlog` pacing within ±10% (§12).
3. **New Mods hooks** from §3.3, one field at a time. *Check:* warp runs don't regress fps or pacing.
4. **War.** Arms, guard and fleet factor, 17 hulls with fallback meshes, BIO-10 organic switch.
5. **Pools and gate events**, plus the Vault, Shard and Gate systems in `Galaxy.cs`.
6. **Meters and repeatable crises**, reusing runaway, secession and storms.
7. **Actor-agnostic powers** and the civ acts, the reuse group first.
8. **Ascension paths and apotheosis**: Divine and Fallen civs, gold visuals.
9. **Unlimited god** and the 16 new acts.
10. **Tech screen** (§13).
11. **Great works and hull models** in Blender: 16 new structures (Ringworld reuses the Vessh asset) and 8 new hulls × 3 families. The kit in `assets/stations/kit.py` and `assets/ships/build.py` already supports this.
12. **Balance pass** across 5 seeds.

---

## 16. Open questions for you

1. Do titans at T5 (bible pacing) feel right, or should HUL-7 move back toward T8?
2. Should divine civs keep playing the normal economy, or leave the sim and act only as gods?
3. The Lucent gates: should they spawn at game start (visible, sealed, ominous) or appear only when the first civ reaches STL-3?
4. Should you be able to see each civ's current hand of 3 and swap a card? That would be a softer Dream.

---

## 17. Implementation status (built October 2026)

All twelve phases are in the game. Code lives in `src/Sim/` (TechData, Research, Exotics, Meters, War, CivActs, Apotheosis, Powers) and `src/View/` (TechScreen, VoicePanel, Megastructures, Ships, GalaxyOverlay).

**Deviations from the plan, and why:**
- **Spine pacing** needed more than the cost knob. Spine techs get `research.spineBias` (4) and the lowest-tier available spine tech is always dealt into its area's hand, and they are exempt from the repeat penalty. Over 4 seeds: Seed Ships 2148-2197 (baseline ~2180), Black Hole Forges 2331-2343 (baseline 2333), exotic layer (UMB-2) 2364-2407 (target 2350-2450).
- **Non-spine cost** is `TierCost x costScaleNonSpine (1.0) x nonSpineTierMult[tier]` (new array in progression.json: x1 up to T4, rising to x5 at T10). At 0.35 the whole ordinary tree was known by about 2300.
- **Tier gate** counts only techs the civ can still learn: an exclusive choice (e.g. the Chrono road) otherwise made the next Beyond tier unreachable.
- **GE8 (the Vault)** had no key a first civ could use (wrecks and contract both need someone who already knows GLY-1), so an **expedition** key was added: reach the perceived Vault and roll 2.5% a year, scaled by Fleet.
- **Apotheosis falls** use the 20% chance spread over a century of rolls, and only when luck is below 1 (low faith or atrocities). At the flat rate, high-score civs fell far more often than they rose. Over 1400-year runs each galaxy now ends with 1-3 Exalted civs and a few Fallen.
- **Exalted, not divine**: followed progression.json (lesser powers with limits, max 3), not the earlier "Divine" wording.
- **Alien hulls**: the organic and crystalline families got the 8 new classes too (24 new models in all).

**Answers to the open questions (defaults taken, change in data):**
1. Titans stay at T5 as the bible has them (HUL-7); move HUL-7's tier in the catalogue if they feel early.
2. Exalted civs keep playing the normal economy, plus their lesser acts.
3. The Lucent Gates exist from the start, hidden; they are revealed (with a milestone) when the first civ perceives them, and their seals weaken from 2600 (`Simulation.GateSealYear`).
4. Yes: the tech screen shows each civ's hand in every slot, and the voice can swap, redraw, pin, ban, decide forks, gift and erase.

**Test flags:** `--techlog` (pacing), `--hands` (every research draw), `--costscale=`, `--spinebias=`, `--voice=act,...` (scripted voice acts), `--mega=id,...` / `--mega=all`, `--savetest` (now also exercises voice edits; round-trips byte-identical).
