#!/bin/bash
# usage: tools/fracrate.sh OUTDIR -> runs the six acceptance seeds and 18 more headless; one line per seed: fractured, how humanity split
cd "$(dirname "$0")/.."
out=$1
tools/hseeds.sh "$out" 20610 7 4242 1 99 2026 11 12 13 14 15 16
tools/hseeds.sh "$out/x" 17 18 19 21 22 23 24 25 26 27 28 29
for f in "$out"/*.txt "$out"/x/*.txt; do
  s=$(basename $f .txt)
  echo "$s $(grep -h '^STRIFESTATS' $f | grep -o 'fractured=[a-z]*') $(grep -h '^SPLITSTATS' $f | grep -o 'split:H[A-Za-z]*=[0-9]*' | tr '\n' ' ') $(grep -h '^FRACTURESTATS' $f | grep -o 'humanSystems.*') $(grep '^D 29' $f | grep -o 'hive=[A-Za-z]*' | tail -1)"
done
