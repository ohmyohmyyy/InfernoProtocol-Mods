# Patterned finishes — 0.4.1

Flat tints have been replaced with procedural base-texture patterns: Woodland, Blue Circuit, Embercrack and Brasswork (same numeric unlock IDs). Source albedo shading and sampled alpha are retained; normal, metallic and emission maps are unchanged. Each active material owns its generated texture and releases it on removal. The station shows a pattern swatch plus the original item icon, not an exact 3D preview. Existing texture UVs determine placement; seams and small features need in-game review. Local-only and specialized-renderer limitations below still apply.

The small cabinet marked FINISHES beside the board uses the normal interact button. Stand nearer the cabinet than the board. Choose a finish on the left, an equipment type in the middle, then Apply. Original restores the default appearance. No possession check or inventory consumption is involved.

Quest rewards (retroactive for completed contracts):

- A place to begin: Woodland.
- Firm foundations: Blue Circuit.
- Shambling company: Embercrack.
- A proper workshop: Brasswork.
- Larger teeth: Arctic Digital.
- Keep it together: Hazard.
- Forest sentries: Tigerstripe.
- Broken ambush: Crimson Rally.
- Supply contract s05 (filled water bottles): Tidebreaker.
- Supply contract s09 (copper ore): Topographic.
- Veteran hunter: Royal Weave.
- Scavenger patrol: Desert Shards.

Each palette can be selected independently for 12 supported weapon/tool/clothing types listed in the station. Choices persist per character within the world's quest save. Completing these quests still gives the ordinary item and XP, not just a skin.

These patterned finishes use renderer-owned material copies. Currently applies to the local player's static held-item renderer and individual worn clothing renderers; specialized animated item modules and inventory mannequin previews are not yet supported. No changes to stats, item data, world loot, or shared material assets. Other players do not yet receive your visual appearance.

Shader compatibility, each clothing mesh, pooled held-item swaps, station placement and controller readability need in-game verification before publishing.

Test: unlock a palette, apply without owning the item, later equip it, switch items, restore Original, reconnect/reload, and confirm no tint remains on unrelated items or skin/body meshes. Check wardrobe and board interactions independently. Check a pre-existing completed quest unlocks its palette without awarding XP/items again.

Exploration HUD is reduced to a small LV label and thin bar (roughly 144x39 reference pixels), without the old large badge/heading/XP numbers.
