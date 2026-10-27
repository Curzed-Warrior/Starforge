#!/bin/bash
# usage: tools/seeds.sh OUTDIR [seeds...] -> OUTDIR/<seed>.txt and OUTDIR/times.txt (sequential, so timings are real)
cd "$(dirname "$0")/.."
G="$(cat .godotpath)"
out=$1; shift
seeds=${@:-20610 7 4242 1 99 2026}
mkdir -p "$out"; : > "$out/times.txt"
for s in $seeds; do
  t0=$(date +%s%N)
  "$G" --headless --path . -- --warp=940 --trace --seed=$s > "$out/$s.txt" 2>&1
  t1=$(date +%s%N)
  echo "$s $(( (t1 - t0) / 1000000 ))ms" >> "$out/times.txt"
done
