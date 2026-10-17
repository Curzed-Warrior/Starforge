---
name: station-asset
description: Rebuild a Starforge station, megastructure or ship in the live Blender, export its .glb, re-import it into Godot and render it for review. Use when a build script under assets/stations/<id>/ or assets/ships/hum/ changed, or the user asks to rebuild/re-render an asset.
disable-model-invocation: true
argument-hint: <station id, e.g. outpost | ship class, e.g. hum/titan> [camera note]
---

# Rebuild an asset: Blender -> .glb -> Godot -> render

Target: `$ARGUMENTS`

1. **Build in the live Blender** (Blender MCP `execute_blender_code`):
   - station: `exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/<id>/build.py").read())`
   - ship: `exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/<class>.py").read())`
   Every MCP call starts a fresh namespace: re-exec what you need in the same call.
   Calls time out at ~60 s but Blender keeps going. Do NOT re-run on timeout: poll the output file's mtime:
   ```bash
   f=assets/stations/<id>/<id>.glb; t0=$(stat -c %Y "$f"); for i in $(seq 1 60); do [ $(stat -c %Y "$f") -gt $t0 ] && break; sleep 5; done
   ```
2. **Import into Godot** (needed for any new or changed .glb / texture):
   ```bash
   "$(cat .godotpath)" --headless --path . --import 2>&1 | grep -iE "error" | head
   ```
3. **Render for review** in Blender: set `OUT`, `CAM=((x,y,z),(tx,ty,tz))`, `LENS` then
   `exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/render.py").read())` -> `assets/stations/renders/<OUT>.png`.
   For ship reference rounds: `python tools/shipcmp.py <class> <iter>`; stations: `python tools/compare.py`.
4. **Review** the render with the `render-qa` subagent (keeps the image tokens out of the main thread). Fix and repeat.
5. **In-game check** (optional): `tools/shot.sh <name> --mega=<id> --megafocus=<Shown_Name>` -> `shots/<name>.png`.
6. Commit the build script change: `git add -A && git commit -m "<asset>: <what changed>"` (the .glb is gitignored).

Gotchas: `fast_box` frames must be right-handed (x × y = z); emission is boosted x4 in game, keep it low;
build facing +Z then Rx(+pi/2); `kit.restyle()` swaps Hull/HullDark on everything built before it.
