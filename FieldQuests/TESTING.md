# FieldQuests validation

## Completed automated checks

The verification runner links the actual quest/progression/transaction code used by the mod. It also inspects the installed game's interop assembly using dnlib.

- 24 unique quests; positive XP; all level gates reachable using available quests.
- Level boundaries and multi-level XP gains.
- No retroactive kill progress; species matching; caps; simultaneous matching hunts.
- Six active quests; locked quest refusal; duplicate accept; abandon/reset.
- Duplicate claim protection; progression overflow does not mutate state.
- Personal progress JSON round-trip and separation between players.
- Resource planning across multiple containers; exact quantities; partial-stack metadata preservation.
- Insufficient resources; full carried inventory; reward placement after freeing a slot.
- Hunts do not consume unrelated resources.
- Verified inventory commit; stale snapshot refusal before writes.
- Mid-write exception rollback; silent native-write failure; rollback fault escalation.
- Every objective/reward enum and required adapter/hook signature exists in the installed game assembly.
- Runtime-catalog filtering: missing rewards, missing objective items/creatures, lookup exceptions, and a fully available catalog. Enum checks alone do not validate runtime assets.

The suite also checks pre-board saves load unchanged and the compiled board has no character rig/clone dependencies.

Run `dotnet run --project tests/FieldQuests.Tests/FieldQuests.Tests.csproj -c Release` from the repository root.

## In-game acceptance pass still required

1. Boot a test world. Look for `FieldQuests 0.2.3 loaded`, `FieldQuests catalog ready` and `Quest Board ready at`. Confirm the board uses saved ground coordinates (-23.165, 0.199, -20.094), yaw 3.526, without waiting for traders. Visually check the user's measured spot beside the house and verify the steps remain clear. Missing assets such as ClothHelmet should warn only for their quests.
2. Follow the QUEST BOARD marker. Verify the timber frame, pinned papers and sign are visible from both sides and aligned with the posts/panel collision. No character model should appear. Confirm placement is on clear ground and the off-screen marker points toward the board.
3. Approach and face either side of the board. Verify Read Board appears, walls block interaction, and walking away hides the prompt.
4. Open using F9, then your normal controller Interact button as shown on screen. Test a rebound keyboard/controller Interact button. Confirm no reload, native interaction or other overlapping action occurs. Walk away while holding Interact and release; native interaction should recover. Check pause, inventory, death and disconnect restore input correctly.
5. Use mouse clicks, wheel, every filter and page control. Check readable layout at your resolution, including long quest descriptions and confirmation messages.
6. Use D-pad, A, B, X and bumpers. Verify no dropped items, item usage, camera motion or Utility Wheel while the journal is open. Hold a navigation key while closing; gameplay should receive it only after release.
7. Accept A place to begin. Put three logs in your bag and five in your owned placed storage. Verify 3 carried / 5 stored; confirm delivery; check exactly eight logs removed, one axe delivered and 100 XP granted.
8. Repeat with insufficient resources, all inventory/backpack slots full, and a turn-in that frees a slot. No materials should disappear on rejected claims.
9. Verify someone else's chest and world loot are excluded. Verify your worn backpack is included.
10. Accept Small trouble, kill rats directly, and confirm only matching kills after accepting count. Try a nonmatching creature and abandon/reaccept.
11. Complete a hunt. Confirm the shown item and XP arrive once. Rapidly click/press confirm; verify no duplicate payout.
12. Save normally, return to the menu, and reload the same world. Verify XP, active hunts and completed quests persist. Load another world and verify separation.
13. Join a 0.2.0 host from a second 0.2.0 client. Confirm both logs report the same board position, including late joins. Test turn-in, ownership, host-credited kills, reconnect, capacity checks and host save/reload. An unmodded host should leave the locator waiting without granting rewards.
14. Test death, leaving interaction range, focus loss and session disconnect while the journal is open; input and cursor should recover.

Runtime visual and multiplayer results are not claimed by the automated suite. Record these results before publishing a release.
