#!/usr/bin/env bash
# Builds Release and writes dist/MortarBepInExBridge-<version>.zip in Thunderstore's layout:
# manifest.json, icon.png (assets/icon.png, a 256x256 placeholder), README.md and the plugin dll at the zip root.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
csproj="$root/src/MortarBepInExBridge/MortarBepInExBridge.csproj"
version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$csproj")"
[ -n "$version" ] || { echo "no <Version> in $csproj" >&2; exit 1; }

dotnet build "$csproj" -c Release
stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT
cp "$root/src/MortarBepInExBridge/bin/Release/netstandard2.1/MortarBepInExBridge.dll" "$root/assets/icon.png" "$root/README.md" "$stage/"
sed "s/@VERSION@/$version/" "$root/manifest.template.json" >"$stage/manifest.json"

mkdir -p "$root/dist"
zip="$root/dist/MortarBepInExBridge-$version.zip"
rm -f "$zip"
(cd "$stage" && zip -q -X "$zip" manifest.json icon.png README.md MortarBepInExBridge.dll)
(cd "$root/dist" && sha256sum "$(basename "$zip")" >"$(basename "$zip").sha256")
echo "$zip"
