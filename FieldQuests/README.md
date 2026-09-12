# ContentPlus 0.5.1

![ContentPlus](../thunderstore/ContentPlus/icon.png)

Quest boards, supply contracts, creature hunts, XP levels and twelve unlockable patterned equipment finishes.

ContentPlus was previously developed as FieldQuests. The internal DLL name, plugin ID, network channels and `FieldQuests.v1.json` save filename intentionally remain unchanged. Existing quests, XP and skin selections carry over. Do not install a second copy of the old FieldQuests DLL alongside ContentPlus.

Quest/reward tuning and migration notes: [design review](DESIGN.md). Active supply quests use the revised terms; existing XP and completed quests are preserved.

The Quest Board brings 24 authored one-time assignments to Inferno Protocol: 12 supply turn-ins and 12 creature hunts. Every completion grants XP and one existing game item. Level unlocks provide a foundation for future progression systems; levels do not currently alter combat stats.

## Find the Quest Board

Look beside the starting house for a freestanding wooden noticeboard with pinned assignments and a sign on both sides. Approach within 3.8 metres and face the board to see the Read Board prompt. There is no floating compass marker.

The host places the board at the measured ground position X=-23.165, Y=0.199, Z=-20.094 with yaw 3.526 degrees, saved using WorldCoordinates. There is no trader/house lookup, ground search or extra offset. Clients receive the host's exact placement. This is a fixed location for the current starting area, not automatic placement for other map layouts.

The board is made from static geometry and owned materials. There is no character model, skeleton, NPC cloning, or animation dependency. Collision matches the posts and board panel.

Upgrading from the character-based versions keeps the same save file, quest IDs, XP, accepted quests and completed rewards. No reset or migration is needed.

Press your normal **Interact** button to read the board. The prompt shows the button name from your current keyboard/controller binding, including remaps. **F9** remains a keyboard fallback. Mouse/controller journal controls and owned-storage turn-ins are unchanged.

Assignments requiring assets absent from your installed game build are individually hidden and logged; the tested game log currently resolves 22 of 24. Existing active assignments with missing assets can still be abandoned to free a slot without silently deleting their progress.

If you arrive on MagicCarpet, dismount first; the prompt will remind you.

## Journal controls

| Action | Keyboard / mouse | Controller |
| --- | --- | --- |
| Read board nearby | Normal Interact binding; F9 fallback | Normal Interact binding (shown in prompt) |
| Browse assignments | Click a row; wheel; up/down | D-pad up/down |
| Change filter | Click a filter; left/right; Tab | D-pad left/right |
| Change page | Click Previous/Next; Page Up/Down | LB / RB |
| Accept / complete | Click main action; Enter | A / Cross |
| Abandon active assignment | Click Abandon; Delete | X / Square |
| Cancel confirmation / close | Escape or Close | B / Circle |

Turn-in and abandon actions require confirmation. Gameplay input is temporarily suspended while browsing the board. Closing waits for held menu buttons to release before returning them to gameplay. The mouse controls the journal without turning the camera. The panel follows BetterUI's saved palette when available.

## Quests and storage

- Accept up to six assignments at once. Hunts count only kills after acceptance and credited by the game to that player.
- A matching kill advances all matching accepted hunts. Abandoning and reaccepting resets that hunt's progress.
- Supplies are read from your inventory, worn backpack, and loaded player-built storage. The host can use built storage regardless of its recorded placer; remote players remain restricted to their own placements. There is no proximity restriction.
- The native live stash registry covers existing and newly placed storage without scene searches. Counts and claims read current inventories, not cached totals. World loot and unloaded storage remain excluded. The detail panel shows carried and stored totals separately.
- Turn-ins consume carried resources before storage. The reward goes to an empty carried inventory/backpack slot, including one freed by the turn-in. Free a slot first if needed.
- Materials, inventory capacity and current state are validated before writes. Failed writes trigger verified rollback; a rollback failure disables further claims and is logged.
- Rewards include tools, melee weapons, a crossbow, a pistol, clothing and scrap armor. Every quest always grants XP. Each quest can be rewarded once per player per world save.

## Saves and multiplayer

The host owns quest progress and validates quest actions, player identity, proximity, kills and all inventory changes. Both the host and each player using quests need the same ContentPlus version installed. Unmodded players do not see the quest board or journal. Two-machine multiplayer validation remains pending.

Progress is stored in `FieldQuests.v1.json` alongside the current world save. It is written when the game's save-complete event fires, keeping quest state aligned with saved inventories. Unsaved quest progress follows the same save/discard behavior as the world. A `.bak` preserves the previous quest file. Back up or transfer this file with the world. Invalid quest data is logged and not overwritten with an empty save.

Direct player-attributed deaths are supported. Kills whose native damage source cannot be resolved to a player, such as some environmental/trap deaths, do not receive quest credit.

## Installation

With Thunderstore, install ContentPlus and launch using Start modded. The package depends on FrankMods-InfernoProtocolInteropFix-1.0.0, which brings in BepInEx 6 IL2CPP. BetterUI, BetterLights and Utility Wheel are optional, not required.

For manual installation, install BepInEx 6 IL2CPP and the [InfernoProtocolInteropFix](https://thunderstore.io/c/inferno-protocol/p/FrankMods/InfernoProtocolInteropFix/) compatibility patcher first. Close the game, extract the ContentPlus release ZIP into the game folder, and merge its BepInEx directory. Launch through the configured BepInEx loader and check for `ContentPlus 0.5.1 loaded` in BepInEx/LogOutput.log. The ZIP also contains Thunderstore metadata; it is harmless outside the plugins directory.

Copy `InfernoProtocol.FieldQuests.dll` into `BepInEx/plugins/FieldQuests/`, then start the game normally through Steam. For local source builds, run from the mods folder:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\FieldQuests\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\FieldQuests\install.ps1
```

The build script compiles the DLL and runs the automated checks against the local game assemblies. Close the game before installing. To remove the mod, move its plugin folder out of BepInEx/plugins. Keep the quest JSON if you want to resume later.

## Equipment finishes

Interact with the FINISHES cabinet beside the board. Choose a finish, choose equipment, then Apply. No item needs to be carried: the saved appearance applies when supported equipment is equipped. Original restores the default. Quest reward cards identify skin unlocks, including retroactive unlocks for completed quests.

Twelve patterns: Woodland, Blue Circuit, Embercrack, Brasswork, Arctic Digital, Hazard, Tigerstripe, Crimson Rally, Tidebreaker, Topographic, Royal Weave and Desert Shards. See [finish support and limitations](SKINS.md). Appearance is local-only; other players do not receive these skins. Specialized animated item renderers and inventory mannequin previews are not supported.

Mouse: click selections and Apply; wheel browses equipment. Controller: left/right changes finish, up/down chooses equipment, LB/RB changes equipment page, A/Cross applies, B/Circle closes. Choices in the menu are remembered for the current session; applied finishes persist in the world save.

## Community and support

For help, feedback, and bug reports, join the [FrankMods Discord](https://discord.gg/DBe7UWWDCD).

## Validation status

See [TESTING.md](TESTING.md) for automated coverage and the in-game acceptance checklist. A successful build verifies APIs and logic; it does not prove runtime visuals, animation retargeting, controller behavior or a two-machine session.
