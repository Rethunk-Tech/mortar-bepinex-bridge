#!/usr/bin/env bash
# Builds and tests every game build (-p:Game=<folder below src/MortarBepInExBridge/Games>), then the core build last, so
# the build output the gate's own test step reuses is the core one.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"
dotnet restore --locked-mode
for dir in src/MortarBepInExBridge/Games/*/; do
  game="$(basename "$dir")"
  dotnet build MortarBepInExBridge.slnx -c Release --no-restore -p:Game="$game"
  dotnet test --solution MortarBepInExBridge.slnx -c Release --no-build -p:Game="$game"
done
dotnet build MortarBepInExBridge.slnx -c Release --no-restore
