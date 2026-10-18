# Starforge

Earth, 2061. Nine point seven billion people, one world, and a handful of footholds beyond it.
Watch humanity reach the Moon, Mars, the belt and the giants, wrap the Sun in a Dyson swarm,
cross to Alpha Centauri on slow seed ships, then fold space and spread across a galaxy it shares
with five older civilisations. You are a voice they cannot locate.

The PC successor to *Project Kardashev*: the same autonomous council, factions, ethics, crises,
natives, visitors and chronicle, now in one continuous world from a planet's cloud tops to the whole galaxy.

## Run

Double-click `run.bat` (builds the C# and launches Godot 4.7 .NET).

## Controls

| | |
|---|---|
| Right-drag / WASD | orbit the camera |
| Mouse wheel / Q E | zoom, from cloud tops out past the edge of the galaxy |
| Left-click a world or a name | fly there (names of stars and empires work too) |
| 1–9, 0 | jump to the n-th world of the system, 0 = the star |
| G | galaxy view · **X** the galactic core (Sagittarius A*) · **H** home (Earth) |
| Space | pause · **+ / −** time speed |
| F | the Director: camera follows the story and paces time by era |
| P / N / U | cosmic powers · raise a megastructure · the voice's greater acts |
| J | board the Outpost and walk it in first person (WASD, mouse, Space jetpack, Shift run, Esc to leave) |
| C | chronicle · **T** technology · **B** empire borders |
| Esc | menu · **F5** save · **F9** load (autosave every 5 minutes) · **F6** write the chronicle out as a history book |
| F12 | screenshot (**Shift+F12** without the interface), saved to the user folder's `screenshots` |

**Mods:** the rules are plain JSON (`data/war.json`, `data/techs.json`, `data/progression.json`). Copy one into the
Godot user folder as `mods/data/<file>.json` (on Windows `%APPDATA%\Godot\app_userdata\Starforge\mods\data\`) and edit
it: the game reads it in place of the built-in one.
| V | hide the HUD (cinematic) · **L** labels · **M** music · **F11** fullscreen |

## The unseen voice

- **Comet** – a rich comet falls in on a perfect intercept. They start looking outward.
- **Flare** – the star erupts and burns the swarm. The council is humbled.
- **Dream** – a shared dream leans their research toward one branch.
- **Intervene** – ends the current crisis (runaway replicators, secession, plague, war…).
- **Point a constructor here** – make them open a mine on the selected world.
- **Protect them** – forbid harm to a world's natives.
- **Draw them here** – (on an unclaimed star) the next seed ship in range comes to it.
- **Peace** – silence every war humanity is fighting.

## Cinematics

When history turns, the camera becomes a cinematographer: the first seed ship, the first of each kind of megastructure, a new era, Type I and II, first contact, Mars, a new colony, battles, and your own acts. Time slows to a day a second, letterbox bars slide in, and a shot is composed from what's actually around the subject:

- **backlit**: the star burns behind it
- **reveal**: a pull-back until a neighbouring world fills the background
- **horizon**: from just above a nearby planet's limb
- **arc**: a sweep round its lit side
- **chase**: following a departing ship

It never picks the same kind twice in a row. Any key skips a shot. Toggle cinematics in the Esc menu.

## The voice is unlimited

There is no Will and there are no cooldowns. Everything the voice does is witnessed: wonders build **belief**, horrors build belief and **dread**, and aliens who know humanity blame us for horrors. Belief feeds a civilisation's chance at **apotheosis**.

**Powers (P):** Seed Life, Ignite, Shatter, Nova, Revelation (finish every slot's research), Shift Orbit, Herald Ship, Summon.

**Build (N):** raise any of the 36 great works in the viewed system.

**Voice (U):** the greater acts, all aimed at what you are looking at:

| Group | Acts |
|---|---|
| Stars | Kindle a star · Snuff a star · Mark a star · Move it beside the marked one · Open a lane to it · Seal this region |
| Worlds | Forge a living, ocean, desert, ice or gas world |
| Peoples | Uplift the selected world's natives into a new civilisation · Seed a species on an empty star · Bend fate (mercy or ruin for the current crisis, or decide the visitor) |
| Council | Possess: hand the council to any faction · Raise to Exalted · Cast down · Make Fallen |
| Laws | No war · Quick minds (research x2) · Fold forbidden · Fecund · Calm (no new crises) |

**Technology (T)** is also where the voice steers research: swap a card in a slot's hand, redraw a hand, pin a tech to come up next, ban one for fifty years, decide an exclusive fork, gift any tech, or erase a tech from one civ or from the whole galaxy.

## Technology

493 techs in eight branches (Starflight, Industry, Energy, Society, Life, Mind, War, Beyond), loaded from `data/techs.json` (built from `data/tech_catalogue.txt` by `tools/build_techs.py`). Each civilisation researches in three slots (Physics, Engineering, Society), each drawing a weighted hand of three. The original 38 techs are the **spine** and keep their pace. Beyond tier 5 the **exotic layer** opens through gate events: Umbral matter, the Abyssal or Chronogravitic road, the Glyphs and the Vault, the rekindled Shard, and the four sealed Gates on the rim. Eleven **danger meters** (Goo, AI, Flux, Breach, Veil, Elders, Atrocity...) set off repeatable crises. Weapon and defence tiers, fleet doctrine and hull class decide battles. Civs that learn the voice's tricks use them themselves. Five ascension paths can lead a civilisation to **Exalted** status (a lesser god with limits), or to a **Fall**.

## Arc

Cradle (2061) → Inner System → Type I → the Swarm → Twilight → Eclipse (Type II) → seed ships →
fold drives → galactic expansion, contact, wars and schisms → Type III → the Core, Andromeda, Transcendence.
Each faction builds its own great project: the Stellar Engine, the Archive of Everything, the Fold Gate.

## Megastructures and fleets

Every system a civilisation holds fills with engineering as it learns. Labels appear as you approach; click one to fly there.

- **Starbase**, growing from Outpost → Starport → Starhold → Star Fortress → Citadel
- **Orbital / Mega Shipyard**, refitted for each new hull class, with a hull taking shape in the cradle
- **Orbital Foundry**, **Antimatter Factory**, **Laser Sail Array**, **Planetary Shield**, **Arc Furnace**
- **Star Lifter**, **Science Nexus**, **Sentry Array**, **Hyper Relay**, **Fold Jump Flashes**, **Nicoll-Dyson Beam**
- **Bishop Ring**, **Coordination Center**, **Gateway**, **Stable Wormhole**, **Kugelblitz Reactor**, **Black Hole Forge**, **Memory Spire**
- Habitats come as O'Neill cylinders, Stanford tori and Bernal spheres

Ships grow with the yards through seventeen hull classes, from the shuttle to the Lucent Seraph, in three families (human, organic and crystalline). The tech bible's great works join the list: Launch Loop, Singularity Decompressor, Fleet Command Lattice, Cognition Spire, Ringworld, Umbral Fortress, Gate Bastion, System Aegis, Birch Shellworld, Penrose Sphere, Alderson Disk, Glyph Sanctum, Corona Forge, Sun Citadel, Starforge and Lucent Throne.

## Credits

Planet textures: Solar System Scope (solarsystemscope.com), CC BY 4.0.
