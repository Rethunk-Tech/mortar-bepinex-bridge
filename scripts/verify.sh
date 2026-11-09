#!/usr/bin/env bash
# Builds the DLL once, which is the one build for every Mono game, and runs the suite against it. The test project is its
# own runner; `dotnet test` adds seconds of start-up per run around a sub-second suite.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"
dotnet restore --locked-mode
dotnet build MortarBepInExBridge.slnx -c Release --no-restore
dotnet tests/MortarBepInExBridge.Tests/bin/Release/net10.0/MortarBepInExBridge.Tests.dll
