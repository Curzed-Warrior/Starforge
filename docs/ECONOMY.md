# Economy and construction

Code: `src/Sim/Construction.cs` (economy, governments, great-works table), HUD lines in `src/View/Hud.cs` (system panel).

## The loop (Stellaris-shaped, physically scaled)

| Stellaris | Starforge | Unit |
|---|---|---|
| Energy credits | **Power** (planet + swarm capture) | W |
| Minerals | **Stock**: refined mass from mining hubs and star lifting | 1 unit = 1.1×10²⁴ kg (`Defs.SwarmKg`) |
| Alloys / industry jobs | **Industry**: what the foundries can fabricate | kg/yr |
| Pops and jobs | **Workforce**: bio population + digital minds (×8) | multiplier 0.4–3 |
| Stability | **Stability**: crises, wars, crowding, splinters, scaled by government | 0.3–1.2 |
| Authority | **Government**, read from what the civ has become | see below |
| Megastructure stages | **Great works**: mass, power bill, minimum assembly time | kg, J, years |
| Build slots | **Crews**: 1 + era/2 + government + log(pop) | count |

`Industry = 0.02 · PFrac · Foundries · M.Foundry · Boost · SwarmKg · Workforce · Stability · Gov.Industry`.
It is the same foundry capacity that raises the swarm, so it starts at a few million tonnes a year in 2061
(lunar mass drivers) and reaches about 10²⁵ kg/yr round a finished star. The swarm's growth is capped separately by `Colony.SwarmPace`.

Each step a system:
1. Picks what to build: raised-by-the-voice first, then works already under way, then what the ruling faction wants most, smallest first, up to the crew count.
2. Splits `Industry × WorksShare` among them by priority. The faction gives a bonus to its kind of work, crowding boosts habitats, and war boosts military works.
3. Caps each work by its matter (`budget / mass`), its assembly floor (`1 / MinYears`) and, for energy works, a tenth of the system's power.
4. Takes the matter out of Stock, **smallest need first**, so a Jupiter-mass ringworld can't starve everything else.

With the gate network, finished systems pool 5% (Gate) or 15% (GateNet) of their works industry and galactic power into the capital. That pooling is what makes the ringworld possible at all.

## Where works are built (one star, one job)

- **Everywhere it qualifies:** foundry, shipyard (capital, or era ≥ 2 or pop > 1 B), planetary shield (living worlds), hyper relay, gateway.
- **Corona works:** lifter, corona forge, Sun Citadel. One per star.
- **Wraps:** ringworld, Alderson disk. They need a quiet sunlike star (0.35–2.6 L☉), never Sol, at most 3 settled worlds, and no corona or black-hole work, because lifting or mining the star would wreck the habitat.
- **Black-hole works:** forge, Penrose sphere, decompressor. They share one hole, away from home, at the most massive star available.
- **Solitary works:** Starforge, Birch shellworld. Each takes a system to itself.
- **Beam:** wherever a swarm is at least half complete and nothing wraps the star.
- **Everything else** (minds, war works, transit, kugelblitz, antimatter, furnace, Bishop ring) is the capital's.

A work with no suitable system waits ("needs a suitable star") until one is colonised. Sites are replanned yearly, but a work already begun never moves.

## Logistics: how many systems speed things up

Every system sends up to half of its works budget, and up to half of its stockpile, to the civilisation's other building sites. How much reaches each site depends on the link:

| Link | Share of the sender's works budget |
|---|---|
| No interstellar ships | 0 (each star builds alone) |
| Sublight freighters | 1% / (1 + d/4) |
| Fold drive | 6% / (1 + d/100) |
| Gateways at both ends | 15% |
| Gateways + gate network | 25% |
| Hyper relay at the sender | +3 points |
| Major shipyard at the sender | ×1.25 (prefabricated sections) |

d is in galaxy units (~50 ly). Power is shared the same way, which pays the ringworld's spin bill.

Typical run:
- **Black Hole Forge at Sirius:** begun 2340 with 9×10¹⁵ kg/yr of local industry, plus 4×10²² kg/yr shipped in. Done 2410.
- **Ringworld at Alpha Centauri:** 2431–2745. Its own industry is 3×10²⁴ kg/yr and the levy brings in 5×10²⁴.

Without the levy, the same ringworld estimated 700 000 years.

## What finished works give back

| Effect | Works |
|---|---|
| Industry | foundry ×1.15, shipyard ×1.2, launch loop ×1.1, antimatter ×1.1, corona forge ×1.5, Starforge ×1.3 |
| Power | kugelblitz +0.5% of the star, black hole forge +0.3 L☉, Penrose sphere +1 L☉, decompressor +0.1 L☉ |
| Mining | arc furnace ×1.3 |
| Star lifting | the tech alone runs at 35%; the Star Lifter runs at 150% |
| Research | sentry +10%, nexus +25%, memory +30%, spire +50%, coordination center +10% |
| Housing | Bishop ring +3 B, ringworld +100 000 B, Birch +400 000 B, Alderson +1 000 000 B |
| Stability | shields, aegis, sentry, coordination center, throne, sanctum, citadels |
| Crews | coordination center +1, fleet lattice +1 |

## Governments

| Government | When | Industry | Works share | Crews | Unrest |
|---|---|---|---|---|---|
| Assembly | default | ×1.0 | 22% | +0 | ×1.0 |
| Directorate | Ruthless ethic or Imperial | ×1.15 | 32% | +1 | ×1.25 (hardship hits harder) |
| Synthetic Commonwealth | >60% digital or S7 Digital Ascension | ×1.35 | 35% | +1 | ×0.7 |
| Hive Consensus | S8 Hive Consensus | ×1.5 | 45% | +2 | none |

Works share rises by 30 points once the swarm is finished. Ruling faction:
- Forgewrights favour industrial and stellar works.
- Choristers favour mind works and neglect war.
- Wayfarers favour transit and habitats.

## Great works: mass, power, floor

Masses are order-of-magnitude estimates from geometry × areal density, anchored where the literature gives a number.

| Work | Mass (kg) | Power bill (J) | Floor (yr) | Basis |
|---|---|---|---|---|
| Launch loop | 5e9 | | 8 | Lofstrom loop, ~2000 km of rotor and sheath |
| Laser sail array | 1e11 | | 10 | 60 km thin-mirror array |
| Orbital foundry | 1e12 | | 12 | ~100 Stanford tori (each ~10⁷ t, mostly shielding) |
| Shipyard | 1e13 | | 15 | |
| Planetary shield | 3e13 | | 20 | |
| Sentry array | 3e13 | | 25 | 1800 km dish at ~3 kg/m² |
| Hyper relay, antimatter factory | 1e14 | | 15–20 | |
| Coordination center | 1e15 | | 30 | |
| Kugelblitz reactor | 1e15 | 1e28 | 40 | the black hole's own mass-energy, E = mc² |
| Arc furnace, nexus, lattice | 1e16 | | 20–40 | |
| Bishop ring | 5e16 | | 80 | 1000 km radius × 500 km: 3×10¹² m² of floor, soil, ~10 t/m² of air, CNT hoop. Soil and biosphere set the floor |
| Gateway | 1e17 | 1e30 | 40 | |
| Star lifter | 5e18 | | 60 | Criswell; Scoggins & Kipping lift ~5×10¹⁹ kg/yr |
| Memory spire, Lucent Throne, wormhole | 1e19 | wormhole 1e33 (a month of the Sun) | 80–100 | |
| Nicoll-Dyson beam, spire, decompressor | 1e20 | | 60–100 | the swarm already exists; this is the lens and steering |
| Corona forge, Sun Citadel | 1e21 | | 100–150 | |
| Penrose sphere | 2e21 | | 200 | 40 000 km shell at ~10⁵ kg/m² |
| Black hole forge, System Aegis | 1e22 | | 120–200 | |
| Starforge | 1e26 | 1e35 | 1000 | |
| Ringworld | 2e27 | 1e39 | 500 | Niven: one Jupiter. Spin to 1200 km/s ≈ 10³⁹ J, a hundred thousand years of the Sun's output |
| Birch shellworld | 1e28 | | 1000 | tens of Jupiters |
| Alderson disk | 1e30 | | 2000 | exceeds the Sun's mass; effectively a galactic-era dream |

For scale, Armstrong and Sandberg's *Eternity in Six Hours* (2013) has Mercury (3.3×10²³ kg) taken apart for a Dyson swarm in about 31 years by exponential self-replication. That is the industry the game's foundries reach around the Swarm era.

## Typical run (seed 0, warp 900 yr)

- **Shipyard**: 2087–2146 (industry ~4×10⁹ kg/yr at start).
- **Foundry**: 2137–2149.
- **Star lifter**: 2237–2297.
- **Bishop ring**: 2338–2402.
- **Black hole forge**: 2344–2427.
- **Penrose sphere**: 2468–2551.
- **Ringworld**: begun 2433, 32% by 2961. It is mass-bound because Jupiter's worth of matter has to be mined.
- **Birch shellworld and Alderson disk**: barely started.

Check with:

```bash
SF_TRACE=1 tools/shot.sh x --warp=900 --techlog --trace
```

This prints MEGA begin/done lines and a BUILT summary.

## Sources
- [Bishop ring (habitat)](https://en.wikipedia.org/wiki/Bishop_ring_(habitat)): dimensions only, no published mass.
- [Stanford torus](https://en.wikipedia.org/wiki/Stanford_torus): ~10 million tons, 95% shielding.
- [Eternity in six hours (Armstrong & Sandberg)](https://www.practicalethics.ox.ac.uk/node/174891): Mercury to swarm.
- [Ringworld (Larry Niven wiki)](https://larryniven.fandom.com/wiki/Ringworld): ~2×10²⁷ kg.
- [Alderson disk](https://en.wikipedia.org/wiki/Alderson_disk): more than a solar mass.
- [Star lifting](https://en.wikipedia.org/wiki/Star_lifting) and [Lazarus Stars (Scoggins & Kipping)](https://arxiv.org/abs/2210.02338): lifting rates.
- [Launch loop](https://en.wikipedia.org/wiki/Launch_loop) and [Orbital ring](https://en.wikipedia.org/wiki/Orbital_ring).

## System resources (deposits)

`src/Sim/Resources.cs`. Each star's system is surveyed once, from its actual generated worlds:

| Deposit | From | Used for |
|---|---|---|
| Energy | star luminosity | power, colony choice |
| Metals | rocky worlds, lava worlds, moons, belts (×3) | colony and site choice |
| Volatiles | ices, oceans, gas and ice giants | habitats need air and water. Built in a volatile-poor system they run at 40% pace, or 80% if a volatile-rich system ships to them |
| Living worlds | | colony choice |
| Exotic Gases | gas giants, 30% chance (Sol always: Jupiter's ³He) | kugelblitz, antimatter factory |
| Rare Crystals | rocky systems, 20% | Nicoll-Dyson beam, shield, aegis, sanctum, citadels |
| Dark Matter | 5% (60% at special sites) | gateway, relay, wormhole, Umbral fortress |
| Neutronium | white-dwarf companions (Sirius B, Procyon B), giant stars, 3% elsewhere | black-hole works, ringworld, Birch, Alderson, Starforge |

Strategic deposits count empire-wide, as in Stellaris: owning one system with Neutronium unlocks it for every ringworld. Without the deposit a work is built with substitutes, at 25% pace.

Effect on choices:
- Seed ships pick targets by distance ÷ (0.5 + `Deposits.Value`), so rich, sunny, watery, living or rare systems are settled first.
- Great works prefer sites holding the strategic deposit they need. Habitats prefer metal-rich, volatile-rich, living systems.

## State of emergency

Evaluated yearly (`Colony.EvaluateEmergency`). It is declared when any of these holds:
- the civ is at war with someone at least 60% as strong;
- it lost a system in the last 15 years;
- there is fighting at home.

It is lifted after 8 years with no threat. While it lasts:
- **Critical works** (war works, shields, aegis, sentry, shipyards) are the only ones built. Everything else pauses where it stands.
- **Spending:** 95% of every system's industry goes to great works, and the swarm slows to 10%.
- **Whole-empire pooling:** every system that can reach a critical site at all sends 100% of its works budget and its entire stockpile there.
- **Crash programme:** assembly floors drop to 40% (round-the-clock work), crews +3, war strength ×1.2.
- **Remaining limits:** labour on site (`LabourKg`, doubled under mobilisation) and the shortened floor. Resources stop being the bottleneck. The HUD says "Limited by hands on site" when labour binds.

Test with:

```bash
SF_EMERGENCY=1 SF_TRACE=1
```

This forces an emergency for 2300–2340.
