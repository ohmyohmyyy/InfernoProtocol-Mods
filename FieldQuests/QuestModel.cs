using System;
using System.Collections.Generic;
using System.Linq;

namespace InfernoProtocol.FieldQuests;

public sealed class Quest
{
    public readonly string Id, Title, Description, Target, Reward;
    public readonly int Amount, Xp, Level;
    public readonly bool Hunt;
    public Quest(string id, string title, string description, string target, int amount, string reward, int xp, int level, bool hunt)
    { Id = id; Title = title; Description = description; Target = target; Amount = amount; Reward = reward; Xp = xp; Level = level; Hunt = hunt; }
}

public static class QuestCatalog
{
    public static readonly Quest[] All =
    {
        new("s01", "A place to begin", "A good camp starts with solid timber. Bring logs for the supply reserve.", "Log", 8, "LeatherShoes", 100, 1, false),
        new("s02", "Firm foundations", "We need stone to reinforce the paths around camp.", "Stone", 12, "WoodenSpear", 120, 1, false),
        new("s03", "Light the way", "Help us keep the perimeter lit after sundown.", "Torch", 3, "Flashlight", 140, 1, false),
        new("s04", "First-aid reserve", "A small medical reserve can save a life. Stock the infirmary.", "Bandage", 3, "LeatherFlaskEmpty", 150, 1, false),
        new("s05", "A warm welcome", "Bring drinking water for the next arrivals.", "WaterBottleFilled", 2, "StonePickaxe", 130, 1, false),
        new("s06", "Field dressings", "Supply clean cloth for field dressings. Earn protective headgear for your next expedition.", "ClothPiece", 6, "ScrapHelmet", 180, 2, false),
        new("s07", "A proper workshop", "Deliver planks so we can expand the work area.", "Plank", 8, "ScrapAxe", 220, 2, false),
        new("s08", "Keep it together", "Tape is always in short supply at the repair bench.", "Tape", 3, "ScrapPickaxe", 240, 2, false),
        new("s09", "Copper prospect", "Bring copper ore for the camp's first metalwork trials.", "CopperOre", 8, "BronzeHammer", 280, 3, false),
        new("s10", "The other half", "Tin will let us put that copper to better use.", "TinOre", 8, "BronzeAxe", 280, 3, false),
        new("s11", "Bronze standard", "A batch of finished ingots will get the new workshop running.", "BronzeIngot", 4, "MiningHelmet", 360, 4, false),
        new("s12", "Power reserve", "Deliver spare batteries for our night patrols.", "Battery", 3, "GasMask", 360, 4, false),
        new("h01", "Small trouble", "Thin out the rats around our supply routes. Only your kills after accepting count.", "Rat", 5, "NailBat", 150, 1, true),
        new("h02", "Shambling company", "Keep the walking dead away from our newcomers.", "Zombie", 5, "ScrapMachete", 180, 1, true),
        new("h03", "Larger teeth", "Giant rats have been tearing through the supply trails.", "GiantRat", 3, "Crossbow", 240, 2, true),
        new("h04", "Night watch", "Clear a larger group of zombies so our scouts can get through.", "Zombie", 15, "ScrapHelmet", 320, 2, true),
        new("h05", "Scavenger patrol", "Trash eaters keep following our supply parties.", "TrashEater", 5, "ScrapMachete", 320, 2, true),
        new("h06", "Unwanted company", "Put down the meat slimes blocking our route.", "MeatSlime", 5, "ScrapPant", 360, 3, true),
        new("h07", "Forest sentries", "The leaf hunters have made the treeline dangerous.", "LeafHunter", 5, "ScrapShirt", 400, 3, true),
        new("h08", "A quieter night", "Hunt the ghouls before another patrol goes missing.", "Ghoul", 4, "IronSpear", 420, 3, true),
        new("h09", "Broken ambush", "Stop the goblin fighters stalking the outer paths.", "GoblinMelee", 8, "BronzeMachete", 440, 4, true),
        new("h10", "Buzz off", "Giant flies are making the nearby routes miserable.", "GiantFly", 6, "GasMask", 460, 4, true),
        new("h11", "Hold the line", "Prove the camp can rely on you against a sustained zombie threat.", "Zombie", 30, "Colt1911", 600, 5, true),
        new("h12", "Veteran hunter", "Take down the stitchers threatening our expeditions.", "Stitcher", 5, "Katana", 750, 5, true)
    };
    public static Quest Find(string id) => All.FirstOrDefault(q => q.Id == id);
    // Enum entries can exist without a usable asset in the current game build.
    // Validate each contract independently; one unavailable reward must not stop the board.
    public static Dictionary<string, string> Unavailable(Func<string, bool> itemExists, Func<string, bool> creatureExists)
    {
        var result = new Dictionary<string, string>();
        foreach (Quest q in All)
        {
            try
            {
                if (!itemExists(q.Reward)) result[q.Id] = "Missing reward: " + q.Reward;
                else if (!(q.Hunt ? creatureExists(q.Target) : itemExists(q.Target)))
                    result[q.Id] = "Missing objective: " + q.Target;
            }
            catch (Exception e) { result[q.Id] = "Asset lookup failed: " + e.Message; }
        }
        return result;
    }
    public static int Level(int xp)
    {
        int level = 1;
        while (level < 100 && xp >= Threshold(level + 1)) level++;
        return level;
    }
    public static int Threshold(int level) => checked(100 * (level - 1) * level);
}

public sealed class Progress
{
    public int Xp { get; set; }
    public Dictionary<string, int> Active { get; set; } = new();
    public HashSet<string> Completed { get; set; } = new();
    public Dictionary<string, int> Skins { get; set; } = new();
    public string Accept(Quest quest)
    {
        if (quest == null) return "Unknown assignment.";
        if (Completed.Contains(quest.Id)) return "Already completed.";
        if (Active.ContainsKey(quest.Id)) return "Already in your journal.";
        if (QuestCatalog.Level(Xp) < quest.Level) return $"Requires level {quest.Level}.";
        if (Active.Count >= 6) return "Your journal holds six active quests. Finish or abandon one first.";
        Active.Add(quest.Id, 0);
        return "Quest accepted.";
    }
    public void Kill(string creature)
    {
        foreach (Quest q in QuestCatalog.All)
            if (q.Hunt && q.Target == creature && Active.TryGetValue(q.Id, out int count))
                Active[q.Id] = Math.Min(q.Amount, count + 1);
    }
    public void Complete(Quest quest)
    {
        if (!Active.ContainsKey(quest.Id) || Completed.Contains(quest.Id))
            throw new InvalidOperationException("Quest is not claimable.");
        int total = checked(Xp + quest.Xp);
        Completed.Add(quest.Id);
        Active.Remove(quest.Id);
        Xp = total;
    }
}

// Plans operate on snapshots; the game adapter verifies and commits the entire plan on the host.
public readonly struct StackData : IEquatable<StackData>
{
    public readonly string Item;
    public readonly int Count;
    public readonly byte Durability, Var1, Var2;
    public StackData(string item, int count, byte durability = 0, byte var1 = 0, byte var2 = 0)
    { Item = item; Count = count; Durability = durability; Var1 = var1; Var2 = var2; }
    public StackData WithCount(int count) => new(Item, count, Durability, Var1, Var2);
    public bool Equals(StackData other) => Count == other.Count && (Count == 0 ||
        Item == other.Item && Durability == other.Durability && Var1 == other.Var1 && Var2 == other.Var2);
    public override bool Equals(object obj) => obj is StackData other && Equals(other);
    public override int GetHashCode() => Count == 0 ? 0 : HashCode.Combine(Item, Count, Durability, Var1, Var2);
    public static bool operator ==(StackData a, StackData b) => a.Equals(b);
    public static bool operator !=(StackData a, StackData b) => !a.Equals(b);
}
public sealed class Slot
{
    public string Container { get; init; }
    public int Index { get; init; }
    public bool RewardDestination { get; init; }
    public StackData Before { get; init; }
    public StackData After { get; set; }
}
public static class TurnInPlanner
{
    public static bool Plan(List<Slot> slots, Quest quest, StackData reward, out string error)
    {
        foreach (Slot s in slots) s.After = s.Before;
        int remaining = quest.Hunt ? 0 : quest.Amount;
        foreach (Slot s in slots)
        {
            if (remaining == 0) break;
            if (s.Before.Item != quest.Target || s.Before.Count <= 0) continue;
            int take = Math.Min(remaining, s.Before.Count);
            s.After = s.Before.WithCount(s.Before.Count - take);
            remaining -= take;
        }
        if (remaining != 0) { error = "Not enough materials. Nothing was consumed."; return false; }
        Slot destination = slots.FirstOrDefault(s => s.RewardDestination && s.After.Count == 0);
        if (destination == null) { error = "Free one inventory or backpack slot for your reward."; return false; }
        destination.After = reward;
        error = "";
        return true;
    }
}

public static class SlotTransaction
{
    public static void Commit(List<Slot> changed, Func<Slot, StackData> read, Action<Slot, StackData> write)
    {
        foreach (Slot s in changed)
            if (read(s) != s.Before) throw new InvalidOperationException("An inventory changed. Try again.");
        int attempted = 0;
        try
        {
            foreach (Slot s in changed)
            {
                if (read(s) != s.Before) throw new InvalidOperationException("An inventory changed during turn-in.");
                attempted++;
                write(s, s.After);
                if (read(s) != s.After) throw new InvalidOperationException("Inventory write did not stick.");
            }
        }
        catch (Exception cause)
        {
            var failures = new List<Exception>();
            for (int i = attempted - 1; i >= 0; i--)
            {
                try
                {
                    write(changed[i], changed[i].Before);
                    if (read(changed[i]) != changed[i].Before) throw new InvalidOperationException("Rollback verification failed.");
                }
                catch (Exception rollback) { failures.Add(rollback); }
            }
            if (failures.Count > 0) { failures.Insert(0, cause); throw new AggregateException("Inventory rollback failed.", failures); }
            throw;
        }
    }
}

public sealed class QuestRow
{
    public string Id { get; set; }
    public int Count { get; set; }
    public int Carried { get; set; }
    public int Stored { get; set; }
    public string State { get; set; }
}
public sealed class QuestView
{
    public int Protocol { get; set; } = 1;
    public string Message { get; set; } = "";
    public int Xp { get; set; }
    public List<QuestRow> Rows { get; set; } = new();
    public bool LocationOnly { get; set; }
    public bool ProgressOnly { get; set; }
    public Dictionary<string, int> Skins { get; set; } = new();
    public List<int> SkinUnlocks { get; set; } = new();
    public QuestBoardLocation Location { get; set; }
}
public sealed class QuestBoardLocation
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Yaw { get; set; }
    public bool IsValid => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z) && float.IsFinite(Yaw) &&
        Math.Abs(X) < 1000000 && Math.Abs(Y) < 1000000 && Math.Abs(Z) < 1000000;
}
