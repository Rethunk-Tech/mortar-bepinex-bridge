#!/usr/bin/env bash
# Builds Release and writes dist/MortarBepInExBridge-<version>.zip in Thunderstore's layout:
# manifest.json, icon.png (assets/icon.png, 256x256) and README.md at the zip root, the plugin dll under plugins/ and
# the startup-timing patcher dll under patchers/. It also writes MortarBepInExBridge-<version>-nexus.zip, the same dlls
# under BepInEx/plugins/ and BepInEx/patchers/, which unpacks into the game folder as Nexus Mods and Vortex expect.
# DIST overrides the output folder (default dist/).
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
csproj="$root/src/MortarBepInExBridge/MortarBepInExBridge.csproj"
patcher="$root/src/MortarBepInExBridge.Patcher/MortarBepInExBridge.Patcher.csproj"
version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$csproj")"
[ -n "$version" ] || {
  echo "no <Version> in $csproj" >&2
  exit 1
}

# Every dotnet invocation leaves an empty dir in TMPDIR, so it gets one that the trap removes.
scratch="$(mktemp -d)"
trap 'rm -rf "$scratch"' EXIT
export TMPDIR="$scratch"
dotnet restore --locked-mode "$csproj"
dotnet restore --locked-mode "$patcher"
dotnet build "$csproj" -c Release --no-restore
dotnet build "$patcher" -c Release --no-restore
stage="$scratch/stage"
mkdir -p "$stage/plugins" "$stage/patchers"
cp "$root/src/MortarBepInExBridge/bin/Release/netstandard2.1/MortarBepInExBridge.dll" "$stage/plugins/"
cp "$root/src/MortarBepInExBridge.Patcher/bin/Release/netstandard2.1/MortarBepInExBridge.Patcher.dll" "$stage/patchers/"
cp "$root/assets/icon.png" "$root/README.md" "$stage/"
sed "s/@VERSION@/$version/" "$root/manifest.template.json" >"$stage/manifest.json"

dist="${DIST:-$root/dist}"
mkdir -p "$dist"
zip="$dist/MortarBepInExBridge-$version.zip"
rm -f "$zip"
(cd "$stage" && zip -q -X "$zip" manifest.json icon.png README.md plugins/MortarBepInExBridge.dll patchers/MortarBepInExBridge.Patcher.dll)
(cd "$dist" && sha256sum "$(basename "$zip")" >"$(basename "$zip").sha256")

nexus="$scratch/nexus"
mkdir -p "$nexus/BepInEx/plugins/MortarBepInExBridge" "$nexus/BepInEx/patchers"
cp "$stage/plugins/MortarBepInExBridge.dll" "$nexus/BepInEx/plugins/MortarBepInExBridge/"
cp "$stage/patchers/MortarBepInExBridge.Patcher.dll" "$nexus/BepInEx/patchers/"
nexus_zip="$dist/MortarBepInExBridge-$version-nexus.zip"
rm -f "$nexus_zip"
(cd "$nexus" && zip -q -X "$nexus_zip" BepInEx/plugins/MortarBepInExBridge/MortarBepInExBridge.dll BepInEx/patchers/MortarBepInExBridge.Patcher.dll)
(cd "$dist" && sha256sum "$(basename "$nexus_zip")" >"$(basename "$nexus_zip").sha256")
echo "$zip"
echo "$nexus_zip"
