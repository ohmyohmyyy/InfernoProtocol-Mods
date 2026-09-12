using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Game.AI;
using Game.Combat;
using Game.Data;
using Game.LevelOperations;
using Game.PlayerOperations;
using SaveSystem;
using Unity.Netcode;
using UnityEngine;

namespace InfernoProtocol.FieldQuests;

internal sealed class QuestService
{
    private Dictionary<string, Progress> _players = new();
    private string _saveFile;
    private bool _faulted;
    private readonly HashSet<int> _deaths = new();
    private readonly Dictionary<int, ulong> _lastAttackers = new();
    private readonly Dictionary<int, ulong> _pendingAttackers = new();
    private static Dictionary<string, string> _unavailable = new();
    internal bool Ready => _saveFile != null && !_faulted;
    internal void Load()
    {
        string path = SaveManager.GetSaveFile("FieldQuests.v1.json");
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("No world save path.");
        if (File.Exists(path))
        {
            _players = JsonSerializer.Deserialize<Dictionary<string, Progress>>(File.ReadAllText(path))
                ?? throw new InvalidDataException("Empty quest save.");
            foreach (Progress p in _players.Values)
                if (p == null || p.Active == null || p.Completed == null || p.Xp < 0 || p.Xp > 10000000)
                    throw new InvalidDataException("Invalid quest progress.");
        }
        _saveFile = path;
        QuestPlugin.LogSource.LogInfo($"Quest save loaded: {path}");
    }
    internal void SaveWithWorld()
    {
        if (!Ready) return;
        string temp = _saveFile + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(_saveFile));
        File.WriteAllText(temp, JsonSerializer.Serialize(_players));
        if (File.Exists(_saveFile)) File.Replace(temp, _saveFile, _saveFile + ".bak");
        else File.Move(temp, _saveFile);
    }
    private Progress Get(Player player)
    {
        // Offline players use the local identity; platform IDs distinguish online players.
        string key = player.platformId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!_players.TryGetValue(key, out Progress p)) _players.Add(key, p = new Progress());
        return p;
    }
    internal void Respawn(ANPC npc)
    {
        int instanceId = npc.GetInstanceID();
        _deaths.Remove(instanceId);
        _lastAttackers.Remove(instanceId);
        _pendingAttackers.Remove(instanceId);
    }
    internal void BeginDamage(ANPC npc, ulong causerId)
    {
        if (!Ready || npc == null || !npc.IsServer) return;
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening || !manager.IsServer ||
            !manager.ConnectedClients.TryGetValue(causerId, out NetworkClient client) || client?.PlayerObject == null) return;
        int instanceId = npc.GetInstanceID();
        _pendingAttackers[instanceId] = causerId;
        // Host/solo damage originates inside this process and does not always take
        // the remote-client validation path. Its connected local client ID is
        // already authoritative, so retain it beyond the DamageServer call.
        if (causerId == manager.LocalClientId) _lastAttackers[instanceId] = causerId;
    }
    internal void ValidateDamage(ANPC npc, ulong causerId, bool accepted)
    {
        if (!Ready || npc == null || !npc.IsServer) return;
        int instanceId = npc.GetInstanceID();
        if (accepted)
        {
            BeginDamage(npc, causerId);
            if (_pendingAttackers.TryGetValue(instanceId, out ulong pending) && pending == causerId)
                _lastAttackers[instanceId] = causerId;
        }
        else if (_pendingAttackers.TryGetValue(instanceId, out ulong pending) && pending == causerId)
        {
            _pendingAttackers.Remove(instanceId);
        }
    }
    internal void EndDamage(ANPC npc, ulong causerId)
    {
        if (npc == null) return;
        int instanceId = npc.GetInstanceID();
        if (_pendingAttackers.TryGetValue(instanceId, out ulong pending) && pending == causerId)
            _pendingAttackers.Remove(instanceId);
    }
    internal void Death(ANPC npc, DamageData damage)
    {
        int instanceId = npc.GetInstanceID();
        if (!Ready || !npc.IsServer || _deaths.Contains(instanceId)) return;
        Player player = ResolveDamageSource(damage);
        if (player == null && _lastAttackers.TryGetValue(instanceId, out ulong causerId))
            player = ResolveClientPlayer(causerId);
        if (player == null && _pendingAttackers.TryGetValue(instanceId, out causerId))
            player = ResolveClientPlayer(causerId);
        if (player == null || !player.IsSpawned || !HasPersistentIdentity(player)) return;
        _deaths.Add(instanceId);
        _lastAttackers.Remove(instanceId);
        _pendingAttackers.Remove(instanceId);
        string creature = npc.npcId.ToString();
        Progress progress = Get(player);
        bool tracked = QuestCatalog.All.Any(q => q.Hunt && q.Target == creature && progress.Active.ContainsKey(q.Id));
        progress.Kill(creature);
        if (tracked) QuestPlugin.LogSource.LogInfo($"Quest kill credited: {creature} to client {player.OwnerClientId}.");
    }
    private static Player ResolveDamageSource(DamageData damage)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !damage.source.TryGet(out NetworkBehaviour source, manager) || source == null) return null;
        Player player = source.TryCast<Player>();
        if (player == null) player = source.GetComponentInParent<Player>();
        if (player == null && source.NetworkObject != null) player = ResolveClientPlayer(source.NetworkObject.OwnerClientId);
        return player;
    }
    private static Player ResolveClientPlayer(ulong clientId)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) || client?.PlayerObject == null)
            return null;
        Player player = client.PlayerObject.GetComponent<Player>();
        if (player != null) return player;
        foreach (Player candidate in UnityEngine.Object.FindObjectsOfType<Player>())
            if (candidate != null && candidate.IsSpawned && candidate.OwnerClientId == clientId) return candidate;
        return null;
    }
    internal QuestView Execute(Player player, string verb, string id, Vector3 npcPosition)
    {
        if (!Ready) return new QuestView { Message = "Quest save unavailable. Check the game log." };
        if (player == null || !player.IsSpawned || player.isDead) return new QuestView { Message = "Player unavailable." };
        if (!HasPersistentIdentity(player)) return new QuestView { Message = "A persistent platform ID is required for multiplayer quest progress." };
        Progress p = Get(player);
        p.Skins ??= new Dictionary<string, int>();
        if (verb == "progress") return Appearance(p, new QuestView { ProgressOnly = true, Xp = p.Xp });
        if (verb == "wardrobe") return Appearance(p, new QuestView { Xp = p.Xp });
        if (verb == "skin")
        {
            if (Vector3.Distance(player.transform.position, npcPosition) > 5f) return View(player, "Return to the cosmetic station.");
            string[] choice = id.Split('|');
            if (SaveManager.isBusy || choice.Length != 2 || !int.TryParse(choice[1], out int finish) ||
                !SkinCatalog.Select(p.Skins, p.Completed, choice[0], finish)) return View(player, "That finish is unavailable or locked.");
            SaveWithWorld();
            return Appearance(p, new QuestView { Xp = p.Xp, Message = finish == 0 ? "Original appearance restored." : "Appearance saved. Applies when you equip this item." });
        }
        string message = "";
        if (verb != "view")
        {
            if (Vector3.Distance(player.transform.position, npcPosition) > 5f)
                return View(player, "Return to the quest board to manage assignments.");
            if (SaveManager.isBusy) return View(player, "The world is saving. Try again in a moment.");
            Quest q = QuestCatalog.Find(id);
            if (q == null) return View(player, "Unknown assignment.");
            if (_unavailable.ContainsKey(id) && verb != "abandon")
                return View(player, "This assignment is unavailable in this game build. No items were taken.");
            if (verb == "accept") message = p.Accept(q);
            else if (verb == "abandon")
                message = p.Active.Remove(q.Id) ? "Quest abandoned. Its hunt progress was cleared." : "Quest is not active.";
            else if (verb == "claim") message = Claim(player, p, q);
            else message = "Unknown action.";
        }
        return View(player, message);
    }
    private static bool HasPersistentIdentity(Player player) => player.platformId != 0 ||
        (NetworkManager.Singleton.IsServer && NetworkManager.Singleton.ConnectedClientsList.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<NetworkClient>>().Count == 1 &&
         player.OwnerClientId == NetworkManager.Singleton.LocalClientId);
    private sealed class Container
    {
        internal string Key;
        internal NetworkList<ItemData> Inventory;
        internal bool Carried;
    }
    private List<Container> Containers(Player player, bool includeStorage = true)
    {
        var result = new List<Container> { new() { Key = "inventory", Inventory = player.inventory, Carried = true } };
        if (player.cachedBackpack != null && player.cachedBackpack.IsSpawned)
            result.Add(new Container { Key = "backpack", Inventory = player.cachedBackpack.inventory, Carried = true });
        if (!includeStorage) return result;
        var seen = new HashSet<IntPtr>();
        foreach (Container carried in result) seen.Add(carried.Inventory.Pointer);
        bool localHost = NetworkManager.Singleton.IsServer && player.OwnerClientId == NetworkManager.Singleton.LocalClientId;
        // Native registry is maintained by ItemStash spawn/despawn. Read it fresh for
        // each view/claim so existing, newly built and removed storage stay in sync.
        // No scene scans, proximity calculations, or cached item totals.
        if (ItemStash.allStashes != null)
            foreach (ItemStash stash in ItemStash.allStashes)
            {
                if (stash == null || !stash.IsSpawned || stash.inventory == null) continue;
                APlacable placed = stash.attachedPlacable;
                if (placed == null) continue;
                bool playerChest = placed.placableItem == Items.WoodenChest_01 || placed.placableItem == Items.ScrapChest;
                if (!StorageAccess.CanUse(placed.placedByPlatformId, player.platformId, localHost,
                    placed.loadedFromSave, playerChest, placed.placableId, stash.TryCast<LootChest>() != null)) continue;
                if (!seen.Add(stash.inventory.Pointer)) continue;
                result.Add(new Container { Key = "storage-" + stash.GetInstanceID(), Inventory = stash.inventory });
            }
        return result.OrderBy(c => c.Carried ? 0 : 1).ThenBy(c => c.Key, StringComparer.Ordinal).ToList();
    }
    private static StackData Pack(ItemData d) => new(d.item.ToString(), d.count, d.durability, d.customVar1, d.customVar2);
    private static ItemData Unpack(StackData d) => d.Count == 0 ? ItemData.empty : new ItemData
    {
        item = Enum.Parse<Items>(d.Item), count = checked((byte)d.Count), durability = d.Durability,
        customVar1 = d.Var1, customVar2 = d.Var2
    };
    private string Claim(Player player, Progress p, Quest q)
    {
        if (p.Completed.Contains(q.Id)) return "Already rewarded.";
        if (!p.Active.TryGetValue(q.Id, out int count)) return "Accept this assignment first.";
        if (q.Hunt && count < q.Amount) return "The hunt is still in progress.";
        if (p.Xp > int.MaxValue - q.Xp) return "XP limit reached. Nothing was consumed.";
        List<Container> containers = Containers(player, !q.Hunt);
        var slots = new List<Slot>();
        foreach (Container c in containers)
            for (int i = 0; i < c.Inventory.Count; i++)
                slots.Add(new Slot { Container = c.Key, Index = i, RewardDestination = c.Carried, Before = Pack(c.Inventory[i]) });
        ItemData reward = ItemData.Create(Enum.Parse<Items>(q.Reward), 1);
        if (reward.count == 0 || reward.item != Enum.Parse<Items>(q.Reward))
            return "Reward unavailable. Nothing was consumed.";
        string rewardName = ItemDatabase.GetItemName(reward.item);
        if (!TurnInPlanner.Plan(slots, q, Pack(reward), out string error)) return error;
        var changed = slots.Where(s => s.Before != s.After).ToList();
        var inventories = containers.ToDictionary(c => c.Key, c => c.Inventory);
        try
        {
            // All writes happen synchronously on the server, without RPC round trips between removals and reward.
            SlotTransaction.Commit(changed, s => Pack(inventories[s.Container][s.Index]),
                (s, data) => inventories[s.Container][s.Index] = Unpack(data));
            p.Complete(q);
            return $"Completed: {q.Title}  /  +{q.Xp} XP  /  {rewardName}";
        }
        catch (Exception e)
        {
            if (e is AggregateException)
            {
                _faulted = true;
                QuestPlugin.LogSource.LogError("Quest inventory rollback failed; claims disabled.");
            }
            QuestPlugin.LogSource.LogError("Quest turn-in failed: " + e);
            return "Turn-in failed; see the game log. No XP granted.";
        }
    }
    private QuestView View(Player player, string message)
    {
        Progress p = Get(player);
        var view = new QuestView { Xp = p.Xp, Message = message };
        var carried = new Dictionary<string, int>();
        var stored = new Dictionary<string, int>();
        foreach (Container c in Containers(player))
            for (int i = 0; i < c.Inventory.Count; i++)
            {
                ItemData d = c.Inventory[i];
                if (d.count == 0) continue;
                var counts = c.Carried ? carried : stored;
                string key = d.item.ToString();
                counts.TryGetValue(key, out int total);
                counts[key] = total + d.count;
            }
        foreach (Quest q in QuestCatalog.All)
        {
            if (_unavailable.ContainsKey(q.Id) && !p.Active.ContainsKey(q.Id) && !p.Completed.Contains(q.Id)) continue;
            carried.TryGetValue(q.Target, out int bag);
            stored.TryGetValue(q.Target, out int chest);
            bool active = p.Active.TryGetValue(q.Id, out int kills);
            int count = q.Hunt ? kills : bag + chest;
            string state = p.Completed.Contains(q.Id) ? "COMPLETED" :
                _unavailable.ContainsKey(q.Id) ? "UNAVAILABLE" :
                active ? (count >= q.Amount ? "READY" : "ACTIVE") :
                QuestCatalog.Level(p.Xp) < q.Level ? "LOCKED" : "AVAILABLE";
            view.Rows.Add(new QuestRow { Id = q.Id, State = state, Count = count, Carried = bag, Stored = chest });
        }
        return Appearance(p, view);
    }
    private static QuestView Appearance(Progress p, QuestView view)
    {
        view.Skins = new Dictionary<string, int>(p.Skins ?? new Dictionary<string, int>());
        view.SkinUnlocks = Enumerable.Range(0, SkinCatalog.Names.Length).Where(i => SkinCatalog.Unlocked(i, p.Completed)).ToList();
        return view;
    }
    internal static void ValidateCatalog()
    {
        var ids = new HashSet<string>();
        foreach (Quest q in QuestCatalog.All)
        {
            if (!ids.Add(q.Id) || q.Xp <= 0 || q.Amount <= 0) throw new InvalidDataException("Invalid quest: " + q.Id);
        }
        _unavailable = QuestCatalog.Unavailable(
            name => Enum.TryParse(name, out Items item) && ItemDatabase.GetItemInfo(item) != null,
            name => Enum.TryParse(name, out NPCs npc) && NPCDatabase.GetNPCInfo(npc) != null);
        foreach (var pair in _unavailable)
            QuestPlugin.LogSource.LogWarning($"Assignment {pair.Key} hidden for this game build: {pair.Value}");
        QuestPlugin.LogSource.LogInfo($"FieldQuests catalog ready: {QuestCatalog.All.Length - _unavailable.Count}/{QuestCatalog.All.Length} assignments available.");
    }
}
