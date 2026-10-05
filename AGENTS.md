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
