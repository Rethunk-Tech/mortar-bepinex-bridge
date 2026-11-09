# Mortar BepInEx Bridge

C# BepInEx 5 plugin (netstandard2.1) that lets Mortar query a running game: the BepInEx loader's companion, as mortar-smapi-bridge is SMAPI's. Compiles against `BepInEx.Core` and `UnityEngine.Modules` (both NuGet), neither shipped; never commit or ship game assemblies.

- Build, test, packaging and release: [HUMANS.md](HUMANS.md).
- Command protocol and discovery file: [README.md](README.md). The framing matches the SMAPI bridge so Mortar's `internal/bridge` client stays one implementation.
- `Protocol` and `BridgeServer` know nothing of the game (`IGameView` is the seam), so tests need no game. Unity objects are main-thread only: the socket threads read copies the plugin keeps current, never `UnityEngine` calls.
- One DLL serves every Mono game, and every game's rows are compiled into it: game code lives in `src/MortarBepInExBridge/Games/<Game>/` behind `IGameHooks` (console, intro steps, version source), and `BuiltGame.For(Application.productName)` picks the row at start-up, so there is no `#if`, no `-p:Game`, and a game with nothing to hook has no folder. A game's tests sit in `tests/.../Games/<Game>/`. One zip, `MortarBepInExBridge`, is published on GitHub releases, Nexus Mods and CurseForge (not Thunderstore, at its team's request).
- The stream overlay is not here: Lethal Company's is its own mod and Valheim's is dropped. This plugin binds only its command port, never an overlay port.
- `src/MortarBepInExBridge.IL2CPP` is the BepInEx 6 IL2CPP plugin (`MortarBepInExBridge_IL2CPP`, net6.0): it compiles Protocol, BridgeServer, ConfigReload, Files and Json from the BepInEx 5 project by link, so edit those there. It references no UnityEngine, so it has no scene, game version, console, perf or log source tags.
- Do not launch the game from agents.
- The version lives in the csproj `<Version>` and `Plugin.Version`; keep them equal.

## Verify

`gate` is the offline gate ([HUMANS.md](HUMANS.md)); `scripts/verify.sh` builds the DLL once and runs the suite against it. Nothing is merged on a red gate.

## Gate budget

`.gate.toml` makes the build restore with `--locked-mode` as CI does, so a drifted `packages.lock.json` fails locally too; the detected build restored unlocked. Budget: 10 s warm, 30 s cold; a cold run is mostly NuGet restore. Build and test are chained because test runs `--no-build`, and nothing else repeats work.

Decided: the OBS overlay keeps accepting `?token=` and sending `Access-Control-Allow-Origin: *`. OBS browser sources need both and the server is loopback-only; do not re-propose a header token or CORS removal.
