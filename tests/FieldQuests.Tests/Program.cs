using InfernoProtocol.FieldQuests;
using System.Text.Json;
using dnlib.DotNet;

int passed = 0;
Test("Every skin has a valid unique quest unlock; old finish IDs stay stable", () =>
{
    Check(SkinCatalog.Names.Length == 13 && SkinCatalog.Quests.Length == 13, "Expected twelve skins plus Original");
    Check(SkinCatalog.Quests.Skip(1).Distinct().Count() == 12, "Duplicate reward assignment");
    Check(SkinCatalog.Quests.Skip(1).All(q => QuestCatalog.Find(q) != null), "Unknown unlock quest");
    Check(SkinCatalog.Names[1] == "Woodland" && SkinCatalog.Names[4] == "Brasswork", "Existing choices shifted");
});
Test("Skin pages wrap and reach every finish including the partial last page", () =>
{
    Check(SkinCatalog.PageStart(0, -1) == 10 && SkinCatalog.PageStart(12, 1) == 0, "Page wrap failed");
    Check(SkinCatalog.PageStart(4, 1) == 5 && SkinCatalog.PageStart(9, 1) == 10 && SkinCatalog.PageStart(10, -1) == 5, "Page navigation skips finishes");
});
Test("Every finish is a deterministic multicolor pattern, not a flat tint", () =>
{
    var fingerprints = new HashSet<string>();
    for (int finish = 1; finish < SkinCatalog.Names.Length; finish++)
    {
        var colors = new HashSet<string>();
        for (int y = 0; y < 37; y++) for (int x = 0; x < 37; x++)
        {
            var a = FinishPattern.Sample(finish, x / 37f, y / 37f);
            var b = FinishPattern.Sample(finish, x / 37f, y / 37f);
            Check(a.R == b.R && a.G == b.G && a.B == b.B, "Pattern is nondeterministic");
            Check(a.R >= 0 && a.R <= 1 && a.G >= 0 && a.G <= 1 && a.B >= 0 && a.B <= 1, "Invalid texture channel");
            colors.Add($"{a.R:F3}/{a.G:F3}/{a.B:F3}");
        }
        Check(colors.Count >= 3, "Finish is a flat tint");
        Check(fingerprints.Add(string.Join(";", colors.OrderBy(c => c))), "Duplicate finish palettes");
    }
});
Test("Skins unlock from completed quests without requiring equipment ownership", () =>
{
    Progress p = new();
    Check(!SkinCatalog.Select(p.Skins, p.Completed, "LeatherShoes", 1), "Locked finish applied");
    p.Completed.Add("s01");
    Check(SkinCatalog.Select(p.Skins, p.Completed, "LeatherShoes", 1), "Earned finish rejected without inventory");
    Check(SkinCatalog.Select(p.Skins, p.Completed, "WoodenSpear", 1), "Same earned palette unusable on supported item");
    Check(p.Skins["LeatherShoes"] == 1 && p.Skins["WoodenSpear"] == 1, "Item-type choices were not independent");
    Check(!SkinCatalog.Select(p.Skins, p.Completed, "Unknown", 1) && !SkinCatalog.Select(p.Skins, p.Completed, "LeatherShoes", 99), "Invalid cosmetic accepted");
    Check(SkinCatalog.Select(p.Skins, p.Completed, "LeatherShoes", 0) && !p.Skins.ContainsKey("LeatherShoes"), "Original not restored");
});
Test("Skin choices round-trip and legacy progress inherits unlocks without reward replay", () =>
{
    var p = JsonSerializer.Deserialize<Progress>("{\"Xp\":100,\"Active\":{},\"Completed\":[\"s01\"]}");
    Check(p.Skins.Count == 0 && SkinCatalog.Unlocked(1, p.Completed), "Legacy unlock lost");
    SkinCatalog.Select(p.Skins, p.Completed, "ScrapHelmet", 1);
    var restored = JsonSerializer.Deserialize<Progress>(JsonSerializer.Serialize(p));
    Check(restored.Skins["ScrapHelmet"] == 1 && restored.Xp == 100, "Appearance save changed progress");
    Check(restored.Accept(QuestCatalog.Find("s01")) == "Already completed.", "Unlock replayed old quest");
});
Test("Exploration progress packet preserves XP without a quest or storage payload", () =>
{
    var packet = new QuestView { ProgressOnly = true, Xp = 340 };
    var copy = JsonSerializer.Deserialize<QuestView>(JsonSerializer.Serialize(packet));
    Check(copy.ProgressOnly && copy.Xp == 340 && copy.Rows.Count == 0 && !copy.LocationOnly, "Progress packet changed");
    Check(QuestCatalog.Level(copy.Xp) == 2, "HUD level mismatch");
});
Test("Starter rewards avoid gathering prerequisites and obsolete contract assets", () =>
{
    Check(QuestCatalog.Find("s01").Reward == "LeatherShoes", "Log delivery returns prerequisite axe");
    Check(QuestCatalog.Find("s02").Reward == "WoodenSpear", "Stone delivery returns prerequisite pickaxe");
    Check(QuestCatalog.All.All(q => q.Reward != "ClothHelmet" && q.Target != "CannedBeansClosed"), "Known unavailable contract assets remain");
    Check(QuestCatalog.All.All(q => q.Hunt || q.Target != q.Reward), "Supply quest returns its own objective");
});
Test("Saved anonymous player chest is counted for host despite missing placement ID", () =>
    Check(StorageAccess.CanUse(0, 123, true, true, true, 0, true), "Reloaded chest excluded"));
Test("Anonymous storage is not exposed to remote players or world loot", () =>
{
    Check(!StorageAccess.CanUse(0, 123, false, true, true, 0, false), "Remote player can consume anonymous storage");
    Check(!StorageAccess.CanUse(0, 123, true, true, false, 42, true), "World loot included");
    Check(!StorageAccess.CanUse(0, 123, true, false, true, 0, false), "Unowned unsaved chest included");
});
Test("Observed saved chest and future placed storage are available to host", () =>
{
    Check(StorageAccess.CanUse(123, 123, false, false, true, 42, true), "Owned chest excluded by component type");
    Check(StorageAccess.CanUse(456, 123, true, true, true, 169, false), "Observed saved chest excluded by differing placer ID");
    Check(StorageAccess.CanUse(456, 123, true, false, true, 174, false), "Future chest excluded");
    Check(StorageAccess.CanUse(456, 123, true, false, false, 175, false), "Future storage rack excluded");
    Check(!StorageAccess.CanUse(456, 123, false, true, true, 169, false), "Remote ownership restriction lost");
});
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
void Test(string name, Action body)
{
    body(); passed++; Console.WriteLine("PASS " + name);
}
Slot Slot(string container, int index, string item, int count, bool carried = false, byte durability = 0) =>
    new() { Container = container, Index = index, RewardDestination = carried, Before = new StackData(item, count, durability, 7, 9) };
Quest supply = QuestCatalog.Find("s01");
StackData reward = new("StoneAxe", 1, 100);

Test("24 unique assignments, positive XP and balanced starter unlocks", () =>
{
    Check(QuestCatalog.All.Length == 24, "Expected 24 quests");
    Check(QuestCatalog.All.Select(q => q.Id).Distinct().Count() == 24, "Duplicate IDs");
    Check(QuestCatalog.All.All(q => q.Xp > 0 && q.Amount > 0 && q.Level >= 1), "Invalid quest");
    var pending = QuestCatalog.All.ToList();
    int xp = 0;
    while (pending.Count > 0)
    {
        Quest available = pending.FirstOrDefault(q => q.Level <= QuestCatalog.Level(xp));
        Check(available != null, "Level gate makes remaining quests unreachable");
        xp += available.Xp; pending.Remove(available);
    }
    Check(QuestCatalog.Level(xp) >= 5, "Progression never reaches final tier");
});
Test("Missing runtime reward hides only its assignment, not the board catalog", () =>
{
    var missing = QuestCatalog.Unavailable(name => name != "LeatherFlaskEmpty", _ => true);
    Check(missing.Count == 1 && missing.ContainsKey("s04"), "Missing helmet stopped unrelated quests");
    Check(QuestCatalog.All.Length - missing.Count == 23, "Healthy quests were lost");
});
Test("Runtime objectives and creatures are checked independently", () =>
{
    var missing = QuestCatalog.Unavailable(name => name != "Log", name => name != "Rat");
    Check(missing.Count == 2 && missing.ContainsKey("s01") && missing.ContainsKey("h01"), "Missing objective was accepted");
});
Test("Asset lookup exceptions are isolated and don't truncate validation", () =>
{
    var missing = QuestCatalog.Unavailable(name => name == "LeatherFlaskEmpty" ? throw new InvalidOperationException("Missing asset") : true, _ => true);
    Check(missing.Count == 1 && missing.ContainsKey("s04"), "Lookup exception escaped validation");
    Check(!missing.ContainsKey("h12"), "Later quests weren't preserved");
});
Test("Healthy runtime catalog preserves all assignments", () =>
{
    Check(QuestCatalog.Unavailable(_ => true, _ => true).Count == 0, "Valid catalog was reduced");
});
Test("Catalog remains reachable when two replacement assets are unavailable", () =>
{
    var unavailable = QuestCatalog.Unavailable(name => name != "LeatherFlaskEmpty" && name != "ClothPiece", _ => true);
    var pending = QuestCatalog.All.Where(q => !unavailable.ContainsKey(q.Id)).ToList();
    Check(pending.Count == 22, "Wrong observed runtime catalog");
    int xp = 0;
    while (pending.Count > 0)
    {
        Quest next = pending.FirstOrDefault(q => q.Level <= QuestCatalog.Level(xp));
        Check(next != null, "Unavailable quests block progression");
        xp += next.Xp; pending.Remove(next);
    }
});
Test("Host quest-board location round-trips with exact coordinates", () =>
{
    var packet = new QuestView { LocationOnly = true, Location = new QuestBoardLocation { X = 17.5f, Y = -4, Z = 92, Yaw = 135 } };
    var copy = JsonSerializer.Deserialize<QuestView>(JsonSerializer.Serialize(packet));
    Check(copy.LocationOnly && copy.Location.IsValid && copy.Location.X == 17.5f && copy.Location.Y == -4 && copy.Location.Z == 92 && copy.Location.Yaw == 135, "Location changed in transit");
});
Test("Quest-board location rejects invalid and extreme coordinates", () =>
{
    Check(new QuestBoardLocation().IsValid, "Origin is a valid location");
    Check(!new QuestBoardLocation { X = float.NaN }.IsValid, "NaN accepted");
    Check(!new QuestBoardLocation { Y = float.PositiveInfinity }.IsValid, "Infinity accepted");
    Check(!new QuestBoardLocation { Z = 1000001 }.IsValid, "Extreme position accepted");
    Check(!new QuestBoardLocation { Yaw = float.NaN }.IsValid, "Invalid rotation accepted");
});
Test("Pre-board quest progress loads without migration or reset", () =>
{
    Progress saved = JsonSerializer.Deserialize<Progress>("{\"Xp\":340,\"Active\":{\"h01\":2},\"Completed\":[\"s01\",\"s02\"]}");
    Check(saved.Xp == 340 && saved.Active["h01"] == 2 && saved.Completed.SetEquals(new[] { "s01", "s02" }), "Old quest progress changed");
});
Test("Board uses the latest saved ground position and yaw without player-height offset", () =>
{
    Check(BoardPlacement.X == -23.165f && BoardPlacement.Y == .199f && BoardPlacement.Z == -20.094f && BoardPlacement.Yaw == 3.526f, "Saved placement changed");
});
Test("House placement uses the visitor's right for rotated entrances", () =>
{
    var north = BoardSiteRules.Offset(0, 1, 4, 1.4f);
    Check(Math.Abs(north.X + 4) < .0001f && Math.Abs(north.Z - 1.4f) < .0001f, "Right side reversed for north-facing house");
    var east = BoardSiteRules.Offset(1, 0, 4, 1.4f);
    Check(Math.Abs(east.X - 1.4f) < .0001f && Math.Abs(east.Z - 4) < .0001f, "Right side reversed for east-facing house");
});
Test("Board edge stays outside the doorway approach clearance", () =>
{
    Check(!BoardSiteRules.ClearsEntrance(2), "Board can intrude into entrance corridor");
    foreach (float offset in new[] { 4f, 4.6f, 3.6f, 5.2f, 5.8f })
        Check(BoardSiteRules.ClearsEntrance(offset), "Preferred site lacks entry clearance");
});
Test("Outdoor anchor excludes the observed high-altitude trading interior", () =>
{
    Check(!BoardSiteRules.IsOutdoorAnchor(3823.93f, 3900), "Staged interior accepted as outdoor house");
    Check(BoardSiteRules.IsOutdoorAnchor(.8f, 20), "Nearby outdoor trader rejected");
    Check(!BoardSiteRules.IsOutdoorAnchor(0, 500), "Unrelated distant trading area accepted");
});
Test("Porch placement mirrors trader's side while keeping stairs clear", () =>
{
    Check(BoardSiteRules.OppositeTraderOffset(-3.6f) == 3.6f, "Trader position not mirrored");
    Check(BoardSiteRules.ClearsEntrance(BoardSiteRules.OppositeTraderOffset(-1)), "Narrow house blocks stairs");
});
Test("XP boundaries and multiple level gains", () =>
{
    Check(QuestCatalog.Level(0) == 1 && QuestCatalog.Level(199) == 1, "Level 1 bounds");
    Check(QuestCatalog.Level(200) == 2 && QuestCatalog.Level(599) == 2, "Level 2 bounds");
    Check(QuestCatalog.Level(600) == 3 && QuestCatalog.Level(2000) == 5, "Multi-level gain");
});
Test("Kills before acceptance never count; correct species only", () =>
{
    Progress p = new(); p.Kill("Rat"); p.Accept(QuestCatalog.Find("h01"));
    Check(p.Active["h01"] == 0, "Retroactive credit");
    p.Kill("Zombie"); Check(p.Active["h01"] == 0, "Wrong species");
    for (int i = 0; i < 20; i++) p.Kill("Rat");
    Check(p.Active["h01"] == 5, "Kill counter not capped");
});
Test("One kill advances all accepted matching hunts", () =>
{
    Progress p = new() { Xp = 200 };
    p.Accept(QuestCatalog.Find("h02")); p.Accept(QuestCatalog.Find("h04")); p.Kill("Zombie");
    Check(p.Active["h02"] == 1 && p.Active["h04"] == 1, "Matching quests not updated");
});
Test("Locked quests and six-quest journal limit", () =>
{
    Progress p = new(); p.Accept(QuestCatalog.Find("h12")); Check(p.Active.Count == 0, "Accepted locked quest");
    foreach (Quest q in QuestCatalog.All.Where(q => q.Level == 1)) p.Accept(q);
    Check(p.Active.Count == 6, "Journal limit failed");
});
Test("Duplicate accept retains hunt progress; abandoning resets it", () =>
{
    Progress p = new(); Quest q = QuestCatalog.Find("h01"); p.Accept(q); p.Kill("Rat"); p.Accept(q);
    Check(p.Active[q.Id] == 1, "Duplicate acceptance reset progress");
    p.Active.Remove(q.Id); p.Accept(q); Check(p.Active[q.Id] == 0, "Abandon retained progress");
});
Test("Duplicate claim cannot award XP twice", () =>
{
    Progress p = new(); p.Accept(supply); p.Complete(supply);
    bool rejected = false;
    try { p.Complete(supply); } catch (InvalidOperationException) { rejected = true; }
    Check(rejected && p.Xp == supply.Xp && p.Completed.Count == 1, "Duplicate payout");
    p.Accept(supply); Check(!p.Active.ContainsKey(supply.Id), "Reaccepted completed quest");
});
Test("Reward XP overflow leaves progression untouched", () =>
{
    Progress p = new() { Xp = int.MaxValue }; p.Active.Add(supply.Id, 0);
    try { p.Complete(supply); } catch (OverflowException) { }
    Check(p.Active.ContainsKey(supply.Id) && p.Completed.Count == 0, "Partially completed overflow");
});
Test("Progress saves and reloads personal state", () =>
{
    Progress p = new(); p.Accept(supply); p.Complete(supply); p.Accept(QuestCatalog.Find("h01")); p.Kill("Rat");
    var all = new Dictionary<string, Progress> { ["111"] = p, ["222"] = new Progress() };
    var loaded = JsonSerializer.Deserialize<Dictionary<string, Progress>>(JsonSerializer.Serialize(all));
    Check(loaded["111"].Xp == 100 && loaded["111"].Active["h01"] == 1 && loaded["111"].Completed.Contains("s01"), "Progress roundtrip failed");
    Check(loaded["222"].Xp == 0, "Player isolation failed");
});
Test("Split turn-in across carried items and two storages", () =>
{
    var slots = new List<Slot> { Slot("bag", 0, "Log", 3, true), Slot("chest1", 0, "Log", 4), Slot("chest2", 0, "Log", 5) };
    Check(TurnInPlanner.Plan(slots, supply, reward, out _), "Split turn-in refused");
    Check(slots[0].After == reward && slots[1].After.Count == 0 && slots[2].After.Count == 4, "Incorrect split/reward");
    Check(slots[2].After.Var1 == 7 && slots[2].After.Var2 == 9, "Partial stack metadata changed");
});
Test("Reward requires carried capacity, never gets hidden in storage", () =>
{
    var slots = new List<Slot> { Slot("bag", 0, "Bow", 1, true), Slot("chest", 0, "Log", 8) };
    Check(!TurnInPlanner.Plan(slots, supply, reward, out string error) && error.Contains("slot"), "Full backpack accepted");
    Check(slots[1].Before.Count == 8, "Planner mutated actual snapshot");
});
Test("Insufficient resources refuse without modifying snapshots", () =>
{
    var slots = new List<Slot> { Slot("bag", 0, "Log", 7, true) };
    Check(!TurnInPlanner.Plan(slots, supply, reward, out _), "Underpayment accepted");
    Check(slots[0].Before.Count == 7, "Snapshot mutated");
});
Test("Hunt reward consumes no unrelated items", () =>
{
    var slots = new List<Slot> { Slot("bag", 0, "Log", 5, true), Slot("bag", 1, "Unkown", 0, true) };
    Check(TurnInPlanner.Plan(slots, QuestCatalog.Find("h01"), reward, out _), "Hunt reward refused");
    Check(slots[0].After == slots[0].Before && slots[1].After == reward, "Hunt ate inventory");
});
Test("Inventory transaction applies and verifies every change", () =>
{
    var slots = new List<Slot> { Slot("bag", 0, "Log", 3, true), Slot("chest", 0, "Log", 8) };
    TurnInPlanner.Plan(slots, supply, reward, out _);
    var live = slots.ToDictionary(s => s, s => s.Before);
    SlotTransaction.Commit(slots, s => live[s], (s, data) => live[s] = data);
    Check(live[slots[0]] == reward && live[slots[1]].Count == 3, "Wrong live transaction");
});
Test("Concurrent inventory change aborts before any write", () =>
{
    var slots = new List<Slot> { Slot("bag", 0, "Log", 8, true) };
    TurnInPlanner.Plan(slots, supply, reward, out _);
    int writes = 0;
    try { SlotTransaction.Commit(slots, s => new StackData("Log", 7), (s, data) => writes++); }
    catch (InvalidOperationException) { }
    Check(writes == 0, "Wrote stale transaction");
});
Test("Exception mid-transaction restores all previously written stacks", () =>
{
    var slots = new List<Slot> { Slot("bag", 0, "Log", 3, true), Slot("chest", 0, "Log", 8) };
    TurnInPlanner.Plan(slots, supply, reward, out _);
    var live = slots.ToDictionary(s => s, s => s.Before);
    int writes = 0; bool rejected = false;
    try
    {
        SlotTransaction.Commit(slots, s => live[s], (s, data) =>
        { if (++writes == 2) throw new IOException("Simulated failure"); live[s] = data; });
    }
    catch (IOException) { rejected = true; }
    Check(rejected && slots.All(s => live[s] == s.Before), "Rollback lost items");
});
Test("Silently rejected native write is detected and rolled back", () =>
{
    var slots = new List<Slot> { Slot("bag", 0, "Log", 8, true) };
    TurnInPlanner.Plan(slots, supply, reward, out _);
    var live = slots.ToDictionary(s => s, s => s.Before); bool rejected = false;
    try { SlotTransaction.Commit(slots, s => live[s], (s, data) => { }); }
    catch (InvalidOperationException) { rejected = true; }
    Check(rejected && live[slots[0]] == slots[0].Before, "Silent failure accepted");
});
Test("Reentrant slot change is preserved while our earlier write rolls back", () =>
{
    var slots = new List<Slot> { Slot("bag", 0, "Log", 3, true), Slot("chest", 0, "Log", 8) };
    TurnInPlanner.Plan(slots, supply, reward, out _);
    var live = slots.ToDictionary(s => s, s => s.Before);
    bool first = true;
    try
    {
        SlotTransaction.Commit(slots, s => live[s], (s, data) =>
        {
            live[s] = data;
            if (first) { first = false; live[slots[1]] = new StackData("Log", 6); }
        });
    }
    catch (InvalidOperationException) { }
    Check(live[slots[0]] == slots[0].Before && live[slots[1]].Count == 6, "Overwrote concurrent inventory change");
});
Test("Rollback failure reports a hard fault", () =>
{
    var slots = new List<Slot> { Slot("bag", 0, "Log", 3, true), Slot("chest", 0, "Log", 8) };
    TurnInPlanner.Plan(slots, supply, reward, out _);
    var live = slots.ToDictionary(s => s, s => s.Before); int writes = 0; bool faulted = false;
    try { SlotTransaction.Commit(slots, s => live[s], (s, data) => { if (++writes > 1) throw new IOException(); live[s] = data; }); }
    catch (AggregateException) { faulted = true; }
    Check(faulted, "Rollback fault lost");
});

string gameDir = args.Length > 0 ? args[0] : @"C:\Program Files (x86)\Steam\steamapps\common\Inferno Protocol";
using ModuleDefMD game = ModuleDefMD.Load(Path.Combine(gameDir, "BepInEx", "interop", "Assembly-CSharp.dll"));
var types = game.GetTypes().ToDictionary(t => t.FullName);
Test("All 24 objective/reward names exist in installed game enums", () =>
{
    var items = types["Items"].Fields.Select(f => f.Name.String).ToHashSet();
    var npcs = types["Game.AI.NPCs"].Fields.Select(f => f.Name.String).ToHashSet();
    foreach (Quest q in QuestCatalog.All)
    {
        Check(items.Contains(q.Reward), "Missing reward " + q.Reward);
        Check((q.Hunt ? npcs : items).Contains(q.Target), "Missing objective " + q.Target);
    }
});
Test("Required owner, spawn, save, death and inventory adapters exist", () =>
{
    foreach (var pair in new[] {
        ("APlacable", "placedByPlatformId"), ("Game.PlayerOperations.Player", "platformId"),
        ("Game.PlayerOperations.PlayerDependencies", "defaultSpawnPoint"),
        ("Game.LevelOperations.ItemStash", "allStashes"), ("SaveSystem.SaveManager", "onSaveComplete") })
        Check(types[pair.Item1].Properties.Any(p => p.Name == pair.Item2), "Missing game property " + pair);
    Check(types["Game.AI.ANPC"].Methods.Any(m => m.Name == "OnDeathServer_InternalServer" && m.MethodSig.Params.Count == 1 && m.MethodSig.Params[0].FullName == "Game.Combat.DamageData&"), "Kill hook signature mismatch");
    Check(types["Game.Combat.DamageData"].Fields.Any(f => f.Name == "source"), "No damage attribution");
});
Test("Compiled quest tracking never patches the live damage pipeline", () =>
{
    string dll = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../FieldQuests/bin/Release/net6.0/InfernoProtocol.FieldQuests.dll"));
    using ModuleDefMD mod = ModuleDefMD.Load(dll);
    string[] forbidden = { "DamageServer", "ValidateDamageRequestOnServerSide", "DamageServerNoCheck", "DamageServerRpc" };
    var strings = mod.GetTypes().Where(t => t.HasMethods).SelectMany(t => t.Methods)
        .Where(m => m.HasBody).SelectMany(m => m.Body.Instructions)
        .Select(i => i.Operand?.ToString() ?? string.Empty).ToArray();
    foreach (string method in forbidden)
        Check(!strings.Any(s => s.Contains(method)), "ContentPlus still references live damage method " + method);
});
Test("Compiled quest giver uses a static board with no character rig or clone path", () =>
{
    string dll = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../FieldQuests/bin/Release/net6.0/InfernoProtocol.FieldQuests.dll"));
    using ModuleDefMD mod = ModuleDefMD.Load(dll);
    var storageCalls = mod.GetTypes().Single(t => t.Name == "QuestService").Methods.Single(m => m.Name == "Containers")
        .Body.Instructions.Select(i => i.Operand).OfType<IMethod>().Select(m => m.FullName).ToArray();
    Check(storageCalls.Any(c => c.Contains("get_allStashes")), "Live stash registry missing");
    Check(!storageCalls.Any(c => c.Contains("FindObjects") || c.Contains("GetComponent") || c.Contains("Distance")), "Storage discovery performs unnecessary searches");
    Check(!mod.GetTypes().Any(t => t.Name == "Quartermaster"), "Old character implementation remains");
    var board = mod.GetTypes().Single(t => t.Name == "QuestBoard");
    var calls = board.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions)
        .Select(i => i.Operand).OfType<IMethod>().Select(m => m.FullName).ToArray();
    Check(calls.Any(c => c.Contains("GameObject::CreatePrimitive")), "Board geometry missing");
    Check(!calls.Any(c => c.Contains("SkinnedMeshRenderer") || c.Contains("Animator") || c.Contains("BakeMesh") || c.Contains("::Instantiate")), "Character rendering dependency remains");
});
Console.WriteLine($"\n{passed} checks passed. Runtime scene, UI rendering, and multiplayer still require in-game validation.");
