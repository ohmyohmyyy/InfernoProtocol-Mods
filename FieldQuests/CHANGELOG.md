# Changelog

## 0.5.0 - ContentPlus

- Rename the public mod to ContentPlus; retain legacy plugin/save identifiers for compatibility.
- Prepare the Thunderstore package, matching gameplay-mod icon and GitHub source/manual setup documentation.

## 0.4.4

- Give the finishes station a dedicated three-step layout: choose finish, choose equipment, preview and apply. Enlarge the pattern swatch and replace quest progress/XP clutter with collection progress.
- Show finish page numbers, current equipment appearances, clear locked/applied states and the quest needed for each unlock. Disable unchanged applications for both mouse and controller.
- Remember finish and equipment selection when reopening the cabinet during the session. Show cabinet-specific mouse/controller controls instead of quest/abandon instructions.
- Restore the original quest layout when reopening the board. Runtime visual and controller testing remains required.

## 0.4.3

- Add Tidebreaker waves (s05), Topographic contours (s09), Royal Weave interlaced ribbons (h12), and Desert Shards angular camouflage (h05), bringing the collection to twelve patterned finishes.
- Finish page buttons now cycle through any number of pages, including the partial last page. Controller cycling still reaches every finish.
- Preserve existing finish IDs and selections; already-completed reward quests unlock the new finishes automatically without repeating item/XP rewards.
- Validate all twelve patterns and page navigation in the automated checks. In-game appearance still needs visual review.

## 0.4.2

- Add Arctic Digital (Larger teeth), Hazard (Keep it together), Tigerstripe (Forest sentries), and Crimson Rally (Broken ambush). Existing finish IDs remain stable; completed quests grant these unlocks retroactively.
- Add a second finish-selector page with mouse buttons and full controller cycling; preserve selected equipment when changing finishes.
- Show skin unlocks in the quest reward card, confirmation, completion popup and receipt.

## 0.4.1

- Replace flat tints with four procedural base-texture designs: Woodland camouflage, Blue Circuit stripes, Embercrack fissures and Brasswork lattice.
- Composite source shading into owned 512px textures; preserve sampled alpha, normal/metallic/emission maps and shared assets. Release generated textures when a finish is removed.
- Show actual pattern swatches instead of pretending recolored item icons are a 3D skin preview. Keep finish IDs/unlocks/selections compatible.
- UV seam placement and specialized item renderers still require in-game testing; these are patterned finishes, not hand-authored per-mesh texture layouts.

## 0.4.0

- Reduce the exploration HUD to a minimal LV label and hairline XP bar.
- Add the Finishes cabinet and a cosmetic selection page, quest-earned palettes, per-item saved choices and Original reset without inventory requirements.
- Begin local held-item/clothing material tint support without changing shared assets; see SKINS.md for prototype limitations and tests.
- Backfill unlocks from completed quest IDs; retain XP/completions and existing rewards.

## 0.3.3

- Add a compact translucent top-left exploration HUD: level badge, XP totals and progress bar matching the BetterUI palette.
- Hide during menu interaction, loss of focus and death; wait for authoritative progress rather than showing a guessed level.
- Request progress without inventory enumeration every five seconds outside the journal. Existing journal responses update the HUD immediately.

## 0.3.2

- Replace footer-only turn-in/abandon confirmation with a centered modal: materials, source split, reward icon and XP, dedicated confirm/cancel actions.
- Keep processing and result feedback in the modal; block background input and same-press confirmation, validate current quest state before submission.
- Support mouse, controller left/right + A/B, and keyboard Tab/arrows + Enter/Esc.

## 0.3.1

- Add a theme-colored reward receipt with level-up feedback after host-confirmed completion, plus smooth XP progression using unscaled time.
- Add processing feedback and duplicate-submit suppression while a claim is pending, with timeout recovery.
- Retain selected quest identity across refreshes, show list counts and keyboard/mouse help when no controller is connected.
- Keep completion effects inside the journal, with no flashing or extra gameplay overlay.

## 0.3.0

- Rebalance starter equipment and reduce high-cost supply deliveries; see DESIGN.md for the full review.
- Replace unavailable helmet/beans contract assets. Existing IDs and completion records remain; active supply terms update.
- Show carried/stored consumption on confirmation and correct shared-storage wording.
- Validate reward creation and XP capacity before consumption; skip storage enumeration for hunt claims.

## 0.2.5

- Fix the confirmed rejection of chest 169 (4 logs): recorded placer ID differed from the host's current identity. Host supply turn-ins now include all loaded player-built storage; remote ownership checks remain.
- Use the native live stash registry for existing and future storage. Remove repeated scene scans, hierarchy searches and verbose per-chest diagnostic output.
- Preserve inventory deduplication and transactional consumption/rewards.

## 0.2.4


- Discover live storage from both the scene and native registry; recover missing placement references within the chest hierarchy.
- Deduplicate inventories so multiple discovery paths cannot inflate counts or consume a stack twice.
- Log each chest's eligibility and log-item count when the storage report changes. Actual runtime exclusion still needs verification with the affected chest.

## 0.2.3

- Recognize saved wooden/scrap chests with missing ownership for the host, including chest components derived from LootChest. Keep explicit ownership restrictions and exclude world loot. Display counts and turn-ins use the same storage selection.

- Remove the floating quest-board direction/distance HUD; retain the nearby interaction prompt.

- Place the board at the user's saved ground coordinates (-23.165, 0.199, -20.094), yaw 3.526 degrees.
- Remove trader/house discovery from spawning so missing or multiple traders cannot block the board.
- Preserve host-to-client placement synchronization and existing quest progress.

## 0.2.2

- Fix the wrong landmark: the previous trading-interior door was at Y=3823.93, not at the outdoor house, causing the old player-near fallback to be used.
- Use a nearby outdoor trader and house geometry (or nearby physical house door), mirroring the trader across the front of the house toward the marked right-hand porch side.
- Reject indoor/maze/staging-area anchors. Remove player-near and starting-area fallback placement instead of silently leaving the board in the clearing.
- Keep footprint, stairs-side and reader clearance checks; retain the existing board appearance and quest progress.
- Add high-altitude-anchor and mirrored-porch-placement regression checks. Actual alignment to the screenshot still needs in-game verification.

## 0.2.1

- Prefer a front-right site beside the trading house's exterior entrance (right when approaching the house), facing outward.
- Check doorway-side clearance, the board footprint, flat ground and standing room for reading; log when the preferred landmark/site is unavailable and a fallback is used.
- Refine the header with a subtitle, add inset frame trim and iron fasteners, and vary the alignment and writing on pinned notices.
- Disable tiny decorative shadows to reduce visual noise and unnecessary shadow draw calls.
- Add rotated-entrance/right-side and doorway-clearance checks. Placement still needs an in-game check against the intended house.

## 0.2.0

- Replace the character quest giver with a freestanding timber quest board, pinned notices, metal trim and a two-sided sign.
- Remove the NPC cloning, skeleton, animation, pose baking and character visibility code entirely.
- Rename the marker, interaction prompt and journal branding to Quest Board; use the existing Interact binding to read it.
- Match collision to the posts/panel and check enough placement clearance for the wider board.
- Preserve existing quest saves, XP, rewards, owned-storage turn-ins, controller/mouse controls and host-selected location sync.
- Add upgrade-save and compiled static-board checks. The in-game board appearance still requires testing.

## 0.1.4

- Replace manually reconstructed skin/bone bindings with an intact visual clone; remove all gameplay components before activation.
- Draw character poses through ordinary MeshRenderers, with original rest geometry or the last valid pose retained when skinning fails.
- Validate pose bounds and fit the body to a 1.8-metre standing height at its interaction point.
- Double-buffer pose meshes, cap nearby pose updates at 15 Hz and stop repeatedly failing bakes. Release all owned mesh buffers on cleanup.
- Add geometry validation/scale regression tests and rig/mesh diagnostics. Build and 30 automated checks pass; in-game visibility is not yet verified.

## 0.1.3

- Address the invisible quartermaster model: activate copied visual branches, clear renderer suppression, and select a layer visible to the player camera.
- Use private opaque character materials with the source base textures/normal maps, independent of native spawn or dissolve state. Shared game materials remain untouched.
- Restore collapsed visual ancestors and protect visibility against animation activation tracks.
- Keep only the highest-detail meshes from copied LOD groups, avoiding overlapping LOD duplicates.
- Add renderer/material diagnostics and release owned materials during cleanup. Quest progress, placement and interaction are unchanged.
- Build and 28 automated checks pass; actual appearance requires in-game validation.

## 0.1.2

- Add scene-trader and loaded-asset visual fallbacks when the trader database has no usable prefab.
- Replace the narrow placement search with walkable-ground sampling, multiple surface checks and a wider set of nearby positions.
- Fall back from the original starting spawn to a loaded trader or a clear outdoor spot near the host. Never use the player's maze position as the camp fallback.
- Synchronize the host-selected NPC location to clients instead of independently picking different spots.
- Add a readable direction/distance marker, including off-screen directions and explicit placement-waiting status.
- Keep placement failures retryable, with stage-specific diagnostics instead of silently leaving no NPC.
- Add regression checks for the observed 22-quest catalog and location-message validation. Runtime spawn/visual checks still require a game restart.

## 0.1.1

- Fixed startup aborting on the unavailable ClothHelmet reward. Runtime asset checks now isolate unavailable assignments instead of stopping the quest system.
- Preserve unavailable accepted quests and allow abandoning them; reject their turn-ins without taking any items.
- Replace View/Share with the game's actual Interact binding and display its button name. Keyboard F9 remains an alternative.
- Reserve conflicting native actions only while the quartermaster is in focus; wait for button release before returning them to gameplay.
- Broaden facing tolerance and ignore the player's own colliders during visibility checks. Looking up/down at close range no longer loses the talk prompt.
- Accept validated partial quest catalogs over multiplayer and report NPC placement retries in the log.
- Add four automated runtime-catalog regression cases. In-game verification remains required.

## 0.1.0

- Added the Quartermaster beside the original starting spawn using the native trader's visual model.
- Added 12 supply and 12 hunting assignments with XP and existing-game item rewards.
- Added personal XP, level unlocks, six active quests and persistent completion history.
- Added owned-storage and worn-backpack turn-ins with server-side inventory planning and rollback.
- Added a palette-aware journal with reward icons, live counts, mouse controls and controller navigation.
- Added host validation and multiplayer quest messages.
- Added automated progression, transaction and installed-game API checks.
