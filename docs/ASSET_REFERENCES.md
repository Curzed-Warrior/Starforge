# Asset overhaul: references and design brief

*October 2026. Research for rebuilding every station, megastructure and hull. Images were viewed, not downloaded. Sources are listed at the end.*

## What the best references have in common

Studied: the 1975 NASA Ames colony paintings (Rick Guidice, Don Davis), Alexey Pyatov's *Horizon* station (ArtStation), and image searches covering shipyards, star lifters, black-hole stations, Nicoll-Dyson beams, gates, ringworlds, orbital factories, sensor dishes, capital ships, and organic and crystal alien ships. Many search results are AI images, so they count only as mood. The design lessons come from the human-made pieces.

1. **Three scales of form, always.** Primary masses read from across a system. Secondary modules sit at about 5–15% of the primary size: pods, tanks, hangars, arms. Tertiary detail sits at about 0.5–2%: greebles, pipes, vents, lights. Our first megastructures had the primary and tertiary scales but almost nothing in between, which is why they read as "a ring with texture on it".
2. **Purposeful asymmetry.** *Horizon* has arms of different lengths, one whole arm given over to a drydock, and modules clustered rather than evenly spaced. Keep a symmetric skeleton where physics demands it (spin, beams), but break the secondary layer.
3. **Value and colour contrast.** Light panels against dark ones. One accent colour per structure (signal red, hazard orange, or teal), used on about 5% of panels and on stripes. Lit windows in long strips. A few large flat colour fields, such as red hull plates or white solar backs, give the eye places to rest.
4. **Everything looks like it does something.** Radiator fins angled edge-on to the star. Solar wings. Spherical tanks in ribbed cages. Pipe runs along spines. Docking arms with ships tied up. Gantry cranes on rails. Antenna and dish clusters. Each structure needs at least four of these "functional verbs".
5. **Construction logic.** Trusses brace anything long. Rings hang on spokes. Heavy parts sit on collars. Seams and bands segment long forms every 1–3 diameters.
6. **Light as detail.** Hundreds of small lights: nav strobes at extremities, window rows, floodlights washing hull faces, glowing intakes. From a distance they *are* the structure.
7. **Scale cues.** Small ships docked or in transit, and human-sized windows. A structure without them reads as a toy.

## Kit additions (kit.py v2)

- `pipe_run`: parallel pipes following a path, with clamps.
- `tank_cage`: spheres or capsules inside a ribbed cage.
- `radiator_bank`: rows of thin fins on a manifold.
- `solar_wing`: a hinged array with cell grid and frame.
- `dock_arm`: an extendable arm with a hull tied up at its end.
- `gantry`: a crane bridge on twin rails.
- `dish`: a parabolic antenna on a yoke.
- `strobe`: nav lights at extremities.
- `accent`: paints a fraction of panels in the accent material.
- `band`: segmentation collars.
- Materials: `HullLight` (white panels), `Accent` (signal red), `AccentOrange` and `Hazard` (striped), `Panel` (mid grey), `Gold` (foil), `Ice`.
- Later: `restyle(col)` swaps Hull/HullDark so dark metal is primary (used on the starbases), and `strip_lights` adds broken runs of warm light round a ring.

Kit fixes found during the overhaul (both affected every asset; all were re-exported):
- `fast_box` wound its faces inward. Blender's Cycles renders hid it, but EEVEE and Godot cull back faces, so greebles, light strips and panels showed their far insides in game. `_UV_FACES` now winds outward.
- Object names are global in `stations.blend`, so a second asset's `r0`/`ring`/`disc`/`core`/`halo` exported as `r0.003` and the game's exact-name lookups missed it (animation silently off). `obj()` now claims the exact name and moves a stale *mesh* holder aside (never a light).
- Large flat surfaces need clearance from the hull behind them: 1 m apart z-fights at ringworld/Alderson scale.

## Great works (greatworks2.py v2) and their references

Saved in `assets/stations/renders/refs/`, compared side by side in `renders/cmp_<slug>.png`:
- Launch Loop: Lofstrom launch-loop diagram (`launchloop_lofstrom.jpg`): stadium track, cable-fan stays from a keel, deflector stations, cargo sleds.
- Singularity Decompressor: Stellaris matter decompressor (`decompressor_stellaris.jpg`): saturated blue coil frames round the drill heads.
- Fleet Command Lattice: modular station complex (`lattice_modular.jpg`): seamed sensor domes on drum stations, trusses with lit transit tubes.
- Cognition Spire: Marius Andrei station (`spire_andrei.jpg`): stacked finned saucers, two tilted broken rings.
- Ringworld: Halo installation from orbit (`ringworld_halo.jpg`): huge repeating hull plates with sockets, lit rim crests.
- Umbral Fortress: purple dark-citadel mood (`umbral_purple.jpg`): jagged spire cathedral, shard halo.
- Gate Bastion: jump gate (`gate_jumpgate.jpg`): bevelled segments, turret towers, rune collar.
- System Aegis: shield emitter (`aegis_emitter.jpg`): dark prong cradle with blue-burning inner edges round a focus.
- Birch Shellworld, Penrose Sphere: geodesic Dyson lattice (`shell_geodesic.jpg`): strut frames with hub nodes, part-closed with dark and lit panels.
- Alderson Disk: Neil Blevins (`alderson_blevins.jpg`): scorched inner zone, narrow habitable band, broad ice plains.
- Glyph Sanctum: floating monoliths (`sanctum_monoliths.jpg`): carved stones with burning glyphs riding the rune rings.
- Corona Forge: heat-shield station (`corona_shield.jpg`): curved shields facing the star, foundries sheltering behind.
- Sun Citadel, Starforge: star-engineering dishes and beams (`starforge_beams.jpg`).
- Lucent Throne: ivory, gold and halo palette (`throne_mood.jpg`); the halo is placed at its own centre so it spins in place.

## Brief per asset

### Starbases (outpost, starport, fortress, citadel)
These are already the strongest assets: the outpost is the quality bar. Upgrade them, don't replace them. Add docked ships at every arm, a drydock arm on the starport and above, asymmetric module clusters, radiator banks, more accent panels, and nav strobes.

### Habitats (O'Neill cylinder, Stanford torus, Bernal sphere): currently procedural, so make real assets
- **Torus:** an outer ring with a faceted glass band and a chevron radiation shield. Spokes to a hub with a docking spindle. A tilted primary mirror above, and secondary louvred mirrors (Davis, AC76-0525).
- **Bernal sphere:** a sphere with ribbed agricultural rings ("crystal palace" tori) either side. A long axle spindle carrying solar wings and docking at both ends. Banded radiator and mirror rings (Guidice, AC76-0965).
- **O'Neill pair:** twin cylinders with three land/window stripes each and three huge hinged mirrors. Agricultural "bead necklace" rings round the caps, and an end-cap spindle with dish and docking (Guidice, AC75-1085).

### Orbital Shipyard / Mega Shipyard (yard)
An open box-truss cradle about 1.4 times the hull's length, with gantry cranes on rails along the top and sides. Clamp arms reach in to the hull. Rows of floodlights face inward. A fabrication block feeds the cradle at one end, and parts tanks hang underneath. The hull in the cradle is half-plated (the game already grows it). A *Horizon*-style drydock arm with red accent plates.

### Orbital Foundry
A crucible core hangs inside a structural ring. Mass-driver catch funnels face the Moon, and slag-glow radiators fan outward like the current ones, but with tiered manifolds. Ingot conveyors run as pipe runs to a hull-plate rolling mill: a long box with glowing slots. Smoke-dark panels with orange heat accents.

### Antimatter Factory
A large particle-accelerator ring. The ring is a segmented tube of magnet modules every few degrees, with yellow-black hazard bands. Solar collector wings are tangential to the ring. A trap sphere sits at the hub in a heavy cage, held by struts. Cryo tank cages and a containment ship dock.

### Laser Sail Array
A hexagonal mosaic of phased mirror tiles in an articulated frame, set in a cradle. Each tile is a hex with a rim. Power plants sit behind on a truss spine, there are sail stacks waiting in magazines, and a targeting dish. The current asset is a flat teal hex sheet, so this needs depth and frames.

### Nicoll-Dyson Beam
A focusing station: an enormous segmented primary mirror (petals with gaps), secondary mirror on a tripod, and a beam barrel of rings. Radiator wings and a control citadel at the back.

### Sentry Array
A huge segmented dish with ribs and a mesh between them. The feed horn sits on a quadrupod, and the dish rides an alt-az yoke on a base station. Smaller dishes cluster around it. The current one is a flat disc.

### Science Nexus
Keep the three counter-rotating rings, but each ring becomes a band of data-vault modules with windows. The core becomes a faceted "brain" sphere with glowing seams and cooling towers. Add docking spindles.

### Coordination Center
A hub with eight radial command arms, each ending in a pod. A halo ring of communication relays, and a central spire with a dish crown.

### Black Hole Forge
Keep the accretion swirl. Feeder conveyors bring mined planetoids in, which means rocky chunks in a cradle. Energy taps are tall collector towers pointing at the disk, the frame is a polar ring, and radiator wings fan out.

### Arc Furnace (cracker)
Harvester claws round the world, with mining rigs and glowing cut lines. Collector conveyors run up to an orbital refinery.

### Star Lifter
Polar magnetic funnel rings above each pole, with stacked coil rings. Plasma collector towers and a collector station with tanks. Magnetic field coils are torus segments.

### Bishop Ring
Keep the inner land texture. Add rim walls with a window band, spokes with transit tubes, a hub docking spindle, and an outer hull of panel bands with radiators.

### Memory Spire
A double helix of light around a tall crystalline spine. Ring platforms at intervals, data pylons, and a base station.

### Kugelblitz Reactor
Keep the cages. Make them heavier: emitter modules with heat sinks, and a power trunk to the outside.

### Hyper Relay
A beacon pylon: a tall spindle with a ring and emitter vanes, a comm dish, and solar wings.

### Stable Wormhole
A ring of exotic-matter stabilisers: segmented with blue-glow seams, with anchor pylons at four points, monitoring stations, and traffic lights.

### Gateway
A massive ring of armoured segments, with power couplers glowing at its joints. Docking stations sit at its corners and a traffic-control tower stands above.

### The tech bible's great works
- **Launch Loop:** a loop track with anchor deflector stations, cargo sleds, and power plants.
- **Singularity Decompressor:** twin drill spindles into a black hole, a heavy frame, and pipe runs.
- **Fleet Command Lattice:** a truss lattice of command nodes with docked ships and comm dishes.
- **Cognition Spire:** a needle with stacked ring decks and glowing veins.
- **Ringworld:** rim walls, a land/sea floor, shadow-square ring, and spill mountains.
- **Umbral Fortress:** a dark octagonal citadel with spikes and a violet halo.
- **Gate Bastion:** gate ring towers with gun batteries.
- **System Aegis:** emitter pylons.
- **Birch Shellworld:** nested shells of habitat plates.
- **Penrose Sphere:** a panelled sphere with polar jets.
- **Alderson Disk:** a disk world with rim walls.
- **Glyph Sanctum:** a rune keep with rings.
- **Corona Forge:** heat-shielded foundry pods.
- **Sun Citadel:** a mirror crown.
- **Starforge:** compression arms round a stellar seed.
- **Lucent Throne:** a tiered throne of light.

Each of these gets the secondary-scale pass (modules, tanks, radiators, dock arms and lights) and the accent colour.

### Ships
- **Human:** wedge and spine forms with armour belts. Recessed trenches with lit windows. Stacked superstructure toward the stern. Engine banks with nozzle detail. Red or white accent stripes and hull numbers (as plain panels). Dorsal and ventral turret rows on barbettes. Capital ships carry hangar bays, PD clusters, sensor masts and radiator fins.
- **Organic:** a dark carapace in overlapping plates, segmented. Bioluminescent veins in teal or green along the plate seams. Spines and swept crescent fins. Pod clusters and a glowing heart at the stern. Avoid "tube with stripes".
- **Crystalline:** long faceted spires around a translucent glowing core. Facets in two values (dark smoked crystal and pale lit crystal). Thin crystalline fins, with growth that sweeps back like frost. Bright prism tips.

## Sources
- [Space Colony Art from the 1970s (NSS / NASA Ames)](https://nss.org/settlement/nasa/70sArtHiRes/70sArt/art.html)
- [The High Frontier: Human Colonies in Space](https://en.wikipedia.org/wiki/The_High_Frontier:_Human_Colonies_in_Space)
- [Alexey Pyatov, Горизонт (Horizon) space station, ArtStation](https://www.artstation.com/artwork/Xmg8L)
- [ArtStation search: space station](https://www.artstation.com/search?sort_by=relevance&query=space%20station)
- [Elite Dangerous Wiki: Starport](https://elite-dangerous.fandom.com/wiki/Starport) (Coriolis "inverted skyscrapers, exoskeleton, and defence turrets")
- [PCGamesN: Stellaris megastructures](https://www.pcgamesn.com/stellaris/megastructures)
- Bing image searches for orbital shipyards, star lifting, black hole stations, Nicoll-Dyson beams, jump gates, ringworlds, orbital factories, radio telescope arrays, capital ships, and organic and crystal alien ships (mood only; many results are AI-generated)
