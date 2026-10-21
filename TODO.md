# TODO

Work that is decided but not built. `AGENTS.md` and `README.md` describe what
exists; this file is the only place that describes what does not.

Sections are unordered.

## Overlay providers cover only Lethal Company and Valheim

`OverlayProviders.For` (`IOverlayProvider.cs:22`) switches on
`Application.productName` for `"Lethal Company"` and `"Valheim"`. Any other
BepInEx 5 game that Mortar launches with stream overlay on hits
`Plugin.StartOverlay` (`Plugin.cs:123`): it logs a warning and leaves the
HTTP server off. The command channel still works. README's overlay key table
(`README.md:77`) lists only those two games.

Decided shape: one `IOverlayProvider` per Mortar game id that has an OBS
layout, named types as strings, `Probe` for reads, registered in the same
switch. Keys match the page layout; a renamed field is omitted, never zero.
`UnknownGameHasNoProvider` (`OverlayTests.cs:143`) keeps Stardew (SMAPI, not
BepInEx) as null.

Rejected: a generic "dump public fields" provider. The page needs named keys.
Rejected: shipping game assemblies.

Deferred because each game is a research task (session object, in-game test,
reflection names) larger than a gap-fill and easy to get wrong.

### Read

1. `src/MortarBepInExBridge/Overlay/IOverlayProvider.cs:22` `For` — the
   switch a new game extends.
2. `src/MortarBepInExBridge/Overlay/LethalCompanyProvider.cs` and
   `ValheimProvider.cs` — the pattern: `Read` returns null out of session.
3. `src/MortarBepInExBridge/Overlay/Probe.cs` — member/method reads by name.
4. `src/MortarBepInExBridge/Plugin.cs:123` — warning when `For` is null.
5. `README.md:77` — the key table that ships with a new provider.

### Traps

- **productName is the Unity string, not Mortar's id.** Lethal Company is
  `"Lethal Company"`; the overlay `game` field is `lethal-company`.
- **Main-thread only.** `Read` runs from `OverlayRunner.Update`. No Unity
  calls from the HTTP thread.
- **Do not reference game assemblies.** AGENTS.md: the plugin stays generic.

### Acceptance

- `For` on the new productName returns a provider; `For("Stardew Valley")`
  stays null.
- In session, `/state` JSON has `inGame` true, `game` set to Mortar's id, and
  the documented keys; missing fields are absent, not zero.
- Out of session, snapshot is `{"inGame":false}`.
- README table lists the new keys. `gate` stays green. No game DLL in the
  repo.

## Intro skip steps exist only for Lethal Company

`IntroSkip.Steps` (`IntroSkip.cs:57`) names Lethal Company's init scenes.
`IntroSkip.For` still runs in every game, and `SplashStop` still asks Unity to
end its own splash (any engine that has that API). Valheim and other titles
keep their own boot UI: Mortar's `startup/skip-intro` and `MORTAR_SKIP_INTRO=1`
only stop the engine splash there.

Decided shape: more `IntroStep` rows, typed as strings, `choice: true` only
for Menu mode. No game assemblies. A game that lacks the types is untouched
(`IntroSkip.Find` returns null, `IntroRunner.RunStep` yields break).

Rejected: clicking UI by screen coordinates. Rejected: a Valheim step guessed
from memory without a measured scene/type list.

Deferred: needs a recorded scene graph per game; wrong steps can skip a
disclaimer the player must see.

### Read

1. `src/MortarBepInExBridge/IntroSkip.cs:10` `IntroMode` — Play / Animations /
   Menu.
2. `src/MortarBepInExBridge/IntroSkip.cs:57` `Steps` — Lethal Company only.
3. `src/MortarBepInExBridge/IntroSkip.cs:77` `For` — scene + mode filter.
4. `src/MortarBepInExBridge/IntroRunner.cs:69` `OnSceneLoaded` — applies
   steps.
5. `README.md:28` — documents Lethal Company Online/LAN; other games would
   be named here.

### Traps

- **Choice steps in Animations mode.** `choice: true` is Menu-only. Putting
  a LAN/auto-continue step without that flag skips a decision the player
  setting must keep.
- **sceneLoaded vs Start.** Lethal Company flags are cleared after Awake and
  before Start. Copy that timing; a delayed step that runs after Start may
  be too late.
- **Types as strings.** A mistyped name is a silent no-op.

### Acceptance

- `IntroSkip.Requested` is unchanged. A game without the new types still
  plays its own intro besides the engine splash.
- Menu mode reaches that game's main menu unattended in a sandbox launch.
  Animations mode skips boot movies and leaves start-up choices.
- Tests assert which steps fire per scene/mode, like
  `IntroSkipTests`. `gate` stays green. README names the game.

## Game version sources cover only Lethal Company

`GameVersion.Sources` (`GameVersion.cs:33`) has one row:
`GameNetworkManager.gameVersionNum` formatted `v{0}`. Valheim already has an
overlay provider, but `status` reports Unity's `Application.version` there
(`gameVersionSource` `unity`) even when the pause menu shows a different
number. `Plugin.ReadGameVersion` (`Plugin.cs:89`) already re-reads on scene
change.

Decided shape: another `GameVersionSource` (type, static instance, field,
format, start-up message) for each game whose displayed version is not
`Application.version`. Prefix-patch that start-up message at priority First
so mod postfixes cannot shift it, same as Lethal Company / MoreCompany.

Rejected: parsing version from the window title. Rejected: reading after
Awake without a prefix, which is how MoreCompany's +9950 leaked into status.

Deferred: the Valheim (and other) field names are not in this repo and must
be confirmed in-game; a wrong field is worse than `unity`.

### Read

1. `src/MortarBepInExBridge/GameVersion.cs:11` `GameVersionSource` — the
   five strings a new row fills.
2. `src/MortarBepInExBridge/GameVersion.cs:33` `Sources` — Lethal Company
   only.
3. `src/MortarBepInExBridge/GameVersion.cs:43` `Hook` — patches start-up.
4. `src/MortarBepInExBridge/GameVersion.cs:75` `Read` — hooked value, else
   live field, else null.
5. `tests/MortarBepInExBridge.Tests/GameVersionTests.cs` — fixtures for a
   new source.
6. `README.md:56` — `gameVersionSource` `game` vs `unity`.

### Traps

- **Mods rewrite the field in Awake postfix.** The prefix on the named
  start-up message is the whole point. Reading `Instance` later is the
  fallback when Hook did not run.
- **format string.** Lethal Company is `v{0}`. Another game may already
  include the letter, or use a string field.
- **Find by type name.** Same as IntroSkip: no game assembly reference.

### Acceptance

- In a fixture for the new type, `Read` returns the formatted start-up
  value, not a postfix-mutated one.
- A process with none of the source types still returns null from `Read`,
  and status uses `Application.version` with `gameVersionSource` `unity`.
- Lethal Company's path is unchanged. `gate` stays green.

## No `perf stop` beside `perf start`

`Protocol.Handle` (`Protocol.cs:76`) has `perf` and `perf start`.
`IGameView.Perf(bool start)` (`Protocol.cs:38`) restarts the window when
start is true. `PerfSwitch.Reply` (`PerfSwitch.cs:26`) sets `restart` and, on
the first start, wraps Harmony patches. There is no command that ends
instrumentation or freezes the window. A launch that has not started
measuring answers `{"measured":false}` to `perf`. Unknown-command help lists `perf or perf start`
(`Protocol.cs:81`).

Decided shape: only if Mortar's single `internal/bridge` client grows a stop.
Then `perf stop` would set a flag that `PerfHost.Update` honours by skipping
`frames.Add` / leaving `summary` as the last JSON, without unpatching Harmony
(unpatch mid-frame is unsafe). Reply `ok {"measured":true}` or
`{"measured":false}` when measuring never started.

Rejected: unpatching Harmony on stop. Rejected: adding the verb in this
bridge first, which would fork the shared client.

Deferred: new protocol verb; the SMAPI and BepInEx bridges share framing so
Mortar stays one implementation. Shipping stop here alone changes nothing
Mortar can call.

### Read

1. `src/MortarBepInExBridge/Protocol.cs:58` `Handle` — the switch and help
   list at `:81`.
2. `src/MortarBepInExBridge/Protocol.cs:38` `IGameView.Perf` — boolean start.
3. `src/MortarBepInExBridge/Perf/PerfSwitch.cs:26` `Reply` — unmeasured vs
   restart.
4. `tests/MortarBepInExBridge.Tests/ProtocolTests.cs:19` — command table and
   unknown-command text at `:38`.
5. `README.md:58` — perf / perf start rows.

### Traps

- **One client.** AGENTS.md: framing matches the SMAPI bridge. A BepInEx-only
  verb is dead until Mortar learns it, and SMAPI's profiler is a different
  shape.
- **Unmeasured `perf start`.** It starts measuring; `perf stop` before any
  start must not.
- **Help string.** Adding a verb updates the unknown-command list and the
  README table in the same commit.

### Acceptance

- Existing `ping` / `status` / `plugins` / `perf` / `perf start` replies are
  unchanged.
- If built: `perf stop` after measuring started keeps `measured` true and stops
  accumulating frames; before any start it is `{"measured":false}`.
- Mortar's bridge client understands the verb in the same release.
- `gate` stays green.
