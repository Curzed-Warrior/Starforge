#!/bin/bash
# usage: tools/hseeds.sh OUTDIR [seeds...] -> headless runner, all seeds in parallel (statistics only: timings are not real)
cd "$(dirname "$0")/.."
H=tools/headless/bin/Release/net8.0/Headless.dll
out=$1; shift
seeds=${@:-20610 7 4242 1 99 2026}
mkdir -p "$out"; rm -f "$out"/*.txt
for s in $seeds; do dotnet $H --warp=940 --trace --seed=$s > "$out/$s.txt" 2>&1 & done
wait
