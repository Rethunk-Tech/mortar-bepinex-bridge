# TODO

Work that is decided but not built. `AGENTS.md` and `README.md` describe what
exists; this file is the only place that describes what does not.

Sections are unordered.

## Overlay providers cover only Lethal Company and Valheim

`IGameHooks.Overlay` (`GameHooks.cs:13`) is what a game build implements.
Lethal Company and Valheim each return a provider; the core build's
`BuiltGame.Hooks` is null. Any other BepInEx 5 game that Mortar launches with
stream overlay on hits `Plugin.StartOverlay` (`Plugin.cs:179`): it logs a
warning and leaves the HTTP server off. The command channel still works.
README's overlay key table (`README.md:86`) lists only those two games.

Decided shape: one `IOverlayProvider` per Mortar game id that has an OBS
layout, named types as strings, `Probe` for reads, registered through that
game's `IGameHooks.Overlay`. Keys match the page layout; a renamed field is
omitted, never zero. `TheCoreBuildHasNoGameHooks` (`CoreBuildTests.cs:11`)
keeps the core build (no `-p:Game`) as null.

Rejected: a generic "dump public fields" provider. The page needs named keys.
Rejected: shipping game assemblies.

Deferred because each game is a research task (session object, in-game test,
reflection names) larger than a gap-fill and easy to get wrong.

### Read

1. `src/MortarBepInExBridge/GameHooks.cs:13` `IGameHooks.Overlay` — the
   method a new game implements.
2. `src/MortarBepInExBridge/Games/LethalCompany/LethalCompanyProvider.cs` and
   `Games/Valheim/ValheimProvider.cs` — the pattern: `Read` returns null out
   of session.
3. `src/MortarBepInExBridge/Probe.cs` — member/method reads by name.
4. `src/MortarBepInExBridge/Plugin.cs:192` — warning when the provider is
   null.
5. `README.md:86` — the key table that ships with a new provider.

### Traps

- **productName is the Unity string, not Mortar's id.** Lethal Company is
  `"Lethal Company"`; the overlay `game` field is `lethal-company`.
- **Main-thread only.** `Read` runs from `OverlayRunner.Update`. No Unity
  calls from the HTTP thread.
- **Do not reference game assemblies.** AGENTS.md: the plugin stays generic.

### Acceptance

- A game build's `Hooks.Overlay` returns a provider; the core build's
  `Hooks` stays null.
- In session, `/state` JSON has `inGame` true, `game` set to Mortar's id, and
  the documented keys; missing fields are absent, not zero.
- Out of session, snapshot is `{"inGame":false}`.
- README table lists the new keys. `gate` stays green. No game DLL in the
  repo.

## Intro skip steps exist only for Lethal Company

`LethalCompanyHooks.Steps` (`LethalCompanyHooks.cs:15`) names Lethal Company's init scenes.
`IntroSkip.For` still runs in every game, and `SplashStop` still asks Unity to
end its own splash (any engine that has that API). Valheim and other titles
keep their own boot UI: Mortar's `startup/skip-intro` and `MORTAR_SKIP_INTRO=1`
only stop the engine splash there.

Decided shape: more `IntroStep` rows, typed as strings, `choice: true` only
for Menu mode. No game assemblies. A game that lacks the types is untouched
(`Probe.Find` returns null, `IntroRunner.RunStep` yields break).

Rejected: clicking UI by screen coordinates. Rejected: a Valheim step guessed
from memory without a measured scene/type list.

Deferred: needs a recorded scene graph per game; wrong steps can skip a
disclaimer the player must see.

### Read

1. `src/MortarBepInExBridge/IntroSkip.cs:9` `IntroMode` — Play / Animations /
   Menu.
2. `src/MortarBepInExBridge/Games/LethalCompany/LethalCompanyHooks.cs:15`
   `Steps` — Lethal Company only.
3. `src/MortarBepInExBridge/IntroSkip.cs:63` `For` — scene + mode filter.
4. `src/MortarBepInExBridge/IntroRunner.cs:70` `OnSceneLoaded` — applies
   steps.
5. `README.md:32` — documents Lethal Company Online/LAN; other games would
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

Blocked: needs Valheim's `assembly_valheim.dll` or confirmed in-game field names, and no maintainer owns Valheim (NOMAD, 2026-11-05). Pick up only when a Valheim install or a contributor with one is available.

`LethalCompanyHooks.Versions` (`LethalCompanyHooks.cs:23`) has one row:
`GameNetworkManager.gameVersionNum` formatted `v{0}`. Valheim already has an
overlay provider, but `status` reports Unity's `Application.version` there
(`gameVersionSource` `unity`) even when the pause menu shows a different
number. `Plugin.ReadGameVersion` (`Plugin.cs:155`) already re-reads on scene
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

1. `src/MortarBepInExBridge/GameVersion.cs:12` `GameVersionSource` — the
   five strings a new row fills.
2. `src/MortarBepInExBridge/Games/LethalCompany/LethalCompanyHooks.cs:23`
   `Versions` — Lethal Company only.
3. `src/MortarBepInExBridge/GameVersion.cs:39` `Hook` — patches start-up.
4. `src/MortarBepInExBridge/GameVersion.cs:71` `Read` — hooked value, else
   live field, else null.
5. `tests/MortarBepInExBridge.Tests/GameVersionTests.cs` — fixtures for a
   new source.
6. `README.md:61` — `gameVersionSource` `game` vs `unity`.

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
