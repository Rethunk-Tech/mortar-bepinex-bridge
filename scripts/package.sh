#!/usr/bin/env bash
# Builds Release and writes dist/MortarBepInExBridge-<version>.zip in Thunderstore's layout:
# manifest.json, icon.png (assets/icon.png, 256x256) and README.md at the zip root, the plugin dll under plugins/.
# DIST overrides the output folder (default dist/).
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
csproj="$root/src/MortarBepInExBridge/MortarBepInExBridge.csproj"
version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$csproj")"
[ -n "$version" ] || { echo "no <Version> in $csproj" >&2; exit 1; }

# Every dotnet invocation leaves an empty dir in TMPDIR, so it gets one that the trap removes.
scratch="$(mktemp -d)"
trap 'rm -rf "$scratch"' EXIT
export TMPDIR="$scratch"
dotnet build "$csproj" -c Release
stage="$scratch/stage"
mkdir -p "$stage/plugins"
cp "$root/src/MortarBepInExBridge/bin/Release/netstandard2.1/MortarBepInExBridge.dll" "$stage/plugins/"
cp "$root/assets/icon.png" "$root/README.md" "$stage/"
sed "s/@VERSION@/$version/" "$root/manifest.template.json" >"$stage/manifest.json"

dist="${DIST:-$root/dist}"
mkdir -p "$dist"
zip="$dist/MortarBepInExBridge-$version.zip"
rm -f "$zip"
(cd "$stage" && zip -q -X "$zip" manifest.json icon.png README.md plugins/MortarBepInExBridge.dll)
(cd "$dist" && sha256sum "$(basename "$zip")" >"$(basename "$zip").sha256")
echo "$zip"
