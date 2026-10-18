# Headless runner

The simulation (`src/Sim`) and its self-tests (`src/Tests`) built as a plain .NET 8 console app, with a small stand-in
for the Godot types they use (`GodotShim.cs`: vectors, colours, `Mathf`, `GD.Print`, `OS`, `FileAccess`). No engine,
no GPU: it runs anywhere `dotnet` does, including CI and cloud sessions.

```bash
dotnet build -c Release tools/headless
H=tools/headless/bin/Release/net8.0/Headless.dll
dotnet $H --selftest                    # every self-test (or --selftest=name_part)
dotnet $H --warp=939 --trace --seed=7   # a whole game, with the chronicle and WARSTATS / STRIFESTATS
dotnet $H --warp=939 --trace --quiet --seeds=20610,7,4242,1,99,2026   # stats lines only, several seeds
dotnet $H --warp=300 --savetest         # save, load, save: the two files must match
dotnet $H --warp=600 --techlog          # research pacing
dotnet $H --warp=939 --strifedump       # every civ's grievance, ladder and causes at the end
dotnet $H --warp=939 --savetime         # what a save and a load cost at that size
SF_RES_OVERLAY=/tmp/exp dotnet $H ...   # tuning: /tmp/exp/data/war.json replaces the repository's
```

Run it from inside the repository: `res://` is the repository root, `user://` a per-process temporary folder.

The game itself still runs these modes too (`Main` and this runner share `src/Sim/Runs.cs`). The shim's maths follows
Godot's, but single-precision rounding is not guaranteed bit-identical, so a seed can play out slightly differently
here than in the engine; statistics across seeds are what to compare. A run of one seed is deterministic.
