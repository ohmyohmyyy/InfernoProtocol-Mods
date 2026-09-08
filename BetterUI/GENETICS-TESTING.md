# Genetics tree - local preview

Preview 3 uses a centered rounded panel instead of a full-screen dashboard. Thin organic branches, under-node captions, soft selection glow, two subtle travelling DNA points and a short opening fade keep the tree calm. A visible Recenter button resets pan/zoom. Mouse hints take over when the mouse is used even if a controller is plugged in. Both icons and their captions can be clicked.

Set `ReducedMotion = true` in the `[Genetics]` section of the BetterUI config to disable decorative motion and smooth focus travel. Navigation follows node positions instead of wrapping at row edges. A pure layout/navigation test covers bounds, overlap and full controller reachability for collections through 64 genes (4,162 assertions); this is not a substitute for runtime UI review.

Open the game's normal genetics panel. BetterUI reads the active native genetics fields; it never changes gene acquisition, stages, effects or saves. Connections are a visual collection layout, not skill prerequisites. Names, descriptions, stages included in native titles, and icons come from the game.

Mouse: click a circular node to inspect it, drag blank tree space to pan, wheel to zoom. Arrow keys navigate; Home recenters; Escape or the top-right Close area closes.

Controller: D-pad chooses the next node in that direction without wrapping at the edges, right stick pans, triggers zoom, Y/Triangle recenters, B/Circle closes.

The tree uses the remembered BetterUI color palette. Native fields are checked only while open, every 0.75 seconds; graphics rebuild only when the displayed data changes. Geometry and cached circle textures are owned by the mod. No world searches or gene writes are performed. Closing restores the native panel's CanvasGroup and the input maps that were previously enabled; held navigation buttons must release before gameplay input returns.

Fallback: F8 temporarily disables BetterUI, or set Genetics.Enabled=false in its BepInEx configuration. Rendering failures disable the tree for the session and restore the original genetics panel.

Before publishing, test zero/one/many genes, stage changes, acquiring/removing genes, reopening, inventory close, pause, controller close with held inputs, mouse drag outside the viewport, zoom limits, ultrawide and 1280x720, and returning to crafting. Check descriptions fit and the native genetics shortcut reappears after closing. This build has compile validation only; runtime UI and controller checks are pending.
