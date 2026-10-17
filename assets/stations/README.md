# Station assets

Built in Blender from scripts, so every station can be rebuilt or edited. Units are metres, Z is up, and they're exported as `.glb` for Godot.

Run these inside Blender. Make the textures first, then build any station, then optionally render a preview:

```python
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/make_textures.py").read())   # tiling PBR texture sets
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/outpost/build.py").read())   # or starport / fortress / citadel
OUT = "outpost/hero"; CAM = ((88, -74, 36), (0, 0, -2))
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/render.py").read())          # preview render
```

| Station | Size | Interior | In game |
|---|---|---|---|
| outpost | ~90 m | full, walkable | starbase tier 0 |
| starport | ~430 m tall | exterior only | tiers 1–2 |
| fortress | ~300 m | exterior only | tier 3 |
| citadel | ~700 m | exterior only | tier 4 |

Megastructures built the same way. In the game, `Megastructures.Dress` swaps each one's procedural hull for the asset, fitted to the same size, and keeps the procedural glows and beams. Nodes the game animates take their name from the asset: `r0`–`r2` (nexus), `disc` (gateway), `swirl` (forge), `ring` (antimatter).

| Asset | Structure |
|---|---|
| foundry | Orbital Foundry |
| antimatter | Antimatter Factory |
| nexus | Science Nexus |
| sentry | Sentry Array |
| gateway | Gateway |
| coord | Coordination Center |
| forge | Black Hole Forge, with Matter Decompressors |
| yard | Mega Shipyard (the hull grows inside its cradle) |
| cracker | Arc Furnace (wraps the mined world; unit planet radius 100) |
| lifter | Star Lifter (polar collector rings and funnels; unit star radius 100) |
| bishop | Bishop Ring (land texture generated in its build script) |
| memory | Memory Spire |
| kugel | Kugelblitz Reactor (cages `c0`–`c2` spin) |
| laser | Laser Sail Array (faces -Y, the game's +Z) |
| beam | Nicoll-Dyson Beam (faces -Y) |
| relay | Hyper Relay beacon (the game places three copies) |
| wormhole | Stable Wormhole (faces -Y) |

Only the Planetary Shield and the fold-jump flashes stay procedural: they're pure effects.

- `kit.py` holds the shared pieces: materials, revolve, rings, tunnels, greebles (kitbash detail), window rows, trusses, antennas, lamps and export.
- Any mesh whose name ends in `-col` gets trimesh collision when Godot imports it.
- All faces point outward from their solid, so backface culling is safe.
