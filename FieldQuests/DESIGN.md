# Quest/reward pass — 0.3.0

Principles: starter deliveries broaden equipment rather than return a prerequisite; workshop deliveries provide a tool upgrade; hunts provide combat equipment or protection; XP is guaranteed on completion. These are initial tuning judgments, not measured economy values.

| Contract | Revised exchange | Reason |
| --- | --- | --- |
| A place to begin | 8 logs → leather shoes + 100 XP | Useful off-role equipment, not the prerequisite axe |
| Firm foundations | 12 stone → wooden spear + 120 XP | Early defense rather than a gathering prerequisite |
| Light the way | 3 torches → flashlight + 140 XP | Keep the lighting upgrade |
| First-aid reserve | 3 bandages → empty leather flask + 150 XP | Lower medical cost; replace unavailable helmet with reusable utility |
| A warm welcome | 2 filled water bottles → stone pickaxe + 130 XP | Lower survival cost; unlock another gathering role |
| Field dressings (s06) | 6 cloth pieces → scrap helmet + 180 XP | Replace unavailable canned-beans objective |
| A proper workshop | 8 planks → scrap axe + 220 XP | Deliberate upgrade, lower processed-material cost |
| Keep it together | 3 tape → scrap pickaxe + 240 XP | Lower scarce-consumable cost |
| Copper prospect | 8 copper ore → bronze hammer + 280 XP | Lower ore cost; building role |
| The other half | 8 tin ore → bronze axe + 280 XP | Lower ore cost; harvesting upgrade |
| Bronze standard | 4 bronze ingots → mining helmet + 360 XP | Lower refined-material cost; mining utility |
| Power reserve | 3 batteries → gas mask + 360 XP | Lower consumable cost; expedition protection |

Hunts reviewed: rat reward becomes nail bat, early zombie reward becomes scrap machete, giant-fly reward becomes gas mask. Other hunt rewards remain as combat/protection progression. Zombie objectives deliberately overlap when accepted together. Crossbow and Colt rewards still require separately obtained ammunition; ammo bundles and reward choices are future work, not implemented here.

Save compatibility: IDs, XP thresholds and hunt targets/counts are unchanged. Accepted supply quests use updated costs/rewards immediately; s06 now requests cloth instead of beans. Completed quests are not reopened or rewarded again. New assets are still validated at runtime; enum presence alone is not proof of a usable asset.

QoL/safety: confirmation shows carried/stored consumption, storage text describes host sharing accurately, invalid rewards and XP overflow are rejected before mutation. Existing duplicate-claim and transactional inventory checks remain.

Performance: storage comes from the native registry, not scene/radius searches. Counts refresh only with board polling; hunt claims read carried reward capacity without enumerating storage. Full-save persistence remains tied to the game's save event.

Manual validation: check all 24 assets at startup, inspect revised reward names/icons, complete a supply quest using shared storage, test full bags and repeated confirmation, and confirm existing saves retain progress. Combat difficulty and actual material acquisition time still need playtesting; no claim of final economic balance.
