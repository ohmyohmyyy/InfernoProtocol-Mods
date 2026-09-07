namespace InfernoProtocol.FieldQuests;

internal static class StorageAccess
{
    internal static bool CanUse(ulong owner, ulong player, bool localHost, bool saved,
        bool playerChest, ulong placementId, bool loot)
    {
        if (!playerChest && (loot || placementId == 0)) return false;
        // Placer ID records who built it, not exclusive inventory access.
        // The host may use the world's built storage, old and newly placed.
        if (localHost && (placementId != 0 || saved || owner != 0)) return true;
        if (owner != 0) return owner == player;
        return false;
    }
}
