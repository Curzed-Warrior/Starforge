# Starforge

PC space-civ god game (successor to the phone game Project Kardashev). Godot 4.7.2 .NET, C#. Starts on Earth; graphics
are the selling point; autonomous civ with optional micro (Stellaris-like, no forced micromanagement).

## Layout
- `src/Sim/` simulation (Civ, Colony, Construction, Policy, Research, War, SaveGame). `src/View/` rendering, HUD, camera.
- `shaders/` Godot shaders. `data/` techs.json, progression.json. `docs/` design docs, blueprints.
- `assets/stations/<id>/build.py`, `assets/ships/hum/<class>.py`: Blender build scripts -> `.glb` the game loads.
  `kit.py` / `kit2.py` / `assets/ships/shipkit.py` are the shared Blender helpers.
- `tools/blueprints/`: the outpost drawing set (ISO drafting in matplotlib, DXF via ezdxf).
  `outpost_design.py` is the source of truth for the outpost interior and writes `docs/blueprints/openings.json`,
  which the exterior `assets/stations/outpost/build.py` reads (one build: every window outside is a room inside).

## Run / verify
- Game screenshot: `tools/shot.sh NAME --focus=Earth|Sun|Galaxy|<body> --dist= --yaw= --pitch= --year=` -> `shots/NAME.png`.
- Godot binary path is in `.godotpath`. After any new or changed `.glb`/texture:
  `"<godot>_console.exe" --headless --path . --import`.
- Blender: live instance via the Blender MCP. Each MCP call gets a fresh Python namespace (re-exec scripts) and calls
  time out at ~60 s while Blender keeps working: poll the output file's mtime instead of re-running.
- Blueprints: `python tools/blueprints/outpost_set.py [SHEET...]` (no args = full set + DXF + CSV).
- Without Godot (CI, cloud): `dotnet build -c Release tools/headless`, then `dotnet tools/headless/bin/Release/net8.0/Headless.dll
  --selftest` / `--warp=939 --trace --seed=N` / `--warp=300 --savetest` (see tools/headless/README.md).

## Scale and conventions
- True scale: 1 unit = Earth radius in the game (`View/Frame.cs` floating origin + scaled space). Assets are in metres.
- Godot is single precision: use `sv.TrueStar` / `bv.TrueView`, never `GlobalPosition`, as true positions.
- Game brightens emissive materials x4: keep emission low in Blender.
- Station facing: build facing +Z then Rx(+pi/2). `fast_box` frames must be right-handed or faces invert.
- Outpost Rev B: generated gravity, 1 g down = -Z everywhere; ring Deck A FFL z -0.2 (= spoke floor = spine hub deck),
  Deck B FFL z -3.5; curved ring faces are walls with one window per 3.75 deg frame bay.

## Gotchas
- Don't write Python containing `\n` escapes through bash heredocs: they become real newlines. Use files / the Edit tool.
- Lines with non-ASCII (·, ×, —) fail to match in Python heredoc edits on Windows: use the Edit tool.
- Reading full A1 sheets / renders is expensive: crop or use the render-qa subagent.
- Generated output is gitignored (`.godot/`, `shots/`, renders, `*.glb`); rebuild it from the scripts.
