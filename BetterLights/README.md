# BetterLights

BetterLights is a per-light color customization mod for Inferno Protocol.

BetterLights customizes placed `Torch`, `Standing Torch`, and `Wall Torch` objects individually. It also detects environmental lantern objects from the game's scene hierarchy. One selected color updates the emitted Unity light and any relevant fire or emissive visuals.

Craftable torches are saved by the game's persistent placable ID. Environmental lanterns are saved by their stable world position. Both restore automatically from the BepInEx config.

## Controls

- Stand within roughly 3 metres and look toward a placed torch or lantern.
- Press `R3` when the small in-world prompt appears.
- Move the right stick around the color wheel. Direction chooses hue; distance from the center chooses saturation.
- Press D-pad left or right to cycle through useful warm, red, green, blue, violet, pink, and white presets.
- Press `R3` again to save.
- Press `B` to cancel and restore the previous color.
- Press `L3` to preview the torch's original game color; press `R3` to confirm the reset.

Keyboard fallback: `F7` opens/saves, arrow keys adjust hue and saturation, and `Escape` cancels.

Mouse controls: press `F7` or the middle mouse button while targeting a light. Click and drag directly on the color wheel, click any preset, then use the visible `Save`, `Original`, or `Cancel` buttons. BetterLights unlocks the cursor only while the picker is open and restores the previous cursor state afterward.

The small targeting prompt now shows the selected light type, distance, current color, and brief save confirmation. The picker shows live hue/saturation values and keeps itself inside the visible screen area.

## Multiplayer

BetterLights includes an optional, host-authoritative synchronization layer. Compatible clients announce themselves to the host, receive a snapshot of every current torch and lantern color, and receive later colors when the host saves them. Messages are sent only to players who have BetterLights, so unmodded players can still connect normally but will see the game's original light colors.

To see synchronized colors, both the host and connecting player need the same BetterLights version installed. Only the host can edit lights during a multiplayer session; clients display the host's choices.

## Configuration

Settings and saved per-torch colors are stored in:

```text
BepInEx\config\com.holden.infernoprotocol.betterlights.cfg
```
