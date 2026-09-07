using System;
using System.Collections.Generic;
using Game.LevelOperations;
using Game.PlayerOperations;
using UnityEngine;

namespace InfernoProtocol.FieldQuests;

internal static class BoardHouseSite
{
    internal static bool TryFind(out Vector3 position, out Vector3 facing, out string description)
    {
        position = facing = default;
        description = "Waiting for the outdoor trader and porch";
        // Trade_Interior2 is a separately staged interior thousands of metres away.
        // Anchor to a live outdoor trader, not an Interior prefab's door references.
        foreach (TradeController trader in UnityEngine.Object.FindObjectsOfType<TradeController>())
        {
            if (trader == null || trader.GetComponentInParent<Interior>() != null || trader.GetComponentInParent<MazeLayer>() != null) continue;
            Vector3 traderPoint = trader.transform.position;
            // Reject distant staging areas even when they are not parented to Interior.
            if (Player.localPlayer == null || Player.isInMaze ||
                !BoardSiteRules.IsOutdoorAnchor(traderPoint.y - Player.localPlayer.transform.position.y,
                    Vector3.Distance(traderPoint, Player.localPlayer.transform.position))) continue;
            if (!TryHouse(traderPoint, out Vector3 center, out Vector3 outward, out string landmark))
            {
                description = $"Outdoor trader at {traderPoint}; waiting for nearby house geometry";
                continue;
            }
            Vector3 rightDirection = Vector3.Cross(outward, Vector3.up).normalized;
            float oppositeSide = BoardSiteRules.OppositeTraderOffset(Vector3.Dot(traderPoint - center, rightDirection));
            float porchFront = Vector3.Dot(traderPoint - center, outward);
            // Mirror the trader across the house, then step just in front of the porch posts.
            foreach (float forwardExtra in new[] { .65f, 1f, 1.35f })
            foreach (float sideExtra in new[] { 0f, .35f, .7f })
            {
                float right = oppositeSide + sideExtra;
                if (!BoardSiteRules.ClearsEntrance(right)) continue;
                Vector3 candidate = center + rightDirection * right + outward * (porchFront + forwardExtra);
                candidate.y = traderPoint.y;
                if (!TryGround(candidate, outward, out Vector3 feet)) continue;
                position = feet; facing = feet + outward;
                description = $"outdoor house front-right porch opposite trader at {traderPoint}; {landmark}; house center={center}";
                return true;
            }
            description = $"Outdoor porch found ({landmark}), but the marked side has no clear board footprint";
        }
        return false;
    }
    private static bool TryHouse(Vector3 trader, out Vector3 center, out Vector3 outward, out string landmark)
    {
        center = outward = default; landmark = "";
        var checkedRoots = new HashSet<int>();
        float best = float.MaxValue;
        foreach (Collider nearby in Physics.OverlapSphere(trader + Vector3.up, 12f, ~4, QueryTriggerInteraction.Ignore))
        for (Transform root = nearby.transform; root != null; root = root.parent)
        {
            if (!checkedRoots.Add(root.GetInstanceID())) continue;
            string name = root.name.ToLowerInvariant();
            if (!name.Contains("house") && !name.Contains("cabin") && !name.Contains("building") && !name.Contains("shop") && !name.Contains("trade")) continue;
            if (root.GetComponentInParent<Interior>() != null || root.GetComponentInParent<MazeLayer>() != null) continue;
            bool found = false; Bounds bounds = default;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!found || bounds.size.y < 2.5f || bounds.size.y > 15f || bounds.size.x > 30f || bounds.size.z > 30f ||
                Mathf.Max(bounds.size.x, bounds.size.z) < 4f) continue;
            float score = Vector3.Distance(bounds.ClosestPoint(trader), trader);
            if (score > 7f || score >= best) continue;
            Vector3 toTrader = trader - bounds.center; toTrader.y = 0;
            Vector3 forward = root.forward; forward.y = 0; forward.Normalize();
            Vector3 side = root.right; side.y = 0; side.Normalize();
            Vector3 normal = Mathf.Abs(Vector3.Dot(toTrader, forward)) >= Mathf.Abs(Vector3.Dot(toTrader, side)) ? forward : side;
            if (normal.sqrMagnitude < .5f) continue;
            if (Vector3.Dot(normal, toTrader) < 0) normal = -normal;
            center = bounds.center; center.y = trader.y;
            outward = normal; landmark = root.name; best = score;
        }
        if (best < float.MaxValue) return true;
        // Some houses have generic mesh-root names. A nearby physical door is a
        // separate outdoor landmark; it is not the staged InteriorDoor used previously.
        foreach (HandledPhysicalDoor door in UnityEngine.Object.FindObjectsOfType<HandledPhysicalDoor>())
        {
            if (door.GetComponentInParent<Interior>() != null || door.GetComponentInParent<MazeLayer>() != null) continue;
            Vector3 doorPoint = door.objectRenderer != null ? door.objectRenderer.bounds.center : door.transform.position;
            float distance = Vector3.Distance(doorPoint, trader);
            if (distance > 9f || distance >= best || Mathf.Abs(doorPoint.y - trader.y) > 3f) continue;
            Vector3 normal = door.transform.forward; normal.y = 0;
            if (normal.sqrMagnitude < .1f) continue;
            normal.Normalize();
            if (Vector3.Dot(normal, trader - doorPoint) < 0) normal = -normal;
            center = doorPoint; center.y = trader.y;
            outward = normal; landmark = "outdoor physical door " + door.name; best = distance;
        }
        return best < float.MaxValue;
    }
    private static bool TryGround(Vector3 candidate, Vector3 outward, out Vector3 feet)
    {
        feet = default;
        var hits = new List<RaycastHit>();
        foreach (RaycastHit hit in Physics.RaycastAll(candidate + Vector3.up * 2.5f, Vector3.down, 6f, ~4, QueryTriggerInteraction.Ignore)) hits.Add(hit);
        hits.Sort((a, b) => Mathf.Abs(a.point.y - candidate.y).CompareTo(Mathf.Abs(b.point.y - candidate.y)));
        foreach (RaycastHit hit in hits)
        {
            if (hit.normal.y < .97f || Mathf.Abs(hit.point.y - candidate.y) > 2.5f ||
                (hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic)) continue;
            Vector3 ground = hit.point + Vector3.up * .04f;
            Quaternion rotation = Quaternion.LookRotation(outward);
            if (Physics.CheckBox(ground + Vector3.up * 1.2f, new Vector3(1.08f, 1.14f, .4f), rotation, ~4, QueryTriggerInteraction.Ignore)) continue;
            Vector3 reading = ground + outward * 1.4f;
            if (!Physics.Raycast(reading + Vector3.up, Vector3.down, out RaycastHit approach, 2f, ~4, QueryTriggerInteraction.Ignore) ||
                Mathf.Abs(approach.point.y - ground.y) > .3f) continue;
            if (Physics.CheckCapsule(approach.point + Vector3.up * .4f, approach.point + Vector3.up * 1.5f, .32f, ~4, QueryTriggerInteraction.Ignore)) continue;
            feet = ground;
            return true;
        }
        return false;
    }
}
