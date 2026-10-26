# Gameplay overhaul: questions

Goal: a more realistic Stellaris, with automation by default and micromanagement available on demand.

Answer however suits you. One word is fine, "skip" is fine, and you can just write the question number with your answer. Each question has my suggested default after the arrow.

## Who you are and how much control you have
1. **Who is the player?** Do you play *as* the civilisation's government (Stellaris), or does the "unseen voice" god layer stay on top of a nation you steer? → Both. You are the nation's leadership, and the god powers become an optional mode or late-game unlock.
2. **One nation or many?** Do you control one empire for the whole game, or can you take over others (a splinter, a vassal, an alien race)? → One, with an option to switch later.
3. **Automation model:** a global "autopilot" toggle per area (economy, research, construction, military, diplomacy, colonisation), with per-system overrides? → Yes. Each area is either AI-run under your priorities ("governor") or under your manual control.
4. **Governors:** when automated, do you set *priorities* (sliders/focuses) that the AI follows, or does it pick freely from its government and faction? → You set priorities; the AI fills in the rest.
5. **Notifications:** if the AI handles everything, what should interrupt you? Wars, first contact, finished megastructures, elections, crises… → An event inbox with pause rules you choose, like Stellaris's.

## Time and pacing
6. **Time model:** keep real-time with speed controls and pause (as now)? → Yes.
7. **Game length:** how long should a "full" game take you in real hours, and in game years (currently 2061 to roughly 3000)? → 15–25 hours, 2061 to about 3500.
8. **Victory conditions:** are there any (galactic hegemony, ascension, the Starforge, survival of the endgame crisis), or is it sandbox only? → Several optional ones, with sandbox allowed.

## Economy (building on what exists: power, mass, industry, deposits)
9. **Resources:** keep the physical set (energy in W, mass in kg, industry in kg/yr, strategic deposits), or add Stellaris-style abstract ones such as influence, unity and consumer goods? → Physical, plus **Influence** (for claims and diplomacy) and **Unity** (for traditions).
10. **Stockpiles and trade:** should resources be stockpiled, traded with other empires and moved between your systems by logistics (as now)? → Yes. Trade routes become visible, real convoys.
11. **Planets and jobs:** Stellaris has districts, buildings and pop jobs on every planet. Do you want planet-level management at all? → Light. Each world gets a "specialisation" (forge world, agri world, research world…) and the AI builds within it. You can open a world to place buildings by hand if you want.
12. **Pops:** abstract population counts (as now), or individual pop groups with species, jobs, happiness and political leanings? → Pop *groups* per world: species × stratum × faction. No individual pops.

## Government, politics and factions
13. **Government types:** right now they are *derived* (Assembly, Directorate, Synthetic Commonwealth, Hive). Should you *choose* government, ethics and civics (Stellaris), keep them emergent, or both? → You choose at start; it can shift through elections, reforms and revolutions.
14. **Elections and factions:** keep the Wayfarers, Choristers and Forgewrights council with elections and schisms, and let you influence them (campaign, suppress, appease)? → Yes, with player actions that cost influence.
15. **Leaders:** add named leaders (admirals, governors, scientists) with traits, as in Stellaris? → Yes, but few: one per fleet, sector and research area.
16. **Stability and unrest:** should unhappy systems riot, strike, secede or start civil wars? → Yes. Secession already exists as "splinters"; expand it.

## Expansion and territory
17. **Borders:** Stellaris-style influence borders and claims, or just "systems you own" (as now)? → Borders from colonies and starbases, with claims for war goals.
18. **Colonisation:** do you pick each colony target, or does the AI expand under your priorities (as now, with seed ships)? → The AI proposes and you approve, or set it to auto.
19. **Habitability:** should species have preferred planet types (as in Stellaris) that limit where they can live without habitats or terraforming? → Yes. That makes habitats and terraforming matter.
20. **Map scale:** the galaxy is huge (hundreds of colonies late). Should you manage *sectors* (groups of systems with a governor) rather than individual systems? → Yes. Sectors are the main unit of management.

## Military
21. **Fleet control:** do you command fleets directly (move, attack, retreat, stances), or set war goals and let admirals execute? → Both. Strategic orders by default, direct control on demand.
22. **Ship design:** a Stellaris-style ship designer (slots, weapons, components) using the new true-scale hulls? → Yes, with auto-design on by default.
23. **Combat view:** watch battles in 3D at true scale, as cinematic auto-resolve? → Battles play out in the system view and get a cinematic camera; the outcome is simulated. The player may take command (stage 6): pause, order squadrons or single ships, same numbers as auto-resolve; setting under Policies > Battles.
24. **Ground combat and invasion:** needed? → Light: transport armies, siege, occupation.
25. **War score and peace:** Stellaris-style war goals, exhaustion and peace deals? → Yes.

## Diplomacy and other empires
26. **AI empires:** should alien empires play by the *same rules* as you, with the same economy, construction and research? → Yes. It's more realistic and makes the galaxy feel alive.
27. **Diplomacy depth:** treaties, federations, vassals, trade deals, a galactic council? → Treaties, trade deals and federations first; a galactic community later.

## Research
28. **Research model:** keep the current tree (about 400 techs, three slots with drawn cards, auto-picked), or allow free choice? → Keep the cards with auto-pick, plus a manual pick and queue when you want it.

## Presentation and scope
29. **UI style:** Stellaris-like layout (top resource bar, outliner on the right, empire screens) skinned to our look? → Yes. A top resource bar, a right-hand outliner (sectors, fleets, projects), and tabbed empire screens.
30. **The god layer:** what happens to the voice powers (Comet, Flare, Dream, Smite…) and the "Faith" mechanic? → Optional "Ascended" game mode, or a late-game path where your civilisation *becomes* the voice.
31. **Priority order:** which do you want first? (a) top bar, outliner and control framework; (b) planets, pops and jobs; (c) fleets and war; (d) diplomacy; (e) government and politics. → a, then e, then c, then b, then d.
32. **Saves and modding:** keep the data in JSON (techs.json, progression.json) so it's moddable, as Stellaris is? → Yes. Move more rules into data files.

---
## Answers (2026-10-17)

| # | Topic | Decision |
|---|---|---|
| 1 | Role | You lead the nation; the god powers are always available too (see 30) |
| 2 | Nations | One empire, with the option to switch to a splinter or vassal later |
| 3 | Automation | Per-area auto/manual toggles with per-system overrides |
| 4 | Governors | You set priorities; the AI fills in the details |
| 5 | Alerts | Wars and battles, first contact and diplomacy, big milestones, crises and unrest |
| 6 | Time | Real-time with pause |
| 7 | Length | 15–25 h, 2061 to about 3500 |
| 8 | Victory | Optional wins, sandbox allowed |
| 9 | Resources | **Physical, plus the Stellaris resources including Gigastructures and ACOT ones, dropping any that don't fit the realistic game** |
| 10 | Trade | Stockpiles, with trade and logistics as visible convoys |
| 11 | Planets | A specialisation per world; the AI builds within it; optional hand placement |
| 12 | Pops | Pop groups: species × stratum × faction |
| 13 | Government | Chosen at start; can shift through elections, reforms and revolutions |
| 14 | Factions | Keep the council and elections; player actions cost influence |
| 15 | Leaders | A few: admirals, governors, scientists, with traits |
| 16 | Unrest | Riots, secession, civil war |
| 17 | Borders | **Influence borders and claims, PLUS hyperlane chokepoints** |
| 18 | Colonies | The AI proposes, you approve (or full auto) |
| 19 | Habitability | Species preferences; habitats and terraforming get around them |
| 20 | Scale | Sectors with governors |
| 21 | Fleets | Strategic orders, with direct control on demand |
| 22 | Designer | Stellaris-style designer, auto-design on by default |
| 23 | Combat | 3D in the system view, cinematic camera, simulated outcome |
| 24 | Ground | **None for now; a future expansion** |
| 25 | War | Claims, war goals, exhaustion and peace deals |
| 26 | AI | Same rules as the player |
| 27 | Diplomacy | Treaties and federations first, a galactic council later |
| 28 | Research | **Driven by the empire's needs and state, like the real world: war speeds military research, many wealthy companies speed trade and logistics, and so on.** You can still steer it. |
| 29 | UI | Stellaris layout in our skin |
| 30 | God layer | **Always on** |
| 31 | Order | UI and control framework first; then government and politics, war, planets, diplomacy |
| 32 | Modding | Rules in JSON data |

---
## Progress

### Phase (a): UI and control framework (2026-10-17)
- **Policy** (`src/Sim/Policy.cs`): every civ has one. Aliens stay on the council, so they play by the same rules.
  - **Areas:** Economy, Research, Construction, Colonisation, Military, Diplomacy. Each is run by the council (automatic) or by you (manual).
  - **Manual:** the council still does the legwork, but each decision becomes a proposal.
    - Research: the hand of three cards.
    - Construction: each new great work.
    - Colonisation: each seed-ship launch.
    - Military: declaring war.
    - Diplomacy: peace offers.
    - Economy: you set the works share.
  - **Priorities** (×0–2): six great-work kinds and three research areas. The council follows them in both modes.
  - **Per-system overrides:** the works share, settable by hand per system.
- **Alerts:** War, Battle, Contact, Decision, Milestone, Crisis, Unrest. Each has a pause rule (by default War, Contact, Decision and Crisis pause).
- **UI:**
  - Top resource bar.
  - Right-hand outliner: decisions, wars, systems, great works, research, ships.
  - Empire screen (E): Overview, Policies, Systems, Inbox. I opens the inbox.
- **Saved:** policy and overrides. Waiting proposals are raised again after loading.
- **Test flags:** `--manual=all|Research,...`, `--decide` (accept every proposal at once), `--open=empire|policies|systems|inbox`.

Next: (e) government and politics. Choose government, ethics and civics at the start, add Influence and Unity, and give faction actions an influence cost.
