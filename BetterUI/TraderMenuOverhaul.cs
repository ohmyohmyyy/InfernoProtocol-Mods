using System.Collections.Generic;
using Game.UI;
using Game.UI.Trade;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace InfernoProtocol.BetterUI;

internal sealed class TraderMenuOverhaul
{
    private readonly TraderMenuBuilder _builder = new();
    private readonly HashSet<int> _configuredFields = new();
    private int _configuredRootId;
    private int _paletteIndex = -1;

    internal void Prepare(TradeUI trader)
    {
        if (trader == null) return;
        if (_configuredRootId != trader.GetInstanceID())
        {
            _builder.Build(trader);
            _configuredRootId = trader.GetInstanceID();
            _configuredFields.Clear();
            _paletteIndex = -1;
        }
        Apply(trader);
    }

    internal void Apply(TradeUI trader)
    {
        if (trader == null) return;
        if (_configuredRootId != trader.GetInstanceID())
        {
            Prepare(trader);
            return;
        }

        if (_paletteIndex != BetterUIPlugin.PaletteIndex)
        {
            _paletteIndex = BetterUIPlugin.PaletteIndex;
            RefreshPalette(trader);
        }

        UpdateRootWidth();
        ConfigureContent(trader._contentContainer);
        float nameColumnWidth = MeasureNameColumn(trader);
        if (trader._activeFields != null)
        {
            for (int i = 0; i < trader._activeFields.Count; i++)
            {
                TradeFieldUI field = trader._activeFields[i];
                if (field == null) continue;
                int id = field.GetInstanceID();
                if (_configuredFields.Add(id)) BuildField(field);
                StyleField(field, nameColumnWidth);
            }
        }

        bool hideUnavailable = trader._maskUntradable == TradeFieldMask.HideAllUntradable;
        StyleToggleButton(trader._hideUntradableButton, hideUnavailable);
        StyleFooterButton(_builder.CloseButton, ButtonVariant.Danger);
        BetterUIStyler.InputField(trader._searchInputField);
        BetterUIStyler.Scrollbars(_builder.ReplacementRoot != null ? _builder.ReplacementRoot.transform : null);
        trader._tradeBackgroundDefaultColor = BetterUITheme.CraftingCanvas;
        trader._tradableFieldColor = BetterUITheme.CraftingReady;
        trader._untradableFieldColor = BetterUITheme.PanelElevated;
        TradeUI.ToggleActiveColor = BetterUITheme.AccentSoft;
        TradeUI.ToggleInactiveColor = BetterUITheme.PanelSoft;
    }

    internal void SetVisible(bool visible)
    {
        if (_builder.ReplacementRoot != null && _builder.ReplacementRoot.activeSelf != visible)
            _builder.ReplacementRoot.SetActive(visible);
    }

    internal bool HandleInput()
    {
        RectTransform button = _builder.ThemeButton;
        if (button == null || _builder.ReplacementRoot == null ||
            !_builder.ReplacementRoot.activeInHierarchy || Mouse.current == null) return false;
        Canvas canvas = button.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        bool hovered = RectTransformUtility.RectangleContainsScreenPoint(button, Mouse.current.position.ReadValue(), camera);
        Image background = button.GetComponent<Image>();
        if (background != null)
        {
            background.color = hovered ? BetterUITheme.SlotHover : BetterUITheme.PanelSoft;
            Outline outline = background.GetComponent<Outline>();
            if (outline != null) outline.effectColor = hovered ? BetterUITheme.Accent : BetterUITheme.BorderSoft;
        }
        if (!hovered || !Mouse.current.leftButton.wasPressedThisFrame) return false;
        BetterUIPlugin.CyclePalette();
        return true;
    }

    private static void ConfigureContent(Transform content)
    {
        if (content == null) return;
        var layouts = content.GetComponents<LayoutGroup>();
        VerticalLayoutGroup vertical = null;
        for (int i = 0; i < layouts.Length; i++)
        {
            VerticalLayoutGroup candidate = layouts[i].TryCast<VerticalLayoutGroup>();
            if (candidate != null) vertical = candidate;
            else if (layouts[i] != null) layouts[i].enabled = false;
        }
        if (vertical == null) vertical = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.enabled = true;
        vertical.padding = new RectOffset { left = 8, right = 8, top = 8, bottom = 8 };
        vertical.spacing = 8f;
        vertical.childAlignment = TextAnchor.UpperCenter;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static void BuildField(TradeFieldUI field)
    {
        var oldChildren = new List<Transform>();
        for (int i = 0; i < field.transform.childCount; i++) oldChildren.Add(field.transform.GetChild(i));

        var surfaceObject = new GameObject("BetterUI_TradeFieldSurface", Il2CppType.Of<RectTransform>(),
            Il2CppType.Of<CanvasRenderer>(), Il2CppType.Of<Image>());
        surfaceObject.transform.SetParent(field.transform, false);
        RectTransform surfaceRect = surfaceObject.GetComponent<RectTransform>();
        surfaceRect.anchorMin = Vector2.zero; surfaceRect.anchorMax = Vector2.one;
        surfaceRect.offsetMin = Vector2.zero; surfaceRect.offsetMax = Vector2.zero;
        Image surface = surfaceObject.GetComponent<Image>();
        surface.raycastTarget = false;
        surfaceRect.SetAsFirstSibling();

        // Native trade rows use opaque presentation sprites that remain black
        // even after tinting. Redirect native availability updates to our clean
        // surface, then retire the old presentation child below.
        field._backgroundImage = surface;

        var rowObject = new GameObject("BetterUI_TradeRow", Il2CppType.Of<RectTransform>(),
            Il2CppType.Of<HorizontalLayoutGroup>());
        rowObject.transform.SetParent(field.transform, false);
        RectTransform row = rowObject.GetComponent<RectTransform>();
        row.anchorMin = Vector2.zero; row.anchorMax = Vector2.one;
        row.offsetMin = new Vector2(15f, 9f); row.offsetMax = new Vector2(-15f, -9f);
        HorizontalLayoutGroup horizontal = rowObject.GetComponent<HorizontalLayoutGroup>();
        horizontal.padding = new RectOffset { left = 4, right = 4, top = 0, bottom = 0 };
        horizontal.spacing = 12f;
        horizontal.childAlignment = TextAnchor.MiddleCenter;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = false;

        var costObject = new GameObject("BetterUI_TradeCost", Il2CppType.Of<RectTransform>(),
            Il2CppType.Of<HorizontalLayoutGroup>(), Il2CppType.Of<LayoutElement>());
        costObject.transform.SetParent(row, false);
        HorizontalLayoutGroup costs = costObject.GetComponent<HorizontalLayoutGroup>();
        costs.spacing = 6f; costs.childAlignment = TextAnchor.MiddleLeft;
        costs.childControlWidth = true; costs.childControlHeight = true;
        costs.childForceExpandWidth = false; costs.childForceExpandHeight = false;
        int visibleRequirements = 0;
        if (field._requirementContainers != null)
            for (int i = 0; i < field._requirementContainers.Length; i++)
            {
                TradeItemContainerUI slot = field._requirementContainers[i];
                if (slot == null) continue;
                if (slot.gameObject.activeSelf) visibleRequirements++;
                slot.transform.SetParent(costObject.transform, false);
                Normalize(slot.GetComponent<RectTransform>());
                BuildingMenuBuilder.SetLayout(slot.gameObject, 58f, 58f, 0f, 58f, 58f, 0f);
            }
        visibleRequirements = Mathf.Max(1, visibleRequirements);
        float costWidth = (visibleRequirements * 58f) + ((visibleRequirements - 1) * costs.spacing);
        BuildingMenuBuilder.SetLayout(costObject, costWidth, costWidth, 0f, 60f, 60f, 0f);

        TextMeshProUGUI arrow = CloneLabel(field._outputNameTmp, "BetterUI_TradeArrow", row);
        if (arrow != null)
        {
            arrow.text = ">"; arrow.alignment = TextAlignmentOptions.Center;
            BetterUIStyler.Text(arrow, 38f, BetterUITheme.Accent, FontStyles.Normal);
            BuildingMenuBuilder.SetLayout(arrow.gameObject, 34f, 42f, 0f, 58f, 58f, 0f);
        }

        if (field._outputContainer != null)
        {
            field._outputContainer.transform.SetParent(row, false);
            Normalize(field._outputContainer.GetComponent<RectTransform>());
            BuildingMenuBuilder.SetLayout(field._outputContainer.gameObject, 64f, 64f, 0f, 64f, 64f, 0f);
        }
        if (field._outputNameTmp != null)
        {
            field._outputNameTmp.transform.SetParent(row, false);
            Normalize(field._outputNameTmp.rectTransform);
            BuildingMenuBuilder.SetLayout(field._outputNameTmp.gameObject, 150f, 260f, 0f, 58f, 58f, 0f);
        }
        if (field._tradeButton != null)
        {
            field._tradeButton.transform.SetParent(row, false);
            Normalize(field._tradeButton.GetComponent<RectTransform>());
            BuildingMenuBuilder.SetLayout(field._tradeButton.gameObject, 112f, 128f, 0f, 44f, 44f, 0f);
        }

        int rowId = row.GetInstanceID();
        int surfaceId = surfaceRect.GetInstanceID();
        int backgroundId = field._backgroundImage != null ? field._backgroundImage.transform.GetInstanceID() : 0;
        for (int i = 0; i < oldChildren.Count; i++)
        {
            Transform child = oldChildren[i];
            if (child == null || child.parent == null || child.parent.GetInstanceID() != field.transform.GetInstanceID()) continue;
            int id = child.GetInstanceID();
            if (id != rowId && id != surfaceId && id != backgroundId) child.gameObject.SetActive(false);
        }
        row.SetAsLastSibling();
        BuildingMenuBuilder.SetLayout(field.gameObject, 0f, 0f, 1f, 90f, 90f, 0f);
    }

    private static void StyleField(TradeFieldUI field, float nameColumnWidth)
    {
        bool available = field.canAfford;
        Image background = field._backgroundImage;
        if (background == null) background = field.GetComponent<Image>();
        if (background != null)
        {
            RectTransform backgroundRect = background.rectTransform;
            RectTransform fieldRect = field.GetComponent<RectTransform>();
            if (backgroundRect != null && fieldRect != null &&
                backgroundRect.GetInstanceID() != fieldRect.GetInstanceID())
            {
                backgroundRect.anchorMin = Vector2.zero;
                backgroundRect.anchorMax = Vector2.one;
                backgroundRect.offsetMin = Vector2.zero;
                backgroundRect.offsetMax = Vector2.zero;
                backgroundRect.localScale = Vector3.one;
                backgroundRect.SetAsFirstSibling();
            }
            Color fill = available ? BetterUITheme.CraftingReady : BetterUITheme.PanelElevated;
            MakeFlat(background, fill, BetterUITheme.BorderSoft);
            background.raycastTarget = false;
        }

        if (field._requirementContainers != null)
            for (int i = 0; i < field._requirementContainers.Length; i++)
                StyleItemSlot(field._requirementContainers[i], 58f, BetterUITheme.BorderSoft);
        UpdateRequirementWidth(field);
        StyleItemSlot(field._outputContainer, 64f, BetterUITheme.BorderSoft);

        if (field._outputNameTmp != null)
        {
            BetterUIStyler.Text(field._outputNameTmp, 18f,
                available ? BetterUITheme.Text : BetterUITheme.TextUnavailable, FontStyles.Bold);
            field._outputNameTmp.enableAutoSizing = true;
            field._outputNameTmp.fontSizeMin = 13f;
            field._outputNameTmp.fontSizeMax = 18f;
            field._outputNameTmp.alignment = TextAlignmentOptions.MidlineLeft;
            field._outputNameTmp.overflowMode = TextOverflowModes.Ellipsis;
            field._outputNameTmp.margin = new Vector4(4f, 0f, 4f, 0f);
            BuildingMenuBuilder.SetLayout(
                field._outputNameTmp.gameObject, nameColumnWidth, nameColumnWidth, 0f, 58f, 58f, 0f);
        }
        if (field._tradeButton != null)
        {
            if (field._tradeButton.buttonText != null) field._tradeButton.buttonText.text = "TRADE";
            StyleTradeButton(field._tradeButton, available);
        }
        Transform row = field.transform.Find("BetterUI_TradeRow");
        Transform arrow = row != null ? row.Find("BetterUI_TradeArrow") : null;
        TextMeshProUGUI arrowText = arrow != null ? arrow.GetComponent<TextMeshProUGUI>() : null;
        if (arrowText != null) arrowText.color = available ? BetterUITheme.Accent : BetterUITheme.TextDim;
    }

    private static float MeasureNameColumn(TradeUI trader)
    {
        float width = 150f;
        if (trader == null || trader._activeFields == null) return width;
        for (int i = 0; i < trader._activeFields.Count; i++)
        {
            TradeFieldUI field = trader._activeFields[i];
            if (field == null || field._outputNameTmp == null || !field.gameObject.activeSelf) continue;
            width = Mathf.Max(width, field._outputNameTmp.preferredWidth + 16f);
        }
        return Mathf.Clamp(width, 150f, 300f);
    }

    private void UpdateRootWidth()
    {
        RectTransform root = _builder.ReplacementRoot != null
            ? _builder.ReplacementRoot.GetComponent<RectTransform>()
            : null;
        RectTransform parent = root != null && root.parent != null
            ? root.parent.GetComponent<RectTransform>()
            : null;
        if (root == null || parent == null) return;
        float width = Mathf.Min(760f, Mathf.Max(620f, parent.rect.width - 32f));
        root.offsetMin = new Vector2(-width * .5f, 0f);
        root.offsetMax = new Vector2(width * .5f, 0f);
    }

    private static void UpdateRequirementWidth(TradeFieldUI field)
    {
        Transform row = field != null ? field.transform.Find("BetterUI_TradeRow") : null;
        Transform cost = row != null ? row.Find("BetterUI_TradeCost") : null;
        if (cost == null) return;
        int count = 0;
        if (field._requirementContainers != null)
            for (int i = 0; i < field._requirementContainers.Length; i++)
                if (field._requirementContainers[i] != null && field._requirementContainers[i].gameObject.activeSelf)
                    count++;
        count = Mathf.Max(1, count);
        float width = (count * 58f) + ((count - 1) * 6f);
        BuildingMenuBuilder.SetLayout(cost.gameObject, width, width, 0f, 60f, 60f, 0f);
    }

    private static void StyleItemSlot(ItemContainerUI slot, float size, Color border)
    {
        if (slot == null) return;
        Image background = slot.GetComponent<Image>();
        bool backgroundIsItem = background != null && slot.itemImage != null &&
            background.GetInstanceID() == slot.itemImage.GetInstanceID();
        if (background != null && !backgroundIsItem)
        {
            MakeFlat(background, BetterUITheme.Slot, border);
        }
        if (slot.itemImage != null)
        {
            slot.itemImage.enabled = true;
            slot.itemImage.preserveAspect = true;
            slot.itemImage.raycastTarget = false;
            RectTransform art = slot.itemImage.rectTransform;
            if (art != null && art.GetInstanceID() != slot.GetComponent<RectTransform>().GetInstanceID())
            {
                art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
                art.offsetMin = new Vector2(6f, 6f); art.offsetMax = new Vector2(-6f, -6f);
                art.localScale = Vector3.one;
            }
        }
        if (slot.countBackgroundImage != null)
        {
            BetterUIStyler.RoundedImage(slot.countBackgroundImage, BetterUITheme.CountBackground);
            RectTransform badge = slot.countBackgroundImage.rectTransform;
            badge.anchorMin = badge.anchorMax = new Vector2(1f, 0f);
            badge.pivot = new Vector2(1f, 0f); badge.anchoredPosition = new Vector2(-2f, 2f);
            badge.sizeDelta = new Vector2(24f, 18f); badge.SetAsLastSibling();
        }
        if (slot.itemCountTmp != null)
        {
            BetterUIStyler.Text(slot.itemCountTmp, 12f, BetterUITheme.Text, FontStyles.Bold);
            slot.itemCountTmp.alignment = TextAlignmentOptions.Center;
        }
        BuildingMenuBuilder.SetLayout(slot.gameObject, size, size, 0f, size, size, 0f);
    }

    private void RefreshPalette(TradeUI trader)
    {
        if (_builder.ReplacementRoot == null) return;
        var images = _builder.ReplacementRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null) continue;
            Color? fill = image.name switch
            {
                "BetterUI_TraderMenu" => BetterUITheme.CraftingCanvas,
                "BetterUI_TraderHeader" => BetterUITheme.Panel,
                "BetterUI_TraderToolbar" => BetterUITheme.PanelElevated,
                "BetterUI_TraderFooter" => BetterUITheme.Panel,
                "BetterUI_TraderBody" => BetterUITheme.Panel,
                "BetterUI_TraderScroll" => BetterUITheme.CraftingWell,
                "Viewport" => BetterUITheme.CraftingWell,
                "BetterUI_TraderThemeButton" => BetterUITheme.PanelSoft,
                "BetterUI_TraderScrollbar" => BetterUITheme.PanelSoft,
                "Handle" => BetterUITheme.Accent,
                _ => null
            };
            if (fill.HasValue)
            {
                image.color = fill.Value;
                Outline outline = image.GetComponent<Outline>();
                if (outline != null) outline.effectColor = image.name == "BetterUI_TraderMenu"
                    ? BetterUITheme.Accent : BetterUITheme.BorderSoft;
            }
        }
        var labels = _builder.ReplacementRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (label == null) continue;
            if (label.name == "BetterUI_TraderTitle") label.color = BetterUITheme.Text;
            else if (label.name == "BetterUI_TraderStatus") label.color = BetterUITheme.Accent;
            else if (label.name == "BetterUI_TraderOffersHeading" || label.name == "BetterUI_TraderHint")
                label.color = BetterUITheme.TextMuted;
        }
        BetterUIStyler.InputField(trader._searchInputField);
    }

    private static void StyleTradeButton(MazeButton button, bool available)
    {
        if (button == null) return;
        button._menuButtonShader = false;
        if (!available)
        {
            BetterUIStyler.Button(button, ButtonVariant.Secondary);
            MakeFlat(button.backgroundImage != null ? button.backgroundImage.TryCast<Image>() : null,
                BetterUITheme.PanelSoft, BetterUITheme.BorderSoft);
            return;
        }

        Color normal = BetterUITheme.CraftingSelected;
        button.backgroundColor = normal;
        button.hoverColor = BetterUITheme.CraftingSelectedHover;
        button.textColor = BetterUITheme.Text;
        button.hoverTextColor = BetterUITheme.Text;
        button._disabledBackgroundColor = BetterUITheme.PanelSoft;
        button._disabledTextColor = BetterUITheme.TextDim;
        button.UpdateVisualState(true);
        MakeFlat(button.backgroundImage != null ? button.backgroundImage.TryCast<Image>() : null,
            button.Interactable ? normal : BetterUITheme.PanelSoft,
            button.Interactable ? BetterUITheme.Accent : BetterUITheme.BorderSoft);
        if (button.buttonText != null)
            BetterUIStyler.Text(button.buttonText, 15f, BetterUITheme.Text, FontStyles.Bold);
    }

    private static void StyleToggleButton(MazeButton button, bool active)
    {
        if (button == null) return;
        button._menuButtonShader = false;
        BetterUIStyler.ToggleButton(button, active);
        Color fill = active ? BetterUITheme.CraftingSelected : BetterUITheme.PanelSoft;
        MakeFlat(button.backgroundImage != null ? button.backgroundImage.TryCast<Image>() : null,
            fill, active ? BetterUITheme.Accent : BetterUITheme.BorderSoft);
    }

    private static void StyleFooterButton(MazeButton button, ButtonVariant variant)
    {
        if (button == null) return;
        button._menuButtonShader = false;
        BetterUIStyler.Button(button, variant);
        Color fill = BetterUITheme.PanelSoft;
        Color border = variant == ButtonVariant.Danger ? BetterUITheme.Danger : BetterUITheme.Border;
        button.backgroundColor = fill;
        button.hoverColor = variant == ButtonVariant.Danger ? BetterUITheme.DangerSoft : BetterUITheme.SlotHover;
        button.textColor = BetterUITheme.Text;
        button.hoverTextColor = BetterUITheme.Text;
        button.UpdateVisualState(true);
        MakeFlat(button.backgroundImage != null ? button.backgroundImage.TryCast<Image>() : null, fill, border);
        if (button.buttonText != null)
            BetterUIStyler.Text(button.buttonText, 15f, BetterUITheme.Text, FontStyles.Bold);
    }

    private static void MakeFlat(Image image, Color fill, Color border)
    {
        if (image == null) return;
        image.enabled = true;
        image.material = null;
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = fill;
        BetterUIStyler.Effects(image, border, false);
        Outline outline = image.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = true;
            outline.effectColor = border;
            outline.effectDistance = new Vector2(0.75f, -0.75f);
            outline.useGraphicAlpha = true;
        }
    }

    private static TextMeshProUGUI CloneLabel(TextMeshProUGUI template, string name, Transform parent)
    {
        if (template == null) return null;
        GameObject item = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
        item.name = name; item.SetActive(true);
        TextMeshProUGUI label = item.GetComponent<TextMeshProUGUI>();
        label.text = string.Empty; label.raycastTarget = false;
        Normalize(label.rectTransform);
        return label;
    }

    private static void Normalize(RectTransform rect)
    {
        if (rect == null) return;
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = Vector2.zero;
    }
}
