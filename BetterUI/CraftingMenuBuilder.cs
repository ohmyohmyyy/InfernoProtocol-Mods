using System;
using System.Collections.Generic;
using Game.UI;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InfernoProtocol.BetterUI;

/// <summary>
/// Creates a new crafting window and moves the game's live controls into it.
/// Gameplay callbacks remain on the original controls; only the legacy visual
/// shell and layout hierarchy are replaced.
/// </summary>
internal sealed class CraftingMenuBuilder
{
    private int _builtForRootId;

    internal GameObject ReplacementRoot { get; private set; }

    internal void Build(CraftingTableUI crafting)
    {
        if (crafting == null || _builtForRootId == crafting.GetInstanceID())
        {
            return;
        }

        Transform owner = crafting.transform;
        Canvas canvas = crafting.GetComponentInParent<Canvas>();
        Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;
        Transform displayRoot = rootCanvas != null
            ? rootCanvas.transform
            : canvas != null ? canvas.transform : owner;
        var legacyChildren = new List<Transform>();
        for (int i = 0; i < owner.childCount; i++)
        {
            legacyChildren.Add(owner.GetChild(i));
        }

        RectTransform root = CreateSurface("BetterUI_CraftingMenu", displayRoot, BetterUITheme.CraftingCanvas);
        root.anchorMin = new Vector2(0.035f, 0.055f);
        root.anchorMax = new Vector2(0.965f, 0.945f);
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.SetAsLastSibling();
        ReplacementRoot = root.gameObject;
        BetterUIStyler.Effects(root.GetComponent<Image>(), BetterUITheme.Accent, true);

        RectTransform header = CreateSurface("BetterUI_Header", root, BetterUITheme.Panel);
        AnchorTop(header, 84f, 0f, 0f);
        HorizontalLayoutGroup headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
        headerLayout.padding = Padding(20, 20, 12, 12);
        headerLayout.spacing = 12f;
        headerLayout.childAlignment = TextAnchor.MiddleCenter;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = false;

        RectTransform body = CreateRect("BetterUI_Body", root);
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(14f, 64f);
        body.offsetMax = new Vector2(-14f, -98f);

        RectTransform categoriesPanel = CreateSurface("BetterUI_Categories", body, BetterUITheme.PanelElevated);
        categoriesPanel.anchorMin = Vector2.zero;
        categoriesPanel.anchorMax = new Vector2(0.18f, 1f);
        categoriesPanel.offsetMin = Vector2.zero;
        categoriesPanel.offsetMax = Vector2.zero;

        RectTransform browserPanel = CreateSurface("BetterUI_RecipeBrowser", body, BetterUITheme.Panel);
        browserPanel.anchorMin = new Vector2(0.192f, 0f);
        browserPanel.anchorMax = new Vector2(0.585f, 1f);
        browserPanel.offsetMin = Vector2.zero;
        browserPanel.offsetMax = Vector2.zero;

        RectTransform detailsPanel = CreateSurface("BetterUI_Details", body, BetterUITheme.PanelElevated);
        detailsPanel.anchorMin = new Vector2(0.597f, 0f);
        detailsPanel.anchorMax = Vector2.one;
        detailsPanel.offsetMin = Vector2.zero;
        detailsPanel.offsetMax = Vector2.zero;

        BuildSection("header", () => BuildHeader(crafting, header));
        BuildSection("classification rail", () => BuildCategories(crafting, categoriesPanel));
        BuildSection("schematic index", () => BuildBrowser(crafting, browserPanel));
        BuildSection("inspection workspace", () => BuildDetails(crafting, detailsPanel));
        BuildSection("command footer", () => BuildFooter(crafting, root));

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
        _builtForRootId = crafting.GetInstanceID();
        BetterUIPlugin.ModLog.LogInfo("Built replacement terminal-workbench crafting hierarchy.");
    }

    private static void BuildCategories(CraftingTableUI crafting, RectTransform categoriesPanel)
    {
        VerticalLayoutGroup layout = categoriesPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = Padding(12, 12, 16, 12);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI heading = CreateLabel("BetterUI_CategoriesHeading", categoriesPanel, crafting._titleTMP);
        heading.text = "CLASSIFICATION";
        heading.fontSize = 13f;
        heading.fontStyle = FontStyles.Bold;
        heading.color = BetterUITheme.TextMuted;
        heading.characterSpacing = 1.6f;
        heading.alignment = TextAlignmentOptions.MidlineLeft;
        SetLayout(heading.gameObject, 0f, 0f, 1f, 26f, 26f, 0f);

        var filters = new List<MazeButton>
        {
            crafting.filterMiscButton,
            crafting.filterCombatButton,
            crafting.filterFoodButton,
            crafting.filterHealthyButton,
            crafting.filterClothingButton
        };
        filters.Sort(CompareFilterButtons);
        for (int i = 0; i < filters.Count; i++)
        {
            MazeButton filter = filters[i];
            if (filter == null)
            {
                continue;
            }

            MoveToLayout(filter.transform, categoriesPanel, 0f, 0f, 1f);
            SetLayout(filter.gameObject, 0f, 0f, 1f, 48f, 48f, 0f);
            if (filter.buttonText != null)
            {
                filter.buttonText.alignment = TextAlignmentOptions.MidlineLeft;
                filter.buttonText.margin = new Vector4(42f, 0f, 8f, 0f);
            }
        }

        TextMeshProUGUI status = CreateLabel("BetterUI_FilterStatus", categoriesPanel, crafting._titleTMP);
        status.text = "// LIVE INDEX\n// LOCAL CACHE";
        status.fontSize = 11f;
        status.fontStyle = FontStyles.Normal;
        status.color = BetterUITheme.TextDim;
        status.characterSpacing = 1.2f;
        status.alignment = TextAlignmentOptions.BottomLeft;
        SetLayout(status.gameObject, 0f, 0f, 1f, 48f, 0f, 1f);
    }

    private static void BuildBrowser(CraftingTableUI crafting, RectTransform browserPanel)
    {
        RectTransform browserHeader = CreateSurface("BetterUI_RecipeHeader", browserPanel, BetterUITheme.PanelElevated);
        AnchorTop(browserHeader, 94f, 8f, 8f);
        TextMeshProUGUI heading = CreateLabel("BetterUI_RecipeHeading", browserHeader, crafting._titleTMP);
        heading.text = "SCHEMATIC INDEX";
        heading.fontSize = 13f;
        heading.fontStyle = FontStyles.Bold;
        heading.color = BetterUITheme.TextMuted;
        heading.characterSpacing = 1.5f;
        heading.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform headingRect = heading.GetComponent<RectTransform>();
        if (headingRect != null)
        {
            headingRect.anchorMin = new Vector2(0f, 0.5f);
            headingRect.anchorMax = Vector2.one;
            headingRect.offsetMin = new Vector2(12f, 0f);
            headingRect.offsetMax = new Vector2(-12f, 0f);
        }

        if (crafting._searchInputField != null)
        {
            RectTransform searchRect = crafting._searchInputField.GetComponent<RectTransform>();
            if (searchRect != null)
            {
                searchRect.SetParent(browserHeader, false);
                NormalizeRect(searchRect);
                searchRect.anchorMin = Vector2.zero;
                searchRect.anchorMax = new Vector2(1f, 0.5f);
                searchRect.offsetMin = new Vector2(10f, 8f);
                searchRect.offsetMax = new Vector2(-10f, -4f);
                LayoutElement searchLayout = searchRect.GetComponent<LayoutElement>();
                if (searchLayout != null)
                {
                    searchLayout.ignoreLayout = true;
                }
            }
        }

        RectTransform scrollRoot = CreateSurface("BetterUI_RecipeScroll", browserPanel, BetterUITheme.CraftingWell);
        scrollRoot.anchorMin = Vector2.zero;
        scrollRoot.anchorMax = Vector2.one;
        scrollRoot.offsetMin = new Vector2(8f, 8f);
        scrollRoot.offsetMax = new Vector2(-8f, -102f);

        RectTransform viewport = CreateSurface("Viewport", scrollRoot, BetterUITheme.CraftingWell);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(8f, 8f);
        viewport.offsetMax = new Vector2(-22f, -8f);
        viewport.GetComponent<Image>().raycastTarget = true;
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        Transform content = CraftingTableUI.craftingRecipesContainer;
        RectTransform contentRect = content != null ? content.GetComponent<RectTransform>() : null;
        if (contentRect != null)
        {
            contentRect.SetParent(viewport, false);
            NormalizeRect(contentRect);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, contentRect.sizeDelta.y);
        }

        Scrollbar scrollbar = CreateScrollbar(scrollRoot);
        ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.content = contentRect;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.decelerationRate = 0.12f;
        scroll.scrollSensitivity = 34f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scroll.verticalScrollbarSpacing = 4f;
    }

    private static void BuildHeader(CraftingTableUI crafting, RectTransform header)
    {
        if (crafting._titleTMP != null)
        {
            crafting._titleTMP.text = "WORKBENCH // FABRICATION";
            crafting._titleTMP.alignment = TextAlignmentOptions.MidlineLeft;
            crafting._titleTMP.margin = Vector4.zero;
            MoveToLayout(crafting._titleTMP.transform, header, 220f, 420f, 1f);
        }

        RectTransform spacer = CreateRect("BetterUI_HeaderSpacer", header);
        SetLayout(spacer.gameObject, 0f, 0f, 1f, 1f, 1f, 0f);

        TextMeshProUGUI station = CreateLabel("BetterUI_StationStatus", header, crafting._titleTMP);
        station.text = "STN-04  //  ONLINE\nLOCAL FABRICATION NETWORK";
        station.fontSize = 11f;
        station.fontStyle = FontStyles.Bold;
        station.color = BetterUITheme.Accent;
        station.characterSpacing = 1.4f;
        station.alignment = TextAlignmentOptions.MidlineRight;
        SetLayout(station.gameObject, 150f, 220f, 0f, 48f, 48f, 0f);
    }

    private static void BuildFooter(CraftingTableUI crafting, RectTransform root)
    {
        RectTransform footer = CreateSurface("BetterUI_CommandFooter", root, BetterUITheme.Panel);
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

        AddFooterPrompt(footer, crafting._titleTMP, "[ LMB ]  SELECT", 150f);
        AddFooterPrompt(footer, crafting._titleTMP, "[ WHEEL ]  SCROLL", 165f);

        RectTransform spacer = CreateRect("BetterUI_FooterSpacer", footer);
        SetLayout(spacer.gameObject, 0f, 0f, 1f, 1f, 1f, 0f);

        if (crafting._showHandCraftingButton != null)
        {
            MoveToLayout(crafting._showHandCraftingButton.transform, footer, 42f, 46f, 0f);
        }

        BuildThemeButton(footer);

        if (crafting._hideUncraftableButton != null)
        {
            MoveToLayout(crafting._hideUncraftableButton.transform, footer, 42f, 46f, 0f);
        }

        MazeButton close = FindButton(crafting.transform, "CLOSE");
        if (close != null)
        {
            if (close.buttonText != null)
            {
                close.buttonText.text = "[ ESC ]  EXIT";
            }

            MoveToLayout(close.transform, footer, 138f, 154f, 0f);
            StyleFlatButton(close, BetterUITheme.PanelSoft, BetterUITheme.Border, BetterUITheme.TextMuted);
        }
    }

    private static void BuildThemeButton(RectTransform footer)
    {
        RectTransform button = CreateSurface("BetterUI_ThemeButton", footer, BetterUITheme.PanelSoft);
        SetLayout(button.gameObject, 42f, 46f, 0f, 38f, 38f, 0f);
        Image buttonImage = button.GetComponent<Image>();
        buttonImage.raycastTarget = true;
        BetterUIStyler.Effects(buttonImage, BetterUITheme.Border, false);

        var iconObject = new GameObject(
            "BetterUI_ColorWheelIcon",
            Il2CppType.Of<RectTransform>(),
            Il2CppType.Of<CanvasRenderer>(),
            Il2CppType.Of<RawImage>(),
            Il2CppType.Of<LayoutElement>());
        iconObject.transform.SetParent(button, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(26f, 26f);

        LayoutElement iconLayout = iconObject.GetComponent<LayoutElement>();
        iconLayout.ignoreLayout = true;
        RawImage icon = iconObject.GetComponent<RawImage>();
        icon.texture = BetterUITheme.ColorWheelTexture;
        icon.color = Color.white;
        icon.raycastTarget = false;
    }

    private static void AddFooterPrompt(Transform footer, TextMeshProUGUI template, string value, float width)
    {
        TextMeshProUGUI prompt = CreateLabel("BetterUI_CommandPrompt", footer, template);
        prompt.text = value;
        prompt.fontSize = 12f;
        prompt.fontStyle = FontStyles.Bold;
        prompt.color = BetterUITheme.TextMuted;
        prompt.characterSpacing = 1f;
        prompt.alignment = TextAlignmentOptions.MidlineLeft;
        SetLayout(prompt.gameObject, width * 0.55f, width, 0f, 32f, 32f, 0f);
    }

    private static void BuildDetails(CraftingTableUI crafting, RectTransform detailsPanel)
    {
        RectTransform detailsHeader = CreateSurface("BetterUI_DetailsHeader", detailsPanel, BetterUITheme.CraftingWell);
        AnchorTop(detailsHeader, 50f, 8f, 8f);
        TextMeshProUGUI heading = CreateLabel("BetterUI_DetailsHeading", detailsHeader, crafting._titleTMP);
        heading.text = "ITEM INSPECTION // COMPONENT ANALYSIS";
        heading.fontSize = 15f;
        heading.fontStyle = FontStyles.Bold;
        heading.color = BetterUITheme.TextMuted;
        heading.characterSpacing = 1.8f;
        heading.alignment = TextAlignmentOptions.MidlineLeft;
        heading.enableWordWrapping = false;
        heading.overflowMode = TextOverflowModes.Ellipsis;
        RectTransform headingRect = heading.GetComponent<RectTransform>();
        if (headingRect != null)
        {
            headingRect.anchorMin = Vector2.zero;
            headingRect.anchorMax = Vector2.one;
            headingRect.offsetMin = new Vector2(12f, 0f);
            headingRect.offsetMax = new Vector2(-12f, 0f);
        }

        SelectedCraftingRecipeUI selected = crafting._selectedRecipeUI;
        if (selected != null)
        {
            RectTransform selectedRect = selected.GetComponent<RectTransform>();
            if (selectedRect != null)
            {
                selectedRect.SetParent(detailsPanel, false);
                NormalizeRect(selectedRect);
                selectedRect.anchorMin = Vector2.zero;
                selectedRect.anchorMax = Vector2.one;
                // Keep the live crafting-progress strip from covering the last
                // requirement or action at the bottom of the scroll view.
                selectedRect.offsetMin = new Vector2(10f, 76f);
                selectedRect.offsetMax = new Vector2(-10f, -58f);
                selectedRect.gameObject.SetActive(true);
                HideLegacyDetailsHeading(selected);
                ConfigureEmptyState(crafting, selected);
                BuildSelectedContent(crafting, selected);
            }
        }

        CraftingProgressUI progress = crafting._craftingProgressUI;
        if (progress != null)
        {
            RectTransform progressRect = progress.GetComponent<RectTransform>();
            if (progressRect != null)
            {
                progressRect.SetParent(detailsPanel, false);
                NormalizeRect(progressRect);
                progressRect.anchorMin = new Vector2(0f, 0f);
                progressRect.anchorMax = new Vector2(1f, 0f);
                progressRect.pivot = new Vector2(0.5f, 0f);
                progressRect.offsetMin = new Vector2(10f, 10f);
                progressRect.offsetMax = new Vector2(-10f, 72f);
                progressRect.SetAsLastSibling();
            }
        }
    }

    private static void ConfigureEmptyState(CraftingTableUI crafting, SelectedCraftingRecipeUI selected)
    {
        if (selected.didntSelectRecipeParent == null)
        {
            return;
        }

        RectTransform emptyRect = selected.didntSelectRecipeParent.GetComponent<RectTransform>();
        if (emptyRect != null)
        {
            emptyRect.anchorMin = Vector2.zero;
            emptyRect.anchorMax = Vector2.one;
            emptyRect.offsetMin = Vector2.zero;
            emptyRect.offsetMax = Vector2.zero;
        }

        var labels = selected.didntSelectRecipeParent.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (labels.Length > 0)
        {
            labels[0].text = "SELECT A SCHEMATIC";
            labels[0].fontSize = 16f;
            labels[0].fontStyle = FontStyles.Bold;
            labels[0].color = BetterUITheme.TextMuted;
            labels[0].alignment = TextAlignmentOptions.Center;
            return;
        }

        TextMeshProUGUI prompt = CreateLabel(
            "BetterUI_SelectRecipePrompt",
            selected.didntSelectRecipeParent.transform,
            crafting._titleTMP);
        prompt.text = "SELECT A SCHEMATIC";
        prompt.fontSize = 16f;
        prompt.fontStyle = FontStyles.Bold;
        prompt.color = BetterUITheme.TextMuted;
        prompt.alignment = TextAlignmentOptions.Center;
        RectTransform promptRect = prompt.GetComponent<RectTransform>();
        if (promptRect != null)
        {
            promptRect.anchorMin = Vector2.zero;
            promptRect.anchorMax = Vector2.one;
            promptRect.offsetMin = Vector2.zero;
            promptRect.offsetMax = Vector2.zero;
        }
    }

    private static void BuildSelectedContent(CraftingTableUI crafting, SelectedCraftingRecipeUI selected)
    {
        if (selected.approvedParent == null)
        {
            return;
        }

        Transform approved = selected.approvedParent.transform;
        RectTransform approvedRect = approved != null ? approved.GetComponent<RectTransform>() : null;
        if (approvedRect != null)
        {
            approvedRect.anchorMin = Vector2.zero;
            approvedRect.anchorMax = Vector2.one;
            approvedRect.offsetMin = Vector2.zero;
            approvedRect.offsetMax = Vector2.zero;
        }

        var legacyChildren = new List<Transform>();
        for (int i = 0; i < approved.childCount; i++)
        {
            legacyChildren.Add(approved.GetChild(i));
        }

        RectTransform scrollRoot = CreateSurface("BetterUI_DetailsScroll", approved, BetterUITheme.PanelElevated);
        scrollRoot.anchorMin = Vector2.zero;
        scrollRoot.anchorMax = Vector2.one;
        scrollRoot.offsetMin = Vector2.zero;
        scrollRoot.offsetMax = Vector2.zero;

        RectTransform viewport = CreateSurface("Viewport", scrollRoot, BetterUITheme.PanelElevated);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(0f, 4f);
        viewport.offsetMax = new Vector2(-18f, -4f);
        Mask viewportMask = viewport.gameObject.AddComponent<Mask>();
        viewportMask.showMaskGraphic = true;

        RectTransform content = CreateSurface("BetterUI_DetailsContent", viewport, BetterUITheme.PanelElevated);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = Padding(14, 14, 14, 14);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform overview = CreateSurface("BetterUI_OutputSummary", content, BetterUITheme.CraftingWell);
        SetLayout(overview.gameObject, 0f, 0f, 1f, 132f, 132f, 0f);
        HorizontalLayoutGroup overviewLayout = overview.gameObject.AddComponent<HorizontalLayoutGroup>();
        overviewLayout.padding = Padding(16, 16, 12, 12);
        overviewLayout.spacing = 18f;
        overviewLayout.childAlignment = TextAnchor.MiddleLeft;
        overviewLayout.childControlWidth = true;
        overviewLayout.childControlHeight = true;
        overviewLayout.childForceExpandWidth = false;
        overviewLayout.childForceExpandHeight = false;
        MoveToLayout(selected._outputItemContainer != null ? selected._outputItemContainer.transform : null, overview, 104f, 104f, 0f);
        MoveToLayout(selected._outputItemNameTMP != null ? selected._outputItemNameTMP.transform : null, overview, 160f, 240f, 1f);

        if (selected._sliderContainer != null)
        {
            MoveToLayout(selected._sliderContainer.transform, content, 0f, 0f, 1f);
            SetLayout(selected._sliderContainer, 0f, 0f, 1f, 34f, 38f, 0f);
        }

        TextMeshProUGUI materialsLabel = CreateLabel("BetterUI_MaterialsHeading", content, crafting._titleTMP);
        materialsLabel.text = "REQUIRED COMPONENTS  //  CURRENT BATCH";
        materialsLabel.fontSize = 15f;
        materialsLabel.fontStyle = FontStyles.Bold;
        materialsLabel.color = BetterUITheme.TextMuted;
        materialsLabel.characterSpacing = 1.2f;
        materialsLabel.alignment = TextAlignmentOptions.MidlineLeft;
        SetLayout(materialsLabel.gameObject, 0f, 0f, 1f, 24f, 26f, 0f);

        if (selected._requiredItemsContainer != null)
        {
            selected._requiredItemsContainer.SetParent(content, false);
            SetLayout(selected._requiredItemsContainer.gameObject, 0f, 0f, 1f, 80f, 80f, 0f);
        }

        RectTransform status = CreateRect("BetterUI_RequirementsStatus", content);
        SetLayout(status.gameObject, 0f, 0f, 1f, 0f, 52f, 0f);
        VerticalLayoutGroup statusLayout = status.gameObject.AddComponent<VerticalLayoutGroup>();
        statusLayout.spacing = 5f;
        statusLayout.childAlignment = TextAnchor.MiddleLeft;
        statusLayout.childControlWidth = true;
        statusLayout.childControlHeight = true;
        statusLayout.childForceExpandWidth = true;
        statusLayout.childForceExpandHeight = false;
        MoveToLayout(selected._needCraftingTableTmp != null ? selected._needCraftingTableTmp.transform : null, status, 0f, 0f, 1f);
        MoveToLayout(selected._needBlueprintTmp != null ? selected._needBlueprintTmp.transform : null, status, 0f, 0f, 1f);
        if (selected._needCraftingTableTmp != null)
        {
            SetLayout(selected._needCraftingTableTmp.gameObject, 0f, 0f, 1f, 20f, 22f, 0f);
        }

        if (selected._needBlueprintTmp != null)
        {
            SetLayout(selected._needBlueprintTmp.gameObject, 0f, 0f, 1f, 20f, 22f, 0f);
        }

        if (selected._craftButton != null)
        {
            MoveToLayout(selected._craftButton.transform, content, 0f, 0f, 1f);
            if (selected._craftButton.buttonText != null)
            {
                selected._craftButton.buttonText.text = "[ E ]  FABRICATE";
            }

            SetLayout(selected._craftButton.gameObject, 0f, 0f, 1f, 56f, 58f, 0f);
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
        scroll.scrollSensitivity = 30f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        int approvedId = approved.GetInstanceID();
        for (int i = 0; i < legacyChildren.Count; i++)
        {
            Transform child = legacyChildren[i];
            if (child != null && child.parent != null && child.parent.GetInstanceID() == approvedId)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private static Scrollbar CreateScrollbar(RectTransform parent)
    {
        RectTransform track = CreateSurface("BetterUI_Scrollbar", parent, BetterUITheme.PanelSoft);
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

    private static void HideLegacyDetailsHeading(SelectedCraftingRecipeUI selected)
    {
        var labels = selected.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            TextMeshProUGUI label = labels[i];
            if (label != null && string.Equals(label.text?.Trim(), "Details", System.StringComparison.OrdinalIgnoreCase))
            {
                label.gameObject.SetActive(false);
            }
        }
    }

    private static MazeButton FindButton(Transform root, string label)
    {
        var buttons = root.GetComponentsInChildren<MazeButton>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            MazeButton button = buttons[i];
            string text = button != null && button.buttonText != null ? button.buttonText.text : string.Empty;
            if (string.Equals(text?.Trim(), label, System.StringComparison.OrdinalIgnoreCase))
            {
                return button;
            }
        }

        return null;
    }

    private static int CompareFilterButtons(MazeButton left, MazeButton right)
    {
        return FilterRank(left).CompareTo(FilterRank(right));
    }

    private static int FilterRank(MazeButton button)
    {
        string text = button != null && button.buttonText != null
            ? button.buttonText.text.ToUpperInvariant()
            : string.Empty;
        if (text.Contains("WEAPON") || text.Contains("COMBAT")) return 0;
        if (text.Contains("MED")) return 1;
        if (text.Contains("TOOL")) return 2;
        if (text.Contains("CLOTH")) return 3;
        if (text.Contains("MISC")) return 4;
        return 5;
    }

    private static void MoveToLayout(Transform item, Transform parent, float minimumWidth, float preferredWidth, float flexibleWidth)
    {
        if (item == null)
        {
            return;
        }

        item.SetParent(parent, false);
        RectTransform itemRect = item.GetComponent<RectTransform>();
        if (itemRect != null)
        {
            NormalizeRect(itemRect);
        }

        LayoutElement layout = item.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = item.gameObject.AddComponent<LayoutElement>();
        }

        layout.minWidth = minimumWidth;
        layout.preferredWidth = preferredWidth;
        layout.flexibleWidth = flexibleWidth;
        layout.minHeight = 42f;
        layout.preferredHeight = 46f;
        layout.flexibleHeight = 0f;
    }

    private static void SetLayout(
        GameObject target,
        float minimumWidth,
        float preferredWidth,
        float flexibleWidth,
        float minimumHeight,
        float preferredHeight,
        float flexibleHeight)
    {
        LayoutElement layout = target.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = target.AddComponent<LayoutElement>();
        }

        layout.minWidth = minimumWidth;
        layout.preferredWidth = preferredWidth;
        layout.flexibleWidth = flexibleWidth;
        layout.minHeight = minimumHeight;
        layout.preferredHeight = preferredHeight;
        layout.flexibleHeight = flexibleHeight;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var gameObject = new GameObject(name, Il2CppType.Of<RectTransform>());
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private static RectTransform CreateSurface(string name, Transform parent, Color color)
    {
        var gameObject = new GameObject(
            name,
            Il2CppType.Of<RectTransform>(),
            Il2CppType.Of<CanvasRenderer>(),
            Il2CppType.Of<Image>());
        gameObject.transform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>();
        image.material = null;
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        BetterUIStyler.Effects(image, BetterUITheme.BorderSoft, false);
        Outline outline = image.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectDistance = new Vector2(1f, -1f);
        }

        return gameObject.GetComponent<RectTransform>();
    }

    private static void StyleFlatButton(MazeButton button, Color fill, Color border, Color text)
    {
        if (button == null)
        {
            return;
        }

        button.backgroundColor = fill;
        button.hoverColor = new Color(border.r, border.g, border.b, 0.34f);
        button.textColor = text;
        button.hoverTextColor = BetterUITheme.Text;
        Image image = button.backgroundImage != null ? button.backgroundImage.TryCast<Image>() : null;
        if (image != null)
        {
            image.enabled = true;
            image.material = null;
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = fill;
            BetterUIStyler.Effects(image, border, false);
            Outline outline = image.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectDistance = new Vector2(1f, -1f);
            }
        }

        if (button.buttonText != null)
        {
            BetterUIStyler.Text(button.buttonText, 13f, text, FontStyles.Bold);
            button.buttonText.characterSpacing = 1f;
        }

        button.UpdateVisualState(true);
    }

    private static TextMeshProUGUI CreateLabel(string name, Transform parent, TextMeshProUGUI template)
    {
        if (template == null)
        {
            throw new InvalidOperationException($"Cannot create {name}: no initialized TextMeshPro template is available.");
        }

        GameObject gameObject = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
        gameObject.name = name;
        gameObject.SetActive(true);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        if (rect != null)
        {
            NormalizeRect(rect);
        }

        TextMeshProUGUI label = gameObject.GetComponent<TextMeshProUGUI>();
        if (label == null)
        {
            throw new InvalidOperationException($"Cannot create {name}: cloned object has no TextMeshProUGUI component.");
        }

        label.text = string.Empty;
        label.raycastTarget = false;
        return label;
    }

    private static void NormalizeRect(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void BuildSection(string sectionName, Action build)
    {
        try
        {
            build();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Crafting replacement failed while building the {sectionName}.",
                exception);
        }
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
        return new RectOffset
        {
            left = left,
            right = right,
            top = top,
            bottom = bottom
        };
    }
}
