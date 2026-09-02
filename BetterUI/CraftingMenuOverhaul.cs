using System.Collections.Generic;
using Game.Data;
using Game.UI;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace InfernoProtocol.BetterUI;

/// <summary>
/// Owns the crafting presentation as a self-contained feature. The game remains
/// responsible for recipe filtering, selection, quantities, crafting, and saves.
/// This class only reshapes and styles the existing controls, so new recipes and
/// ingredients automatically flow through the same layout.
/// </summary>
internal sealed class CraftingMenuOverhaul
{
    private readonly CraftingMenuBuilder _menuBuilder = new();
    private readonly HashSet<int> _configuredRecipes = new();
    private readonly HashSet<int> _configuredIngredients = new();
    private readonly HashSet<int> _configuredButtons = new();
    private readonly List<CraftingRecipeUI> _recipes = new();
    private readonly Dictionary<int, byte> _recipeVisualStates = new();
    private readonly MazeButton[] _filterButtons = new MazeButton[6];
    private int _configuredRootId;
    private int _recipeHierarchyStamp = int.MinValue;
    private int _workspaceRecipeId = int.MinValue;
    private int _workspaceQuantity = int.MinValue;
    private int _recipeCountState = int.MinValue;
    private int _layoutRebuildPasses;
    private Transform _recipeContainer;
    private GridLayoutGroup _recipeGrid;
    private TextMeshProUGUI _recipeHeading;
    private GameObject _replacementRoot;
    private RectTransform _themeButton;
    private int _paletteIndex = -1;

    internal void SetVisible(bool visible)
    {
        if (_replacementRoot != null && _replacementRoot.activeSelf != visible)
        {
            _replacementRoot.SetActive(visible);
        }
    }

    internal bool HandleInput()
    {
        if (_themeButton == null
            || _replacementRoot == null
            || !_replacementRoot.activeInHierarchy
            || Mouse.current == null)
        {
            return false;
        }

        Canvas canvas = _themeButton.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        bool hovered = RectTransformUtility.RectangleContainsScreenPoint(
            _themeButton,
            Mouse.current.position.ReadValue(),
            camera);
        Image background = _themeButton.GetComponent<Image>();
        MakeFlat(
            background,
            hovered ? BetterUITheme.SlotHover : BetterUITheme.PanelSoft,
            hovered ? BetterUITheme.Accent : BetterUITheme.Border);

        if (!hovered || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return false;
        }

        BetterUIPlugin.CyclePalette();
        return true;
    }

    private void RefreshPalette(CraftingTableUI crafting)
    {
        if (_replacementRoot == null || crafting == null)
        {
            return;
        }

        var images = _replacementRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            Color fill;
            bool restyle = true;
            switch (image.name)
            {
                case "BetterUI_CraftingMenu":
                    fill = BetterUITheme.CraftingCanvas;
                    break;
                case "BetterUI_Header":
                case "BetterUI_RecipeBrowser":
                case "BetterUI_CommandFooter":
                    fill = BetterUITheme.Panel;
                    break;
                case "BetterUI_Categories":
                case "BetterUI_Details":
                case "BetterUI_RecipeHeader":
                case "BetterUI_DetailsScroll":
                case "BetterUI_DetailsContent":
                    fill = BetterUITheme.PanelElevated;
                    break;
                case "BetterUI_RecipeScroll":
                case "BetterUI_DetailsHeader":
                case "BetterUI_OutputSummary":
                    fill = BetterUITheme.CraftingWell;
                    break;
                case "BetterUI_ThemeButton":
                case "BetterUI_Scrollbar":
                    fill = BetterUITheme.PanelSoft;
                    break;
                case "Handle":
                    restyle = image.transform.parent != null
                        && image.transform.parent.name == "BetterUI_Scrollbar";
                    fill = BetterUITheme.Accent;
                    break;
                case "Viewport":
                    fill = image.transform.parent != null
                        && image.transform.parent.name == "BetterUI_RecipeScroll"
                            ? BetterUITheme.CraftingWell
                            : BetterUITheme.PanelElevated;
                    break;
                default:
                    restyle = false;
                    fill = Color.clear;
                    break;
            }

            if (restyle)
            {
                MakeFlat(
                    image,
                    fill,
                    image.name == "BetterUI_CraftingMenu" ? BetterUITheme.Accent : BetterUITheme.BorderSoft);
            }
        }

        var labels = _replacementRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (label == null)
            {
                continue;
            }

            switch (label.name)
            {
                case "BetterUI_StationStatus":
                    label.color = BetterUITheme.Accent;
                    break;
                case "BetterUI_FilterStatus":
                    label.color = BetterUITheme.TextDim;
                    break;
                case "BetterUI_CategoriesHeading":
                case "BetterUI_RecipeHeading":
                case "BetterUI_DetailsHeading":
                case "BetterUI_CommandPrompt":
                case "BetterUI_MaterialsHeading":
                    label.color = BetterUITheme.TextMuted;
                    break;
            }
        }

        _recipeVisualStates.Clear();
        _workspaceRecipeId = int.MinValue;
        _workspaceQuantity = int.MinValue;
        _layoutRebuildPasses = Mathf.Max(_layoutRebuildPasses, 2);
        StyleWindow(crafting);
        ConfigureToolbar(crafting);
        StyleRecipeWorkspace(crafting._selectedRecipeUI);
        StyleProgress(crafting._craftingProgressUI);
        BetterUIStyler.Scrollbars(_replacementRoot.transform);
    }

    internal void Apply(CraftingTableUI crafting)
    {
        if (crafting == null)
        {
            return;
        }

        int rootId = crafting.GetInstanceID();
        if (_configuredRootId != rootId)
        {
            ResetFor(crafting);
        }

        if (_paletteIndex != BetterUIPlugin.PaletteIndex)
        {
            _paletteIndex = BetterUIPlugin.PaletteIndex;
            RefreshPalette(crafting);
        }

        RefreshToolbarStates(crafting);
        RefreshRecipeBrowser();
        RefreshWorkspace(crafting._selectedRecipeUI);
        RebuildOpeningLayouts();
    }

    private void ResetFor(CraftingTableUI crafting)
    {
        _configuredRootId = crafting.GetInstanceID();
        _menuBuilder.Build(crafting);
        _replacementRoot = _menuBuilder.ReplacementRoot;
        Transform themeButton = _replacementRoot != null
            ? _replacementRoot.transform.Find("BetterUI_CommandFooter/BetterUI_ThemeButton")
            : null;
        _themeButton = themeButton != null ? themeButton.GetComponent<RectTransform>() : null;
        _paletteIndex = BetterUIPlugin.PaletteIndex;
        _configuredRecipes.Clear();
        _configuredIngredients.Clear();
        _configuredButtons.Clear();
        _recipes.Clear();
        _recipeVisualStates.Clear();
        _recipeHierarchyStamp = int.MinValue;
        _workspaceRecipeId = int.MinValue;
        _workspaceQuantity = int.MinValue;
        _recipeCountState = int.MinValue;
        _layoutRebuildPasses = 4;
        _recipeGrid = null;
        Transform recipeHeading = crafting.transform.Find(
            "BetterUI_CraftingMenu/BetterUI_Body/BetterUI_RecipeBrowser/BetterUI_RecipeHeader/BetterUI_RecipeHeading");
        _recipeHeading = recipeHeading != null ? recipeHeading.GetComponent<TextMeshProUGUI>() : null;

        _filterButtons[0] = crafting.filterMiscButton;
        _filterButtons[1] = crafting.filterCombatButton;
        _filterButtons[2] = crafting.filterFoodButton;
        _filterButtons[3] = crafting.filterHealthyButton;
        _filterButtons[4] = crafting.filterClothingButton;
        _filterButtons[5] = crafting._hideUncraftableButton;

        StyleWindow(crafting);
        ConfigureToolbar(crafting);
        ConfigureRecipeBrowser();
        StyleRecipeWorkspace(crafting._selectedRecipeUI);
        StyleProgress(crafting._craftingProgressUI);
        BetterUIStyler.Scrollbars(crafting.transform);
    }

    private static void StyleWindow(CraftingTableUI crafting)
    {
        TextMeshProUGUI title = crafting._titleTMP;
        if (title == null)
        {
            return;
        }

        BetterUIStyler.Heading(title, 32f);
        title.characterSpacing = 3.2f;

        RectTransform panel = FindReplacementPanel(title.transform);
        Image panelImage = BetterUIStyler.Surface(panel, BetterUITheme.CraftingCanvas, BetterUITheme.Accent);
        MakeFlat(panelImage, BetterUITheme.CraftingCanvas, BetterUITheme.Accent);
        AddTerminalOverlay(panel);
    }

    private void ConfigureToolbar(CraftingTableUI crafting)
    {
        TMP_InputField search = crafting._searchInputField;
        BetterUIStyler.InputField(search);
        if (search != null)
        {
            MakeFlat(search.GetComponent<Image>(), BetterUITheme.CraftingWell, BetterUITheme.Border);
            LayoutElement searchLayout = GetOrAddLayout(search.gameObject);
            searchLayout.minHeight = 46f;
            searchLayout.preferredHeight = 46f;
            searchLayout.flexibleWidth = 1f;

            TMP_Text placeholder = search.placeholder != null ? search.placeholder.TryCast<TMP_Text>() : null;
            if (placeholder != null)
            {
                placeholder.text = "SEARCH SCHEMATICS...";
            }
        }

        for (int i = 0; i < _filterButtons.Length; i++)
        {
            MazeButton button = _filterButtons[i];
            if (button == null)
            {
                continue;
            }

            ConfigureToolbarButton(button);
        }

        Transform filterRow = FindCommonParent(_filterButtons);
        HorizontalOrVerticalLayoutGroup layout = filterRow != null
            ? filterRow.GetComponent<HorizontalOrVerticalLayoutGroup>()
            : null;
        if (layout != null)
        {
            layout.spacing = 8f;
            layout.padding = CreatePadding(4, 4, 4, 4);
            layout.childAlignment = TextAnchor.MiddleLeft;
        }

        // This launcher belongs to the inventory UI, not the crafting panel.
        // Reparenting or styling it prevents the game from restoring the button
        // after hand crafting closes.
    }

    private void RefreshToolbarStates(CraftingTableUI crafting)
    {
        for (int i = 0; i < _filterButtons.Length; i++)
        {
            MazeButton button = _filterButtons[i];
            if (button == null)
            {
                continue;
            }

            bool active = i < 5
                ? IsFilterActive(crafting._activeFilterMask, i)
                : crafting.maskUncraftableType != CraftingTableRecipeMask.None;
            StyleTerminalToggle(button, active);
        }
    }

    private void ConfigureToolbarButton(MazeButton button)
    {
        int id = button.GetInstanceID();
        if (!_configuredButtons.Add(id))
        {
            return;
        }

        LayoutElement layout = GetOrAddLayout(button.gameObject);
        string parentName = button.transform.parent != null ? button.transform.parent.name : string.Empty;
        bool isCategory = parentName == "BetterUI_Categories";
        bool isFooter = parentName == "BetterUI_CommandFooter";
        if (isFooter)
        {
            layout.minHeight = 38f;
            layout.preferredHeight = 38f;
            layout.minWidth = 42f;
            layout.preferredWidth = 46f;
            layout.flexibleWidth = 0f;
            return;
        }

        if (isCategory)
        {
            ConfigureCategoryIcon(button);
        }

        layout.minHeight = isCategory ? 48f : 40f;
        layout.preferredHeight = isCategory ? 48f : 40f;
        layout.minWidth = isCategory ? 0f : 84f;
        layout.preferredWidth = isCategory ? 0f : layout.preferredWidth;
        layout.flexibleWidth = 1f;
    }

    private static void ConfigureCategoryIcon(MazeButton button)
    {
        if (button == null)
        {
            return;
        }

        Transform iconTransform = button.transform.Find("BetterUI_CategoryIcon");
        if (iconTransform == null)
        {
            var iconObject = new GameObject(
                "BetterUI_CategoryIcon",
                Il2CppType.Of<RectTransform>(),
                Il2CppType.Of<CanvasRenderer>(),
                Il2CppType.Of<RawImage>(),
                Il2CppType.Of<LayoutElement>());
            iconObject.transform.SetParent(button.transform, false);
            iconTransform = iconObject.transform;
            LayoutElement element = iconObject.GetComponent<LayoutElement>();
            element.ignoreLayout = true;
        }

        RectTransform rect = iconTransform.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(20f, 0f);
        rect.sizeDelta = new Vector2(18f, 18f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.SetAsLastSibling();

        RawImage icon = iconTransform.GetComponent<RawImage>();
        string label = button.buttonText != null
            ? RemoveCategoryNumberPrefix(button.buttonText.text)
            : string.Empty;
        icon.texture = BetterUITheme.GetCategoryIconTexture(label);
        icon.color = BetterUITheme.TextMuted;
        icon.raycastTarget = false;

        if (button.buttonText != null)
        {
            button.buttonText.text = label;
            button.buttonText.alignment = TextAlignmentOptions.MidlineLeft;
            button.buttonText.margin = new Vector4(42f, 0f, 8f, 0f);
        }

        NormalizeCategoryButtonGeometry(button);
    }

    private void ConfigureRecipeBrowser()
    {
        _recipeContainer = CraftingTableUI.craftingRecipesContainer;
        if (_recipeContainer == null)
        {
            return;
        }

        ConfigureRecipeContent(_recipeContainer);

        ScrollRect scroll = _recipeContainer.GetComponentInParent<ScrollRect>();
        RectTransform listPanel = scroll != null
            ? scroll.GetComponent<RectTransform>()
            : _recipeContainer.parent != null ? _recipeContainer.parent.GetComponent<RectTransform>() : null;
        BetterUIStyler.Surface(listPanel, BetterUITheme.CraftingWell, BetterUITheme.BorderSoft, false);
        if (listPanel != null)
        {
            LayoutElement listLayout = GetOrAddLayout(listPanel.gameObject);
            listLayout.minWidth = 520f;
            listLayout.flexibleWidth = 1.65f;
        }

        if (scroll != null)
        {
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.elasticity = 0.08f;
            scroll.scrollSensitivity = 32f;
        }
    }

    private void RefreshRecipeBrowser()
    {
        if (_recipeContainer == null)
        {
            return;
        }

        RefreshRecipeGridLayout();

        int hierarchyStamp = GetHierarchyStamp(_recipeContainer);
        if (_recipeHierarchyStamp != hierarchyStamp)
        {
            RebuildRecipeCache(hierarchyStamp);
        }

        CraftingRecipeUI selectedRecipe = CraftingTableUI.selectedRecipeOnListUI;
        int visibleRecipes = 0;
        int craftableRecipes = 0;
        for (int i = 0; i < _recipes.Count; i++)
        {
            CraftingRecipeUI recipe = _recipes[i];
            if (recipe == null)
            {
                _recipeHierarchyStamp = int.MinValue;
                continue;
            }

            if (recipe._requiredItemsContainer != null && recipe._requiredItemsContainer.gameObject.activeSelf)
            {
                recipe._requiredItemsContainer.gameObject.SetActive(false);
            }

            NeutralizeNativeRecipeVisuals(recipe);

            if (recipe.gameObject.activeSelf)
            {
                visibleRecipes++;
                if (recipe.playerCanCraft)
                {
                    craftableRecipes++;
                }
            }

            bool selected = SameObject(recipe, selectedRecipe);
            byte state = selected ? (byte)2 : recipe.playerCanCraft ? (byte)1 : (byte)0;
            int id = recipe.GetInstanceID();
            if (!_recipeVisualStates.TryGetValue(id, out byte previousState) || previousState != state)
            {
                _recipeVisualStates[id] = state;
                StyleRecipeCard(recipe, selected);
            }

            RefreshRecipeRowContent(recipe, selected);
        }

        int countState = unchecked((visibleRecipes * 397) ^ craftableRecipes);
        string heading = $"SCHEMATIC INDEX  //  {visibleRecipes:000} SHOWN  //  {craftableRecipes:000} READY";
        if (_recipeHeading != null
            && (_recipeCountState != countState || _recipeHeading.text != heading))
        {
            _recipeCountState = countState;
            _recipeHeading.text = heading;
        }
    }

    private void RebuildRecipeCache(int hierarchyStamp)
    {
        _recipes.Clear();
        _recipeVisualStates.Clear();
        var recipes = _recipeContainer.GetComponentsInChildren<CraftingRecipeUI>(true);
        for (int i = 0; i < recipes.Length; i++)
        {
            if (recipes[i] != null)
            {
                _recipes.Add(recipes[i]);
            }
        }

        _recipeHierarchyStamp = hierarchyStamp;
        _layoutRebuildPasses = Mathf.Max(_layoutRebuildPasses, 2);
    }

    private static void RefreshRecipeRowContent(CraftingRecipeUI recipe, bool selected)
    {
        if (recipe == null)
        {
            return;
        }

        Transform row = recipe.transform.Find("BetterUI_RowContent");
        if (row != null && !row.gameObject.activeSelf)
        {
            row.gameObject.SetActive(true);
        }

        if (recipe._outputItemName != null)
        {
            if (!recipe._outputItemName.gameObject.activeSelf)
            {
                recipe._outputItemName.gameObject.SetActive(true);
            }

            recipe._outputItemName.enabled = true;
            BetterUIStyler.Text(
                recipe._outputItemName,
                16f,
                selected
                    ? BetterUITheme.Text
                    : recipe.playerCanCraft ? BetterUITheme.Text : BetterUITheme.TextUnavailable,
                FontStyles.Bold);
            recipe._outputItemName.SetAllDirty();
        }

        TMP_Text status = row != null
            ? row.Find("BetterUI_RowStatus")?.GetComponent<TMP_Text>()
            : null;
        if (status != null)
        {
            status.text = selected ? "SELECTED" : recipe.playerCanCraft ? "READY" : "MISSING";
            BetterUIStyler.Text(
                status,
                11f,
                selected
                    ? BetterUITheme.Text
                    : recipe.playerCanCraft ? BetterUITheme.Success : BetterUITheme.TextUnavailable,
                FontStyles.Bold);
            status.alignment = TextAlignmentOptions.MidlineRight;
        }

        StyleListIcon(recipe._outputItemContiner, 48f);
    }

    private void RefreshWorkspace(SelectedCraftingRecipeUI selected)
    {
        if (selected == null)
        {
            return;
        }

        ItemContainerUI selectedOutput = selected._outputItemContainer != null
            ? selected._outputItemContainer.TryCast<ItemContainerUI>()
            : null;
        RefreshCountBadge(selectedOutput);
        FitItemArtwork(selected._outputItemContainer, 104f);

        CraftingRecipeUI selectedRecipe = CraftingTableUI.selectedRecipeOnListUI;
        bool hasVisibleSelection = selectedRecipe != null && selectedRecipe.gameObject.activeInHierarchy;
        if (selected.didntSelectRecipeParent != null
            && selected.didntSelectRecipeParent.activeSelf == hasVisibleSelection)
        {
            selected.didntSelectRecipeParent.SetActive(!hasVisibleSelection);
        }

        if (selected.approvedParent != null && selected.approvedParent.activeSelf != hasVisibleSelection)
        {
            selected.approvedParent.SetActive(hasVisibleSelection);
        }

        int recipeId = selectedRecipe != null ? selectedRecipe.GetInstanceID() : 0;
        int quantity = selected._craftCountSlider != null
            ? Mathf.RoundToInt(selected._craftCountSlider.value)
            : 0;
        StyleIngredientList(selected._requiredItemsContainer, selected);
        if (_workspaceRecipeId == recipeId && _workspaceQuantity == quantity)
        {
            return;
        }

        _workspaceRecipeId = recipeId;
        _workspaceQuantity = quantity;
        _layoutRebuildPasses = Mathf.Max(_layoutRebuildPasses, 2);
    }

    private static void ConfigureRecipeContent(Transform recipeContainer)
    {
        VerticalLayoutGroup vertical = recipeContainer.GetComponent<VerticalLayoutGroup>();
        GridLayoutGroup grid = recipeContainer.GetComponent<GridLayoutGroup>();
        if (vertical == null && grid == null)
        {
            vertical = recipeContainer.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        if (vertical != null)
        {
            vertical.padding = CreatePadding(4, 4, 4, 4);
            vertical.spacing = 2f;
            vertical.childAlignment = TextAnchor.UpperCenter;
            vertical.childControlWidth = true;
            vertical.childControlHeight = false;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;
        }

        if (grid != null)
        {
            grid.padding = CreatePadding(4, 4, 4, 4);
            grid.spacing = new Vector2(0f, 2f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 1;
            grid.cellSize = new Vector2(360f, 58f);
        }

        ContentSizeFitter fitter = recipeContainer.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = recipeContainer.gameObject.AddComponent<ContentSizeFitter>();
        }

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void RefreshRecipeGridLayout()
    {
        if (_recipeGrid == null)
        {
            _recipeGrid = _recipeContainer.GetComponent<GridLayoutGroup>();
        }

        if (_recipeGrid == null)
        {
            return;
        }

        RectTransform contentRect = _recipeContainer.GetComponent<RectTransform>();
        float width = contentRect != null ? contentRect.rect.width : 0f;
        if (width <= 1f)
        {
            return;
        }

        const int columns = 1;
        float horizontalPadding = _recipeGrid.padding.left + _recipeGrid.padding.right;
        float available = width - horizontalPadding - (_recipeGrid.spacing.x * (columns - 1));
        float cellWidth = Mathf.Max(260f, available / columns);

        if (_recipeGrid.constraintCount != columns)
        {
            _recipeGrid.constraintCount = columns;
        }

        Vector2 targetSize = new(cellWidth, 58f);
        if (Vector2.SqrMagnitude(_recipeGrid.cellSize - targetSize) > 0.25f)
        {
            _recipeGrid.cellSize = targetSize;
        }
    }

    private void StyleRecipeCard(CraftingRecipeUI recipe, bool selected)
    {
        if (recipe == null)
        {
            return;
        }

        bool craftable = recipe.playerCanCraft;
        Color fill = selected
            ? BetterUITheme.CraftingSelected
            : craftable ? new Color(0.02f, 0.09f, 0.04f, 0.82f) : new Color(0.01f, 0.025f, 0.015f, 0.72f);
        Color border = selected ? BetterUITheme.Accent : BetterUITheme.BorderSoft;

        Image background = GetOrCreateCardSurface(recipe.transform);
        NeutralizeNativeRecipeVisuals(recipe);
        bool configure = _configuredRecipes.Add(recipe.GetInstanceID());
        if (configure)
        {
            ConfigureRecipeRow(recipe);
        }

        if (background != null)
        {
            MakeFlat(background, fill, border);
        }

        BetterUIStyler.Text(
            recipe._outputItemName,
            16f,
            selected ? BetterUITheme.Text : craftable ? BetterUITheme.Text : BetterUITheme.TextUnavailable,
            FontStyles.Bold);

        if (recipe._outputItemName != null)
        {
            recipe._outputItemName.enableWordWrapping = false;
            recipe._outputItemName.overflowMode = TextOverflowModes.Ellipsis;
        }

        StyleListIcon(recipe._outputItemContiner, 48f);

        LayoutElement layout = GetOrAddLayout(recipe.gameObject);
        layout.minHeight = 58f;
        layout.preferredHeight = 58f;
        layout.flexibleHeight = 0f;

        if (configure)
        {
            DisableListCardTooltips(recipe);
            AddAvailabilityRail(
                recipe.transform,
                selected ? BetterUITheme.CraftingWell : craftable ? BetterUITheme.Success : BetterUITheme.TextDim);
        }
        else
        {
            Image rail = FindDecoration(recipe.transform, "BetterUI_AvailabilityRail");
            BetterUIStyler.Tint(
                rail,
                selected ? BetterUITheme.CraftingWell : craftable ? BetterUITheme.Success : BetterUITheme.TextDim);
        }
    }

    private static void NeutralizeNativeRecipeVisuals(CraftingRecipeUI recipe)
    {
        CraftingRecipeUI.canCraftColor = Color.clear;
        CraftingRecipeUI.cantCraftColor = Color.clear;
        CraftingRecipeUI.selectedColor = Color.clear;

        Image rootImage = recipe.GetComponent<Image>();
        if (rootImage != null)
        {
            rootImage.material = null;
            rootImage.color = new Color(0f, 0f, 0f, 0.001f);
        }

        var rootOutlines = recipe.GetComponents<Outline>();
        for (int i = 0; i < rootOutlines.Length; i++)
        {
            if (rootOutlines[i] != null)
            {
                rootOutlines[i].enabled = false;
            }
        }

        MazeButton button = recipe._selectButton;
        if (button == null)
        {
            return;
        }

        button.backgroundColor = Color.clear;
        button.hoverColor = Color.clear;
        button.textColor = Color.clear;
        button.hoverTextColor = Color.clear;
        button._disabledBackgroundColor = Color.clear;

        Graphic nativeBackground = button.backgroundImage;
        int nativeBackgroundId = 0;
        if (nativeBackground != null)
        {
            nativeBackgroundId = nativeBackground.GetInstanceID();
            nativeBackground.enabled = true;
            nativeBackground.material = null;
            nativeBackground.color = new Color(0f, 0f, 0f, 0.001f);
            nativeBackground.raycastTarget = true;
            Image nativeImage = nativeBackground.TryCast<Image>();
            if (nativeImage != null)
            {
                nativeImage.sprite = null;
                nativeImage.type = Image.Type.Simple;
            }

            Outline nativeOutline = nativeBackground.GetComponent<Outline>();
            if (nativeOutline != null)
            {
                nativeOutline.enabled = false;
            }
        }

        int nameId = recipe._outputItemName != null ? recipe._outputItemName.GetInstanceID() : 0;
        Image itemImage = recipe._outputItemContiner != null ? recipe._outputItemContiner.itemImage : null;
        int itemImageId = itemImage != null ? itemImage.GetInstanceID() : 0;
        ItemContainerUI itemContainer = recipe._outputItemContiner != null
            ? recipe._outputItemContiner.TryCast<ItemContainerUI>()
            : null;
        int countTextId = itemContainer != null && itemContainer.itemCountTmp != null
            ? itemContainer.itemCountTmp.GetInstanceID()
            : 0;
        int countBackgroundId = itemContainer != null && itemContainer.countBackgroundImage != null
            ? itemContainer.countBackgroundImage.GetInstanceID()
            : 0;
        Image slotBackground = recipe._outputItemContiner != null
            ? recipe._outputItemContiner.GetComponent<Image>()
            : null;
        int slotBackgroundId = slotBackground != null ? slotBackground.GetInstanceID() : 0;

        var graphics = recipe.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null || IsBetterUIDecoration(graphic.transform))
            {
                continue;
            }

            int graphicId = graphic.GetInstanceID();
            bool preserve = graphicId == nameId
                || graphicId == itemImageId
                || graphicId == countTextId
                || graphicId == countBackgroundId
                || graphicId == slotBackgroundId
                || graphicId == nativeBackgroundId;
            if (!preserve)
            {
                graphic.enabled = false;
                Outline outline = graphic.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.enabled = false;
                }
            }
        }
    }

    private static bool IsBetterUIDecoration(Transform transform)
    {
        Transform current = transform;
        for (int depth = 0; current != null && depth < 3; depth++)
        {
            if (current.name.StartsWith("BetterUI_"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static void DisableListCardTooltips(CraftingRecipeUI recipe)
    {
        if (recipe._outputItemContiner == null)
        {
            return;
        }

        var triggers = recipe._outputItemContiner.GetComponentsInChildren<ItemPeekTrigger>(true);
        for (int i = 0; i < triggers.Length; i++)
        {
            if (triggers[i] != null)
            {
                triggers[i].enabled = false;
            }
        }
    }

    private void StyleRecipeWorkspace(SelectedCraftingRecipeUI selected)
    {
        if (selected == null)
        {
            return;
        }

        Image workspace = BetterUIStyler.Surface(
            selected.GetComponent<RectTransform>(),
            BetterUITheme.PanelElevated,
            BetterUITheme.Border);
        MakeFlat(workspace, BetterUITheme.PanelElevated, BetterUITheme.Border);
        LayoutElement workspaceLayout = GetOrAddLayout(selected.gameObject);
        workspaceLayout.minWidth = 320f;
        workspaceLayout.preferredWidth = 340f;
        workspaceLayout.flexibleWidth = 1f;
        BetterUIStyler.Text(selected._outputItemNameTMP, 26f, BetterUITheme.Text, FontStyles.Bold);
        if (selected._outputItemNameTMP != null)
        {
            selected._outputItemNameTMP.enableWordWrapping = false;
            selected._outputItemNameTMP.overflowMode = TextOverflowModes.Ellipsis;
            LayoutElement nameLayout = GetOrAddLayout(selected._outputItemNameTMP.gameObject);
            nameLayout.minHeight = 30f;
            nameLayout.preferredHeight = 32f;
        }

        StyleTerminalSlot(selected._outputItemContainer, 104f, BetterUITheme.Primary);
        StyleIngredientList(selected._requiredItemsContainer, selected);
        StyleQuantity(selected);
        StyleRequirementMessage(selected._needCraftingTableTmp, BetterUITheme.Danger, selected.transform);
        StyleRequirementMessage(selected._needBlueprintTmp, BetterUITheme.Primary, selected.transform);

        if (selected._craftButton != null)
        {
            StyleTerminalAction(selected._craftButton);
            LayoutElement craftLayout = GetOrAddLayout(selected._craftButton.gameObject);
            craftLayout.minHeight = 56f;
            craftLayout.preferredHeight = 58f;
            craftLayout.minWidth = 180f;
        }

        StyleEmptyState(selected.didntSelectRecipeParent);
        StyleApprovedState(selected.approvedParent);
    }

    private void StyleIngredientList(Transform container, SelectedCraftingRecipeUI selected)
    {
        if (container == null)
        {
            return;
        }

        VerticalLayoutGroup vertical = container.GetComponent<VerticalLayoutGroup>();
        GridLayoutGroup grid = container.GetComponent<GridLayoutGroup>();
        if (vertical == null && grid == null)
        {
            vertical = container.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        if (vertical != null)
        {
            vertical.padding = CreatePadding(4, 4, 4, 4);
            vertical.spacing = 8f;
            vertical.childAlignment = TextAnchor.UpperCenter;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;
        }

        var ingredients = selected.GetComponentsInChildren<CraftingRecipeIngredient>(true);
        int visibleIngredients = 0;
        for (int i = 0; i < ingredients.Length; i++)
        {
            CraftingRecipeIngredient ingredient = ingredients[i];
            if (ingredient == null)
            {
                continue;
            }

            if (ingredient.gameObject.activeSelf)
            {
                visibleIngredients++;
            }

            Color previous = ingredient._backgroundImage != null
                ? ingredient._backgroundImage.color
                : BetterUITheme.PanelSoft;
            bool unavailable = LooksLikeUnavailableState(previous);
            Color fill = unavailable ? BetterUITheme.DangerSoft : BetterUITheme.PanelSoft;
            Color border = unavailable ? BetterUITheme.Danger : BetterUITheme.BorderSoft;

            MakeFlat(ingredient._backgroundImage, fill, border);
            BetterUIStyler.Text(
                ingredient._requiredItemNameTmp,
                18f,
                unavailable ? BetterUITheme.Text : BetterUITheme.TextMuted,
                FontStyles.Bold);
            StyleTerminalSlot(
                ingredient._requiredItemContainer,
                68f,
                border,
                null,
                true,
                true);

            if (_configuredIngredients.Add(ingredient.GetInstanceID()))
            {
                ConfigureIngredientRow(ingredient);
                LayoutElement layout = GetOrAddLayout(ingredient.gameObject);
                layout.minHeight = 84f;
                layout.preferredHeight = 84f;
                layout.flexibleHeight = 0f;
            }

            RefreshIngredientStatus(ingredient, unavailable);
        }

        float containerHeight = Mathf.Max(88f, (visibleIngredients * 84f) + (Mathf.Max(0, visibleIngredients - 1) * 8f) + 8f);
        LayoutElement containerLayout = GetOrAddLayout(container.gameObject);
        containerLayout.minHeight = containerHeight;
        containerLayout.preferredHeight = containerHeight;
        containerLayout.flexibleHeight = 0f;

        if (grid != null)
        {
            RectTransform containerRect = container.GetComponent<RectTransform>();
            float width = containerRect != null ? Mathf.Max(240f, containerRect.rect.width - 8f) : 420f;
            grid.padding = CreatePadding(4, 4, 4, 4);
            grid.spacing = new Vector2(0f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 1;
            grid.cellSize = new Vector2(width, 84f);
        }
    }

    private static void ConfigureIngredientRow(CraftingRecipeIngredient ingredient)
    {
        if (ingredient == null || ingredient.transform.Find("BetterUI_IngredientRow") != null)
        {
            return;
        }

        var rowObject = new GameObject(
            "BetterUI_IngredientRow",
            Il2CppType.Of<RectTransform>(),
            Il2CppType.Of<HorizontalLayoutGroup>(),
            Il2CppType.Of<LayoutElement>());
        rowObject.transform.SetParent(ingredient.transform, false);
        RectTransform row = rowObject.GetComponent<RectTransform>();
        row.anchorMin = Vector2.zero;
        row.anchorMax = Vector2.one;
        row.offsetMin = new Vector2(6f, 5f);
        row.offsetMax = new Vector2(-6f, -5f);
        row.SetAsLastSibling();

        LayoutElement rowElement = rowObject.GetComponent<LayoutElement>();
        rowElement.ignoreLayout = true;
        HorizontalLayoutGroup layout = rowObject.GetComponent<HorizontalLayoutGroup>();
        layout.padding = CreatePadding(8, 10, 0, 0);
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        if (ingredient._requiredItemContainer != null)
        {
            MoveRecipeElement(ingredient._requiredItemContainer.transform, row);
        }

        if (ingredient._requiredItemNameTmp == null)
        {
            return;
        }

        MoveRecipeElement(ingredient._requiredItemNameTmp.transform, row);
        ingredient._requiredItemNameTmp.alignment = TextAlignmentOptions.MidlineLeft;
        ingredient._requiredItemNameTmp.margin = Vector4.zero;
        ingredient._requiredItemNameTmp.enableWordWrapping = false;
        ingredient._requiredItemNameTmp.overflowMode = TextOverflowModes.Ellipsis;
        LayoutElement nameLayout = GetOrAddLayout(ingredient._requiredItemNameTmp.gameObject);
        nameLayout.minWidth = 0f;
        nameLayout.preferredWidth = 180f;
        nameLayout.flexibleWidth = 1f;
        nameLayout.minHeight = 58f;
        nameLayout.preferredHeight = 58f;
        nameLayout.flexibleHeight = 0f;

        GameObject statusObject = UnityEngine.Object.Instantiate(
            ingredient._requiredItemNameTmp.gameObject,
            row,
            false);
        statusObject.name = "BetterUI_IngredientStatus";
        TMP_Text status = statusObject.GetComponent<TMP_Text>();
        if (status != null)
        {
            status.text = string.Empty;
            status.fontSize = 12f;
            status.fontStyle = FontStyles.Bold;
            status.alignment = TextAlignmentOptions.MidlineRight;
            status.raycastTarget = false;
            status.margin = Vector4.zero;
        }

        RectTransform statusRect = statusObject.GetComponent<RectTransform>();
        if (statusRect != null)
        {
            statusRect.localScale = Vector3.one;
            statusRect.localRotation = Quaternion.identity;
            statusRect.anchorMin = new Vector2(0.5f, 0.5f);
            statusRect.anchorMax = new Vector2(0.5f, 0.5f);
            statusRect.pivot = new Vector2(0.5f, 0.5f);
            statusRect.anchoredPosition = Vector2.zero;
        }

        LayoutElement statusLayout = GetOrAddLayout(statusObject);
        statusLayout.minWidth = 84f;
        statusLayout.preferredWidth = 96f;
        statusLayout.flexibleWidth = 0f;
        statusLayout.minHeight = 58f;
        statusLayout.preferredHeight = 58f;
        statusLayout.flexibleHeight = 0f;
    }

    private static void RefreshIngredientStatus(CraftingRecipeIngredient ingredient, bool unavailable)
    {
        Transform statusTransform = ingredient != null
            ? ingredient.transform.Find("BetterUI_IngredientRow/BetterUI_IngredientStatus")
            : null;
        TMP_Text status = statusTransform != null ? statusTransform.GetComponent<TMP_Text>() : null;
        if (status == null)
        {
            return;
        }

        status.text = unavailable ? "MISSING" : "AVAILABLE";
        BetterUIStyler.Text(
            status,
            12f,
            unavailable ? BetterUITheme.Danger : BetterUITheme.Success,
            FontStyles.Bold);
        status.alignment = TextAlignmentOptions.MidlineRight;
    }

    private static void StyleQuantity(SelectedCraftingRecipeUI selected)
    {
        Slider slider = selected._craftCountSlider;
        if (slider != null)
        {
            MakeFlat(slider.GetComponent<Image>(), BetterUITheme.CraftingWell, BetterUITheme.BorderSoft);
            MakeFlat(
                slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null,
                BetterUITheme.Primary,
                BetterUITheme.Primary);
            MakeFlat(
                slider.handleRect != null ? slider.handleRect.GetComponent<Image>() : null,
                BetterUITheme.PrimaryHover,
                BetterUITheme.PrimaryHover);

            LayoutElement sliderLayout = GetOrAddLayout(slider.gameObject);
            sliderLayout.minHeight = 18f;
            sliderLayout.preferredHeight = 18f;
            sliderLayout.flexibleWidth = 1f;

            if (slider.handleRect != null)
            {
                slider.handleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 18f);
                slider.handleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 18f);
            }

            ReserveQuantityLabelSpace(selected, slider);
        }

        BetterUIStyler.Text(selected._craftCountTmp, 16f, BetterUITheme.PrimaryHover, FontStyles.Bold);
        if (selected._craftCountTmp != null)
        {
            StyleExistingParentSurface(
                selected._craftCountTmp,
                BetterUITheme.PrimarySoft,
                BetterUITheme.Primary,
                selected.transform);
        }
    }

    private static void ReserveQuantityLabelSpace(SelectedCraftingRecipeUI selected, Slider slider)
    {
        if (selected._craftCountTmp == null || slider == null)
        {
            return;
        }

        Transform countParent = selected._craftCountTmp.transform.parent;
        Transform sliderParent = slider.transform.parent;
        if (countParent == null || sliderParent == null || countParent.GetInstanceID() != sliderParent.GetInstanceID())
        {
            return;
        }

        RectTransform sliderRect = slider.GetComponent<RectTransform>();
        if (sliderRect != null)
        {
            Vector2 offsetMax = sliderRect.offsetMax;
            offsetMax.x = Mathf.Min(offsetMax.x, -58f);
            sliderRect.offsetMax = offsetMax;
        }

        selected._craftCountTmp.alignment = TextAlignmentOptions.MidlineRight;
    }

    private static void StyleRequirementMessage(TMP_Text text, Color accent, Transform workspaceRoot)
    {
        if (text == null)
        {
            return;
        }

        BetterUIStyler.Text(text, 14f, accent, FontStyles.Bold);
        StyleExistingParentSurface(text, BetterUITheme.PanelSoft, accent, workspaceRoot);
        text.margin = new Vector4(10f, 6f, 10f, 6f);
    }

    private static void StyleExistingParentSurface(TMP_Text text, Color fill, Color border, Transform stop)
    {
        if (text == null || text.transform.parent == null)
        {
            return;
        }

        Transform parent = text.transform.parent;
        if (stop != null && parent.GetInstanceID() == stop.GetInstanceID())
        {
            return;
        }

        RectTransform rect = parent.GetComponent<RectTransform>();
        if (rect != null && rect.GetComponent<Image>() != null)
        {
            Image surface = BetterUIStyler.Surface(rect, fill, border, false);
            MakeFlat(surface, fill, border);
        }
    }

    private static void StyleEmptyState(GameObject emptyState)
    {
        if (emptyState == null)
        {
            return;
        }

        Image empty = emptyState.GetComponent<Image>();
        if (empty != null)
        {
            MakeFlat(empty, BetterUITheme.CraftingWell, BetterUITheme.BorderSoft);
        }
        var labels = emptyState.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            BetterUIStyler.Text(labels[i], 17f, BetterUITheme.TextMuted, FontStyles.Normal);
            labels[i].alignment = TextAlignmentOptions.Center;
        }
    }

    private static void StyleApprovedState(GameObject approved)
    {
        if (approved == null)
        {
            return;
        }

        var labels = approved.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (label == null)
            {
                continue;
            }

            string value = label.text != null ? label.text.ToUpperInvariant() : string.Empty;
            if (value.Contains("INGREDIENT") || value.Contains("REQUIRED") || value.Contains("MATERIAL"))
            {
                BetterUIStyler.Text(label, 13f, BetterUITheme.TextDim, FontStyles.Bold);
                label.characterSpacing = 1.5f;
            }
        }
    }

    private static void StyleProgress(CraftingProgressUI progress)
    {
        if (progress == null)
        {
            return;
        }

        Image progressSurface = BetterUIStyler.Surface(
            progress.GetComponent<RectTransform>(),
            BetterUITheme.PanelElevated,
            BetterUITheme.Accent,
            false);
        MakeFlat(progressSurface, BetterUITheme.PanelElevated, BetterUITheme.Accent);
        Slider slider = progress.progressSlider;
        if (slider != null)
        {
            MakeFlat(slider.GetComponent<Image>(), BetterUITheme.CraftingWell, BetterUITheme.BorderSoft);
            LayoutElement layout = GetOrAddLayout(slider.gameObject);
            layout.minHeight = 14f;
            layout.preferredHeight = 14f;
        }

        MakeFlat(progress.progressFillImage, BetterUITheme.Accent, BetterUITheme.Accent);
        if (progress.itemImage != null)
        {
            progress.itemImage.preserveAspect = true;
        }

        BetterUIStyler.Text(progress.itemCountTMP, 17f, BetterUITheme.Text, FontStyles.Bold);
    }

    private static void StyleTerminalToggle(MazeButton button, bool active)
    {
        if (button == null)
        {
            return;
        }

        Color fill = active
            ? BetterUITheme.CraftingSelected
            : new Color(BetterUITheme.PanelSoft.r, BetterUITheme.PanelSoft.g, BetterUITheme.PanelSoft.b, 0.62f);
        Color hover = active ? BetterUITheme.CraftingSelectedHover : BetterUITheme.AccentSoft;
        Color text = active ? BetterUITheme.Text : BetterUITheme.TextMuted;
        button.backgroundColor = fill;
        button.hoverColor = hover;
        button.textColor = text;
        button.hoverTextColor = BetterUITheme.Text;
        MakeFlat(
            button.backgroundImage != null ? button.backgroundImage.TryCast<Image>() : null,
            fill,
            active ? BetterUITheme.Accent : BetterUITheme.Border);
        if (button.buttonText != null)
        {
            button.buttonText.text = RemoveCategoryNumberPrefix(button.buttonText.text);
            BetterUIStyler.Text(button.buttonText, 13f, text, FontStyles.Bold);
            button.buttonText.characterSpacing = 0.8f;
            if (button.transform.Find("BetterUI_CategoryIcon") != null)
            {
                button.buttonText.alignment = TextAlignmentOptions.MidlineLeft;
                button.buttonText.margin = new Vector4(42f, 0f, 8f, 0f);
            }
        }

        Transform iconTransform = button.transform.Find("BetterUI_CategoryIcon");
        RawImage icon = iconTransform != null ? iconTransform.GetComponent<RawImage>() : null;
        if (icon != null)
        {
            icon.color = active ? BetterUITheme.Text : BetterUITheme.TextMuted;
            icon.texture = BetterUITheme.GetCategoryIconTexture(
                button.buttonText != null ? button.buttonText.text : string.Empty);
            NormalizeCategoryButtonGeometry(button);
        }

        button.UpdateVisualState(true);
    }

    private static void NormalizeCategoryButtonGeometry(MazeButton button)
    {
        if (button == null)
        {
            return;
        }

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        RectTransform backgroundRect = button.backgroundImage != null
            ? button.backgroundImage.rectTransform
            : null;
        StretchCategoryChild(backgroundRect, buttonRect);

        RectTransform textRect = button.buttonText != null
            ? button.buttonText.rectTransform
            : null;
        StretchCategoryChild(textRect, buttonRect);
    }

    private static void StretchCategoryChild(RectTransform child, RectTransform buttonRect)
    {
        if (child == null || buttonRect == null || child.GetInstanceID() == buttonRect.GetInstanceID())
        {
            return;
        }

        child.localScale = Vector3.one;
        child.localRotation = Quaternion.identity;
        child.anchorMin = Vector2.zero;
        child.anchorMax = Vector2.one;
        child.pivot = new Vector2(0.5f, 0.5f);
        child.offsetMin = Vector2.zero;
        child.offsetMax = Vector2.zero;
    }

    private static string RemoveCategoryNumberPrefix(string value)
    {
        string text = value != null ? value.Trim() : string.Empty;
        if (text.Length == 0)
        {
            return text;
        }

        if (text[0] == '[')
        {
            int closingBracket = text.IndexOf(']');
            if (closingBracket > 1)
            {
                bool digitsOnly = true;
                for (int i = 1; i < closingBracket; i++)
                {
                    if (!char.IsDigit(text[i]))
                    {
                        digitsOnly = false;
                        break;
                    }
                }

                if (digitsOnly)
                {
                    return text.Substring(closingBracket + 1).TrimStart();
                }
            }
        }

        int digitCount = 0;
        while (digitCount < text.Length && char.IsDigit(text[digitCount]))
        {
            digitCount++;
        }

        if (digitCount > 0 && digitCount < text.Length)
        {
            char separator = text[digitCount];
            if (char.IsWhiteSpace(separator) || separator == '.' || separator == ':' ||
                separator == '-' || separator == ')')
            {
                return text.Substring(digitCount + 1).TrimStart();
            }
        }

        return text;
    }

    private static void StyleTerminalAction(MazeButton button)
    {
        if (button == null)
        {
            return;
        }

        button.backgroundColor = BetterUITheme.Accent;
        button.hoverColor = BetterUITheme.PrimaryHover;
        button.textColor = BetterUITheme.CraftingWell;
        button.hoverTextColor = BetterUITheme.CraftingWell;
        button._disabledBackgroundColor = new Color(
            BetterUITheme.TextDim.r,
            BetterUITheme.TextDim.g,
            BetterUITheme.TextDim.b,
            0.24f);
        MakeFlat(
            button.backgroundImage != null ? button.backgroundImage.TryCast<Image>() : null,
            BetterUITheme.Accent,
            BetterUITheme.PrimaryHover);
        if (button.buttonText != null)
        {
            BetterUIStyler.Text(button.buttonText, 16f, BetterUITheme.CraftingWell, FontStyles.Bold);
            button.buttonText.characterSpacing = 1.4f;
        }

        button.UpdateVisualState(true);
    }

    private static void StyleListIcon(ItemContainerUIBase slot, float size)
    {
        if (slot == null)
        {
            return;
        }

        StyleTerminalSlot(slot, size, BetterUITheme.BorderSoft, Color.clear, false);
    }

    private static void StyleTerminalSlot(
        ItemContainerUIBase slot,
        float size,
        Color border,
        Color? fillOverride = null,
        bool showBackground = true,
        bool prominentCount = false)
    {
        if (slot == null)
        {
            return;
        }

        Image itemImage = slot.itemImage;
        if (itemImage != null)
        {
            itemImage.enabled = true;
            itemImage.preserveAspect = true;
            itemImage.raycastTarget = false;
        }

        FitItemArtwork(slot, size);

        Image background = slot.GetComponent<Image>();
        bool backgroundIsItem = background != null
            && itemImage != null
            && background.GetInstanceID() == itemImage.GetInstanceID();
        if (background != null && !backgroundIsItem)
        {
            background.enabled = showBackground;
            if (showBackground)
            {
                MakeFlat(background, fillOverride ?? BetterUITheme.Slot, border);
            }
            else
            {
                var outlines = background.GetComponents<Outline>();
                for (int i = 0; i < outlines.Length; i++)
                {
                    if (outlines[i] != null)
                    {
                        outlines[i].enabled = false;
                    }
                }
            }
        }

        ItemContainerUI itemSlot = slot.TryCast<ItemContainerUI>();
        if (itemSlot != null)
        {
            RefreshCountBadge(itemSlot, prominentCount);
        }

        LayoutElement layout = GetOrAddLayout(slot.gameObject);
        layout.minWidth = size;
        layout.minHeight = size;
        layout.preferredWidth = size;
        layout.preferredHeight = size;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;
    }

    private static void FitItemArtwork(ItemContainerUIBase slot, float slotSize)
    {
        if (slot == null || slot.itemImage == null)
        {
            return;
        }

        RectTransform slotRect = slot.GetComponent<RectTransform>();
        RectTransform imageRect = slot.itemImage.rectTransform;
        if (slotRect == null || imageRect == null || slotRect.GetInstanceID() == imageRect.GetInstanceID())
        {
            return;
        }

        float padding = slotSize <= 52f ? 1f : 7f;
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.offsetMin = new Vector2(padding, padding);
        imageRect.offsetMax = new Vector2(-padding, -padding);
        imageRect.localScale = Vector3.one;
        imageRect.localRotation = Quaternion.identity;
        imageRect.SetAsFirstSibling();
    }

    private static void RefreshCountBadge(ItemContainerUI itemSlot, bool prominent = false)
    {
        if (itemSlot == null)
        {
            return;
        }

        TMP_Text countText = itemSlot.itemCountTmp;
        string count = countText != null ? countText.text?.Trim() : string.Empty;
        bool show = !string.IsNullOrEmpty(count)
            && count != "0"
            && (prominent || count != "1");
        float badgeWidth = prominent
            ? count.Length > 2 ? 26f : 22f
            : count.Length > 2 ? 22f : 18f;
        float badgeHeight = prominent ? 18f : 16f;

        Image background = itemSlot.countBackgroundImage;
        if (background != null)
        {
            background.enabled = show;
            background.raycastTarget = false;
            if (show)
            {
                MakeFlat(background, BetterUITheme.CountBackground, BetterUITheme.BorderSoft);
                RectTransform badgeRect = background.rectTransform;
                badgeRect.anchorMin = new Vector2(1f, 0f);
                badgeRect.anchorMax = new Vector2(1f, 0f);
                badgeRect.pivot = new Vector2(1f, 0f);
                badgeRect.anchoredPosition = prominent ? new Vector2(-2f, 2f) : Vector2.zero;
                badgeRect.sizeDelta = new Vector2(badgeWidth, badgeHeight);
                badgeRect.SetAsLastSibling();
            }
        }

        if (countText == null)
        {
            return;
        }

        countText.enabled = show;
        if (!show)
        {
            return;
        }

        BetterUIStyler.Text(countText, prominent ? 12f : 11f, BetterUITheme.Text, FontStyles.Bold);
        countText.alignment = TextAlignmentOptions.Center;
        RectTransform textRect = countText.rectTransform;
        if (background != null && countText.transform.parent == background.transform)
        {
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }
        else
        {
            textRect.anchorMin = new Vector2(1f, 0f);
            textRect.anchorMax = new Vector2(1f, 0f);
            textRect.pivot = new Vector2(1f, 0f);
            textRect.anchoredPosition = prominent ? new Vector2(-2f, 2f) : Vector2.zero;
            textRect.sizeDelta = new Vector2(badgeWidth, badgeHeight);
            textRect.SetAsLastSibling();
        }
    }

    private static void MakeFlat(Image image, Color fill, Color border)
    {
        if (image == null)
        {
            return;
        }

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

        var shadows = image.GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length; i++)
        {
            Shadow shadow = shadows[i];
            if (shadow != null && shadow.TryCast<Outline>() == null)
            {
                shadow.enabled = false;
            }
        }
    }

    private static void StyleItemSlot(ItemContainerUIBase slot, float size, Color border)
    {
        if (slot == null)
        {
            return;
        }

        Image itemImage = slot.itemImage;
        if (itemImage != null)
        {
            itemImage.preserveAspect = true;
        }

        Image background = slot.GetComponent<Image>();
        bool backgroundIsItem = background != null && itemImage != null && background.GetInstanceID() == itemImage.GetInstanceID();
        if (background != null && !backgroundIsItem)
        {
            BetterUIStyler.RoundedImage(background, BetterUITheme.Slot);
            BetterUIStyler.Effects(background, border, false);
        }

        ItemContainerUI itemSlot = slot.TryCast<ItemContainerUI>();
        if (itemSlot != null)
        {
            BetterUIStyler.RoundedImage(itemSlot.countBackgroundImage, BetterUITheme.CountBackground);
            BetterUIStyler.Text(itemSlot.itemCountTmp, 16f, BetterUITheme.Text, FontStyles.Bold);
        }

        LayoutElement layout = GetOrAddLayout(slot.gameObject);
        layout.minWidth = size;
        layout.minHeight = size;
        layout.preferredWidth = size;
        layout.preferredHeight = size;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;
    }

    private static void AddAvailabilityRail(Transform card, Color color)
    {
        var railObject = new GameObject(
            "BetterUI_AvailabilityRail",
            Il2CppType.Of<RectTransform>(),
            Il2CppType.Of<CanvasRenderer>(),
            Il2CppType.Of<Image>(),
            Il2CppType.Of<LayoutElement>());
        railObject.transform.SetParent(card, false);

        RectTransform rect = railObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(3f, 0f);
        rect.SetAsLastSibling();

        LayoutElement layout = railObject.GetComponent<LayoutElement>();
        layout.ignoreLayout = true;

        Image rail = railObject.GetComponent<Image>();
        rail.raycastTarget = false;
        rail.color = color;
    }

    private static Image GetOrCreateCardSurface(Transform card)
    {
        Image existing = FindDecoration(card, "BetterUI_CardSurface");
        if (existing != null)
        {
            return existing;
        }

        var surfaceObject = new GameObject(
            "BetterUI_CardSurface",
            Il2CppType.Of<RectTransform>(),
            Il2CppType.Of<CanvasRenderer>(),
            Il2CppType.Of<Image>(),
            Il2CppType.Of<LayoutElement>());
        surfaceObject.transform.SetParent(card, false);

        RectTransform rect = surfaceObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.SetAsFirstSibling();

        LayoutElement layout = surfaceObject.GetComponent<LayoutElement>();
        layout.ignoreLayout = true;

        Image surface = surfaceObject.GetComponent<Image>();
        surface.raycastTarget = false;
        return surface;
    }

    private static void ConfigureRecipeRow(CraftingRecipeUI recipe)
    {
        if (recipe == null || recipe.transform.Find("BetterUI_RowContent") != null)
        {
            return;
        }

        var rowObject = new GameObject(
            "BetterUI_RowContent",
            Il2CppType.Of<RectTransform>(),
            Il2CppType.Of<HorizontalLayoutGroup>(),
            Il2CppType.Of<LayoutElement>());
        rowObject.transform.SetParent(recipe.transform, false);
        RectTransform row = rowObject.GetComponent<RectTransform>();
        row.anchorMin = Vector2.zero;
        row.anchorMax = Vector2.one;
        row.offsetMin = new Vector2(10f, 4f);
        row.offsetMax = new Vector2(-10f, -4f);
        row.SetAsLastSibling();

        LayoutElement rowElement = rowObject.GetComponent<LayoutElement>();
        rowElement.ignoreLayout = true;
        HorizontalLayoutGroup layout = rowObject.GetComponent<HorizontalLayoutGroup>();
        layout.padding = CreatePadding(8, 8, 0, 0);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        if (recipe._outputItemContiner != null)
        {
            MoveRecipeElement(recipe._outputItemContiner.transform, row);
        }

        if (recipe._outputItemName != null)
        {
            MoveRecipeElement(recipe._outputItemName.transform, row);
            LayoutElement nameLayout = GetOrAddLayout(recipe._outputItemName.gameObject);
            nameLayout.minWidth = 0f;
            nameLayout.preferredWidth = 220f;
            nameLayout.flexibleWidth = 1f;
            nameLayout.minHeight = 44f;
            nameLayout.preferredHeight = 44f;
            nameLayout.flexibleHeight = 0f;
            recipe._outputItemName.alignment = TextAlignmentOptions.MidlineLeft;
            recipe._outputItemName.margin = Vector4.zero;

            GameObject statusObject = UnityEngine.Object.Instantiate(
                recipe._outputItemName.gameObject,
                row,
                false);
            statusObject.name = "BetterUI_RowStatus";
            TMP_Text status = statusObject.GetComponent<TMP_Text>();
            if (status != null)
            {
                status.text = string.Empty;
                status.fontSize = 11f;
                status.fontStyle = FontStyles.Bold;
                status.color = BetterUITheme.TextUnavailable;
                status.alignment = TextAlignmentOptions.MidlineRight;
                status.enableWordWrapping = false;
                status.raycastTarget = false;
                status.margin = Vector4.zero;
            }

            RectTransform statusRect = statusObject.GetComponent<RectTransform>();
            if (statusRect != null)
            {
                statusRect.localScale = Vector3.one;
                statusRect.localRotation = Quaternion.identity;
                statusRect.anchorMin = new Vector2(0.5f, 0.5f);
                statusRect.anchorMax = new Vector2(0.5f, 0.5f);
                statusRect.pivot = new Vector2(0.5f, 0.5f);
                statusRect.anchoredPosition = Vector2.zero;
            }

            LayoutElement statusLayout = GetOrAddLayout(statusObject);
            statusLayout.minWidth = 76f;
            statusLayout.preferredWidth = 84f;
            statusLayout.flexibleWidth = 0f;
            statusLayout.minHeight = 44f;
            statusLayout.preferredHeight = 44f;
            statusLayout.flexibleHeight = 0f;
        }
    }

    private static void MoveRecipeElement(Transform element, Transform parent)
    {
        if (element == null)
        {
            return;
        }

        element.SetParent(parent, false);
        RectTransform rect = element.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }
    }

    private static void AddTerminalOverlay(RectTransform panel)
    {
        if (panel == null || panel.Find("BetterUI_TerminalScanlines") != null)
        {
            return;
        }

        var overlayObject = new GameObject(
            "BetterUI_TerminalScanlines",
            Il2CppType.Of<RectTransform>(),
            Il2CppType.Of<CanvasRenderer>(),
            Il2CppType.Of<RawImage>(),
            Il2CppType.Of<LayoutElement>());
        overlayObject.transform.SetParent(panel, false);

        RectTransform rect = overlayObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.SetAsLastSibling();

        LayoutElement layout = overlayObject.GetComponent<LayoutElement>();
        layout.ignoreLayout = true;

        RawImage scanlines = overlayObject.GetComponent<RawImage>();
        scanlines.texture = BetterUITheme.ScanlineTexture;
        scanlines.color = new Color(0f, 0f, 0f, 0.10f);
        scanlines.uvRect = new Rect(0f, 0f, 1f, 180f);
        scanlines.raycastTarget = false;
    }

    private static Image FindDecoration(Transform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        Transform child = parent.Find(name);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private static bool LooksLikeUnavailableState(Color color)
    {
        return color.r > 0.35f && color.r > color.g * 1.18f && color.r > color.b * 1.08f;
    }

    private static bool IsFilterActive(CraftingRecipeFilter mask, int buttonIndex)
    {
        CraftingRecipeFilter flag;
        switch (buttonIndex)
        {
            case 0:
                flag = CraftingRecipeFilter.Misc;
                break;
            case 1:
                flag = CraftingRecipeFilter.Combat;
                break;
            case 2:
                flag = CraftingRecipeFilter.Placable;
                break;
            case 3:
                flag = CraftingRecipeFilter.Medical;
                break;
            case 4:
                flag = CraftingRecipeFilter.Clothing;
                break;
            default:
                return false;
        }

        return (mask & flag) != 0;
    }

    private static Transform FindCommonParent(MazeButton[] buttons)
    {
        Transform parent = null;
        for (int i = 0; i < buttons.Length; i++)
        {
            MazeButton button = buttons[i];
            if (button == null)
            {
                continue;
            }

            if (parent == null)
            {
                parent = button.transform.parent;
            }
            else if (button.transform.parent == null || button.transform.parent.GetInstanceID() != parent.GetInstanceID())
            {
                return null;
            }
        }

        return parent;
    }

    private static LayoutElement GetOrAddLayout(GameObject target)
    {
        LayoutElement layout = target.GetComponent<LayoutElement>();
        return layout != null ? layout : target.AddComponent<LayoutElement>();
    }

    private void RebuildOpeningLayouts()
    {
        if (_layoutRebuildPasses <= 0)
        {
            return;
        }

        _layoutRebuildPasses--;
        Canvas.ForceUpdateCanvases();

        RectTransform rootRect = _replacementRoot != null
            ? _replacementRoot.GetComponent<RectTransform>()
            : null;
        if (rootRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
        }

        RectTransform recipeRect = _recipeContainer != null
            ? _recipeContainer.GetComponent<RectTransform>()
            : null;
        if (recipeRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(recipeRect);
        }

        Canvas.ForceUpdateCanvases();
    }

    private static int GetHierarchyStamp(Transform container)
    {
        int count = container.childCount;
        unchecked
        {
            int stamp = count * 397;
            if (count > 0)
            {
                Transform first = container.GetChild(0);
                Transform last = container.GetChild(count - 1);
                stamp = (stamp * 397) ^ (first != null ? first.GetInstanceID() : 0);
                stamp = (stamp * 397) ^ (last != null ? last.GetInstanceID() : 0);
            }

            return stamp;
        }
    }

    private static RectOffset CreatePadding(int left, int right, int top, int bottom)
    {
        var padding = new RectOffset
        {
            left = left,
            right = right,
            top = top,
            bottom = bottom
        };
        return padding;
    }

    private static RectTransform FindReplacementPanel(Transform title)
    {
        Transform current = title;
        while (current != null)
        {
            if (current.name == "BetterUI_CraftingMenu")
            {
                return current.GetComponent<RectTransform>();
            }

            current = current.parent;
        }

        return title != null && title.parent != null
            ? title.parent.GetComponent<RectTransform>()
            : null;
    }

    private static bool SameObject(UnityEngine.Object left, UnityEngine.Object right)
    {
        return left != null && right != null && left.GetInstanceID() == right.GetInstanceID();
    }
}
