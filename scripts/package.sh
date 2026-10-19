#!/usr/bin/env bash
# Builds Release once per package (the core build, then one per folder below src/MortarBepInExBridge/Games) and writes,
# for each, dist/<name>-<version>.zip (the layout BepInEx-format installs and Mortar read): manifest.json, icon.png (assets/icon.png, 256x256) and
# README.md at the zip root, the plugin dll under plugins/ and the startup-timing patcher dll under patchers/. <name> is
# MortarBepInExBridge for the core build and MortarBepInExBridge_<Game> for a game build. It also writes
# <name>-<version>-nexus.zip, the same dlls under BepInEx/plugins/ and BepInEx/patchers/, which unpacks into the game
# folder as Nexus Mods and Vortex expect. The package description comes from packages/<Game or core>.txt.
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
dist="${DIST:-$root/dist}"
mkdir -p "$dist"
dotnet restore --locked-mode "$csproj"
dotnet restore --locked-mode "$patcher"
dotnet build "$patcher" -c Release --no-restore

package() {
  local game="$1" name="MortarBepInExBridge" desc="$root/packages/core.txt"
  if [ -n "$game" ]; then
    name="MortarBepInExBridge_$game"
    desc="$root/packages/$game.txt"
  fi
  local description stage nexus zip nexus_zip
  description="$(head -n 1 "$desc")"
  [ -n "$description" ] || {
    echo "no description in $desc" >&2
    exit 1
  }
  dotnet build "$csproj" -c Release --no-restore -p:Game="$game"
  stage="$scratch/$name-stage"
  mkdir -p "$stage/plugins" "$stage/patchers"
  cp "$root/src/MortarBepInExBridge/bin/Release/netstandard2.1/MortarBepInExBridge.dll" "$stage/plugins/"
  cp "$root/src/MortarBepInExBridge.Patcher/bin/Release/netstandard2.1/MortarBepInExBridge.Patcher.dll" "$stage/patchers/"
  cp "$root/assets/icon.png" "$root/README.md" "$stage/"
  sed -e "s/@NAME@/$name/" -e "s/@VERSION@/$version/" -e "s|@DESCRIPTION@|$description|" "$root/manifest.template.json" >"$stage/manifest.json"

  zip="$dist/$name-$version.zip"
  rm -f "$zip"
  (cd "$stage" && zip -q -X "$zip" manifest.json icon.png README.md plugins/MortarBepInExBridge.dll patchers/MortarBepInExBridge.Patcher.dll)
  (cd "$dist" && sha256sum "$(basename "$zip")" >"$(basename "$zip").sha256")

  nexus="$scratch/$name-nexus"
  mkdir -p "$nexus/BepInEx/plugins/MortarBepInExBridge" "$nexus/BepInEx/patchers"
  cp "$stage/plugins/MortarBepInExBridge.dll" "$nexus/BepInEx/plugins/MortarBepInExBridge/"
  cp "$stage/patchers/MortarBepInExBridge.Patcher.dll" "$nexus/BepInEx/patchers/"
  nexus_zip="$dist/$name-$version-nexus.zip"
  rm -f "$nexus_zip"
  (cd "$nexus" && zip -q -X "$nexus_zip" BepInEx/plugins/MortarBepInExBridge/MortarBepInExBridge.dll BepInEx/patchers/MortarBepInExBridge.Patcher.dll)
  (cd "$dist" && sha256sum "$(basename "$nexus_zip")" >"$(basename "$nexus_zip").sha256")
  echo "$zip"
  echo "$nexus_zip"
}

package ""
for dir in "$root"/src/MortarBepInExBridge/Games/*/; do
  package "$(basename "$dir")"
done
