# Changelog

## 0.3.5 - 2026-09-12

- Removed recurring full-world light scans and optimized nearby targeting.

## 0.3.4 - 2026-09-12

- Fixed host light colors not syncing to joined players.

## 0.3.1 - 2026-09-03

- Expands lantern targets from the light child to the complete visual object so their mesh renderers are included.
- Tints emission properties across every lantern material slot while leaving base/albedo colors untouched.
- Supports common Standard, URP, HDRP, and custom glow-property names and preserves existing HDR brightness.
- Keeps existing saved lantern identities stable while expanding the visual root.

## 0.3.0 - 2026-09-03

- Adds full mouse control for the color wheel, presets, save, original-color reset, and cancel actions.
- Opens mouse mode with `F7` or the middle mouse button and safely restores the previous cursor state on close.
- Adds an opt-in handshake and host-authoritative color synchronization for players who have BetterLights installed.
- Sends newly connected compatible clients a snapshot of all current torch and lantern colors.
- Leaves unmodded clients compatible without sending them BetterLights network traffic.
- Reduces the default light-color prompt range from 4.25 metres to 3 metres and migrates the earlier test default.

## 0.2.0 - Local test build

- Adds environmental lantern detection without applying flame behavior to lanterns.
- Saves individual lantern colors by stable world position.
- Adds D-pad color presets alongside continuous right-stick selection.
- Adds live hue and saturation readouts.
- Adds target distance, saved-color confirmation, and safer screen-edge positioning.
- Reduces full scene-light scans while keeping targeting responsive.

## 0.1.0 - Local test build

- Adds individual Torch, Standing Torch, and Wall Torch color customization.
- Changes fire particles, material emission, and actual Unity lights together.
- Saves torch colors by persistent placement ID.
