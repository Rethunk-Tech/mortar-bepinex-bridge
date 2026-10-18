# Mortar BepInEx Bridge runbook

How to build, test and package the plugin. What it does: [README.md](README.md); rules for agents: [AGENTS.md](AGENTS.md).

## Build and test

Requires the .NET SDK 10 or later (the plugin targets netstandard2.1, Unity's Mono profile).

```sh
dotnet build -c Release
dotnet test --solution MortarBepInExBridge.slnx -c Release
```

BepInEx comes from the `BepInEx.Core` package (nuget.bepinex.dev, listed in `nuget.config`). UnityEngine comes from the `UnityEngine.Modules` package (2022.3.62, Lethal Company's Unity) on the same feed, so no game install is needed to build. Game assemblies are never committed.

`gate` from the repo root is the offline gate. `lefthook install` sets up the git hooks (needs `lefthook` and `gitleaks` on PATH).

## Packaging and release

`scripts/package.sh` builds Release and writes `dist/MortarBepInExBridge-<version>.zip` (plus `.sha256`) in Thunderstore's layout: `manifest.json`, `icon.png` (256x256) and `README.md` at the zip root, `plugins/MortarBepInExBridge.dll`, and the startup-timing patcher `patchers/MortarBepInExBridge.Patcher.dll`. It also writes `MortarBepInExBridge-<version>-nexus.zip` for Nexus Mods: the same two dlls under `BepInEx/plugins/MortarBepInExBridge/` and `BepInEx/patchers/`, unpacked into the game folder as Vortex expects. `DIST=<dir>` writes elsewhere. `assets/icon.png` is Mortar's app icon scaled to 256x256.

`<Version>` in the csproj is the only place the version is written, and `Plugin.Version` must match it. It fills `manifest.template.json`. To release: bump both, commit, tag `v<version>`. The `v*` tag runs `.github/workflows/ci.yml`, whose release job checks the tag against the csproj version, runs `scripts/package.sh` and attaches the zip and its checksum to a GitHub release; its publish job then uploads the same zip to Thunderstore as team Mortar, to the communities and categories in `thunderstore.toml`, with the `THUNDERSTORE_TOKEN` repository secret. Its nexus job uploads `MortarBepInExBridge-<version>-nexus.zip` to the Nexus Mods pages for Lethal Company (mod 315, file 606) and Valheim (mod 4444, file 25550) with the `NEXUSMODS_API_KEY` secret, through `Nexus-Mods/upload-action`. Its curseforge job uploads the same zip to the CurseForge projects for Lethal Company (1747636) and Valheim (1747644) with the `CURSEFORGE_TOKEN` author token, tagged with every game version CurseForge lists for that game. The manifest declares no dependency, because each community ships its own BepInExPack (Lethal Company `BepInEx-BepInExPack`, Valheim `denikson-BepInExPack_Valheim`) and the plugin runs on any BepInEx 5.

## Install by hand

Copy `plugins/MortarBepInExBridge.dll` from the zip into `BepInEx/plugins/`, and `patchers/MortarBepInExBridge.Patcher.dll` into `BepInEx/patchers/` for startup timing.
