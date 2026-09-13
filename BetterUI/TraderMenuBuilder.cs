using System;
using System.Collections.Generic;
using Game.UI;
using Game.UI.Trade;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InfernoProtocol.BetterUI;

/// <summary>
/// Moves the game's live trader controls into a BetterUI shell. TradeUI retains
/// ownership of every pooled row and callback; this class only changes hierarchy
/// and presentation.
/// </summary>
internal sealed class TraderMenuBuilder
{
    private int _builtForRootId;

    internal GameObject ReplacementRoot { get; private set; }
    internal RectTransform ThemeButton { get; private set; }
    internal MazeButton CloseButton { get; private set; }

    internal void Build(TradeUI trader)
    {
        if (trader == null || _builtForRootId == trader.GetInstanceID()) return;
        TextMeshProUGUI template = FindTextTemplate(trader);
        if (template == null) throw new InvalidOperationException("The trader menu has no initialized text template.");

        Transform legacyRoot = trader.transform;
        Canvas canvas = trader.GetComponentInParent<Canvas>();
        Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;
        Transform displayRoot = rootCanvas != null ? rootCanvas.transform :
            legacyRoot.parent != null ? legacyRoot.parent : trader.transform.parent;
        if (displayRoot == null) displayRoot = trader.transform;
        var legacyChildren = new List<Transform>();
        for (int i = 0; i < legacyRoot.childCount; i++) legacyChildren.Add(legacyRoot.GetChild(i));

        CloseButton = FindCloseButton(legacyRoot);
        RectTransform root = CreateSurface("BetterUI_TraderMenu", displayRoot, BetterUITheme.CraftingCanvas, true);
        // Trading is a compact exchange flow, not a three-column catalog. Keep
        // a readable fixed maximum width while retaining vertical responsiveness.
        root.anchorMin = new Vector2(.5f, .1f);
        root.anchorMax = new Vector2(.5f, .9f);
        root.offsetMin = new Vector2(-380f, 0f);
        root.offsetMax = new Vector2(380f, 0f);
        root.SetAsLastSibling();
        ReplacementRoot = root.gameObject;

        BuildHeader(root, template);
        BuildOffers(trader, root, template);
        BuildFooter(trader, root, template);

        // The component must remain enabled for stock/inventory events. Only its
        // leftover presentation children are retired after live controls move.
        var ownerGraphics = trader.GetComponents<Graphic>();
        for (int i = 0; i < ownerGraphics.Length; i++)
            if (ownerGraphics[i] != null) ownerGraphics[i].enabled = false;
        int legacyId = legacyRoot.GetInstanceID();
        for (int i = 0; i < legacyChildren.Count; i++)
        {
            Transform child = legacyChildren[i];
            if (child != null && child.parent != null && child.parent.GetInstanceID() == legacyId)
                child.gameObject.SetActive(false);
        }

        Image replacementBackground = root.GetComponent<Image>();
        trader._tradeBackground = replacementBackground;
        trader._tradeBackgroundDefaultColor = BetterUITheme.CraftingCanvas;
        trader._tradableFieldColor = BetterUITheme.CraftingReady;
        trader._untradableFieldColor = BetterUITheme.PanelElevated;
        TradeUI.ToggleActiveColor = BetterUITheme.AccentSoft;
        TradeUI.ToggleInactiveColor = BetterUITheme.PanelSoft;

        root.gameObject.SetActive(false);
        _builtForRootId = trader.GetInstanceID();
        BetterUIPlugin.ModLog.LogInfo(
            $"Built BetterUI trader exchange hierarchy using native trade controls " +
            $"(search={(trader._searchInputField != null ? "yes" : "no")}, " +
            $"filter={(trader._hideUntradableButton != null ? "yes" : "no")}, " +
            $"close={(CloseButton != null ? "yes" : "no")}, " +
            $"content={(trader._contentContainer != null ? "yes" : "no")}).");
    }

    private static void BuildHeader(RectTransform root, TextMeshProUGUI template)
    {
        RectTransform header = CreateSurface("BetterUI_TraderHeader", root, BetterUITheme.Panel);
        AnchorTop(header, 84f, 0f, 0f);

        TextMeshProUGUI title = CreateLabel("BetterUI_TraderTitle", header, template);
        title.text = "TRADER // EXCHANGE";
        BetterUIStyler.Heading(title, 28f);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.rectTransform.anchorMin = Vector2.zero;
        title.rectTransform.anchorMax = new Vector2(.58f, 1f);
        title.rectTransform.offsetMin = new Vector2(22f, 8f);
        title.rectTransform.offsetMax = Vector2.zero;

        TextMeshProUGUI status = CreateLabel("BetterUI_TraderStatus", header, template);
        status.text = "LIVE INVENTORY  //  SECURE EXCHANGE\nSTOCK UPDATES AUTOMATICALLY";
        BetterUIStyler.Text(status, 11f, BetterUITheme.Accent, FontStyles.Bold);
        status.characterSpacing = 1.15f;
        status.alignment = TextAlignmentOptions.MidlineRight;
        status.rectTransform.anchorMin = new Vector2(.58f, 0f);
        status.rectTransform.anchorMax = Vector2.one;
        status.rectTransform.offsetMin = Vector2.zero;
        status.rectTransform.offsetMax = new Vector2(-20f, -4f);
    }

    private static void BuildOffers(TradeUI trader, RectTransform root, TextMeshProUGUI template)
    {
        RectTransform body = CreateSurface("BetterUI_TraderBody", root, BetterUITheme.Panel);
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(14f, 66f);
        body.offsetMax = new Vector2(-14f, -96f);

        RectTransform toolbar = CreateSurface("BetterUI_TraderToolbar", body, BetterUITheme.PanelElevated);
        AnchorTop(toolbar, 66f, 8f, 8f);

        TextMeshProUGUI heading = CreateLabel("BetterUI_TraderOffersHeading", toolbar, template);
        heading.text = "AVAILABLE OFFERS";
        BetterUIStyler.Text(heading, 13f, BetterUITheme.TextMuted, FontStyles.Bold);
        heading.characterSpacing = 1.45f;
        heading.alignment = TextAlignmentOptions.MidlineLeft;
        heading.rectTransform.anchorMin = Vector2.zero;
        heading.rectTransform.anchorMax = new Vector2(.48f, 1f);
        heading.rectTransform.offsetMin = new Vector2(14f, 0f);
        heading.rectTransform.offsetMax = Vector2.zero;

        if (trader._searchInputField != null)
        {
            RectTransform search = trader._searchInputField.GetComponent<RectTransform>();
            search.SetParent(toolbar, false);
            search.anchorMin = new Vector2(.55f, .18f);
            search.anchorMax = new Vector2(1f, .82f);
            search.offsetMin = Vector2.zero;
            search.offsetMax = new Vector2(-12f, 0f);
            LayoutElement layout = search.GetComponent<LayoutElement>();
            if (layout != null) layout.ignoreLayout = true;
        }

        RectTransform scrollRoot = CreateSurface("BetterUI_TraderScroll", body, BetterUITheme.CraftingWell);
        scrollRoot.anchorMin = Vector2.zero;
        scrollRoot.anchorMax = Vector2.one;
        scrollRoot.offsetMin = new Vector2(8f, 8f);
        scrollRoot.offsetMax = new Vector2(-8f, -82f);

        RectTransform viewport = CreateSurface("Viewport", scrollRoot, BetterUITheme.CraftingWell);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(8f, 8f);
        viewport.offsetMax = new Vector2(-23f, -8f);
        viewport.GetComponent<Image>().raycastTarget = true;
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        RectTransform content = trader._contentContainer != null
            ? trader._contentContainer.GetComponent<RectTransform>()
            : null;
        if (content != null)
        {
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, content.sizeDelta.y);
        }

        Scrollbar scrollbar = CreateScrollbar(scrollRoot);
        ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.decelerationRate = .12f;
        scroll.scrollSensitivity = 34f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scroll.verticalScrollbarSpacing = 4f;
    }

    private void BuildFooter(TradeUI trader, RectTransform root, TextMeshProUGUI template)
    {
        RectTransform footer = CreateSurface("BetterUI_TraderFooter", root, BetterUITheme.Panel);
        footer.anchorMin = Vector2.zero;
        footer.anchorMax = new Vector2(1f, 0f);
        footer.pivot = new Vector2(.5f, 0f);
        footer.offsetMin = Vector2.zero;
        footer.offsetMax = new Vector2(0f, 52f);
        HorizontalLayoutGroup layout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = Padding(18, 18, 7, 7);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI hint = CreateLabel("BetterUI_TraderHint", footer, template);
        hint.text = "SELECT AN OFFER  //  REVIEW COST  //  TRADE";
        BetterUIStyler.Text(hint, 11f, BetterUITheme.TextMuted, FontStyles.Bold);
        hint.characterSpacing = .8f;
        hint.alignment = TextAlignmentOptions.MidlineLeft;
        BuildingMenuBuilder.SetLayout(hint.gameObject, 220f, 340f, 1f, 30f, 30f, 0f);

        ThemeButton = CreateSurface("BetterUI_TraderThemeButton", footer, BetterUITheme.PanelSoft);
        ThemeButton.GetComponent<Image>().raycastTarget = true;
        BuildingMenuBuilder.SetLayout(ThemeButton.gameObject, 42f, 46f, 0f, 38f, 38f, 0f);
        var iconObject = new GameObject("BetterUI_TraderThemeIcon", Il2CppType.Of<RectTransform>(),
            Il2CppType.Of<CanvasRenderer>(), Il2CppType.Of<RawImage>());
        iconObject.transform.SetParent(ThemeButton, false);
        RawImage icon = iconObject.GetComponent<RawImage>();
        icon.texture = BetterUITheme.ColorWheelTexture; icon.raycastTarget = false;
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(.5f, .5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(25f, 25f);

        if (trader._hideUntradableButton != null)
        {
            MoveToLayout(trader._hideUntradableButton.transform, footer, 42f, 46f, 0f, 38f);
        }

        if (CloseButton != null)
        {
            if (CloseButton.buttonText != null) CloseButton.buttonText.text = "[ ESC ]  CLOSE";
            MoveToLayout(CloseButton.transform, footer, 130f, 150f, 0f, 38f);
        }
    }

    private static TextMeshProUGUI FindTextTemplate(TradeUI trader)
    {
        if (trader._searchInputField != null && trader._searchInputField.textComponent != null)
        {
            TextMeshProUGUI searchText = trader._searchInputField.textComponent.TryCast<TextMeshProUGUI>();
            if (searchText != null) return searchText;
        }
        var labels = trader.GetComponentsInChildren<TextMeshProUGUI>(true);
        return labels.Length > 0 ? labels[0] : null;
    }

    private static MazeButton FindCloseButton(Transform root)
    {
        var buttons = root.GetComponentsInChildren<MazeButton>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            MazeButton button = buttons[i];
            string label = button != null && button.buttonText != null
                ? button.buttonText.text?.Trim().ToUpperInvariant() : string.Empty;
            if (label == "CLOSE" || label == "EXIT" || label.Contains("CLOSE")) return button;
        }
        return null;
    }

    private static Scrollbar CreateScrollbar(RectTransform parent)
    {
        RectTransform track = CreateSurface("BetterUI_TraderScrollbar", parent, BetterUITheme.PanelSoft);
        track.anchorMin = new Vector2(1f, 0f); track.anchorMax = Vector2.one;
        track.pivot = new Vector2(1f, .5f);
        track.offsetMin = new Vector2(-14f, 8f); track.offsetMax = new Vector2(-4f, -8f);
        track.GetComponent<Image>().raycastTarget = true;
        RectTransform handle = CreateSurface("Handle", track, BetterUITheme.Accent);
        handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one;
        handle.offsetMin = Vector2.zero; handle.offsetMax = Vector2.zero;
        handle.GetComponent<Image>().raycastTarget = true;
        Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.size = .2f;
        return scrollbar;
    }

    private static RectTransform CreateSurface(string name, Transform parent, Color color, bool shadow = false)
    {
        var item = new GameObject(name, Il2CppType.Of<RectTransform>(), Il2CppType.Of<CanvasRenderer>(), Il2CppType.Of<Image>());
        item.transform.SetParent(parent, false);
        Image image = item.GetComponent<Image>();
        image.material = null; image.sprite = null; image.type = Image.Type.Simple;
        image.color = color; image.raycastTarget = false;
        BetterUIStyler.Effects(image, name == "BetterUI_TraderMenu" ? BetterUITheme.Accent : BetterUITheme.BorderSoft, shadow);
        return item.GetComponent<RectTransform>();
    }

    private static TextMeshProUGUI CreateLabel(string name, Transform parent, TextMeshProUGUI template)
    {
        GameObject item = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
        item.name = name; item.SetActive(true);
        RectTransform rect = item.GetComponent<RectTransform>();
        if (rect != null) Normalize(rect);
        TextMeshProUGUI label = item.GetComponent<TextMeshProUGUI>();
        label.text = string.Empty; label.raycastTarget = false;
        return label;
    }

    private static void MoveToLayout(Transform item, Transform parent, float minWidth, float preferredWidth,
        float flexibleWidth, float height)
    {
        if (item == null) return;
        item.SetParent(parent, false);
        RectTransform rect = item.GetComponent<RectTransform>();
        if (rect != null) Normalize(rect);
        BuildingMenuBuilder.SetLayout(item.gameObject, minWidth, preferredWidth, flexibleWidth, height, height, 0f);
    }

    private static void Normalize(RectTransform rect)
    {
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = Vector2.zero;
    }

    private static void AnchorTop(RectTransform rect, float height, float left, float right)
    {
        rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, 1f);
        rect.offsetMin = new Vector2(left, -height); rect.offsetMax = new Vector2(-right, 0f);
    }

    private static RectOffset Padding(int left, int right, int top, int bottom) =>
        new() { left = left, right = right, top = top, bottom = bottom };
}
