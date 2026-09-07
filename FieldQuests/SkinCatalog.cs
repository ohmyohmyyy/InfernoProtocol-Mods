using System;
using System.Collections.Generic;
using System.Linq;

namespace InfernoProtocol.FieldQuests;

public static class SkinCatalog
{
    public static readonly string[] Items = { "WoodenSpear", "NailBat", "ScrapAxe", "ScrapPickaxe", "ScrapMachete", "BronzeAxe", "IronSpear", "Katana", "LeatherShoes", "ScrapHelmet", "ScrapShirt", "ScrapPant" };
    public static readonly string[] Names = { "Original", "Woodland", "Blue Circuit", "Embercrack", "Brasswork", "Arctic Digital", "Hazard", "Tigerstripe", "Crimson Rally", "Tidebreaker", "Topographic", "Royal Weave", "Desert Shards" };
    public static readonly string[] Quests = { "", "s01", "s02", "h02", "s07", "h03", "s08", "h07", "h09", "s05", "s09", "h12", "h05" };
    public static int PageStart(int selected, int direction)
    {
        int pages = (Names.Length + 4) / 5;
        return ((selected / 5 + direction + pages) % pages) * 5;
    }
    public static bool Unlocked(int finish, HashSet<string> completed) => finish >= 0 && finish < Names.Length &&
        (finish == 0 || completed.Contains(Quests[finish]));
    public static bool Select(Dictionary<string, int> choices, HashSet<string> completed, string item, int finish)
    {
        if (!Items.Contains(item) || !Unlocked(finish, completed)) return false;
        if (finish == 0) choices.Remove(item); else choices[item] = finish;
        return true;
    }
    public static string RewardFor(string quest) => Array.IndexOf(Quests, quest) is int index && index > 0 ? Names[index] + " finish unlocked" : "";
}
