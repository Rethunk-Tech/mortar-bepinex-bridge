<h1 align="center">Mortar BepInEx Bridge</h1>

<div align="center">

![Licence](https://img.shields.io/badge/licence-AGPL--3.0-blue)

</div>

---

The BepInEx loader's companion for [Mortar](https://github.com/Rethunk-Tech/mortar), the mod manager: a BepInEx 5 plugin that lets Mortar ask a running game which plugins are loaded. It is the BepInEx counterpart of [mortar-smapi-bridge](https://github.com/Rethunk-Tech/mortar-smapi-bridge) and works in any Unity Mono game that runs BepInEx 5; Lethal Company is the one Mortar tests it with. Mortar starts the game through its launcher and has no console to type into, so this plugin provides a query channel.

## Builds

One plugin per game, one zip each on GitHub releases, Nexus Mods and CurseForge: `MortarBepInExBridge_LethalCompany` (intro skip, stream overlay, the menu's game version), `MortarBepInExBridge_Valheim` (stream overlay, console), and the core `MortarBepInExBridge` for every other BepInEx 5 Mono game (query, perf, startup timing, engine splash skip). They share one plugin GUID and state file, so install exactly one. The Lethal Company, Valheim and overlay details below apply to the build that names them.

## Quick start

```sh
dotnet build -c Release && dotnet test --solution MortarBepInExBridge.slnx -c Release
```

Prerequisites, packaging and install: [HUMANS.md](HUMANS.md).

## Highlights

- Loopback TCP channel: one connection per command, authenticated with a per-run token
- Writes `mortar-bepinex-bridge.json` (`port`, `token`) in BepInEx's `config` folder and deletes it on exit
- Commands: `console <line>` (runs a line in the game's console, Valheim first), `ping`, `status` (game version, scene, loaded plugins) and `plugins` (structured list); `perf` and `perf start` (frame times, memory, and each plugin's main-thread time per frame, exclusive of nested timed calls)
- Startup timing on a launch Mortar measures: a preloader patcher (`patchers/MortarBepInExBridge.Patcher.dll`) times each plugin's load (constructor and Awake) and the phases to the game's title scene, and writes `startup/<utc>.json` in the Mortar profile. Mortar asks for it by leaving `startup/.measure-launch` (holding the title scene's name) beside the profile's `BepInEx` folder; the launch consumes it, and a launch without it is not timed. Each plugin's row also carries `patchMs` (time its assembly spent in Harmony `Patch` calls, the outermost call timed; part of its load when it patches in Awake) and `sampleMs` (the plugin's main-thread time from the first frame to the title scene: its patch methods and its own Update, LateUpdate and FixedUpdate, the same exclusive accounting `perf` uses; Mono cannot read another thread's stack, so this is instrumented, not stack-sampled), with `sampledOtherMs` for the rest of that window. The patcher also sets `MORTAR_MEASURED_LAUNCH=1` in the game's process, which is what enables `perf`
- Optional loopback stream overlay for Mortar's OBS page: `GET`/`HEAD`/`OPTIONS` `/state`, off unless Mortar's setting is on (below)
- Skip the intro, only when Mortar asks: Mortar's "Skip the intro" setting writes `intro` to `startup/skip-intro` in the profile folder, and the plugin stops the engine's own splash screen in any game (asked from a background thread until the first scene loads, since the engine runs no script frame before then) and starts Lethal Company without its boot animation or cold open, while the Online or LAN choice stays the player's. Mortar's crash check writes `menu`, and `MORTAR_SKIP_INTRO=1` in the game's environment (Mortar's sandbox and regress runs) means the same: the plugin also picks LAN, so the game reaches the main menu unattended. Without a request the plugin changes nothing in the game
- Log sources: every message logged through Unity's logger starts with `⟦src=Type::Method@<IL offset>;obj=<hierarchy path>⟧ ` (the calling method and the object the call named), which Mortar's Console takes off the line; call sites listed in `startup/hidden-log-sources` (`source<TAB>object` lines, an empty object meaning every object) are dropped before they reach the log

## Mod page description (Nexus Mods and CurseForge)

**Mortar BepInEx Bridge** lets the Mortar mod manager see which BepInEx plugins a running game has loaded, so it can show their state without you reading `LogOutput.log`. It adds no gameplay and changes no game content.

- **Install:** Mortar installs and updates it automatically. Install it by hand only if you want to query the game from your own tool.
- **Requires:** BepInEx 5, from your game's BepInExPack.
- **Safety:** it listens on `127.0.0.1` only (never the network, never the internet) and every request needs a random token stored in a file only your user can read. It makes no outgoing connections. Under Proton the game is a Windows build, which has no file modes to restrict, so the token file is as private as the Steam library folder holding the prefix.
- **Source and licence:** [github.com/Rethunk-Tech/mortar-bepinex-bridge](https://github.com/Rethunk-Tech/mortar-bepinex-bridge), AGPL-3.0. Built on BepInEx (LGPL-2.1), which is referenced, not redistributed.

## How it works

On launch the plugin listens on `127.0.0.1` (port chosen by the OS) and writes `mortar-bepinex-bridge.json` in `BepInEx/config`:

```json
{"port":51234,"token":"<64 hex chars>"}
```

The file is restricted to the current user (mode 0600 on Linux and macOS) and deleted on exit. Every request must carry the random 32-byte token, compared in constant time. The config folder is used rather than the plugin's own, because a mod manager chooses where plugins live but BepInEx always has one config folder.

## Protocol

The same framing as the SMAPI bridge: one connection per command. The client sends two LF-terminated lines, the token and the command (limits 128 and 4096 bytes), and reads one line back:

| Command | Reply |
| --- | --- |
| `ping` | `ok` |
| `status` | `ok {"gameVersion":"...","gameVersionSource":"game","scene":"...","plugins":[{"guid":"...","version":"...","location":"Pack/x.dll"}]}`; `location` is the plugin DLL's path below `BepInEx/plugins` (left out for one elsewhere), which tells apart two copies of one GUID and version; `gameVersion` is the version the game shows (`gameVersionSource` `game`; Lethal Company: `v81` from `GameNetworkManager.gameVersionNum`), else Unity's `Application.version` (`unity`) |
| `plugins` | `ok [{"guid":"...","name":"...","version":"...","location":"..."}]` |
| `perf` | `ok {"measured":false}` until measuring starts (from the first frame on a measured launch, else from the first `perf start`); then `ok {"measured":true,"instrumented":...,"seconds":...,"frames":...,"fps":...,"frameMs":{"avg","p50","p95","p99","max"},"monoUsedBytes":...,"monoHeapBytes":...,"gcCollections":...,"baseline":{"frameMs","modsMs","withoutModsMs"},"plugins":[{"guid","msPerFrame","p95Ms","peakMs","share","callsPerFrame","patches","transpilers"}],"patchOwners":{"<harmony id>":["Type::Method"]}}`, rebuilt once a second. `p95Ms` is the plugin's 95th-percentile cost per frame over all frames (about 10% resolution, zero for a plugin timed in under 5% of them), `share` its average over the average frame, and `baseline.withoutModsMs` the average frame less every plugin's timed time |
| `console <line>` | `ok` once the game's own console has been handed the line (run on Unity's main thread; its output goes to the game's console and log), else `error: <why>`. The argument keeps its case. Only a game with a known console takes it: Valheim (`Console.instance.TryRunCommand`); any other answers `error: <game> has no console Mortar can reach`. A game with a console of that shape is one line in `GameConsoles.For` |
| `perf start` | `ok {"measured":true}`: starts measuring on any launch, or restarts the window; the first one also wraps every plugin's Harmony patches and its own MonoBehaviours' Update, LateUpdate and FixedUpdate with timers |
| anything else | `error: unknown command ...` |
| wrong token | `error: unauthorized` |

Apart from `console`, a command is a query, case-insensitive. Replies are one line, so a client that only checks for `ok` keeps working.

## Stream overlay

Off unless Mortar's Stream overlay setting is on. Mortar then writes `startup/overlay.json` in the profile folder that holds `BepInEx` (the same place as `startup/skip-intro`), with the keys the SMAPI bridge's `config.json` uses:

```json
{"OverlayEnabled":true,"OverlayPort":8123,"OverlayToken":"<64 hex chars>"}
```

The plugin reads it at start, listens on `127.0.0.1:<OverlayPort>` and serves `GET /state` (and `HEAD` / `OPTIONS` on that path) with the token in `Authorization: Bearer <token>` or `?token=` (`OPTIONS` is the CORS preflight and does not require a token). Any other path, method or token gets an error status. The overlay token is separate from the command token and the server has no path to the command channel. Values are read on Unity's main thread twice a second and the listener serves the last snapshot. A game with no provider logs a warning and stays off.

`/state` is `{"inGame":false}` outside a session, else `inGame`, `game` (Mortar's game id, which picks the page's layout), `playerName` and the game's own keys:

| Game | Keys |
| --- | --- |
| `lethal-company` | `moon`, `crewAlive`, `crewTotal`, `day` (days spent), `quota`, `quotaProgress`, `daysLeft`, `credits` |
| `valheim` | `biome`, `day`, `bossesDefeated` (names, in the order the bosses are met), `bossCount` |

A key the game does not give (a renamed field after a game update) is left out, never zero. Providers name the game's types as strings and read them by reflection, so the plugin references no game assembly.

## Documentation

| Topic | Location |
| --- | --- |
| Build, test, package | [HUMANS.md](HUMANS.md) |
| Rules for agents | [AGENTS.md](AGENTS.md) |
| Contributing | [CONTRIBUTING.md](CONTRIBUTING.md) |
| Security policy | [SECURITY.md](SECURITY.md) |
| Licence | [LICENSE](LICENSE) |

## Licence

Licensed under the [GNU Affero General Public License v3.0](LICENSE). BepInEx is LGPL-2.1 and is referenced, not redistributed.
