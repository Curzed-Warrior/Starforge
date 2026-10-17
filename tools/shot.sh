#!/bin/bash
# usage: tools/shot.sh name [game args...]  -> shots/name.png
cd "$(dirname "$0")/.."
name=$1; shift
GODOT="$(cat .godotpath)"
"$GODOT" --path . --resolution 1600x900 -- --shot="$(cygpath -w "$PWD/shots/$name.png")" "$@" 2>&1 | grep -v -E "^\s*$|Godot Engine|Vulkan|OpenGL|^$" | grep -i -E "error|fps|exception|warn" | head -20
