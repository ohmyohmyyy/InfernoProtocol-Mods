# Changelog

## Renovator 0.6.0 - 2026-09-12

- Adds host-authoritative synchronization for committed surface finishes and accepted object moves.
- Sends finish snapshots to compatible players when they join an active session.
- Keeps previews and editing host-only while leaving unmodded clients unaffected.

## Renovator 0.5.0 - 2026-09-10

- Adds native-style post-placement movement, rotation, snapping, and precise alignment controls.
- Adds 27 curated finishes for compatible walls, floors, ceilings, and roofs.
- Replaces the Building Hammer's opaque target wash with a lightweight outline.
- Includes mouse/controller UI, safe save handling, and a performance pass for large builds.

## Thunderstore compatibility releases - 2026-09-04

- Added `InfernoProtocolInteropFix` 1.0.0, an automatic preloader repair for malformed Unity 6 IL2CPP interop metadata.
- Updated BetterUI to 0.9.6, Utility Wheel to 0.2.3, and BetterLights to 0.3.3 so Thunderstore installs the compatibility dependency automatically.

## Utility Wheel 0.2.1 - 2026-09-03

- Defers its activation while Artistic's editor is open, preventing competing controller overlays and input-map restoration conflicts.

## BetterLights 0.3.1 - 2026-09-03

- Recolors lantern emissive material channels while preserving the lamp body's base texture.
- Finds lantern renderers across the complete visual object and supports multiple material slots.
- Preserves each material's original emission brightness to prevent excessive bloom.
- Keeps torch flame rendering on its established conservative path.

## BetterLights 0.3.0 - 2026-09-03

- Adds individual color controls for placed torches and environmental lanterns.
- Changes emitted light, flames where present, and emissive visuals together.
- Supports controller, keyboard, and direct mouse color-wheel controls.
- Saves preferences for each light and uses a closer 3-metre interaction range.
- Adds opt-in, host-authoritative synchronization between players with BetterLights installed.

## BetterUI 0.9.4 - 2026-09-02

- Replaces the legacy black crafting-progress popup with an integrated fabrication card inside component analysis.
- Shows the live item icon, vertical icon fill, progress bar, percentage, and batch quantity while crafting.
- Temporarily swaps the ingredient list for fabrication progress and restores it automatically when crafting finishes.
- Keeps the game's original progress component active invisibly so crafting timing and completion behavior remain unchanged.

## BetterUI 0.9.3 - 2026-09-02

- Keeps the inventory's hand-crafting launcher in its original game-owned hierarchy.
- Fixes the hand-crafting button disappearing after the crafting panel is closed.
- Stops BetterUI from restyling that inventory-side launcher.

## Initial public release - 2026-09-02

### BetterUI 0.9.2

- Replaces only the crafting-table presentation with a polished, responsive terminal layout.
- Improves recipe rows, category spacing, component analysis, quantity badges, and action states.
- Adds remembered green, blue, cyan, violet, and amber palettes.
- Keeps the gameplay HUD, inventory, hotbar, storage, pause menu, and global tooltips unchanged.

### Utility Wheel 0.2.0

- Adds a translucent, controller-first radial selector for occupied hotbar slots.
- Supports right-stick selection, release-to-equip, and cancel input.
- Matches BetterUI's remembered palette when BetterUI is installed.
- Includes configurable controller button, opacity, deadzone, and keyboard fallback.
