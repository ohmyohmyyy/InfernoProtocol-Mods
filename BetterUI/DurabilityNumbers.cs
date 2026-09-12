using Game.Data;
using Il2CppInterop.Runtime;

namespace InfernoProtocol.BetterUI;

internal static class DurabilityNumbers
{
    private const int NativeMaximum = byte.MaxValue;

    internal static bool TryGet(ItemData itemData, out int current, out int maximum)
    {
        current = 0;
        maximum = 0;
        if (itemData.count == 0)
        {
            return false;
        }

        ItemDatabaseEntry entry;
        try
        {
            entry = ItemDatabase.GetItemInfo(itemData.item);
        }
        catch
        {
            return false;
        }

        return TryGet(entry, itemData.durability, out current, out maximum);
    }

    private static bool TryGet(
        ItemDatabaseEntry entry,
        byte durability,
        out int current,
        out int maximum)
    {
        current = 0;
        maximum = 0;
        if (entry == null || !entry.hasDurability || entry.durability == null)
        {
            return false;
        }

        ItemDurability rules = entry.durability;
        if (rules.displayMode == DurabilityDisplayMode.Hidden)
        {
            return false;
        }

        if (rules.displayMode == DurabilityDisplayMode.UsageCount && entry.itemModule != null)
        {
            try
            {
                IItemUsageCount usage = entry.itemModule.TryCast<IItemUsageCount>();
                if (usage != null)
                {
                    maximum = usage.totalUsageCount;
                    current = usage.GetRemainingUsageCount(durability);
                    if (maximum > 0)
                    {
                        current = System.Math.Clamp(current, 0, maximum);
                        return true;
                    }
                }
            }
            catch
            {
                // A malformed module should still receive the conventional
                // durability readout below instead of breaking the hotbar.
            }
        }

        // Inferno Protocol stores conventional item durability directly as one
        // byte. New items are created at 255, and use subtracts real durability
        // points from that value. Exposing the stored points avoids rounding the
        // value back into the percentage used by the stock tooltip.
        maximum = NativeMaximum;
        current = durability;
        return true;
    }
}
