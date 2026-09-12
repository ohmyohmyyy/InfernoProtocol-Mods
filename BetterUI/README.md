# BetterUI 0.11.0

Polished crafting and building menus plus a compact genetics tree for Inferno Protocol. This mod changes presentation only, not crafting costs/times, construction rules, gene acquisition, gene stages or effects.

## Genetics

Open the native genetics panel normally. Acquired genes appear along a DNA spine with curved branches, icons, names and their original descriptions. Connections organize the collection visually; they are not unlock prerequisites.

- Mouse: click an icon or caption, drag blank tree space to pan, scroll to zoom. Arrow keys browse; Home or Recenter resets the view; Escape closes.
- Controller: D-pad navigates spatially, right stick pans, triggers zoom, Y/Triangle recenters and B/Circle closes.
- The panel follows BetterUI's remembered palette and supports subtle decorative motion.
- `Genetics.Enabled=false` keeps the original list. `Genetics.ReducedMotion=true` disables decorative motion and smooth focus transitions.

Config: `BepInEx/config/com.holden.infernoprotocol.betterui.cfg`. F8 pauses/resumes BetterUI for the current session. The separate local GeneticsTest helper is not included and is not required.

## Crafting

Recipe search, clearer ingredients/categories, larger icons, integrated crafting progress and remembered color themes remain available. Durable outputs show exact `current / maximum` durability in the inspection panel. Hotbar slots add the same compact exact-value readout below each durable item, while the building catalog intentionally omits durability. Use the crafting footer's color wheel to change the palette.

## Building

The hammer's building catalog now uses the same BetterUI language: a category rail, searchable two-column plan browser, readable material cards, and a larger placement workspace. Its palette is shared with crafting, while the original recipe, resource, unlock, mouse, and controller behavior remains authoritative.

## Installation

Thunderstore: install BetterUI and launch with Start modded. InfernoProtocolInteropFix and BepInEx IL2CPP are dependencies; ContentPlus and the other gameplay mods are optional.

Manual: install BepInEx 6 IL2CPP and [InfernoProtocolInteropFix](https://thunderstore.io/c/inferno-protocol/p/FrankMods/InfernoProtocolInteropFix/), close the game, then extract the release ZIP into the game folder. The DLL belongs at `BepInEx/plugins/BetterUI/InfernoProtocol.BetterUI.dll`. Replace the old copy rather than keeping duplicate DLLs. Restart through your configured mod loader.

Only the local player needs BetterUI. No genetics networking or save changes are made. Untested future game versions may require an update.

## Validation

Build: zero warnings/errors. 4,380 assertions cover layout, controller reachability and batched line geometry through 64 genes. Local gameplay logs confirm the panel opens with 28 native gene fields; the optimized build's clipping fix was tested in game. Broad resolution/accessibility and multiplayer testing is not exhaustive.

Build using `dotnet build BetterUI/BetterUI.csproj -c Release`; run tests with `dotnet run --project tests/GeneticsLayout.Tests/GeneticsLayout.Tests.csproj -c Release`. Supply `-p:GameDir="path to game"` when building against a different installation. Game-generated interop assemblies are required and are not distributed with the source.
