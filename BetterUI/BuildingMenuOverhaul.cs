using System.Collections.Generic;
using Game.LevelOperations;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace InfernoProtocol.BetterUI;

internal sealed class BuildingMenuOverhaul
{
    private readonly BuildingMenuBuilder _builder = new();
    private readonly HashSet<int> _configuredRecipes = new();
    private readonly HashSet<int> _configuredIngredients = new();
    private readonly Dictionary<int, TextMeshProUGUI> _recipeLabels = new();
    private int _configuredRootId;
    private int _paletteIndex = -1;
    private int _activeFilterIndex = -1;
    private bool _catalogAudited;

    internal void Prepare(BuildTemplatesUI building)
    {
        if (building == null)
        {
            return;
        }

        if (_configuredRootId != building.GetInstanceID())
        {
            _configuredRootId = building.GetInstanceID();
            _builder.Build(building);
            _configuredRecipes.Clear();
            _configuredIngredients.Clear();
            _recipeLabels.Clear();
            _paletteIndex = -1;
            _catalogAudited = false;
        }

        // Prime the live controls during Show()'s prefix. The shell is complete
        // before Unity can render the menu's first frame.
        if (_paletteIndex != BetterUIPlugin.PaletteIndex)
        {
            _paletteIndex = BetterUIPlugin.PaletteIndex;
            RefreshPalette(building);
        }

        ConfigureFilters(building);
        ConfigureRecipes(building);
        ConfigureSelected(building._selectedRecipeUI);
    }

    internal void SetActiveFilter(PlacableType filter)
    {
        int next = filter switch
        {
            PlacableType.Wall => 0,
            PlacableType.FloorOrCeiling => 1,
            PlacableType.Stair => 2,
            PlacableType.Stairs_2x1 => 2,
            PlacableType.Functional => 3,
            PlacableType.Misc => 4,
            _ => -1
        };
        _activeFilterIndex = _activeFilterIndex == next ? -1 : next;
    }

    internal void ResetActiveFilter()
    {
        _activeFilterIndex = -1;
    }

    internal void SetVisible(bool visible)
    {
        if (_builder.ReplacementRoot != null && _builder.ReplacementRoot.activeSelf != visible)
        {
            _builder.ReplacementRoot.SetActive(visible);
        }
    }

    internal bool HandleInput()
    {
        RectTransform button = _builder.ThemeButton;
        if (button == null || _builder.ReplacementRoot == null || !_builder.ReplacementRoot.activeInHierarchy || Mouse.current == null)
        {
            return false;
        }

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

        if (!hovered || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return false;
        }

        BetterUIPlugin.CyclePalette();
        return true;
    }

    internal void Apply(BuildTemplatesUI building)
    {
        if (building == null)
        {
            return;
        }

        Prepare(building);

        if (_paletteIndex != BetterUIPlugin.PaletteIndex)
        {
            _paletteIndex = BetterUIPlugin.PaletteIndex;
            RefreshPalette(building);
        }

        ConfigureFilters(building);
        ConfigureRecipes(building);
        ConfigureSelected(building._selectedRecipeUI);
    }

    private void ConfigureFilters(BuildTemplatesUI building)
    {
        StyleFilter(building.filterWallButton, _activeFilterIndex == 0);
        StyleFilter(building.filterFloorCeilingButton, _activeFilterIndex == 1);
        StyleFilter(building.filterStairButton, _activeFilterIndex == 2);
        StyleFilter(building.filterFunctionalButton, _activeFilterIndex == 3);
        StyleFilter(building.filterMiscButton, _activeFilterIndex == 4);
        StyleFooterButton(building._hideUncraftableButton, ButtonVariant.Secondary);
        StyleFooterButton(_builder.CloseButton, ButtonVariant.Danger);
    }

    private static void StyleFilter(MazeButton button, bool active)
    {
        if (button == null) return;
        button._menuButtonShader = false;
        Color fill = active
            ? BetterUITheme.CraftingSelected
            : new Color(BetterUITheme.PanelSoft.r, BetterUITheme.PanelSoft.g, BetterUITheme.PanelSoft.b, 0.62f);
        Color hover = active ? BetterUITheme.CraftingSelectedHover : BetterUITheme.AccentSoft;
        Color text = active ? BetterUITheme.Text : BetterUITheme.TextMuted;
        button.backgroundColor = fill;
        button.hoverColor = hover;
        button.textColor = text;
        button.hoverTextColor = BetterUITheme.Text;
        button._disabledBackgroundColor = fill;
        button._disabledTextColor = BetterUITheme.TextDim;
        Image image = button.backgroundImage != null ? button.backgroundImage.TryCast<Image>() : null;
        MakeFlat(image, fill, active ? BetterUITheme.Accent : BetterUITheme.Border);
        if (button.backgroundImage != null && image == null)
        {
            button.backgroundImage.color = fill;
        }

        if (button.buttonText != null)
        {
            BetterUIStyler.Text(button.buttonText, 13f, text, FontStyles.Bold);
            button.buttonText.characterSpacing = 0.8f;
        }

        button.UpdateVisualState(true);
    }

    private static void StyleFooterButton(MazeButton button, ButtonVariant variant)
    {
        if (button == null) return;
        button._menuButtonShader = false;
        BetterUIStyler.Button(button, variant);
        if (button.backgroundImage == null) return;
        Color color = variant == ButtonVariant.Danger
            ? BetterUITheme.DangerSoft
            : BetterUITheme.PanelSoft;
        button.backgroundImage.color = button.Interactable ? color : BetterUITheme.PanelSoft;
    }

    private void ConfigureRecipes(BuildTemplatesUI building)
    {
        Transform container = building._buildRecipesContainer;
        if (container == null)
        {
            return;
        }

        var recipes = container.GetComponentsInChildren<BuildRecipeUI>(true);
        ConfigureRecipeGrid(container);

        int selectedId = building._selectedRecipeOnListUI != null ? building._selectedRecipeOnListUI.GetInstanceID() : 0;
        for (int i = 0; i < recipes.Length; i++)
        {
            BuildRecipeUI recipe = recipes[i];
            if (recipe == null) continue;
            if (!IsNativeCatalogRecipe(building, recipe) || !IsAllowedByProgression(recipe))
            {
                recipe.gameObject.SetActive(false);
                continue;
            }

            int id = recipe.GetInstanceID();
            if (_configuredRecipes.Add(id)) ConfigureRecipeGeometry(recipe, id);
            StyleRecipe(recipe, id == selectedId);
        }

        if (!_catalogAudited && recipes.Length > 0)
        {
            _catalogAudited = true;
            BetterUIPlugin.ModLog.LogInfo(
                $"Building catalog audited {recipes.Length} hierarchy entries against the native recipe registry; hidden templates and blueprint gates preserved.");
        }
    }

    private static bool IsNativeCatalogRecipe(BuildTemplatesUI building, BuildRecipeUI recipe)
    {
        if (building == null || recipe == null || recipe.recipe == null)
        {
            return false;
        }

        // The stock hierarchy also contains the serialized recipe-card prefab.
        // Its sample data is a ready-built house, but the original menu never
        // treats that prefab as a player-facing plan. Moving the live container
        // into our shell can otherwise expose it through its newly active parent.
        if (building._buildRecipeUIPrefab != null
            && building._buildRecipeUIPrefab.GetInstanceID() == recipe.GetInstanceID())
        {
            return false;
        }

        // BuildTemplatesUI's dictionary is populated by the game's Awake method
        // after it applies its own recipe eligibility rules. Use that registry as
        // the source of truth instead of trusting every BuildRecipeUI found below
        // the hierarchy, which may include dormant templates or future internals.
        if (building._buildRecipeUIs == null
            || !building._buildRecipeUIs.TryGetValue(recipe.recipe, out BuildRecipeUI nativeRecipe)
            || nativeRecipe == null)
        {
            return false;
        }

        return nativeRecipe.GetInstanceID() == recipe.GetInstanceID();
    }

    private static bool IsAllowedByProgression(BuildRecipeUI recipe)
    {
        if (recipe == null || recipe.recipe == null)
        {
            return false;
        }

        Game.Data.CraftingRecipe data = recipe.recipe;
        bool hasBlueprint = data.localPlayerHasBlueprint;
        return !(data.mustHaveBlueprint || data.showOnlyIfHasBlueprint) || hasBlueprint;
    }

    private static void ConfigureRecipeGrid(Transform container)
    {
        GridLayoutGroup grid = container.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            var layouts = container.GetComponents<LayoutGroup>();
            for (int i = 0; i < layouts.Length; i++)
            {
                if (layouts[i] != null) UnityEngine.Object.DestroyImmediate(layouts[i]);
            }

            grid = container.gameObject.AddComponent<GridLayoutGroup>();
        }
        grid.enabled = true;
        grid.padding = new RectOffset { left = 5, right = 5, top = 5, bottom = 5 };
        grid.spacing = new Vector2(8f, 8f);
        RectTransform viewport = container.parent != null ? container.parent.GetComponent<RectTransform>() : null;
        float availableWidth = viewport != null ? viewport.rect.width - 18f : 520f;
        int columns = availableWidth >= 1050f ? 3 : availableWidth >= 470f ? 2 : 1;
        float cardWidth = Mathf.Max(190f, (availableWidth - (grid.spacing.x * (columns - 1))) / columns);
        grid.cellSize = new Vector2(cardWidth, 94f);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;

        ContentSizeFitter fitter = container.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = container.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void ConfigureRecipeGeometry(BuildRecipeUI recipe, int id)
    {
        BuildingMenuBuilder.SetLayout(recipe.gameObject, 210f, 0f, 1f, 94f, 94f, 0f);
        RectTransform root = recipe.GetComponent<RectTransform>();
        if (root != null) root.localScale = Vector3.one;

        if (recipe._selectButton != null)
        {
            RectTransform select = recipe._selectButton.GetComponent<RectTransform>();
            if (select != null)
            {
                select.anchorMin = Vector2.zero;
                select.anchorMax = Vector2.one;
                select.offsetMin = Vector2.zero;
                select.offsetMax = Vector2.zero;
            }
        }

        if (recipe._outputItemContainer != null)
        {
            RectTransform icon = recipe._outputItemContainer.GetComponent<RectTransform>();
            if (icon != null)
            {
                icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
                icon.pivot = new Vector2(0f, 0.5f);
                icon.anchoredPosition = new Vector2(12f, 0f);
                icon.sizeDelta = new Vector2(70f, 70f);
            }
        }

        if (recipe._outputItemName != null)
        {
            recipe._outputItemName.enabled = false;
            Transform existing = recipe.transform.Find("BetterUI_BuildRecipeLabel");
            TextMeshProUGUI display = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
            if (display == null)
            {
                GameObject clone = UnityEngine.Object.Instantiate(recipe._outputItemName.gameObject, recipe.transform, false);
                clone.name = "BetterUI_BuildRecipeLabel";
                clone.SetActive(true);
                display = clone.GetComponent<TextMeshProUGUI>();
            }

            display.enabled = true;
            display.raycastTarget = false;
            display.transform.SetAsLastSibling();
            RectTransform label = display.rectTransform;
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = new Vector2(96f, 8f);
            label.offsetMax = new Vector2(-12f, -8f);
            display.alignment = TextAlignmentOptions.MidlineLeft;
            display.enableWordWrapping = true;
            display.overflowMode = TextOverflowModes.Ellipsis;
            _recipeLabels[id] = display;
        }
    }

    private void StyleRecipe(BuildRecipeUI recipe, bool selected)
    {
        bool available = recipe.playerCanBuild;
        Color fill = selected
            ? BetterUITheme.CraftingSelected
            : available ? BetterUITheme.CraftingReady : BetterUITheme.PanelElevated;
        Color border = selected ? BetterUITheme.Accent : BetterUITheme.BorderSoft;
        Image card = GetOrCreateCardSurface(recipe.transform, "BetterUI_BuildCardSurface");
        NeutralizeNativeRecipeVisuals(recipe);
        MakeFlat(card, fill, border);
        if (recipe._selectButton != null)
        {
            recipe._selectButton._menuButtonShader = false;
        }

        if (recipe._outputItemName != null)
        {
            recipe._outputItemName.enabled = false;
            if (_recipeLabels.TryGetValue(recipe.GetInstanceID(), out TextMeshProUGUI display) && display != null)
            {
                display.text = recipe._outputItemName.text;
                display.gameObject.SetActive(true);
                display.enabled = true;
                display.transform.SetAsLastSibling();
                BetterUIStyler.Text(display, 15f, available ? BetterUITheme.Text : BetterUITheme.TextUnavailable, FontStyles.Bold);
            }
        }

        StyleItemContainer(recipe._outputItemContainer, 70f, false);
    }

    private void ConfigureSelected(SelectedBuildRecipeUI selected)
    {
        if (selected == null)
        {
            return;
        }

        if (selected._outputItemNameTMP != null)
        {
            BetterUIStyler.Text(selected._outputItemNameTMP, 24f, BetterUITheme.Text, FontStyles.Bold);
            selected._outputItemNameTMP.alignment = TextAlignmentOptions.MidlineLeft;
        }

        StyleItemContainer(selected._outputItemContainer, 108f, true);
        selected._defaultPlaceColor = BetterUITheme.Accent;
        StylePlaceButton(selected._placeButton);
        if (selected._placeButton != null && selected._placeButton.buttonText != null)
        {
            BetterUIStyler.Text(selected._placeButton.buttonText, 16f, BetterUITheme.PrimaryHover, FontStyles.Bold);
            selected._placeButton.buttonText.enableWordWrapping = false;
            selected._placeButton.buttonText.overflowMode = TextOverflowModes.Ellipsis;
            selected._placeButton.buttonText.alignment = TextAlignmentOptions.Center;
            selected._placeButton.buttonText.margin = new Vector4(12f, 0f, 12f, 0f);
        }

        if (selected._requiredItemsContainer == null)
        {
            return;
        }

        var ingredients = selected._requiredItemsContainer.GetComponentsInChildren<CraftingRecipeIngredient>(true);
        ConfigureIngredientGrid(selected._requiredItemsContainer, ingredients.Length);
        for (int i = 0; i < ingredients.Length; i++)
        {
            CraftingRecipeIngredient ingredient = ingredients[i];
            if (ingredient == null) continue;
            if (_configuredIngredients.Add(ingredient.GetInstanceID())) ConfigureIngredientGeometry(ingredient);
            StyleIngredient(ingredient);
        }
    }

    private static void ConfigureIngredientGrid(Transform container, int count)
    {
        GridLayoutGroup grid = container.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            var layouts = container.GetComponents<LayoutGroup>();
            for (int i = 0; i < layouts.Length; i++)
            {
                if (layouts[i] != null) UnityEngine.Object.DestroyImmediate(layouts[i]);
            }

            grid = container.gameObject.AddComponent<GridLayoutGroup>();
        }
        grid.enabled = true;
        grid.padding = new RectOffset { left = 4, right = 4, top = 4, bottom = 4 };
        grid.spacing = new Vector2(8f, 8f);
        RectTransform rect = container.GetComponent<RectTransform>();
        float width = rect != null ? Mathf.Max(220f, rect.rect.width - 8f) : 320f;
        grid.cellSize = new Vector2(width, 84f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 1;
        grid.childAlignment = TextAnchor.UpperLeft;

        LayoutElement section = container.parent != null ? container.parent.GetComponent<LayoutElement>() : null;
        if (section != null)
        {
            float height = Mathf.Clamp((count * 86f) + 20f, 104f, 360f);
            section.minHeight = height;
            section.preferredHeight = height;
        }
    }

    private static void ConfigureIngredientGeometry(CraftingRecipeIngredient ingredient)
    {
        BuildingMenuBuilder.SetLayout(ingredient.gameObject, 220f, 0f, 1f, 84f, 84f, 0f);
        RectTransform ingredientRect = ingredient.GetComponent<RectTransform>();
        if (ingredient._backgroundImage != null)
        {
            RectTransform background = ingredient._backgroundImage.rectTransform;
            if (background != null && ingredientRect != null && background.GetInstanceID() != ingredientRect.GetInstanceID())
            {
                background.anchorMin = Vector2.zero;
                background.anchorMax = Vector2.one;
                background.offsetMin = Vector2.zero;
                background.offsetMax = Vector2.zero;
                background.SetAsFirstSibling();
            }
        }

        if (ingredient.transform.Find("BetterUI_BuildIngredientRow") == null)
        {
            var rowObject = new GameObject(
                "BetterUI_BuildIngredientRow",
                Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>(),
                Il2CppInterop.Runtime.Il2CppType.Of<HorizontalLayoutGroup>(),
                Il2CppInterop.Runtime.Il2CppType.Of<LayoutElement>());
            rowObject.transform.SetParent(ingredient.transform, false);
            RectTransform row = rowObject.GetComponent<RectTransform>();
            row.anchorMin = Vector2.zero;
            row.anchorMax = Vector2.one;
            row.offsetMin = new Vector2(8f, 6f);
            row.offsetMax = new Vector2(-10f, -6f);
            row.SetAsLastSibling();
            rowObject.GetComponent<LayoutElement>().ignoreLayout = true;
            HorizontalLayoutGroup layout = rowObject.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset { left = 6, right = 8, top = 0, bottom = 0 };
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            MoveIntoRow(ingredient._requiredItemContainer != null ? ingredient._requiredItemContainer.transform : null, row);
            MoveIntoRow(ingredient._requiredItemNameTmp != null ? ingredient._requiredItemNameTmp.transform : null, row);
            if (ingredient._requiredItemNameTmp != null)
            {
                BuildingMenuBuilder.SetLayout(ingredient._requiredItemNameTmp.gameObject, 0f, 180f, 1f, 62f, 62f, 0f);
                ingredient._requiredItemNameTmp.alignment = TextAlignmentOptions.MidlineLeft;
                ingredient._requiredItemNameTmp.margin = Vector4.zero;
                ingredient._requiredItemNameTmp.enableWordWrapping = false;
                ingredient._requiredItemNameTmp.overflowMode = TextOverflowModes.Ellipsis;
            }
        }
    }

    private static void StyleIngredient(CraftingRecipeIngredient ingredient)
    {
        Image card = ingredient._backgroundImage != null ? ingredient._backgroundImage : ingredient.GetComponent<Image>();
        MakeFlat(card, BetterUITheme.PanelSoft, BetterUITheme.BorderSoft);
        Image root = ingredient.GetComponent<Image>();
        if (root != null && card != null && root.GetInstanceID() != card.GetInstanceID())
        {
            root.enabled = false;
            var outlines = root.GetComponents<Outline>();
            for (int i = 0; i < outlines.Length; i++) if (outlines[i] != null) outlines[i].enabled = false;
        }

        if (ingredient._requiredItemNameTmp != null)
        {
            BetterUIStyler.Text(ingredient._requiredItemNameTmp, 13f, BetterUITheme.TextMuted, FontStyles.Normal);
        }

        StyleItemContainer(ingredient._requiredItemContainer, 62f, true);
    }

    private static void StylePlaceButton(MazeButton button)
    {
        if (button == null) return;
        Color normal = BetterUITheme.AccentSoft;
        Color hover = BetterUITheme.CraftingSelectedHover;
        button._menuButtonShader = false;
        button.backgroundColor = normal;
        button.hoverColor = hover;
        button.textColor = BetterUITheme.Accent;
        button.hoverTextColor = BetterUITheme.Text;
        button._disabledBackgroundColor = BetterUITheme.PanelSoft;
        button._disabledTextColor = BetterUITheme.TextDim;
        button.UpdateVisualState(true);

        if (button.backgroundImage != null)
        {
            button.backgroundImage.color = button.Interactable ? normal : BetterUITheme.PanelSoft;
        }

        Image image = button.backgroundImage != null ? button.backgroundImage.TryCast<Image>() : null;
        if (image != null)
        {
            MakeFlat(
                image,
                button.Interactable ? normal : BetterUITheme.PanelSoft,
                button.Interactable ? BetterUITheme.Accent : BetterUITheme.BorderSoft);
        }
    }

    private static void MoveIntoRow(Transform element, Transform row)
    {
        if (element == null || row == null) return;
        element.SetParent(row, false);
        RectTransform rect = element.GetComponent<RectTransform>();
        if (rect == null) return;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
    }

    private static Image GetOrCreateCardSurface(Transform card, string name)
    {
        Transform existing = card != null ? card.Find(name) : null;
        Image image = existing != null ? existing.GetComponent<Image>() : null;
        if (image != null) return image;
        if (card == null) return null;

        var surfaceObject = new GameObject(
            name,
            Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>(),
            Il2CppInterop.Runtime.Il2CppType.Of<CanvasRenderer>(),
            Il2CppInterop.Runtime.Il2CppType.Of<Image>(),
            Il2CppInterop.Runtime.Il2CppType.Of<LayoutElement>());
        surfaceObject.transform.SetParent(card, false);
        RectTransform rect = surfaceObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetAsFirstSibling();
        surfaceObject.GetComponent<LayoutElement>().ignoreLayout = true;
        image = surfaceObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static void NeutralizeNativeRecipeVisuals(BuildRecipeUI recipe)
    {
        BuildRecipeUI.canBuildColor = Color.clear;
        BuildRecipeUI.cantBuildColor = Color.clear;
        BuildRecipeUI.selectedColor = Color.clear;

        Image root = recipe.GetComponent<Image>();
        if (root != null)
        {
            root.material = null;
            root.color = new Color(0f, 0f, 0f, 0.001f);
            var outlines = root.GetComponents<Outline>();
            for (int i = 0; i < outlines.Length; i++) if (outlines[i] != null) outlines[i].enabled = false;
        }

        MazeButton button = recipe._selectButton;
        if (button == null) return;
        button.backgroundColor = Color.clear;
        button.hoverColor = Color.clear;
        button.textColor = Color.clear;
        button.hoverTextColor = Color.clear;
        button._disabledBackgroundColor = Color.clear;
        Graphic native = button.backgroundImage;
        if (native == null) return;
        native.enabled = true;
        native.material = null;
        native.color = new Color(0f, 0f, 0f, 0.001f);
        native.raycastTarget = true;
        Image nativeImage = native.TryCast<Image>();
        if (nativeImage != null)
        {
            nativeImage.sprite = null;
            nativeImage.type = Image.Type.Simple;
        }

        Outline outline = native.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
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

        var shadows = image.GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length; i++)
        {
            Shadow shadow = shadows[i];
            if (shadow != null && shadow.TryCast<Outline>() == null) shadow.enabled = false;
        }
    }

    private static void StyleItemContainer(ItemContainerUI container, float size, bool showBackground)
    {
        if (container == null) return;
        RectTransform rect = container.GetComponent<RectTransform>();
        if (rect != null) rect.sizeDelta = new Vector2(size, size);
        LayoutElement slotLayout = container.GetComponent<LayoutElement>();
        if (slotLayout == null) slotLayout = container.gameObject.AddComponent<LayoutElement>();
        slotLayout.minWidth = size;
        slotLayout.preferredWidth = size;
        slotLayout.flexibleWidth = 0f;
        slotLayout.minHeight = size;
        slotLayout.preferredHeight = size;
        slotLayout.flexibleHeight = 0f;
        Image background = container.GetComponent<Image>();
        bool backgroundIsItem = background != null && container.itemImage != null
            && background.GetInstanceID() == container.itemImage.GetInstanceID();
        if (background != null && !backgroundIsItem)
        {
            if (showBackground)
            {
                MakeFlat(background, BetterUITheme.Slot, BetterUITheme.BorderSoft);
            }
            else
            {
                background.enabled = false;
                var outlines = background.GetComponents<Outline>();
                for (int i = 0; i < outlines.Length; i++) if (outlines[i] != null) outlines[i].enabled = false;
            }
        }

        if (container.itemImage != null)
        {
            container.itemImage.preserveAspect = true;
            container.itemImage.color = Color.white;
            RectTransform itemRect = container.itemImage.rectTransform;
            itemRect.anchorMin = Vector2.zero;
            itemRect.anchorMax = Vector2.one;
            itemRect.offsetMin = new Vector2(7f, 7f);
            itemRect.offsetMax = new Vector2(-7f, -7f);
        }

        if (container.countBackgroundImage != null)
        {
            container.countBackgroundImage.color = BetterUITheme.CountBackground;
            BetterUIStyler.RoundedImage(container.countBackgroundImage, BetterUITheme.CountBackground);
            RectTransform badge = container.countBackgroundImage.rectTransform;
            badge.anchorMin = badge.anchorMax = new Vector2(1f, 0f);
            badge.pivot = new Vector2(1f, 0f);
            badge.anchoredPosition = new Vector2(-3f, 3f);
            badge.sizeDelta = new Vector2(27f, 21f);
            badge.SetAsLastSibling();
        }

        if (container.itemCountTmp != null)
        {
            BetterUIStyler.Text(container.itemCountTmp, 12f, BetterUITheme.Text, FontStyles.Bold);
            container.itemCountTmp.alignment = TextAlignmentOptions.Center;
            RectTransform count = container.itemCountTmp.rectTransform;
            count.anchorMin = Vector2.zero;
            count.anchorMax = Vector2.one;
            count.offsetMin = Vector2.zero;
            count.offsetMax = Vector2.zero;
        }
    }

    private void RefreshPalette(BuildTemplatesUI building)
    {
        if (_builder.ReplacementRoot == null) return;
        BuildRecipeUI.canBuildColor = BetterUITheme.CraftingReady;
        BuildRecipeUI.cantBuildColor = BetterUITheme.PanelElevated;
        BuildRecipeUI.selectedColor = BetterUITheme.CraftingSelected;
        var images = _builder.ReplacementRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null) continue;
            switch (image.name)
            {
                case "BetterUI_BuildMenu": image.color = BetterUITheme.CraftingCanvas; break;
                case "BetterUI_BuildHeader":
                case "BetterUI_BuildBrowser":
                case "BetterUI_BuildFooter": image.color = BetterUITheme.Panel; break;
                case "BetterUI_BuildCategories":
                case "BetterUI_BuildDetails":
                case "BetterUI_BuildBrowserHeader": image.color = BetterUITheme.PanelElevated; break;
                case "BetterUI_BuildScroll":
                case "BetterUI_BuildDetailsHeader":
                case "BetterUI_BuildSummary": image.color = BetterUITheme.CraftingWell; break;
                case "BetterUI_BuildRequirements":
                case "BetterUI_BuildThemeButton":
                case "BetterUI_BuildScrollbar": image.color = BetterUITheme.PanelSoft; break;
                case "Handle": image.color = BetterUITheme.Accent; break;
            }

            Outline outline = image.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = image.name == "BetterUI_BuildMenu"
                    ? BetterUITheme.Accent
                    : image.name == "Handle"
                        ? new Color(1f, 1f, 1f, 0.22f)
                        : BetterUITheme.BorderSoft;
            }
        }

        var labels = _builder.ReplacementRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (label == null) continue;
            switch (label.name)
            {
                case "BetterUI_BuildTitle": label.color = BetterUITheme.Text; break;
                case "BetterUI_BuildStatus": label.color = BetterUITheme.Accent; break;
                case "BetterUI_BuildCategoriesHeading":
                case "BetterUI_BuildBrowserHeading":
                case "BetterUI_BuildDetailsHeading":
                case "BetterUI_BuildMaterialsHeading":
                case "BetterUI_BuildFooterPrompt":
                case "BetterUI_BuildSelectPrompt": label.color = BetterUITheme.TextMuted; break;
                case "BetterUI_BuildCategoryHint": label.color = BetterUITheme.TextDim; break;
            }
        }

        BetterUIStyler.InputField(building._searchInputField);
        BetterUIStyler.Scrollbars(_builder.ReplacementRoot.transform);
        _configuredRecipes.Clear();
        _configuredIngredients.Clear();
    }

}
