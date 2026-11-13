#!/bin/sh
# Static compile check of the runtime + editor code against Unity's own DLLs, without touching the open Editor.
# See docs/DECISIONS.md D-010. Requires Unity to have generated WoodcraftSimulator(.Editor).csproj at least once.
# Usage (Git Bash, from anywhere):  sh tools/static-build.sh
# Does NOT replace Unity compilation (asmdef rules, ILPP) or play testing.
set -e
# Windows-style path (C:/...) under Git Bash so dotnet/MSBuild understand it; plain pwd elsewhere.
ROOT=$(cd "$(dirname "$0")/.." && { pwd -W 2>/dev/null || pwd; })
OUT="$ROOT/tools/.static-build"
rm -rf "$OUT"; mkdir -p "$OUT"; cd "$ROOT"

compile_items() {
    sed "s#^#    <Compile Include=\"$ROOT/#; s#\$#\" />#"
}

RUNTIME=$(find Assets/_WoodcraftSimulator/Scripts -name '*.cs' -not -path '*/Editor/*' | compile_items)
EDITOR=$(find Assets/_WoodcraftSimulator/Scripts/Editor -name '*.cs' | compile_items)

# Replace Unity's explicit file list with the current files; make Library HintPaths absolute.
awk -v items="$RUNTIME" '/<Compile Include=/{ if(!done){print items; done=1}; next } {print}' WoodcraftSimulator.csproj > "$OUT/WoodcraftSimulator.csproj"
awk -v items="$EDITOR" '/<Compile Include=/{ if(!done){print items; done=1}; next } {print}' WoodcraftSimulator.Editor.csproj > "$OUT/WoodcraftSimulator.Editor.csproj"
sed -i "s#<HintPath>Library.#<HintPath>$ROOT/Library/#" "$OUT"/*.csproj
# Domain sources are compiled into the runtime project here; a separate Domain csproj is not needed.
sed -i '/<ProjectReference Include="WoodcraftSimulator.Domain.csproj"/d' "$OUT"/*.csproj

cd "$OUT" && dotnet build WoodcraftSimulator.Editor.csproj -nologo -v q
