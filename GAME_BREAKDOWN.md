# Starforge: Full Game Breakdown

*State of the game as of October 2026. Godot 4.7.2 .NET (C#), Desktop\starforge.*

> **Update (tech bible integrated):** sections 4 (tech tree), 7 (ships) and 9 (the player) below describe the game before the tech bible. The game now has 493 techs in 8 branches with three research slots, exotic gate events, danger meters, 17 hull classes, 36 great works, civs that use the voice's powers, apotheosis, and an unlimited voice (no Will). See README.md and docs/TECH_INTEGRATION.md §17 for the current state.

---

## 1. What the game is

Starforge is a PC space god-sim that **plays itself**. It starts on Earth on 1 January 2061: 9.7 billion people, one world, and a few footholds. From there, humanity reaches the Moon, Mars, the asteroid belt and the gas giants. It wraps the Sun in a Dyson swarm, sends slow seed ships to Alpha Centauri, learns to fold space, and spreads across a galaxy it shares with five older alien civilisations.

You are **"the unseen voice"**: a god they cannot locate. You don't run the empire. Its council, factions, ethics, research and wars are all autonomous. You watch, you nudge, and now and then you do something terrible or wonderful.

It is the PC successor to the phone game *Project Kardashev*.

**Design pillars (current direction):**
1. **Graphics first.** Visual spectacle is the main selling point.
2. **Mostly autonomous.** It plays out like Stellaris (tech, empires, battles) without you managing it.
3. **God-sim at both scales.** Micro: a single world, its natives, its council. Macro: whole stars and empires.
4. **Cinematic moments.** Major events get composed camera shots that change with the surroundings.
5. *Deferred:* walking around structures yourself. It exists as a prototype but isn't a focus.

---

## 2. One continuous world

There are no loading screens or separate maps. The camera's logarithmic zoom covers about **0.01 to 100,000,000 world units**. One mouse wheel takes you from a planet's cloud tops out past the edge of the galaxy.

| Scale | What you see |
|---|---|
| **Surface / close-up** | Cities and arcologies, spaceports with rockets climbing, satellites, freighters, mines, mass drivers. All of it is derived from the colony's numbers. |
| **System** | The star (corona and glare), planets with atmospheres, clouds, rings and moons, belts, orbit lines, the Dyson swarm, ship traffic, stations and megastructures. |
| **Galaxy** | A barred spiral of about 300,000 star points with unresolved glow, emission nebulae and dust lanes. Empire territories are tinted, swarm-wrapped stars go dark, and ships cross between stars. |
| **Beyond** | Andromeda and other distant galaxies. |

- The viewed star always sits at the world origin, and the galaxy is scaled around it. That keeps the floating-point precision clean at every scale.
- The galaxy has **simulated stars** that civilisations can actually reach. They're placed on the visible arms, and each one stands for about 40 million real stars. Sol and its real neighbourhood are hand-placed.
- Star classes are M, K, G, F and so on, with realistic luminosities. There are few reachable stars in the crowded core.

---

## 3. Factions, ethics and the council

Every civilisation is run by a council of **three factions**. Each faction has a **drive** that weights which research branch it prefers.

| Drive | Faction | Creed | Favours | When it seeds other stars |
|---|---|---|---|---|
| Expansionist | **Wayfarers** | "go further" | Starflight (2.2×) | Almost immediately |
| Contemplative | **Choristers** | "think deeper" | Society (2.2×) | Late (30% swarm capture) |
| Industrious | **Forgewrights** | "build more" | Industry (2.2×), Energy | Mid (10% capture) |

- **Elections** happen every few years. The faction with the most influence takes the council and installs a new **Speaker** (a named leader who gets quotes in the chronicle).
- **Influence shifts** with research: each completed tech feeds its branch's faction. It also shifts with your interventions.
- **Schisms:** if two factions are nearly tied, the losing one leaves.
  - Early on, millions or billions of people take part of the stockpile and vanish into the dark. They can return later as a **rival** fleet.
  - Once interstellar, the far colonies break away as a **new polity** with its own name, such as "the Second Dawn" or "the Free Worlds of X". It starts hostile.
- **Hive Consensus** (tech S8) ends elections and schisms for good.

### Ethics (conscience)

| Ethic | Attitude |
|---|---|
| **Reverent** | Living worlds are sacred, until there is nothing else left. |
| **Pragmatic** | They regret what they consume, and consume it anyway. |
| **Ruthless** | Matter is matter. They mine inhabited worlds and burn conquered systems. |

Conscience is a sliding value from 0 to 1. Events and your acts nudge it, and the civilisation's ethic changes as it crosses thresholds (the chronicle reports "their conscience hardens"). Ethics affect:
- What they'll mine.
- How they treat natives.
- Diplomacy: Reverent pairs warm to each other, and Ruthless civs sour everyone.
- War strength: Ruthless gets +20%.

---

## 4. Technology tree

There are four branches. Each tech needs the previous tier in its own branch, and some also need a tech from another branch (shown in the "Also needs" column). Research choice is weighted by the ruling faction's drive with some randomness. A "restless" civ that is already harvesting power heavily favours Starflight until it has seed ships.

Cost by tier: 0, 6, 15, 35, 80, 180, 400, 1,200, 4,000, 25,000, 70,000.

### X: Starflight ("other suns, and the dark between them")
| Tier | Tech | Effect | Also needs |
|---|---|---|---|
| 0 | Reusable Heavy Lift | Earth orbit is cheap (known at start) | |
| 1 | Nuclear Thermal Rockets | Faster transit; Mars in months | |
| 2 | Fusion Torch Drives | The outer planets open up | |
| 3 | Laser Sails | Gram-scale probes to nearby stars | E2 |
| 4 | **Seed Ships** | Colonise other stars at 5% of light speed | S2 |
| 5 | Antimatter Catalysis | Seed ships at 15% of light speed | |
| 6 | **Fold Drive** | Light-years in days; hubs cost 20% less | |
| 7 | Interstellar Deterrent | Visitors no longer take what they want | |
| 8 | Fold Gates | Permanent doors between stars | |
| 9 | Galactic Gate Network | Regions fill 60% faster; cross the galaxy in years | needs ≥40 colonies |
| 10 | **Andromeda Ark** | A fleet leaves for another galaxy | E9; needs Kardashev 3.0 |

### I: Industry ("machines that build machines that build worlds")
| Tier | Tech | Effect | Also needs |
|---|---|---|---|
| 1 | Lunar Industry | Mining +20%, mass drivers | |
| 2 | Asteroid Mining | The belt and Mercury open; mining +30% | |
| 3 | Self-Replicating Foundries | Foundries +50%; space elevators | |
| 4 | Gas Skimmer Fleets | Gas giant yield ×3 | X2 |
| 5 | Planetary Disassembly | Mining ×2, hubs at half cost, orbital rings | |
| 6 | Stellar Lifting | Mine the star itself | |
| 7 | Star Husbandry | Wrapped regions yield +25% | E6 |
| 8 | Black Hole Forges | Regional harvesting 40% faster | |

### E: Energy ("a star wrapped so tightly it forgets how to shine")
| Tier | Tech | Effect | Also needs |
|---|---|---|---|
| 1 | Commercial Fusion | Planet-bound power ceiling ×10 | |
| 2 | Orbital Solar Arrays | First swarm collectors | |
| 3 | Thin-Film Collectors | Collectors +40% | I2 |
| 4 | Flare Shielding | Flare and collision losses halved | |
| 5 | Statite Sails | Collectors +30% | |
| 6 | Matrioshka Layers | Research ×1.4 | |
| 7 | Shkadov Steering | Collectors +30%; flares nearly harmless | |
| 8 | Kugelblitz Engines | Wrapped regions yield +30% | |
| 9 | **Galactic Core Tap** | Sagittarius A* harnessed; region power ×1.5 | needs a colony near the core |

### S: Society ("minds that outlast bodies, and bodies that outlast stars")
| Tier | Tech | Effect | Also needs |
|---|---|---|---|
| 1 | Arcologies | Population ceiling +40% | |
| 2 | Closed Ecologies | Venus aerostats, colonies anywhere, growth +20% | |
| 3 | Radical Longevity | Growth +30%, research +20% | |
| 4 | Habitat Rings | O'Neill habitats, research +25% | |
| 5 | Planetary Engineering | Terraforming, orbital rings | I2 |
| 6 | Mind Uploading | Research ×1.3; the civ becomes 50% digital | |
| 7 | Digital Ascension | Research ×1.5, industry +20%; 97% digital; conscience hardens | |
| 8 | Hive Consensus | Research ×1.5; no more elections or schisms | |
| 9 | The Long Memory | Research ×2 | |
| 10 | **Transcendence** | The end state | needs ≥90% digital and Kardashev ~2.95 |

**Finale milestones:** *The Core Ignites* (E9), *Andromeda* (X10) and *Transcendence* (S10).

---

## 5. Eras and the Kardashev scale

Power is tracked in watts and shown as a **Kardashev rating**: K = (log₁₀W − 6) / 10.

| Era | Reached at | Flavour |
|---|---|---|
| The Cradle | start (about 2.6×10¹³ W) | One world and a few footholds |
| The Inner System | 2×10¹⁵ W | Industry leaves Earth's gravity well |
| **Type I** | 10¹⁶ W | As much power as falls on the whole Earth |
| The Swarm | 10²¹ W | The Sun measurably dimmed from neighbouring stars |
| Twilight | 10% of the Sun | A tenth of the Sun's light gone |
| Eclipse | 50% of the Sun | More of the Sun is caught than escapes |
| **Type II** | swarm ≥90% complete | "Every photon works for humanity" |
| **Type III** | K ≥ 3.0 galaxy-wide | A quarter of the Milky Way dark, seen from Andromeda |

**Overall arc:**
1. Cradle (2061)
2. Inner System
3. Type I
4. Swarm
5. Twilight
6. Eclipse (Type II)
7. Seed ships
8. Fold drives
9. Galactic expansion, contact, wars and schisms
10. Type III
11. The Core, Andromeda, Transcendence

---

## 6. The economy of a star system (Colony)

Each civilisation's presence in a system is a **Colony**. It runs the phone game's economy plus physical settlement.

- **Hubs**: mining operations on bodies. The Moon starts with one. Asteroid Mining opens the belt and Mercury; Fusion Torch Drives (X2) open the giants. Hubs level up, and a fully mined world is *gone*: "Every gram of it is in orbit now".
- **Foundries**: orbital industry that turns stock into collectors.
- **Swarm capture**: the fraction of the star wrapped in collectors (0 to 1). This drives power, and therefore Kardashev.
- **Stock**: the resource stockpile councils spend on decisions.
- **Population**, with caps from Arcologies, habitats and Greening. **Settlements** spread across worlds.
- **Council decisions**, each with chronicle text:
  - Open hubs and foundries.
  - Launch named frigates.
  - Settle new worlds.
  - Build a **space elevator** (≈22 years), then an **orbital ring** (≈45 years).
  - **Terraform Mars** (≈260 years; it gets a sky at the halfway point).
  - Spin up **O'Neill habitats**.
- **Restraint:** worlds people live on (and the Moon) only get surface works unless the civ turns Ruthless. Venus and Mars become mineable only for a ruthless, late civilisation.
- **Great projects**, one per ruling drive:
  - **Stellar Engine** (Forgewrights, 120 years): a mirror half an orbit wide; the star pushes itself.
  - **Archive of Everything** (Choristers, 110 years): the swarm becomes a mind.
  - **Fold Gate** (Wayfarers, 100 years): one step to every star within 40 light-years.
  - The oldest alien empire, the Vessh, has a finished **Ringworld**.
- **Seed ships:** launched when the drive's threshold is met. Their cadence depends on drive, vigour, fold drive and empire size.
- **Regions:** once fold drives exist, a finished system's daughter flotillas slowly fill the surrounding region. Each sim star stands for millions of real ones.
- **Epitaph:** when a swarm is complete, the chronicle writes a closing summary: years from first light to last collector, worlds consumed, natives, peak population and project.

---

## 7. The galaxy: empires, contact, war

### The five alien civilisations

These are placed at least 220 units from Sol and at least 300 units from each other. Their drives and ethics are random.

| Empire | Species | Age | Tech head start | Notes |
|---|---|---|---|---|
| the Vessh Hegemony | Vessh | Ancient | up to tier 6 | 14 daughter colonies; a finished Ringworld; low vigour (old and slow) |
| the Orun Concord | Orun | Old | tier 5 | 6 colonies; finished Archive of Everything |
| the Tallow Choir | Tallow | Mature | tier 4 | 2 colonies; finished Fold Gate |
| the Hask | Hask | Old | tier 5 | Finished Stellar Engine |
| the Ninefold | Ninefold | Young | tier 1 | Planet-bound, just starting |

A sixth slot, *the Quiet Lattice*, is defined but not spawned.

### Diplomacy and war (Stellaris-like, fully autonomous)
- **First contact** happens when two civs' territories come within range of each other.
- **Relations** run from −100 to +100 and drift each check:
  - A shared border: −1.5.
  - Distance: +0.5.
  - Both Reverent: +1.
  - Either Ruthless: −0.8.
- **War** starts below −60 with a shared border. **Peace** comes above −20 ("Nobody remembers who asked first").
- **Battles:**
  - Strength is power^0.25, with a 20% bonus for Ruthless civs.
  - The winner takes a border system. Home systems are protected while other colonies remain.
  - Ruthless attackers have a 40% chance to **burn** the system instead of taking it.
  - Losing every colony means **extinction**: "The chronicle keeps their name."
- **Splinter polities** from schisms join the galaxy as new empires.

### Ships
Hull classes unlock with technology, and shipyards are refitted for each one.

| Class | Length | Unlocked by |
|---|---|---|
| Shuttle | 24 m | start |
| Corvette | 70 m | Lunar Industry |
| Frigate | 130 m | Asteroid Mining |
| Destroyer | 260 m | Self-Replicating Foundries |
| Cruiser | 460 m | Fusion Torch Drives |
| Battleship | 900 m | Planetary Disassembly |
| Carrier | 900 m | Antimatter Catalysis |
| Titan | 1.8 km | Black Hole Forges |
| Colossus world-ship | 3 km | Fold Gates |

There are three hull families, all built in Blender:
- **Human:** mechanical plating, turrets, window rows, nav lights and drive glow.
- **Organic:** used by half the alien empires. A ribbed bronze seed-pod hull with glowing bands, swept fins and pods.
- **Crystalline:** used by the other half. Faceted shards round a central prism, with light glowing from inside.

Traffic includes freighters, warships, and docking routes to starbases.

---

## 8. Narrative layer (the chronicle)

Everything is narrated in the **chronicle** (press C). It has dozens of random events, gated by circumstance. Some examples:
- A Kessler cascade.
- The first child born on Mars.
- A lunar wedding.
- The Mars independence vote.
- AI citizenship.
- The religion of the Hearth.
- Mining disasters.
- Lullabies about the old sky.
- A monument made from consumed worlds.
- A fusion yacht passing Voyager 1.

**Crises** can last years, and you can end them with *Intervene*:
- Runaway replicators.
- Digital secession.
- Stellar storms from star-lifting.
- Plague.
- War.

**Natives:** worlds can hold civilisations at stone age, bronze age, industrial, atomic or spacefaring level. The council chooses one of three fates:
- **Quarantine:** the world is closed.
- **Uplift:** +population and Contemplative influence.
- **Clearance:** the world is wanted for its mass, and the people become "only a word in the chronicle".

**Rivals:** schismatic exiles return decades later as a named fleet. This ends in talks or a partition.

**The Visitor:** something not ours arrives at the edge of the system. The council reacts in one of five ways:
- Talk: it may hand over a schematic for a technology.
- Study: industry +15%.
- It *takes* 15% of the swarm.
- The fleet fires on it, and a nearby star goes dark.
- It simply watches for years.

**The Signal:** after 2100, humanity may answer a signal from the stars, with consequences.

**Speakers** give quotes coloured by their drive and ethic. For example: "Measure twice, dismantle once." / "Whatever it takes."

---

## 9. The player: the unseen voice

### Nudges (free, with cooldowns)
| Act | Cooldown | Effect |
|---|---|---|
| **Comet** | 20 yr | A rich comet arrives; adds stock, Starflight weight and Expansionist influence |
| **Flare** | 25 yr | Burns about 8% of the swarm; the council is humbled (Contemplative, softer conscience) |
| **Dream** | 12 yr | Leans research toward one branch |
| **Point a constructor** | 8 yr | Forces a mine on a chosen world (grief if it's inhabited) |
| **Protect them** | none | Forbids harm to a world's natives |
| **Draw them here** | 30 yr | The next seed ship goes to a star you choose |
| **Intervene** | none | Ends the current crisis |
| **Peace** | 120 yr | Silences every war humanity is in |

### Cosmic powers (P), paid in **Will**
Will starts at 50, caps at 100, and gathers at 1.5 a year. It grows faster with belief.

| Power | Will | Effect |
|---|---|---|
| Seed Life | 25 | A dead world fills with seas, then green, then clouds |
| Ignite | 60 | A gas giant becomes a burning second sun |
| Shatter | 40 | Break a world into glowing rubble (anyone there dies) |
| Nova | 80 | The star erupts: the swarm burns and the inner worlds are scoured |
| Revelation | 35 | Finish the current research |
| Shift Orbit | 45 | Move a world into the habitable band; it thaws into life over a century |
| Herald Ship | 40 | A crewed seed ship nobody built leaves for the nearest free star |
| Summon | 50 | Call the Visitor down on the homeworld now |

### Build (N)
Raise **any megastructure** in the viewed system for 30 Will, whatever its people know. The camera flies to it.

### Belief and dread
- Every act is witnessed. **Wonders** build **Faith**; **horrors** build Faith *and* **Dread**.
- Faith multiplies Will regeneration: rate × (1 + 2 × Faith), plus a share of alien faith.
- Faith passes three stages, each with a milestone: *The Attentive* (25%), *A Church of the Voice* (55%), *The Believers* (85%).
- Dread hardens hearts. Aliens who have met humanity and suffer a horror **blame humanity**: relations drop by 35, sometimes enough for war.

---

## 10. Megastructures

Each megastructure appears in a system once its owner knows the tech, or once you raise it. Every one is a hand-modelled Blender asset (assets/stations/), and they are labelled as you approach.

| Structure | Tech | Description |
|---|---|---|
| **Starbase** | none (grows by era) | Outpost → Starport → Starhold → Star Fortress → Citadel |
| Mega Shipyard | I2 | Refitted per hull class, with a hull in the cradle |
| Orbital Foundry | I3 | A city-sized crucible pouring hull plate |
| Laser Sail Array | X3 | A phased mirror array that throws lightsails |
| Planetary Shield | E4 | A hexagonal field around the homeworld |
| Antimatter Factory | X5 | A sun-powered collider ring |
| Sentry Array | S5 | A dish wider than a moon, listening |
| Arc Furnace | I5 | Harvester claws clamped round a world, drinking its core |
| Science Nexus | S6 | Counter-rotating rings of uploaded minds |
| Star Lifter | I6 | Polar magnetic funnels pulling up stellar mass |
| Fold Jump Flashes | X6 | Ships folding in and out at the edge |
| Hyper Relay | X6 | Beacons pinning a fold lane across the system |
| Nicoll-Dyson Beam | X7 | The swarm's light as one weapon (needs 30% capture) |
| Bishop Ring | S7 | A 1,000 km habitat ring open to the sky |
| Coordination Center | S8 | Where the hive consensus is kept |
| Gateway | X8 | A fold to every other gate |
| Kugelblitz Reactor | E8 | A caged black hole made of light |
| Black Hole Forge | I8 | A black hole fed planets |
| Memory Spire | S9 | Every mind that ever lived, in a helix of light |
| Stable Wormhole | X9 | A throat to the far side of the galaxy |

Also shown:
- **Earth works:** space elevator, orbital ring, mass-driver streams.
- **Habitats:** O'Neill cylinders, Stanford tori and Bernal spheres.
- **The great projects** (§6), built visibly as their progress climbs:
  - Shkadov stellar engine.
  - Matrioshka archive of nested shells.
  - Fold gate ring.

---

## 11. Cinematics

When history turns, the camera becomes a cinematographer.

**Triggers:**
- The first seed ship (*The Long Voyage*).
- *A Second Sun* (the first colony).
- The first of each megastructure.
- Each new era, Type I and Type II.
- Mars.
- First contact.
- Battles (at most once every 40 years).
- Your own powers.

**How it plays:**
- Time slows to **one day per second**, letterbox bars slide in, and the HUD steps aside. The title goes in the lower third.
- If the event is in another system, the camera **travels there** first.
- Shots are **composed from the surroundings**:
  - **Backlit:** the star burns behind the subject.
  - **Reveal:** a pull-back until a neighbouring world fills the background.
  - **Horizon / rise:** from just above a nearby planet's limb (only if one is close enough).
  - **Arc:** a sweep round the lit side.
  - **Chase:** following a departing ship.
- It never repeats the same kind twice in a row. Shots last about 11 seconds, and any key skips one.
- Afterwards the camera hands control back without a jump, and the **Director** waits 25 seconds before taking over again.
- You can toggle cinematics in the Esc menu.

**The Director (F)** is an autoplay camera. It follows the story and paces time by era.

---

## 12. Presentation and tech

**Shaders:**
- Planets: procedural or textured, with terraform greening and an ignite "fire" mode.
- Atmospheres, clouds, the star's corona and glare.
- Black holes.
- Galaxy points, glow and dust; volumetric nebulae; far galaxies.
- Ship hulls and markers.

**Post-processing:** lens ghosts, colour fringing, vignette and grain.

**Music:** a generative score synthesised live. Pad chords follow the era's mode, with sparse bells and chimes when history happens, through reverb and delay.

**HUD:**
- Clock, Kardashev bar, Will, the civilisation card (faction, Speaker, research, belief and dread) and the chronicle.
- Tech screen (T), empire borders (B) and a history chart.
- Powers and Build panels, title cards, labels.
- All in a thin, translucent glass style.

**Asset pipeline:** Blender 5.2 runs headless with Python build scripts.
- `assets/stations/kit.py` is the shared modelling kit.
- `assets/ships/build.py` builds the hulls.
- The output is exported to glTF, which Godot imports with LODs.
- The game boosts emissives so lights read from across a system.

**Performance:**
- Ship instances are capped per class.
- Megastructures merge their static parts into one mesh per material.
- Late game runs at about 58 fps.

**Saves:** F5 to save, F9 to load, with an autosave every 5 minutes. Saves include Will, altered worlds (greening, ignited, moved orbits), raised structures, and faith and dread.

**Testing:** `tools/shot.sh` takes command-line screenshots. Flags include `--warp=YEARS`, `--cine=`, `--gallery=` and `--cast=`.

---

## 13. Controls

| Key | Action |
|---|---|
| Right-drag / WASD | Orbit the camera |
| Wheel / Q E | Zoom |
| Left-click | Fly to a world, star or empire |
| 1–9, 0 | n-th world of the system / the star |
| G / X / H | Galaxy / galactic core / home |
| Space, + / − | Pause, time speed |
| F | Director |
| P / N | Powers / Build |
| C / T / B | Chronicle / Technology / Borders |
| J | Board the Outpost (first-person prototype) |
| V / L / M / F11 | Hide HUD / labels / music / fullscreen |
| Esc, F5, F9 | Menu, save, load |

---

## 14. Known gaps and what's next

**Known issues:**
- The arrival shot at Alpha Centauri can be dark, and distant structures are sometimes just silhouettes.
- Crystalline alien hulls are dark when backlit. One gallery test ran at 33–38 fps, which is still unexplained.
- Cinematics fire about every 4–8 game years. That may be too often or too rare depending on taste.

**Not yet built:**
- **Micro-level interaction with individuals.** Speakers exist and are quoted, but you can't yet pick out and influence a specific person.
- **Visible fleet battles.** Battles are resolved in the sim and get a cinematic, but there are no ship-to-ship engagements on screen yet.
- **Deeper Stellaris-style systems:** real diplomacy states (treaties, federations, vassals), fleet power as an actual unit, and alien tech trees that differ from humanity's.
- **World-data feeds or cloud features:** none.
- **First-person exploration:** a prototype exists (walking the Outpost), but it is deliberately parked.
