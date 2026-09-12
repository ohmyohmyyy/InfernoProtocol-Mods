# Thunderstore packages

This directory contains upload metadata for the independent Inferno Protocol mods. Finished archives are generated in `dist/thunderstore`.

## Build and validate

From the repository root, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-thunderstore.ps1
```

To package already-built release DLLs:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-thunderstore.ps1 -SkipBuild
```

To validate and package only Renovator after building it:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-thunderstore.ps1 -SkipBuild -Only Renovator
```

The script verifies each manifest, dependency, DLL version, icon dimensions and metadata, archive layout, and SHA-256 hash. It also rejects accidental game assemblies, debug symbols, and nested package roots.

## Upload

Upload each ZIP separately to the **Inferno Protocol** community through the Thunderstore package upload page:

- `BetterUI-0.11.0.zip`
- `UtilityWheel-0.2.3.zip`
- `BetterLights-0.3.3.zip`
- `Renovator-0.5.0.zip`

Select the most accurate categories offered by the upload page. BetterUI and Utility Wheel are client-side quality-of-life mods. BetterLights is a quality-of-life mod with optional host synchronization, but every player who should see synchronized colors needs BetterLights installed. Renovator is a building and quality-of-life mod; it currently operates in single-player or for the host, and its surface finishes are not synchronized to other players.

The package names are deliberately stable. For future updates, increment `version_number` and the matching plugin/assembly version without changing the package name.

## Test before upload

Import each ZIP as a local mod in Thunderstore Mod Manager or r2modman and launch a clean Inferno Protocol profile. Confirm that only the intended DLL is installed below `BepInEx/plugins`, then check `BepInEx/LogOutput.log` for the plugin's loaded message.

The packages depend on `BepInEx-BepInExPack_IL2CPP-6.0.755`, the IL2CPP loader currently listed for the Inferno Protocol community.
