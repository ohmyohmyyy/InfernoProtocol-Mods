namespace InfernoProtocol.FieldQuests;

internal static class BoardSiteRules
{
    // Right is the visitor's right when facing IN toward the entrance, not the door's right.
    internal static (float X, float Z) Offset(float outwardX, float outwardZ, float right, float front) =>
        (-outwardZ * right + outwardX * front, outwardX * right + outwardZ * front);
    internal static bool ClearsEntrance(float right) => right - 1.08f >= 2f;
    internal static bool IsOutdoorAnchor(float heightDifference, float distance) =>
        System.Math.Abs(heightDifference) <= 12f && distance <= 100f;
    internal static float OppositeTraderOffset(float traderSide) => System.Math.Max(3.2f, System.Math.Abs(traderSide));
}
