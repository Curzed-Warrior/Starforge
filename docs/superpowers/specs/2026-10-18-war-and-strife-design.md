# War and Strife: design

*Starforge, 18 October 2026. Agreed in conversation with the project owner; builder's calls are marked **[builder's call]**
and can be changed without changing what gets built.*

## 1. Intent

Starforge's galaxy is beautiful and mostly at peace with itself. The analysis of 18 October found three problems this
design answers:

- The player has nothing at stake, and humanity has no risks: no rebellion, no civil war, no infighting.
- Wars are a coin flip every two years (`War.cs`): no fleets, no fronts, no economy behind them, no reason they end.
- The late game goes flat.

The owner wants war to be **one of the main ways the game is fun**: Stellaris-like at its core, with more layers
underneath, as messy as real history. Space is rich, but nobody can reach all of it, so humanity will fight itself
over who gets what, and over ideals. **By the late game it must be a real possibility that humanity is several
human empires**, some that fought their way out and some that left peacefully.

Every system must be fully in place so the owner can **micromanage any part of it, or leave the whole thing to the
simulation** and watch.

## 2. Goals, non-goals, anti-goals

**Goals**
1. Wars that start for a reason, are fought with fleets and armies over real systems, and end in ways that follow from
   the reason, the score, exhaustion, culture and the home front: surrender, compromise, ceasefire, or annihilation.
2. A war economy: mining, factories, shipyards and barracks, with civilian industry redirected to the war (the US in
   WW2), and the cost of that landing on ordinary people according to their culture.
3. Space battles fought by the numbers with deep detail (weapon types, defence types, hull, crew experience,
   admirals, morale, supply), shown with the game's graphics, with optional Total War-style command.
4. Ground war by the numbers (Stellaris-style), integrated into the overall war.
5. Special forces: elite teams that infiltrate systems you don't control.
6. Internal strife: colonies against the core over unequal access to resources, civil wars over ideals, peaceful
   separations, coups and revolutions. A fractured humanity is a real late-game outcome.
7. War culture: each people starts with its own lore, and its culture drifts with its history.
8. Every area can be automated or run by hand; the AI empires play by exactly the same rules.

**Non-goals (for now)**
- A full ground-war system with its own battle view. Ground war is numbers-based now; it is designed to be swapped for
  a full system later without changing the rest.
- Ship design (the Stellaris designer). Fleets use the civ's best hull and its tech; a designer can come later.
- Trade routes as visible convoys, and diplomacy beyond what war needs (treaties, federations already exist as acts).

**Anti-goals (would be a failure even if it works)**
- Humanity that can never fall apart, or that always falls apart. Unity should be an achievement.
- Wars that all end the same way (the prototype's 25-year stalemates), or that never actually fight (it had 23 battles in
  940 years).
- Micromanagement that is *required*. Leaving an area to the council must play well.
- A war layer that ignores everything else: it must feed and be fed by the economy, research, unrest, the voice and the
  chronicle.
- Chronicle spam: hundreds of battles must not bury history (the problem fixed on 18 October).

## 3. Constraints

- Godot 4.7 .NET, C#. The simulation is `Simulation` (partial class across `src/Sim/*.cs`); views in `src/View/`.
- **Same rules for everyone.** Every civ (humanity, its splinters, aliens) runs through the same code.
- **Scale.** Late games have ~100 civs and 2,000–3,000 colonies (the Vessh alone reached 1,100). Everything that runs
  per colony runs at most yearly; per-tick work is per fleet and per war. A 940-year headless run must stay under ~40 s
  (it is ~25 s today).
- **Saves round-trip** byte-identically (`--savetest`). Older saves must still load.
- **Rules in data.** Tunable numbers live in a new `data/war.json` (the owner chose JSON rules for modding, answer 32).
- **True scale and real drive speeds.** Sublight travel between stars takes decades or centuries; fold drives take
  years. Wars before fold drives are slow and local, and that is intended.
- **Existing hooks to reuse:** tech `arms:` and `guard:` families and tiers, `Mods.Fleet/Ground/WarLoss/Stealth/Spy/Base`,
  hull classes, `Policy` areas and `Alert` decisions, `Splinter()`, the Emergency state, `Narrative` crises and rivals,
  `CivActs` war and espionage acts, `BattleFought` for cinematics, `Spectacle` for battle effects, `Ships.Mesh` for hulls.

## 4. The systems

### 4.1 Why wars start, and how they end (decided)

Every war has a **reason** (casus belli). The reason sets the **stakes**, and the stakes decide whether peace is ever
possible.

| Tier | Reasons | Possible endings |
|---|---|---|
| **Limited** | border claim, a key system (deposit, shipyard, chokepoint), revenge for an insult or an atrocity, tribute | cession of the claimed systems, reparations, ceasefire on the front line, status quo |
| **Principle** | independence vs union (civil war), forcing an ideology or government, breaking free of an overlord, liberation of a people | autonomy instead of independence, partition, vassalage, regime change, recognised independence |
| **Survival** | extermination, holy war, purge of a species, an existential threat (a Wyrm, a machine uprising) | none: it ends when one side is gone or can no longer fight |

**Willingness to talk** comes from four things together: the stakes of the reason, how the war is going (systems held,
battles won: the war score), how tired the people are (exhaustion), and their culture and ethics. A collapsing home front
(unrest, revolts, a peace movement winning an election) forces a government to the table.

**Endings:**
- **Surrender** on the winner's terms (the goal is met).
- **Negotiated compromise**: partial terms, priced by the score.
- **Ceasefire / armistice**: the front freezes where it is, the occupied systems stay occupied, and it can break later.
  It is a state between war and peace, with a border that stays tense.
- **Total collapse** of one side.

Allies join: federations and overlords defend their members; an attacker's friends join if they also dislike the target.
A truce follows peace.

**[builder's call]** The detailed list of reasons and terms, their score costs and thresholds, live in `data/war.json`.
Each term has a price in war score; a compromise is any set of terms whose total the loser will accept given its
stakes, score and exhaustion.

### 4.2 War culture (decided: lore, then drift)

A people's **war culture** is a new layer beside its drive (Wayfarers, Choristers, Forgewrights) and ethic (Reverent,
Pragmatic, Ruthless). It sets how fast war weariness builds, how much manpower can be called up, how far mobilisation
can go before people resist, and how they take defeat.

- **Starting lore.** Humanity in 2061 is mixed, with no mandatory service. Each alien species has its own lore (for
  example, the Vessh as a conscript warrior people). Splinters inherit their parent's culture and diverge.
- **Drift.** Long wars harden a culture or exhaust it; long peace softens it; victory feeds martial pride; defeat feeds
  revanchism or pacifism.
- **Laws** the council (or player) can change: mandatory service on or off, and the conscription level.

**[builder's call]** Culture as a few axes (Martial ↔ Pacifist, Duty: volunteer ↔ mandatory service, Resolve:
brittle ↔ unbreakable) rather than a single label, so drift is continuous; each has a starting value per species in
`data/war.json` and named descriptions for the interface ("a martial people", "they will not be ruled").

### 4.3 The war economy (decided: WW2-style redirection)

- **Mining** produces raw mass (exists: hubs, star lifting, stock).
- **Factories** turn mass into **consumer goods** (the civilian side) or **war materiel** (the military side). Industry
  exists as `IndustryKg`; this splits it.
- **Shipyards** turn materiel into hulls, limited by their number of **slips** (exists as the `yard` great work; slips
  grow with yards and hull class).
- **Barracks** train armies from **manpower** (population × the culture and conscription law).
- **Mobilisation levels**, like Hearts of Iron 4's economy laws: **Peacetime → Partial → War economy → Total war.** Each
  moves more industry and people into the war. **Retooling takes time**, and production lines get more efficient the
  longer they run, so a short war fought on a peacetime economy goes badly.
- **The cost lands at home**, scaled by culture: shortages of consumer goods, conscription and rationing raise grievance,
  slow growth and slow research. Pushing a pacifist people to total war risks strikes and revolt.
- **Key systems matter**: strategic deposits (Dark Matter, Neutronium, Exotic Gases, Rare Crystals), shipyard worlds and
  chokepoints. **Blockading** or occupying them cuts the enemy's production; supply runs through owned and connected
  systems.
- **Automation**: the council sets mobilisation and production by drive, ethic, culture and threat; the player can take
  either.

**[builder's call]** Materiel is a stockpile per civ (not per system) fed by every system's military share, shipped
through the existing logistics links, so a blockaded system's output cannot reach the yards.

### 4.4 Fleets and space battles (decided: deeper numbers, graphics, optional command)

**Fleets** are made of **squadrons** of ships of real hull classes. Fleets have an **admiral** (skill and traits),
**morale**, **supply**, a **stance** and a **mission** (defend, attack, raid, blockade, escort an invasion).

**Battles resolve by the numbers** (auto-resolve), with more depth than Stellaris:
- **Weapon families** (from the tech data): kinetic, energy, missile, strike craft, exotic, doomsday (against planets).
- **Defence families**: armour, shields, point defence, starbase. Each counters a family: armour stops kinetics,
  shields absorb energy, point defence shoots down missiles and craft; exotic weapons bypass part of everything.
- **Hull class**, from corvette to worldship.
- **Crew experience**: green → regular → veteran → elite, earned by surviving battles and lost when ships die.
- **Admirals**, **morale** (a side whose morale breaks retreats), and **supply** (fleets far from supply weaken).

Every battle is shown with the game's **graphics** when the camera is there: real hulls, beams, explosions, at true scale.

**Optional Total War-style command.** When a battle starts the player may take command: pause, give orders, watch it at
true scale. **Command at squadron level, and any squadron can be split down to single ships.** Orders: move, focus
fire, formation, screen, retreat. Squadrons without orders follow the admiral's doctrine. Battles not commanded resolve
by the same numbers. Setting per player: always auto-resolve / ask each time / always command.

### 4.5 Ground war (decided: by the numbers, for now)

Stellaris-style. **Armies** are trained in barracks from manpower, with attack, defence and morale, raised by ground
weapon tech and experience. **Fleets must hold orbit before a landing.** Transports carry armies; they fight the
world's **garrison** (raised by the owner, plus defence works); the stronger, better-supplied force takes the world.
Taking worlds is how systems are occupied, and occupation is how wars are won. Bombardment from orbit can soften a
garrison at a cost in population and atrocity.

Designed as its own module behind a small interface (strength of an invading force vs a garrison, per world), so a full
ground-war system can replace it later.

### 4.6 Special forces (decided: best trained before the war)

Elite teams, few and expensive, sent into **systems you don't control** (enemy, rival, or contested). **Quality comes
from training time**: teams trained in peacetime get the full course and are the best; teams rushed out in wartime are
cheaper and quicker and worse. So a council that plans ahead has the edge.

**Missions**: sabotage a shipyard or a deposit, assassinate an admiral, incite a revolt (feeds the enemy's unrest),
steal technology, prepare a world for invasion (weakens its garrison). Every mission risks capture, and a captured team
is a diplomatic incident. **Counter-intelligence** defends. Reuses `Mods.Spy` and the existing espionage acts.

### 4.7 Infighting and fracture (decided: as messy as real history)

**Two kinds of fault line:**
1. **Colonies against the core, over resources.** Grievance builds from unequal access: industry shipped off to the
   capital's great works, strategic deposits feeding other worlds' projects, distance from the capital, poverty next to
   the core's wealth, crowding, and wartime mobilisation draining the colonies hardest.
2. **Civil wars over ideals.** Factions (drive, ethic, government) split: Wayfarers against Forgewrights, democracy
   against junta, uploaded minds against flesh.

**The ladder**: calm → discontent → strikes (stops shipping its industry) → an armed movement → open revolt. The council
(or the player) answers each movement: **grant self-rule, invest in it, crack down, or wait**, each with costs.

**Separations** come both ways: **peacefully** when the council lets them go, or by **secession war** when it fights to
keep them. Also **coups** (a victorious, ambitious admiral in an unpopular war), **revolutions** (a people pushed too far
overturn the council), and **juntas**. The home system has its own version: a Mars or a Belt that feels used by Earth
can rise.

**The end state the owner wants**: by the late game it is a real possibility that humanity is several human empires
with their own alliances and grudges. The core *can* hold together if it is managed well (fair shares, autonomy,
investment), and drifts apart if neglected or overstretched.

### 4.8 The player's control (decided)

- **Per-area automation** extends today's `Policy` areas: **War** (declarations, reasons and terms), **Fleets**
  (missions, stances, targets), **Production** (mobilisation, factories, yards), **Interior** (answering unrest),
  **Intelligence** (special forces). Any area can be left to the council.
- **Battles**: always auto-resolve / ask each time / always command.
- **Which side you're on**: by default the core. When a splinter forms, the player is offered the chance to take it
  over, and can switch later from the empire screen.

## 5. Interface (what the player sees)

**[builder's call]**, following the agreed Stellaris-style layout (answer 29):
- **Wars** screen: each war's reason, stakes, score, exhaustion, fronts, terms on offer; buttons to propose terms or a
  ceasefire.
- **Military** screen: fleets (squadrons, ships, experience, admiral, mission), armies, special forces, queues.
- **Economy** additions: mobilisation level, civilian vs military share, materiel, shortages, slips.
- **Interior** screen: every system's grievance, its causes, movements, and the four answers.
- **Galaxy map**: fleets and armies as markers with movement lines, fronts, occupied systems, blockades, battles.
- **System view**: battles with real hulls; the command mode's controls.
- The **outliner** lists fleets, wars and movements; alerts follow the existing pause rules.

## 6. Order of work (agreed)

Several projects; each ships working and tested before the next:
1. **Foundation**: `data/war.json`, war culture, the war reasons and endings model, fleets and admirals as units, and
   the economy split (consumer goods / materiel, mobilisation, shipyards, barracks, manpower). Saves.
2. **War in the field**: movement, supply, blockades, auto-resolved battles with the full number model, ground
   invasions, occupation, score, exhaustion, peace, ceasefire. AI on the same rules.
3. **Infighting**: grievance and the ladder, movements and answers, peaceful and violent secession, coups,
   revolutions, the home system's worlds; switching to a splinter.
4. **Special forces**.
5. **Interface** for 1–4 (screens, map, outliner), plus battle graphics in the system view.
6. **Total War-style command** of battles (squadrons and single ships).

## 7. How we'll know it works

Headless `--trace` runs over several seeds, 2061 → 3000, check:
- Every civ fights; battles happen in every war (hundreds per game, not tens), and wars end in a mix of ways.
- **Whether humanity fractures is left to chance** (owner's decision): there is no target rate, and nothing steers a
  game toward or away from it. The only check is that both outcomes really happen across seeds, and that a Sol uprising
  is possible before 2300.
- No runaway empires (the Vessh's 1,100 systems) without fracture.
- Chronicle volume stays readable (~150–300 lines a century).
- Run time under ~40 s; `--savetest` round-trips; frame rate unchanged at the galaxy scale.
