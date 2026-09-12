# Inferno Protocol Mods

All mods are free. If you'd like to support their development, [buy me a coffee](https://buymeacoffee.com/frankmods). Thank you!

Independent gameplay and quality-of-life mods for [Inferno Protocol on Steam](https://store.steampowered.com/app/3908940/Inferno_Protocol/).

## Community and support

For help, feedback, bug reports, and mod updates, join the [FrankMods Discord](https://discord.gg/DBe7UWWDCD).

| Mod | Version | What it changes | Latest release |
| --- | --- | --- | --- |
| BetterUI | 0.11.0 | Polishes crafting and building, displays exact hotbar durability, and presents genetics as an interactive DNA tree. | [Thunderstore](https://thunderstore.io/c/inferno-protocol/p/FrankMods/BetterUI/) |
| Utility Wheel | 0.2.3 | Adds a translucent controller radial menu for occupied hotbar slots. | [Thunderstore](https://thunderstore.io/c/inferno-protocol/p/FrankMods/UtilityWheel/) |
| BetterLights | 0.3.6 | Customizes individual torch and lantern colors with controller and mouse support. | [Thunderstore](https://thunderstore.io/c/inferno-protocol/p/FrankMods/BetterLights/) |
| [ContentPlus](FieldQuests/README.md) | 0.5.2 | Adds a quest board, XP progression and twelve unlockable equipment finishes. | [Thunderstore](https://thunderstore.io/c/inferno-protocol/p/FrankMods/ContentPlus/) |
| [Renovator](Renovator/README.md) | 0.6.0 | Moves and aligns placed objects, applies curated finishes, and synchronizes committed renovations in multiplayer. | [Thunderstore](https://thunderstore.io/c/inferno-protocol/p/FrankMods/Renovator/) |

The mods can be installed separately. Utility Wheel automatically uses BetterUI's remembered color palette when both are installed.

## Requirements

- Windows x64
- Inferno Protocol through Steam
- Unity IL2CPP x64 build of BepInEx 6

These releases were tested with Inferno Protocol Steam build `25029929`, Unity `6000.4.11f1`, and Thunderstore's BepInEx IL2CPP pack `6.0.755`. Inferno Protocol is in active development, so future game updates may require rebuilt mods.

## Thunderstore Mod Manager installation

Install any of the mods from the Inferno Protocol community and launch the game with **Start modded**. Thunderstore installs both BepInEx and `InfernoProtocolInteropFix` as dependencies; the compatibility package repairs this game's generated Unity 6 interop metadata automatically before plugins load.

Normal Steam launches remain unmodded as long as BepInEx is not installed directly in the game directory.

Use the official [BepInEx IL2CPP installation guide](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html) and [BepInEx downloads](https://github.com/BepInEx/BepInEx/releases). Select a Windows x64 Unity IL2CPP package, not a Mono package.

## Manual installation

1. Install BepInEx 6 into the Inferno Protocol game directory.
2. Start the game once so BepInEx creates its folders, then close the game.
3. Download the desired ZIP from the [latest release](https://github.com/ohmyohmyyy/InfernoProtocol-Mods/releases/latest).
4. Extract the ZIP directly into the game directory. Allow Windows to merge the included `BepInEx` folder.
5. Start the game and confirm that the BepInEx console reports the mod as loaded.

The default Steam game directory is:

```text
C:\Program Files (x86)\Steam\steamapps\common\Inferno Protocol
```

After installation, the DLLs should be located at:

```text
BepInEx\plugins\BetterUI\InfernoProtocol.BetterUI.dll
BepInEx\plugins\UtilityWheel\InfernoProtocol.UtilityWheel.dll
BepInEx\plugins\BetterLights\InfernoProtocol.BetterLights.dll
BepInEx\plugins\Renovator\InfernoProtocol.Renovator.dll
```

Do not place the release ZIP itself in `BepInEx\plugins`. Extract it first.

## BetterUI

BetterUI refines crafting, building, hotbar durability, and the genetics panel without changing gameplay. Acquired genes appear in a compact DNA tree with mouse/controller navigation, pan/zoom and reduced-motion support. [Full BetterUI details and setup](BetterUI/README.md).

Features include:

- a responsive, lightly translucent crafting layout;
- larger recipe icons and readable component requirements;
- refined categories, selection states, quantity badges, and craft-button states;
- an integrated fabrication-progress card that temporarily replaces the ingredient list while crafting;
- a streamlined building catalog with categories, search, material inspection, and native unlock filtering;
- exact current/maximum durability values beneath durable hotbar items;
- searchable recipes and live recipe availability updates;
- green, blue, cyan, violet, and amber palettes;
- a footer color-wheel button that cycles and remembers the selected palette;
- `F8` to pause or resume BetterUI for the current session.

Configuration is saved to:

```text
BepInEx\config\com.holden.infernoprotocol.betterui.cfg
```

## Utility Wheel

Utility Wheel includes every occupied, visible hotbar slot rather than filtering for weapons. It is slightly transparent and matches the selected BetterUI palette when BetterUI is installed.

Controller controls:

- Hold `LB` to open the wheel.
- Point with the right stick to select an item.
- Release `LB` to equip the selected item.
- Press `B` before releasing to cancel.

The activation button can be changed from `LB` to `RB` in the config. A `Tab` keyboard fallback is enabled by default for testing or keyboard play.

Configuration is saved to:

```text
BepInEx\config\com.holden.infernoprotocol.utilitywheel.cfg
```

## BetterLights

BetterLights changes individual placed torch and environmental lantern colors. One selection updates the emitted light, flame particles where present, and emissive visuals. Each selection is remembered.

- Stand within roughly 3 metres and look at a light.
- Controller: press `R3`, choose with the right stick or D-pad, then press `R3` to save.
- Mouse: press `F7` or middle mouse, drag on the wheel or click a preset, then click `Save`.
- Use `L3` or the visible `Original` button to restore the original color; use `B`, `Escape`, or `Cancel` to discard a preview.

In multiplayer, the host controls colors. Players with the same BetterLights version receive the host's current colors and later saved changes. Unmodded players remain compatible but see original colors.

Configuration is saved to:

```text
BepInEx\config\com.holden.infernoprotocol.betterlights.cfg
```

## Renovator

Renovator adds native-style post-placement movement and rotation, precise alignment controls, refined hammer targeting, and 27 curated finishes for compatible walls, floors, ceilings, and roofs. It supports mouse, keyboard, and controller input. [Full Renovator details and controls](Renovator/README.md).

Editing is host-authoritative. Players with Renovator 0.6.0 or later see synchronized finishes and accepted object moves; unmodded clients remain unaffected but do not receive custom finishes.

## Uninstall

Close the game, then delete the corresponding plugin directory:

```text
BepInEx\plugins\BetterUI
BepInEx\plugins\UtilityWheel
BepInEx\plugins\BetterLights
BepInEx\plugins\Renovator
```

Deleting a config file is optional. BepInEx will recreate it with defaults if the mod is installed again.

## Troubleshooting

### The mod does not appear

- Confirm that the DLL is inside its plugin folder, not still inside the downloaded ZIP.
- Confirm that the BepInEx console opens and reaches `Chainloader startup complete`.
- Check `BepInEx\LogOutput.log` for the plugin name and the first error after it.
- Confirm that you installed the Windows x64 Unity IL2CPP build of BepInEx 6.

### `Duplicate type with name '<>O'`

Some BepInEx interop outputs for this Unity version contain duplicate compiler-helper types. Thunderstore installations receive `InfernoProtocolInteropFix` automatically. It runs before the plugin chainloader, repairs only the affected generated interop assemblies, and preserves one backup beside each changed file.

For a manual source installation, the repository install script applies the same narrow repair:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-all.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\install.ps1
```

After a game or BepInEx update, allow BepInEx to regenerate its interop files before rebuilding the mods.

## Build from source

Install the .NET 6 SDK, install and run BepInEx once, then execute this from PowerShell in the repository:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-all.ps1
```

If the game is installed elsewhere, provide its path:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-all.ps1 -GameDir 'D:\SteamLibrary\steamapps\common\Inferno Protocol'
```

Builds reference the BepInEx and generated game interop assemblies already present in the selected game directory. Those third-party assemblies are not included in this repository or its releases.

To create the same release archives locally:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-release.ps1 -SkipBuild
```

The finished DLLs and ZIP archives are written to `dist`.

## Notes

This is an unofficial community project and is not affiliated with the developer or publisher of Inferno Protocol. No game-owned assemblies or assets are distributed with these mods.
