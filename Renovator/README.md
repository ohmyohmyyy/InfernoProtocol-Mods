# Renovator

Renovator expands Inferno Protocol's building tools with post-placement editing, clearer targeting, and 27 curated finishes for walls, floors, ceilings, and roofs.

## Features

- Reposition and rotate existing player-built structures, furniture, lighting, storage, and decorations.
- Uses the game's native placement preview, surface rules, validity feedback, and compatible-edge snapping.
- Adds exact nudges, dedicated height controls, five movement steps, and five rotation steps.
- Applies seamless procedural wallpaper, tile, wood, textile, and stone finishes to compatible surfaces.
- Replaces the Building Hammer's opaque target wash with a thin mint outline on placeable objects.
- Supports mouse, keyboard, and controller navigation.
- Saves finish choices with the world and synchronizes committed finishes to connected Renovator clients.
- Synchronizes accepted post-placement moves live; the game's save and initial-join placement data remain authoritative.
- Shares generated textures and mapped meshes, batches world restoration, and avoids whole-scene work during normal gameplay.

Editing remains host-authoritative. Connected players need the same Renovator version to see synchronized finishes and live post-placement moves; clients without the mod receive no custom traffic.

## Open Renovator

Hold the **Building Hammer**, look directly at a player-placed object within 2.7 metres, and press one of:

- `F6`
- Middle mouse
- `R3`

Choose **Surface finishes** or **Move & rotate**. Finish controls are only available for compatible wood and stone surfaces.

## Surface finish controls

- Controller: D-pad browses, `X` cycles pattern size, `A` applies, and `B` returns.
- Mouse: click a finish to preview it, use the wheel or page buttons, select a size, then click **Apply finish**.
- Keyboard: arrow keys browse, `X` cycles size, `Enter` applies, and `Escape` returns.
- Apply **Original** to remove a finish.

Cancelling restores the saved finish.

## Move and rotate controls

Normal aiming and movement intentionally feel like the game's build-placement mode.

- Controller: left stick walks, right stick aims, D-pad nudges, triggers adjust height, bumpers rotate, `X` toggles precise/free nudges, `Y` resets, `L3` changes movement step, `R3` changes angle step, `A` places, and `B` cancels.
- Mouse and keyboard: mouse aim plus `WASD` follows normal placement. `I/J/K/L` nudges, `R/F` adjusts height, `Q/E` rotates, `G` toggles precise/free nudges, `Home` resets adjustments, `Z` changes movement step, and `C` changes angle step. Left-click places and `Escape` cancels.
- Hold `Alt` to release the mouse cursor for the optional clickable movement panel.

Storage containing items cannot be moved. Reset and Cancel restore the starting pose, and edits are limited to 20 metres from it.

## Installation

### Thunderstore

Install Renovator from the Inferno Protocol community and launch with **Start modded**. Its BepInEx and interop-fix dependencies install automatically.

### Manual

1. Install BepInEx 6 for Windows x64 Unity IL2CPP.
2. Install `InfernoProtocolInteropFix` from this repository.
3. Copy `InfernoProtocol.Renovator.dll` into:

```text
BepInEx\plugins\Renovator
```

4. Launch the game normally.

To build and install a local copy from the repository:

```powershell
dotnet build Renovator/Renovator.csproj -c Release
powershell -ExecutionPolicy Bypass -File Renovator/install.ps1
```

## Saves and removal

Finish preferences are stored in `Renovator.v1.json` beside the world save. Invalid files are preserved and writes are disabled instead of silently replacing them.

To uninstall, close the game and remove `BepInEx\plugins\Renovator`. Original visuals return on the next launch; the preferences file remains available if Renovator is installed again.

## Community and support

For help, feedback, and bug reports, join the [FrankMods Discord](https://discord.gg/DBe7UWWDCD).
