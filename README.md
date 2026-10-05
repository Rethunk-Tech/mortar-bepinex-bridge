<h1 align="center">Mortar BepInEx Bridge</h1>

<div align="center">

![Licence](https://img.shields.io/badge/licence-AGPL--3.0-blue)

</div>

---

The BepInEx loader's companion for [Mortar](https://github.com/Rethunk-Tech/mortar), the mod manager: a BepInEx 5 plugin that lets Mortar ask a running game which plugins are loaded. It is the BepInEx counterpart of [mortar-smapi-bridge](https://github.com/Rethunk-Tech/mortar-smapi-bridge) and works in any Unity Mono game that runs BepInEx 5; Lethal Company is the one Mortar tests it with. Mortar starts the game through its launcher and has no console to type into, so this plugin provides a query channel.

## Quick start

```sh
dotnet build -c Release && dotnet test -c Release
```

Prerequisites, packaging and install: [HUMANS.md](HUMANS.md).

## Highlights

- Loopback TCP channel: one connection per command, authenticated with a per-run token
- Writes `mortar-bepinex-bridge.json` (`port`, `token`, `pid`) in BepInEx's `config` folder and deletes it on exit
- Commands: `ping`, `status` (game version, scene, loaded plugins) and `plugins` (structured list)

## Thunderstore page description

**Mortar BepInEx Bridge** lets the Mortar mod manager see which BepInEx plugins a running game has loaded, so it can show their state without you reading `LogOutput.log`. It adds no gameplay and changes no game content.

- **Install:** Mortar installs and updates it automatically. Install it by hand only if you want to query the game from your own tool.
- **Requires:** BepInEx 5 (BepInExPack 5.4.2304 or later).
- **Safety:** it listens on `127.0.0.1` only (never the network, never the internet) and every request needs a random token stored in a file only your user can read. It makes no outgoing connections. Under Proton the game is a Windows build, which has no file modes to restrict, so the token file is as private as the Steam library folder holding the prefix.
- **Source and licence:** [github.com/Rethunk-Tech/mortar-bepinex-bridge](https://github.com/Rethunk-Tech/mortar-bepinex-bridge), AGPL-3.0. Built on BepInEx (LGPL-2.1), which is referenced, not redistributed.

## How it works

On launch the plugin listens on `127.0.0.1` (port chosen by the OS) and writes `mortar-bepinex-bridge.json` in `BepInEx/config`:

```json
{"port":51234,"token":"<64 hex chars>","pid":4242}
```

The file is restricted to the current user (mode 0600 on Linux and macOS) and deleted on exit. Every request must carry the random 32-byte token, compared in constant time. The config folder is used rather than the plugin's own, because a mod manager chooses where plugins live but BepInEx always has one config folder.

## Protocol

The same framing as the SMAPI bridge: one connection per command. The client sends two LF-terminated lines, the token and the command (limits 128 and 4096 bytes), and reads one line back:

| Command | Reply |
| --- | --- |
| `ping` | `ok` |
| `status` | `ok {"gameVersion":"...","scene":"...","plugins":[{"guid":"...","version":"..."}]}` |
| `plugins` | `ok [{"guid":"...","name":"...","version":"..."}]` |
| anything else | `error: unknown command ...` |
| wrong token | `error: unauthorized` |

The game has no command console, so there is nothing to run: a command is a query, case-insensitive. Replies are one line, so a client that only checks for `ok` keeps working.

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
