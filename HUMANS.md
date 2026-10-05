# Mortar BepInEx Bridge runbook

How to build, test and package the plugin. What it does: [README.md](README.md); rules for agents: [AGENTS.md](AGENTS.md).

## Build and test

Requires the .NET SDK 8 or later (the plugin targets netstandard2.1, Unity's Mono profile).

```sh
dotnet build -c Release
dotnet test -c Release
```

BepInEx comes from the `BepInEx.Core` package (nuget.bepinex.dev, listed in `nuget.config`). UnityEngine comes from the `UnityEngine.Modules` package (2022.3.9, Lethal Company's Unity) on the same feed, so no game install is needed to build. Game assemblies are never committed.

`gate` from the repo root is the offline gate. `lefthook install` sets up the git hooks (needs `lefthook` and `gitleaks` on PATH).

## Packaging and release

`scripts/package.sh` builds Release and writes `dist/MortarBepInExBridge-<version>.zip` (plus `.sha256`) in Thunderstore's layout: `manifest.json`, `icon.png`, `README.md` and `MortarBepInExBridge.dll` at the zip root. `assets/icon.png` is a 256x256 placeholder to replace before publishing.

`<Version>` in the csproj is the only place the version is written, and `Plugin.Version` must match it. It fills `manifest.template.json`. To release: bump both, commit, tag `v<version>`.

## Install by hand

Copy `MortarBepInExBridge.dll` into `BepInEx/plugins/`.
