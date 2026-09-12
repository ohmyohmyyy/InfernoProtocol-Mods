using System.Collections.Generic;
using Game.Data;
using Game.PlayerOperations;
using Game.UI;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InfernoProtocol.BetterUI;

internal sealed class HotbarDurabilityDisplay
{
    private const float RefreshInterval = 0.1f;
    private readonly Dictionary<int, Widget> _widgets = new();
    private float _nextRefresh;
    private int _paletteIndex = -1;
    private int _refreshGeneration;

    internal void Tick(bool enabled)
    {
        float now = Time.unscaledTime;
        if (now < _nextRefresh)
        {
            return;
        }

        _nextRefresh = now + RefreshInterval;
        if (!enabled)
        {
            SetAllVisible(false);
            return;
        }

        Il2CppReferenceArray<PlayerItemContainerUI> hotbarSlots = null;
        Il2CppReferenceArray<PlayerItemContainerUI> equippedSlots = null;
        try
        {
            hotbarSlots = PlayerInventoryUI.inventorySlots;
        }
        catch
        {
            // The inventory HUD is not constructed in every scene.
        }

        try
        {
            equippedSlots = Player.inventorySlotsUI;
        }
        catch
        {
            // The equipped-item HUD is not constructed in every scene.
        }

        if ((hotbarSlots == null || hotbarSlots.Length == 0)
            && (equippedSlots == null || equippedSlots.Length == 0))
        {
            SetAllVisible(false);
            return;
        }

        bool paletteChanged = _paletteIndex != BetterUIPlugin.PaletteIndex;
        _paletteIndex = BetterUIPlugin.PaletteIndex;
        _refreshGeneration++;
        RefreshSlots(hotbarSlots, paletteChanged);
        RefreshSlots(equippedSlots, paletteChanged);

        foreach (KeyValuePair<int, Widget> pair in _widgets)
        {
            if (pair.Value.RefreshGeneration != _refreshGeneration && pair.Value.Root != null)
            {
                pair.Value.Root.SetActive(false);
            }
        }
    }

    private void RefreshSlots(
        Il2CppReferenceArray<PlayerItemContainerUI> slots,
        bool paletteChanged)
    {
        if (slots == null)
        {
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            PlayerItemContainerUI slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            ItemData itemData;
            try
            {
                itemData = slot.itemData;
            }
            catch
            {
                continue;
            }

            RefreshSlot(slot, itemData, paletteChanged);
        }
    }

    private void RefreshSlot(PlayerItemContainerUI slot, ItemData itemData, bool paletteChanged)
    {
        int id = slot.GetInstanceID();
        bool widgetCreated = false;
        if (!_widgets.TryGetValue(id, out Widget widget) || widget.Root == null)
        {
            widget = CreateWidget(slot);
            _widgets[id] = widget;
            widgetCreated = true;
        }

        widget.RefreshGeneration = _refreshGeneration;
        if (!DurabilityNumbers.TryGet(itemData, out int current, out int maximum))
        {
            widget.Root.SetActive(false);
            return;
        }

        widget.Root.SetActive(true);
        string value = $"{current}/{maximum}";
        bool valueChanged = widget.LastValue != value;
        if (valueChanged)
        {
            widget.Label.text = value;
            widget.LastValue = value;
        }

        if (paletteChanged || widgetCreated || valueChanged)
        {
            StyleWidget(widget, current, maximum);
        }
    }

    private static Widget CreateWidget(PlayerItemContainerUI slot)
    {
        var rootObject = new GameObject(
            "BetterUI_DurabilityReadout",
            Il2CppType.Of<RectTransform>(),
            Il2CppType.Of<CanvasRenderer>(),
            Il2CppType.Of<Image>());
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.SetParent(slot.transform, false);
        root.anchorMin = new Vector2(0.05f, 0f);
        root.anchorMax = new Vector2(0.95f, 0f);
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = new Vector2(0f, -4f);
        root.sizeDelta = new Vector2(0f, 19f);
        root.SetAsLastSibling();

        Image background = rootObject.GetComponent<Image>();
        background.raycastTarget = false;

        var labelObject = new GameObject(
            "Value",
            Il2CppType.Of<RectTransform>(),
            Il2CppType.Of<CanvasRenderer>(),
            Il2CppType.Of<TextMeshProUGUI>());
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(root, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(2f, 0f);
        labelRect.offsetMax = new Vector2(-2f, 0f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        if (slot.slotNumberIndicator != null)
        {
            label.font = slot.slotNumberIndicator.font;
        }

        label.enableAutoSizing = true;
        label.fontSizeMin = 9f;
        label.fontSizeMax = 13f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Truncate;
        label.raycastTarget = false;

        var widget = new Widget(rootObject, background, label);
        StyleWidget(widget, 1, 1);
        return widget;
    }

    private static void StyleWidget(Widget widget, int current, int maximum)
    {
        Color background = BetterUITheme.Panel;
        background.a = 0.88f;
        BetterUIStyler.RoundedImage(widget.Background, background);
        float remaining = maximum > 0 ? current / (float)maximum : 0f;
        widget.Label.color = remaining <= 0.2f
            ? BetterUITheme.Danger
            : remaining <= 0.5f ? BetterUITheme.Primary : BetterUITheme.Text;
        widget.Label.outlineWidth = 0.12f;
        widget.Label.outlineColor = new Color32(0, 0, 0, 220);
    }

    internal void Dispose()
    {
        foreach (Widget widget in _widgets.Values)
        {
            if (widget.Root != null)
            {
                Object.Destroy(widget.Root);
            }
        }

        _widgets.Clear();
    }

    private void SetAllVisible(bool visible)
    {
        foreach (Widget widget in _widgets.Values)
        {
            if (widget.Root != null)
            {
                widget.Root.SetActive(visible && !string.IsNullOrEmpty(widget.LastValue));
            }
        }
    }

    private sealed class Widget
    {
        internal Widget(GameObject root, Image background, TextMeshProUGUI label)
        {
            Root = root;
            Background = background;
            Label = label;
        }

        internal GameObject Root { get; }
        internal Image Background { get; }
        internal TextMeshProUGUI Label { get; }
        internal string LastValue { get; set; }
        internal int RefreshGeneration { get; set; }
    }
}
