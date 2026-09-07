using System;
using System.Collections.Generic;
using Game.PlayerOperations;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;

namespace InfernoProtocol.FieldQuests;

internal sealed class QuestBoard : IDisposable
{
    internal GameObject Root { get; private set; }
    internal Vector3 Position => Root.transform.position;
    internal Vector3 CosmeticPosition => Root.transform.TransformPoint(new Vector3(1.65f, 0, 0));
    internal string WaitingReason { get; private set; } = "Preparing the quest board";
    private readonly List<Material> _materials = new();

    internal bool Create(Vector3? hostPosition = null, float hostYaw = 0)
    {
        if (Root != null) return true;
        Vector3 point = hostPosition ?? new Vector3(BoardPlacement.X, BoardPlacement.Y, BoardPlacement.Z);
        float yaw = hostPosition.HasValue ? hostYaw : BoardPlacement.Yaw;
        string location = hostPosition.HasValue ? "host-selected location" : "saved ground position (no trader lookup)";
        try
        {
            Root = new GameObject("FieldQuests_QuestBoard");
            Root.layer = 2; Root.SetActive(false);
            Root.transform.position = point;
            Root.transform.rotation = Quaternion.Euler(0, yaw, 0);
            Build();
            Root.SetActive(true);
            WaitingReason = "";
            QuestPlugin.LogSource.LogInfo($"Quest Board ready at {Position}; {location}; static wood-and-paper board, no NPC model or animation.");
            return true;
        }
        catch { Dispose(); throw; }
    }

    private void Build()
    {
        Material frame = Material(new Color(.19f, .105f, .052f));
        Material wood = Material(new Color(.32f, .205f, .11f));
        Material alternate = Material(new Color(.28f, .17f, .085f));
        Material metal = Material(new Color(.08f, .10f, .105f));
        Material paper = Material(new Color(.86f, .80f, .64f));
        Material palePaper = Material(new Color(.75f, .79f, .72f));
        Material ink = Material(new Color(.18f, .20f, .18f));
        Material accent = Material(new Color(.30f, .57f, .49f));
        Part("FinishCabinet", new Vector3(1.65f, .46f, 0), new Vector3(.68f, .92f, .44f), frame);
        Part("FinishCounter", new Vector3(1.65f, .96f, 0), new Vector3(.82f, .09f, .57f), metal);
        Collider(new Vector3(1.65f, .5f, 0), new Vector3(.72f, 1f, .5f));
        for (int i = 1; i < 5; i++)
            Part("FinishSample", new Vector3(1.32f + i * .13f, 1.08f, 0), new Vector3(.09f, .16f, .13f), Material(SkinVisuals.ColorFor(i)));
        foreach (float side in new[] { -1f, 1f })
            BoardText("FINISHES", new Vector3(1.65f, .73f, side * .227f), side, new Vector2(590, 140), 65, new Color(.94f, .90f, .77f));
        foreach (float x in new[] { -.88f, .88f })
        {
            Part("TimberPost", new Vector3(x, 1.12f, 0), new Vector3(.16f, 2.24f, .18f), frame);
            Part("PostFoot", new Vector3(x, .15f, 0), new Vector3(.185f, .30f, .205f), metal);
            Collider(new Vector3(x, 1.12f, 0), new Vector3(.16f, 2.24f, .18f));
        }
        for (int i = 0; i < 6; i++)
            Part("BackingPlank", new Vector3(0, .89f + i * .185f, 0), new Vector3(1.64f, .18f, .10f), i % 2 == 0 ? wood : alternate);
        Part("LowerRail", new Vector3(0, .76f, 0), new Vector3(1.98f, .13f, .18f), frame);
        Part("Header", new Vector3(0, 2.055f, 0), new Vector3(1.98f, .30f, .20f), frame);
        Part("RainCap", new Vector3(0, 2.245f, 0), new Vector3(2.08f, .075f, .34f), metal);
        foreach (float x in new[] { -.78f, .78f })
            Part("InsetSideTrim", new Vector3(x, 1.38f, .07f), new Vector3(.035f, 1.08f, .045f), frame);
        Collider(new Vector3(0, 1.47f, 0), new Vector3(1.8f, 1.5f, .20f));
        // Both faces are readable, so approaching from behind doesn't look like the wrong object.
        foreach (float side in new[] { 1f, -1f })
        {
            BoardText("QUEST BOARD", new Vector3(0, 2.09f, side * .108f), side, new Vector2(1740, 160), 105, new Color(.94f, .90f, .77f));
            BoardText("FIELD ASSIGNMENTS", new Vector3(0, 1.963f, side * .109f), side, new Vector2(1600, 70), 38, new Color(.65f, .76f, .64f));
            foreach (float x in new[] { -.88f, .88f })
            foreach (float y in new[] { .76f, 2.055f })
                Part("IronFastener", new Vector3(x, y, side * .106f), new Vector3(.042f, .042f, .016f), metal);
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * .49f;
                var notice = new GameObject("PinnedNotice");
                notice.layer = 2; notice.transform.SetParent(Root.transform, false);
                notice.transform.localPosition = new Vector3(x, i == 1 ? .025f : 0, 0);
                notice.transform.localRotation = Quaternion.Euler(0, 0, i == 0 ? -1.5f : i == 2 ? 1.2f : 0);
                Part("PostedAssignment", new Vector3(0, 1.37f, side * .066f), new Vector3(.415f, .64f, .014f), i == 1 ? palePaper : paper, notice.transform);
                Part("PaperPin", new Vector3(0, 1.65f, side * .080f), new Vector3(.027f, .027f, .013f), metal, notice.transform);
                Part("NoticeHeading", new Vector3(0, 1.53f, side * .077f), new Vector3(.29f, .045f, .009f), i == 1 ? accent : ink, notice.transform);
                for (int line = 0; line < 4; line++)
                    Part("NoticeWriting", new Vector3(0, 1.40f - line * .064f, side * .077f), new Vector3(line == 3 ? .18f : line % 2 == 0 ? .29f : .24f, .009f, .008f), ink, notice.transform);
                Part("RewardSeal", new Vector3(.125f, 1.12f, side * .079f), new Vector3(.055f, .055f, .012f), accent, notice.transform);
            }
        }
    }
    private Material Material(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
        if (shader == null) throw new InvalidOperationException("Quest board shader unavailable.");
        var material = new Material(shader) { name = "FieldQuests_BoardMaterial", color = color };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .18f);
        _materials.Add(material);
        return material;
    }
    private void Part(string name, Vector3 position, Vector3 size, Material material, Transform parent = null)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name; part.layer = 2;
        part.transform.SetParent(parent != null ? parent : Root.transform, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;
        UnityEngine.Collider collider = part.GetComponent<UnityEngine.Collider>();
        collider.enabled = false;
        UnityEngine.Object.Destroy(collider);
        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        if (size.z < .04f) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    private void Collider(Vector3 center, Vector3 size)
    {
        BoxCollider collider = Root.AddComponent<BoxCollider>();
        collider.center = center; collider.size = size;
    }
    private void BoardText(string text, Vector3 position, float side, Vector2 size, float fontSize, Color color)
    {
        var sign = new GameObject("BoardTitle", Il2CppType.Of<RectTransform>(), Il2CppType.Of<Canvas>());
        sign.transform.SetParent(Root.transform, false);
        sign.layer = 2;
        Canvas canvas = sign.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform rect = sign.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.localPosition = position;
        rect.localRotation = Quaternion.Euler(0, side > 0 ? 180 : 0, 0);
        rect.localScale = Vector3.one * .001f;
        var labelObject = new GameObject("Title", Il2CppType.Of<RectTransform>(), Il2CppType.Of<CanvasRenderer>(), Il2CppType.Of<TextMeshProUGUI>());
        labelObject.transform.SetParent(rect, false);
        labelObject.layer = 2;
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = text; label.fontSize = fontSize; label.color = color;
        label.fontStyle = FontStyles.Bold; label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = false; label.enableWordWrapping = false;
        label.raycastTarget = false;
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero; label.rectTransform.offsetMax = Vector2.zero;
    }
    internal bool CanRead(bool cosmetics = false)
    {
        if (Root == null || Player.mainCamera == null || Player.localPlayer == null) return false;
        Vector3 position = cosmetics ? CosmeticPosition : Position;
        if (Vector3.Distance(Player.localPlayer.transform.position, position) >= (cosmetics ? 2.1f : 3.8f)) return false;
        Vector3 target = position + Vector3.up * (cosmetics ? 1f : 1.4f);
        Vector3 origin = Player.mainCamera.transform.position;
        Vector3 flatDelta = target - origin; flatDelta.y = 0;
        Vector3 facing = Player.mainCamera.transform.forward; facing.y = 0;
        if (flatDelta.sqrMagnitude > .25f && Vector3.Dot(facing.normalized, flatDelta.normalized) < .35f) return false;
        Vector3 delta = target - origin;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~4, QueryTriggerInteraction.Ignore))
        {
            Transform t = hit.transform;
            if (t == null || t.IsChildOf(Root.transform) || t.IsChildOf(Player.localPlayer.transform) || t.IsChildOf(Player.mainCamera.transform)) continue;
            return false;
        }
        return true;
    }
    public void Dispose()
    {
        if (Root != null) UnityEngine.Object.Destroy(Root);
        foreach (Material material in _materials) if (material != null) UnityEngine.Object.Destroy(material);
        _materials.Clear(); Root = null;
    }
}
