#!/usr/bin/env bash
# Builds and tests every game build (-p:Game=<folder below src/MortarBepInExBridge/Games>), then the core build last, so
# the build output left behind is the core one.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"
dotnet restore --locked-mode
# The test project is its own runner; `dotnet test` adds seconds of start-up per run around a sub-second suite.
tests=tests/MortarBepInExBridge.Tests/bin/Release/net10.0/MortarBepInExBridge.Tests.dll
for dir in src/MortarBepInExBridge/Games/*/; do
  game="$(basename "$dir")"
  dotnet build MortarBepInExBridge.slnx -c Release --no-restore -p:Game="$game"
  dotnet "$tests"
done
dotnet build MortarBepInExBridge.slnx -c Release --no-restore
dotnet "$tests"
