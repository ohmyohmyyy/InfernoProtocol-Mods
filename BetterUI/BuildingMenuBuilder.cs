using System;
using System.Collections.Generic;
using Game.UI;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InfernoProtocol.BetterUI;

/// <summary>
/// Moves the live building controls into a BetterUI shell. The original buttons,
/// recipes and callbacks stay intact, so this changes presentation rather than
/// replacing the game's placement system.
/// </summary>
internal sealed class BuildingMenuBuilder
{
    private int _builtForRootId;

    internal GameObject ReplacementRoot { get; private set; }
    internal RectTransform ThemeButton { get; private set; }
    internal MazeButton CloseButton { get; private set; }

    internal void Build(BuildTemplatesUI building)
    {
        if (building == null || _builtForRootId == building.GetInstanceID())
        {
            return;
        }

        TextMeshProUGUI template = FindTextTemplate(building);
        if (template == null)
        {
            throw new InvalidOperationException("The building menu has no initialized text template.");
        }

        Transform owner = building.transform;
        Canvas canvas = building.GetComponentInParent<Canvas>();
        Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;
        Transform displayRoot = rootCanvas != null ? rootCanvas.transform : canvas != null ? canvas.transform : owner;
        DisableOwnerShellGraphics(building);
        var legacyChildren = new List<Transform>();
        for (int i = 0; i < owner.childCount; i++)
        {
            legacyChildren.Add(owner.GetChild(i));
        }

        RectTransform root = CreateSurface("BetterUI_BuildMenu", displayRoot, BetterUITheme.CraftingCanvas, true);
        root.anchorMin = new Vector2(0.035f, 0.055f);
        root.anchorMax = new Vector2(0.965f, 0.945f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.SetAsLastSibling();
        ReplacementRoot = root.gameObject;

        BuildHeader(root, template);

        RectTransform body = CreateRect("BetterUI_BuildBody", root);
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(14f, 64f);
        body.offsetMax = new Vector2(-14f, -92f);

        RectTransform categories = CreateSurface("BetterUI_BuildCategories", body, BetterUITheme.PanelElevated);
        categories.anchorMin = Vector2.zero;
        categories.anchorMax = new Vector2(0.19f, 1f);
        categories.offsetMin = Vector2.zero;
        categories.offsetMax = Vector2.zero;

        RectTransform browser = CreateSurface("BetterUI_BuildBrowser", body, BetterUITheme.Panel);
        browser.anchorMin = new Vector2(0.202f, 0f);
        browser.anchorMax = new Vector2(0.625f, 1f);
        browser.offsetMin = Vector2.zero;
        browser.offsetMax = Vector2.zero;

        RectTransform details = CreateSurface("BetterUI_BuildDetails", body, BetterUITheme.PanelElevated);
        details.anchorMin = new Vector2(0.637f, 0f);
        details.anchorMax = Vector2.one;
        details.offsetMin = Vector2.zero;
        details.offsetMax = Vector2.zero;

        BuildCategories(building, categories, template);
        BuildBrowser(building, browser, template);
        BuildDetails(building, details, template);
        BuildFooter(building, root, template);

        int ownerId = owner.GetInstanceID();
        for (int i = 0; i < legacyChildren.Count; i++)
        {
            Transform child = legacyChildren[i];
            if (child != null && child.parent != null && child.parent.GetInstanceID() == ownerId)
            {
                child.gameObject.SetActive(false);
            }
        }

        root.gameObject.SetActive(true);
        _builtForRootId = building.GetInstanceID();
        BetterUIPlugin.ModLog.LogInfo("Built BetterUI structure-catalog hierarchy.");
    }

    private static void DisableOwnerShellGraphics(BuildTemplatesUI building)
    {
        // BuildTemplatesUI itself carries the full-screen legacy backdrop. It is
        // not one of the child panels hidden below, so disable only graphics on
        // the component's own object while leaving its behaviour and raycasts.
        var graphics = building.GetComponents<Graphic>();
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null) graphics[i].enabled = false;
        }
    }

    private static void BuildHeader(RectTransform root, TextMeshProUGUI template)
    {
        RectTransform header = CreateSurface("BetterUI_BuildHeader", root, BetterUITheme.Panel);
        AnchorTop(header, 78f, 0f, 0f);

        TextMeshProUGUI title = CreateLabel("BetterUI_BuildTitle", header, template);
        title.text = "BUILDING // STRUCTURE CATALOG";
        BetterUIStyler.Heading(title, 28f);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        Stretch(title.rectTransform, 20f, 270f);

        TextMeshProUGUI status = CreateLabel("BetterUI_BuildStatus", header, template);
        status.text = "FIELD CONSTRUCTION  //  READY\nHAMMER LINK ACTIVE";
        BetterUIStyler.Text(status, 11f, BetterUITheme.Accent, FontStyles.Bold);
        status.characterSpacing = 1.2f;
        status.alignment = TextAlignmentOptions.MidlineRight;
        RectTransform rect = status.rectTransform;
        rect.anchorMin = new Vector2(0.65f, 0f);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(0f, 8f);
        rect.offsetMax = new Vector2(-18f, -8f);
    }

    private static void BuildCategories(BuildTemplatesUI building, RectTransform panel, TextMeshProUGUI template)
    {
        VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = Padding(12, 12, 16, 12);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI heading = CreateLabel("BetterUI_BuildCategoriesHeading", panel, template);
        heading.text = "BUILD CLASS";
        BetterUIStyler.Text(heading, 13f, BetterUITheme.TextMuted, FontStyles.Bold);
        heading.characterSpacing = 1.5f;
        heading.alignment = TextAlignmentOptions.MidlineLeft;
        SetLayout(heading.gameObject, 0f, 0f, 1f, 27f, 27f, 0f);

        AddCategory(building.filterWallButton, panel, "WALLS", 0);
        AddCategory(building.filterFloorCeilingButton, panel, "FLOORS + CEILINGS", 1);
        AddCategory(building.filterStairButton, panel, "STAIRS", 2);
        AddCategory(building.filterFunctionalButton, panel, "UTILITY", 3);
        AddCategory(building.filterMiscButton, panel, "DECOR", 4);

        RectTransform spacer = CreateRect("BetterUI_BuildCategorySpacer", panel);
        SetLayout(spacer.gameObject, 0f, 0f, 1f, 1f, 1f, 1f);

        TextMeshProUGUI hint = CreateLabel("BetterUI_BuildCategoryHint", panel, template);
        hint.text = "FILTER THE CATALOG\nTHEN CHOOSE A PLAN";
        BetterUIStyler.Text(hint, 10f, BetterUITheme.TextDim, FontStyles.Normal);
        hint.characterSpacing = 0.8f;
        hint.alignment = TextAlignmentOptions.BottomLeft;
        SetLayout(hint.gameObject, 0f, 0f, 1f, 38f, 38f, 0f);
    }

    private static void AddCategory(MazeButton button, Transform panel, string label, int sibling)
    {
        if (button == null)
        {
            return;
        }

        MoveToLayout(button.transform, panel, 0f, 0f, 1f, 52f);
        button.transform.SetSiblingIndex(sibling + 1);
        if (button.buttonText != null)
        {
            button.buttonText.text = label;
            button.buttonText.alignment = TextAlignmentOptions.MidlineLeft;
            button.buttonText.margin = new Vector4(16f, 0f, 8f, 0f);
        }
    }

    private static void BuildBrowser(BuildTemplatesUI building, RectTransform panel, TextMeshProUGUI template)
    {
        RectTransform header = CreateSurface("BetterUI_BuildBrowserHeader", panel, BetterUITheme.PanelElevated);
        AnchorTop(header, 94f, 8f, 8f);

        TextMeshProUGUI heading = CreateLabel("BetterUI_BuildBrowserHeading", header, template);
        heading.text = "AVAILABLE PLANS";
        BetterUIStyler.Text(heading, 13f, BetterUITheme.TextMuted, FontStyles.Bold);
        heading.characterSpacing = 1.5f;
        heading.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform headingRect = heading.rectTransform;
        headingRect.anchorMin = new Vector2(0f, 0.5f);
        headingRect.anchorMax = Vector2.one;
        headingRect.offsetMin = new Vector2(12f, 0f);
        headingRect.offsetMax = new Vector2(-12f, 0f);

        if (building._searchInputField != null)
        {
            RectTransform search = building._searchInputField.GetComponent<RectTransform>();
            if (search != null)
            {
                search.SetParent(header, false);
                search.anchorMin = Vector2.zero;
                search.anchorMax = new Vector2(1f, 0.5f);
                search.offsetMin = new Vector2(10f, 8f);
                search.offsetMax = new Vector2(-10f, -4f);
                LayoutElement element = search.GetComponent<LayoutElement>();
                if (element != null) element.ignoreLayout = true;
            }
        }

        RectTransform scrollRoot = CreateSurface("BetterUI_BuildScroll", panel, BetterUITheme.CraftingWell);
        scrollRoot.anchorMin = Vector2.zero;
        scrollRoot.anchorMax = Vector2.one;
        scrollRoot.offsetMin = new Vector2(8f, 8f);
        scrollRoot.offsetMax = new Vector2(-8f, -102f);

        RectTransform viewport = CreateSurface("Viewport", scrollRoot, BetterUITheme.CraftingWell);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(7f, 7f);
        viewport.offsetMax = new Vector2(-22f, -7f);
        viewport.GetComponent<Image>().raycastTarget = true;
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        RectTransform content = building._buildRecipesContainer != null
            ? building._buildRecipesContainer.GetComponent<RectTransform>()
            : null;
        if (content != null)
        {
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
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
        scroll.decelerationRate = 0.12f;
        scroll.scrollSensitivity = 36f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scroll.verticalScrollbarSpacing = 4f;
    }

    private static void BuildDetails(BuildTemplatesUI building, RectTransform panel, TextMeshProUGUI template)
    {
        RectTransform header = CreateSurface("BetterUI_BuildDetailsHeader", panel, BetterUITheme.CraftingWell);
        AnchorTop(header, 50f, 8f, 8f);
        TextMeshProUGUI heading = CreateLabel("BetterUI_BuildDetailsHeading", header, template);
        heading.text = "PLAN INSPECTION // MATERIALS";
        BetterUIStyler.Text(heading, 14f, BetterUITheme.TextMuted, FontStyles.Bold);
        heading.characterSpacing = 1.5f;
        heading.alignment = TextAlignmentOptions.MidlineLeft;
        Stretch(heading.rectTransform, 12f, 12f);

        SelectedBuildRecipeUI selected = building._selectedRecipeUI;
        if (selected == null)
        {
            return;
        }

        RectTransform selectedRect = selected.GetComponent<RectTransform>();
        selectedRect.SetParent(panel, false);
        selectedRect.anchorMin = Vector2.zero;
        selectedRect.anchorMax = Vector2.one;
        selectedRect.offsetMin = new Vector2(10f, 10f);
        selectedRect.offsetMax = new Vector2(-10f, -58f);
        selected.gameObject.SetActive(true);
        DisableSelectedShellGraphic(selected.gameObject);
        ConfigureEmptyState(selected, template);
        ConfigureSelectedState(selected, template);
    }

    private static void DisableSelectedShellGraphic(GameObject target)
    {
        if (target == null) return;
        var graphics = target.GetComponents<Graphic>();
        for (int i = 0; i < graphics.Length; i++)
        {
            Image image = graphics[i] != null ? graphics[i].TryCast<Image>() : null;
            if (image != null) image.enabled = false;
        }
    }

    private static void ConfigureEmptyState(SelectedBuildRecipeUI selected, TextMeshProUGUI template)
    {
        if (selected.didntSelectRecipeParent == null)
        {
            return;
        }

        RectTransform rect = selected.didntSelectRecipeParent.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        TextMeshProUGUI prompt = null;
        var labels = selected.didntSelectRecipeParent.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (labels.Length > 0) prompt = labels[0];
        if (prompt == null) prompt = CreateLabel("BetterUI_BuildSelectPrompt", selected.didntSelectRecipeParent.transform, template);
        prompt.text = "SELECT A BUILDING PLAN\n<size=70%>Choose a category or search the catalog</size>";
        BetterUIStyler.Text(prompt, 17f, BetterUITheme.TextMuted, FontStyles.Bold);
        prompt.alignment = TextAlignmentOptions.Center;
        RectTransform promptRect = prompt.rectTransform;
        promptRect.anchorMin = Vector2.zero;
        promptRect.anchorMax = Vector2.one;
        promptRect.offsetMin = new Vector2(24f, 24f);
        promptRect.offsetMax = new Vector2(-24f, -24f);
    }

    private static void ConfigureSelectedState(SelectedBuildRecipeUI selected, TextMeshProUGUI template)
    {
        if (selected.approvedParent == null)
        {
            return;
        }

        Transform approved = selected.approvedParent.transform;
        DisableSelectedShellGraphic(selected.approvedParent);
        RectTransform approvedRect = approved.GetComponent<RectTransform>();
        if (approvedRect != null)
        {
            approvedRect.anchorMin = Vector2.zero;
            approvedRect.anchorMax = Vector2.one;
            approvedRect.offsetMin = Vector2.zero;
            approvedRect.offsetMax = Vector2.zero;
        }

        var oldChildren = new List<Transform>();
        for (int i = 0; i < approved.childCount; i++) oldChildren.Add(approved.GetChild(i));

        RectTransform content = CreateRect("BetterUI_BuildSelectedContent", approved);
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(14f, 14f);
        content.offsetMax = new Vector2(-14f, -14f);
        VerticalLayoutGroup vertical = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.padding = Padding(8, 8, 8, 8);
        vertical.spacing = 12f;
        vertical.childAlignment = TextAnchor.UpperCenter;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        RectTransform summary = CreateSurface("BetterUI_BuildSummary", content, BetterUITheme.CraftingWell);
        SetLayout(summary.gameObject, 0f, 0f, 1f, 136f, 136f, 0f);
        HorizontalLayoutGroup summaryLayout = summary.gameObject.AddComponent<HorizontalLayoutGroup>();
        summaryLayout.padding = Padding(16, 16, 12, 12);
        summaryLayout.spacing = 18f;
        summaryLayout.childAlignment = TextAnchor.MiddleLeft;
        summaryLayout.childControlWidth = true;
        summaryLayout.childControlHeight = true;
        summaryLayout.childForceExpandWidth = false;
        summaryLayout.childForceExpandHeight = false;
        MoveToLayout(selected._outputItemContainer != null ? selected._outputItemContainer.transform : null, summary, 108f, 108f, 0f, 108f);
        MoveToLayout(selected._outputItemNameTMP != null ? selected._outputItemNameTMP.transform : null, summary, 120f, 220f, 1f, 72f);

        TextMeshProUGUI materials = CreateLabel("BetterUI_BuildMaterialsHeading", content, template);
        materials.text = "REQUIRED MATERIALS  //  AVAILABLE / NEEDED";
        BetterUIStyler.Text(materials, 12f, BetterUITheme.TextMuted, FontStyles.Bold);
        materials.characterSpacing = 1.1f;
        materials.alignment = TextAlignmentOptions.MidlineLeft;
        SetLayout(materials.gameObject, 0f, 0f, 1f, 28f, 28f, 0f);

        RectTransform requirements = CreateSurface("BetterUI_BuildRequirements", content, BetterUITheme.PanelSoft);
        SetLayout(requirements.gameObject, 0f, 0f, 1f, 270f, 270f, 0f);
        if (selected._requiredItemsContainer != null)
        {
            RectTransform items = selected._requiredItemsContainer.GetComponent<RectTransform>();
            items.SetParent(requirements, false);
            items.anchorMin = Vector2.zero;
            items.anchorMax = Vector2.one;
            items.offsetMin = new Vector2(10f, 10f);
            items.offsetMax = new Vector2(-10f, -10f);
        }

        RectTransform flexible = CreateRect("BetterUI_BuildDetailsSpacer", content);
        SetLayout(flexible.gameObject, 0f, 0f, 1f, 1f, 1f, 1f);

        if (selected._placeButton != null)
        {
            if (selected._placeButton.buttonText != null) selected._placeButton.buttonText.text = "PLACE STRUCTURE";
            MoveToLayout(selected._placeButton.transform, content, 0f, 0f, 1f, 56f);
        }

        int approvedId = approved.GetInstanceID();
        for (int i = 0; i < oldChildren.Count; i++)
        {
            Transform child = oldChildren[i];
            if (child != null && child.parent != null && child.parent.GetInstanceID() == approvedId)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private void BuildFooter(BuildTemplatesUI building, RectTransform root, TextMeshProUGUI template)
    {
        RectTransform footer = CreateSurface("BetterUI_BuildFooter", root, BetterUITheme.Panel);
        footer.anchorMin = Vector2.zero;
        footer.anchorMax = new Vector2(1f, 0f);
        footer.pivot = new Vector2(0.5f, 0f);
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

        AddFooterPrompt(footer, template, "SELECT  /  PLACE", 150f);
        AddFooterPrompt(footer, template, "SCROLL  /  NAVIGATE", 180f);
        RectTransform spacer = CreateRect("BetterUI_BuildFooterSpacer", footer);
        SetLayout(spacer.gameObject, 0f, 0f, 1f, 1f, 1f, 0f);

        ThemeButton = CreateSurface("BetterUI_BuildThemeButton", footer, BetterUITheme.PanelSoft);
        SetLayout(ThemeButton.gameObject, 42f, 46f, 0f, 38f, 38f, 0f);
        ThemeButton.GetComponent<Image>().raycastTarget = true;
        var iconObject = new GameObject("BetterUI_BuildColorWheel", Il2CppType.Of<RectTransform>(), Il2CppType.Of<CanvasRenderer>(), Il2CppType.Of<RawImage>());
        iconObject.transform.SetParent(ThemeButton, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(25f, 25f);
        RawImage icon = iconObject.GetComponent<RawImage>();
        icon.texture = BetterUITheme.ColorWheelTexture;
        icon.raycastTarget = false;

        if (building._hideUncraftableButton != null)
        {
            MoveToLayout(building._hideUncraftableButton.transform, footer, 42f, 46f, 0f, 38f);
        }

        MazeButton close = FindCloseButton(building.transform);
        if (close != null)
        {
            CloseButton = close;
            if (close.buttonText != null) close.buttonText.text = "[ ESC ]  CLOSE";
            MoveToLayout(close.transform, footer, 130f, 150f, 0f, 38f);
        }
    }

    private static void AddFooterPrompt(Transform footer, TextMeshProUGUI template, string text, float width)
    {
        TextMeshProUGUI prompt = CreateLabel("BetterUI_BuildFooterPrompt", footer, template);
        prompt.text = text;
        BetterUIStyler.Text(prompt, 11f, BetterUITheme.TextMuted, FontStyles.Bold);
        prompt.characterSpacing = 0.8f;
        prompt.alignment = TextAlignmentOptions.MidlineLeft;
        SetLayout(prompt.gameObject, width * 0.65f, width, 0f, 30f, 30f, 0f);
    }

    private static TextMeshProUGUI FindTextTemplate(BuildTemplatesUI building)
    {
        if (building._selectedRecipeUI != null && building._selectedRecipeUI._outputItemNameTMP != null)
        {
            return building._selectedRecipeUI._outputItemNameTMP;
        }

        var labels = building.GetComponentsInChildren<TextMeshProUGUI>(true);
        return labels.Length > 0 ? labels[0] : null;
    }

    private static MazeButton FindCloseButton(Transform root)
    {
        var buttons = root.GetComponentsInChildren<MazeButton>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            MazeButton button = buttons[i];
            string text = button != null && button.buttonText != null ? button.buttonText.text?.Trim().ToUpperInvariant() : string.Empty;
            if (text == "CLOSE" || text == "EXIT" || text.Contains("CLOSE")) return button;
        }

        return null;
    }

    private static Scrollbar CreateScrollbar(RectTransform parent)
    {
        RectTransform track = CreateSurface("BetterUI_BuildScrollbar", parent, BetterUITheme.PanelSoft);
        track.anchorMin = new Vector2(1f, 0f);
        track.anchorMax = Vector2.one;
        track.pivot = new Vector2(1f, 0.5f);
        track.offsetMin = new Vector2(-14f, 8f);
        track.offsetMax = new Vector2(-4f, -8f);
        track.GetComponent<Image>().raycastTarget = true;
        RectTransform handle = CreateSurface("Handle", track, BetterUITheme.Accent);
        handle.anchorMin = Vector2.zero;
        handle.anchorMax = Vector2.one;
        handle.offsetMin = Vector2.zero;
        handle.offsetMax = Vector2.zero;
        handle.GetComponent<Image>().raycastTarget = true;
        Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.size = 0.2f;
        return scrollbar;
    }

    private static void MoveToLayout(Transform item, Transform parent, float minWidth, float preferredWidth, float flexibleWidth, float height)
    {
        if (item == null) return;
        item.SetParent(parent, false);
        RectTransform rect = item.GetComponent<RectTransform>();
        if (rect != null) NormalizeRect(rect);
        SetLayout(item.gameObject, minWidth, preferredWidth, flexibleWidth, height, height, 0f);
    }

    internal static void SetLayout(GameObject target, float minWidth, float preferredWidth, float flexibleWidth, float minHeight, float preferredHeight, float flexibleHeight)
    {
        LayoutElement layout = target.GetComponent<LayoutElement>();
        if (layout == null) layout = target.AddComponent<LayoutElement>();
        layout.minWidth = minWidth;
        layout.preferredWidth = preferredWidth;
        layout.flexibleWidth = flexibleWidth;
        layout.minHeight = minHeight;
        layout.preferredHeight = preferredHeight;
        layout.flexibleHeight = flexibleHeight;
    }

    internal static RectTransform CreateSurface(string name, Transform parent, Color color, bool shadow = false)
    {
        var gameObject = new GameObject(name, Il2CppType.Of<RectTransform>(), Il2CppType.Of<CanvasRenderer>(), Il2CppType.Of<Image>());
        gameObject.transform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>();
        image.material = null;
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        BetterUIStyler.Effects(image, name == "BetterUI_BuildMenu" ? BetterUITheme.Accent : BetterUITheme.BorderSoft, shadow);
        return gameObject.GetComponent<RectTransform>();
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var gameObject = new GameObject(name, Il2CppType.Of<RectTransform>());
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private static TextMeshProUGUI CreateLabel(string name, Transform parent, TextMeshProUGUI template)
    {
        GameObject gameObject = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
        gameObject.name = name;
        gameObject.SetActive(true);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        if (rect != null) NormalizeRect(rect);
        TextMeshProUGUI label = gameObject.GetComponent<TextMeshProUGUI>();
        label.text = string.Empty;
        label.raycastTarget = false;
        return label;
    }

    private static void NormalizeRect(RectTransform rect)
    {
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void Stretch(RectTransform rect, float left, float right)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, 0f);
        rect.offsetMax = new Vector2(-right, 0f);
    }

    private static void AnchorTop(RectTransform rect, float height, float left, float right)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -height);
        rect.offsetMax = new Vector2(-right, 0f);
    }

    private static RectOffset Padding(int left, int right, int top, int bottom)
    {
        return new RectOffset { left = left, right = right, top = top, bottom = bottom };
    }
}
