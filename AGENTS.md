# Mortar BepInEx Bridge

C# BepInEx 5 plugin (netstandard2.1) that lets Mortar query a running game: the BepInEx loader's companion, as mortar-smapi-bridge is SMAPI's. Compiles against `BepInEx.Core` and `UnityEngine.Modules` (both NuGet), neither shipped; never commit or ship game assemblies.

- Build, test, packaging and release: [HUMANS.md](HUMANS.md).
- Command protocol and discovery file: [README.md](README.md). The framing matches the SMAPI bridge so Mortar's `internal/bridge` client stays one implementation.
- `Protocol` and `BridgeServer` know nothing of the game (`IGameView` is the seam), so tests need no game. Unity objects are main-thread only: the socket threads read copies the plugin keeps current, never `UnityEngine` calls.
- Keep the plugin generic: it must work in any BepInEx 5 Mono game, with no Lethal Company types.
- Do not launch the game from agents.
- The version lives in the csproj `<Version>` and `Plugin.Version`; keep them equal.

## Verify

`gate` is the offline gate ([HUMANS.md](HUMANS.md)). Nothing is merged on a red gate.

## Gate budget

`.gate.toml` makes the build restore with `--locked-mode` as CI does, so a drifted `packages.lock.json` fails locally too; the detected build restored unlocked. Measured 2026-10-09 with `gate --profile` at load 2.6 to 3 (CPU is the evidence): warm 2.1 to 2.8 s wall and about 2 to 2.6 CPU-s; cold (a clone without `bin/`, `obj/` or `dist/`, throwaway `NUGET_PACKAGES`) 10.2 s wall and 7.8 CPU-s, 9.5 s of it restore (NuGet download) and build. Within the 10 s warm and 30 s cold budgets; build and test are chained because test runs `--no-build`, and nothing else repeats work.
