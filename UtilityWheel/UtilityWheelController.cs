using System;
using System.Collections.Generic;
using Game.Data;
using Game.PlayerOperations;
using Game.UI;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace InfernoProtocol.UtilityWheel;

public sealed class UtilityWheelController : MonoBehaviour
{
    private sealed class WeaponEntry
    {
        internal int Slot;
        internal string Name;
        internal Sprite Icon;
    }

    private sealed class SlotVisual
    {
        internal Image Sector;
        internal Image Panel;
        internal Image Icon;
        internal Image CurrentMarker;
        internal RectTransform Rect;
    }

    private readonly List<WeaponEntry> _weapons = new();
    private readonly List<SlotVisual> _slotVisuals = new();
    private readonly List<InputActionMap> _disabledMaps = new();
    private readonly Dictionary<int, Sprite[]> _sectorSprites = new();
    private CanvasGroup _canvasGroup;
    private RectTransform _wheelRoot;
    private Image _scrim;
    private Image _wheelBackdrop;
    private Image _centerPanel;
    private TextMeshProUGUI _nameLabel;
    private TextMeshProUGUI _hintLabel;
    private TextMeshProUGUI _titleLabel;
    private Sprite _circleSprite;
    private WheelColors _colors;
    private int _selectedIndex = -1;
    private bool _open;
    private bool _suppressUntilRelease;
    private bool _built;
    private float _targetAlpha;

    internal static bool IsActivationHeld { get; private set; }
    internal static bool IsCommittingSelection { get; private set; }
    internal static bool ActivationControlIsPressed
    {
        get
        {
            Gamepad gamepad = Gamepad.current;
            bool held = gamepad != null &&
                (UtilityWheelPlugin.ControllerButton.Value == ControllerWheelButton.RightShoulder
                    ? gamepad.rightShoulder.isPressed
                    : gamepad.leftShoulder.isPressed);
            Keyboard keyboard = Keyboard.current;
            return held || (UtilityWheelPlugin.KeyboardFallback.Value && keyboard != null && keyboard.tabKey.isPressed);
        }
    }

    public UtilityWheelController(IntPtr pointer) : base(pointer)
    {
    }

    private void Update()
    {
        ReadActivation(out bool held, out bool pressed);
        IsActivationHeld = held;

        if (_suppressUntilRelease)
        {
            if (!held)
            {
                _suppressUntilRelease = false;
            }

            FadeOverlay();
            return;
        }

        if (!UtilityWheelPlugin.Enabled.Value)
        {
            if (_open)
            {
                Close(false);
            }

            FadeOverlay();
            return;
        }

        if (!_open && pressed && CanOpen())
        {
            Open();
        }

        if (_open)
        {
            Gamepad gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonEast.wasPressedThisFrame)
            {
                Close(false);
                _suppressUntilRelease = held;
            }
            else if (!held)
            {
                Close(true);
            }
            else if (!CanRemainOpen())
            {
                Close(false);
                _suppressUntilRelease = true;
            }
            else
            {
                UpdateSelection();
            }
        }

        FadeOverlay();
    }

    private void OnDestroy()
    {
        if (_open)
        {
            RestoreInputMaps(true);
        }

        foreach (Sprite[] sprites in _sectorSprites.Values)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null)
                {
                    UnityEngine.Object.Destroy(sprites[i].texture);
                    UnityEngine.Object.Destroy(sprites[i]);
                }
            }
        }

        if (_circleSprite != null)
        {
            UnityEngine.Object.Destroy(_circleSprite.texture);
            UnityEngine.Object.Destroy(_circleSprite);
        }

        IsActivationHeld = false;
        IsCommittingSelection = false;
    }

    private void Open()
    {
        if (!_built)
        {
            BuildOverlay();
        }

        _colors = UtilityWheelTheme.Load();
        CollectWeapons();
        ApplyPalette();
        BuildSlots();
        _open = true;
        _targetAlpha = 1f;
        _canvasGroup.gameObject.SetActive(true);
        DisableInputMaps();
        UpdateSelectionText();
    }

    private void Close(bool equip)
    {
        if (!_open)
        {
            return;
        }

        _open = false;
        _targetAlpha = 0f;
        RestoreInputMaps(!Cursor.visible);
        if (!equip || _selectedIndex < 0 || _selectedIndex >= _weapons.Count)
        {
            return;
        }

        try
        {
            IsCommittingSelection = true;
            Player.OnInventorySlotSelected((byte)_weapons[_selectedIndex].Slot);
        }
        catch (Exception exception)
        {
            UtilityWheelPlugin.ModLog.LogWarning($"Could not equip wheel selection: {exception.Message}");
        }
        finally
        {
            IsCommittingSelection = false;
        }
    }

    private static bool CanOpen()
    {
        try
        {
            return Player.isLocalPlayerLoaded && Player.localPlayer != null && !Cursor.visible &&
                   !PlayerInventoryUI.isOpen && !SkillWheelManager.isOpen;
        }
        catch
        {
            return false;
        }
    }

    private static bool CanRemainOpen()
    {
        try
        {
            return Player.localPlayer != null && !Cursor.visible && !PlayerInventoryUI.isOpen;
        }
        catch
        {
            return false;
        }
    }

    private static void ReadActivation(out bool held, out bool pressed)
    {
        held = false;
        pressed = false;
        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            ButtonControl control = UtilityWheelPlugin.ControllerButton.Value == ControllerWheelButton.RightShoulder
                ? gamepad.rightShoulder
                : gamepad.leftShoulder;
            held = control.isPressed;
            pressed = control.wasPressedThisFrame;
        }

        Keyboard keyboard = Keyboard.current;
        if (UtilityWheelPlugin.KeyboardFallback.Value && keyboard != null)
        {
            held |= keyboard.tabKey.isPressed;
            pressed |= keyboard.tabKey.wasPressedThisFrame;
        }
    }

    private void CollectWeapons()
    {
        _weapons.Clear();
        Player player = Player.localPlayer;
        if (player == null || player.inventory == null)
        {
            return;
        }

        int hotbarSlotCount = player.inventory.Count;
        var hotbarSlots = Player.inventorySlotsUI;
        if (hotbarSlots != null && hotbarSlots.Length > 0)
        {
            hotbarSlotCount = Mathf.Min(hotbarSlotCount, hotbarSlots.Length);
        }

        for (int slot = 0; slot < hotbarSlotCount; slot++)
        {
            ItemData data = player.inventory[slot];
            if (data.count == 0)
            {
                continue;
            }

            ItemDatabaseEntry entry;
            try
            {
                entry = ItemDatabase.GetItemInfo(data.item);
            }
            catch
            {
                continue;
            }

            if (entry == null)
            {
                continue;
            }

            string displayName;
            try
            {
                displayName = ItemDatabase.GetItemName(data.item);
            }
            catch
            {
                displayName = data.item.ToString();
            }

            _weapons.Add(new WeaponEntry
            {
                Slot = slot,
                Name = string.IsNullOrWhiteSpace(displayName) ? data.item.ToString() : displayName,
                Icon = entry.icon
            });
        }

        byte currentSlot = Player.localCurrentInventorySlot;
        _selectedIndex = _weapons.FindIndex(weapon => weapon.Slot == currentSlot);
    }

    private void UpdateSelection()
    {
        if (_weapons.Count == 0)
        {
            return;
        }

        Vector2 direction = Vector2.zero;
        if (Gamepad.current != null)
        {
            direction = Gamepad.current.rightStick.ReadValue();
        }
        else if (Mouse.current != null)
        {
            direction = Mouse.current.position.ReadValue() - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            direction /= Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) * 0.35f);
        }

        if (direction.magnitude < UtilityWheelPlugin.StickDeadzone.Value)
        {
            return;
        }

        float clockwiseAngle = Mathf.Repeat(Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg, 360f);
        float step = 360f / _weapons.Count;
        int next = Mathf.FloorToInt((clockwiseAngle + (step * 0.5f)) / step) % _weapons.Count;
        if (next == _selectedIndex)
        {
            return;
        }

        _selectedIndex = next;
        RefreshHighlights();
        UpdateSelectionText();
    }

    private void BuildOverlay()
    {
        GameObject canvasObject = new(
            "UtilityWheel_Canvas",
            Il2CppType.Of<RectTransform>(),
            Il2CppType.Of<Canvas>(),
            Il2CppType.Of<CanvasScaler>(),
            Il2CppType.Of<CanvasGroup>());
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _canvasGroup = canvasObject.GetComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        Stretch(canvasRect);
        _scrim = CreateImage("ScreenScrim", canvasRect, null);
        Stretch(_scrim.rectTransform);

        GameObject wheelObject = new("Wheel", Il2CppType.Of<RectTransform>());
        wheelObject.transform.SetParent(canvasRect, false);
        _wheelRoot = wheelObject.GetComponent<RectTransform>();
        _wheelRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _wheelRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _wheelRoot.pivot = new Vector2(0.5f, 0.5f);
        _wheelRoot.sizeDelta = new Vector2(620f, 620f);
        _wheelRoot.anchoredPosition = Vector2.zero;

        _circleSprite = CreateCircleSprite();
        _wheelBackdrop = CreateImage("WheelBackdrop", _wheelRoot, _circleSprite);
        SetCenteredSize(_wheelBackdrop.rectTransform, 600f, 600f);

        _centerPanel = CreateImage("CenterPanel", _wheelRoot, _circleSprite);
        SetCenteredSize(_centerPanel.rectTransform, 220f, 220f);

        _titleLabel = CreateText("Title", _centerPanel.rectTransform, 14f, FontStyles.Bold);
        _titleLabel.text = "UTILITY WHEEL";
        SetAnchored(_titleLabel.rectTransform, new Vector2(0f, 46f), new Vector2(190f, 26f));

        _nameLabel = CreateText("WeaponName", _centerPanel.rectTransform, 22f, FontStyles.Bold);
        SetAnchored(_nameLabel.rectTransform, new Vector2(0f, 6f), new Vector2(190f, 58f));

        _hintLabel = CreateText("Hint", _centerPanel.rectTransform, 11f, FontStyles.Normal);
        _hintLabel.text = $"RELEASE  {ButtonHint}  TO  EQUIP\nB  CANCEL";
        SetAnchored(_hintLabel.rectTransform, new Vector2(0f, -52f), new Vector2(190f, 38f));

        canvasObject.SetActive(false);
        _built = true;
    }

    private void BuildSlots()
    {
        for (int i = 0; i < _slotVisuals.Count; i++)
        {
            if (_slotVisuals[i].Sector != null)
            {
                UnityEngine.Object.Destroy(_slotVisuals[i].Sector.gameObject);
            }

            if (_slotVisuals[i].Panel != null)
            {
                UnityEngine.Object.Destroy(_slotVisuals[i].Panel.gameObject);
            }
        }

        _slotVisuals.Clear();
        if (_weapons.Count == 0)
        {
            _nameLabel.text = "HOTBAR EMPTY";
            _hintLabel.text = "ADD AN ITEM TO YOUR HOTBAR\nB  CANCEL";
            return;
        }

        Sprite[] sectors = GetSectorSprites(_weapons.Count);
        float step = 360f / _weapons.Count;
        for (int i = 0; i < _weapons.Count; i++)
        {
            Image sector = CreateImage("Sector_" + i, _wheelRoot, sectors[i]);
            SetCenteredSize(sector.rectTransform, 590f, 590f);
            sector.transform.SetAsFirstSibling();

            float angle = i * step * Mathf.Deg2Rad;
            Vector2 position = new(Mathf.Sin(angle) * 208f, Mathf.Cos(angle) * 208f);
            Image panel = CreateImage("Weapon_" + i, _wheelRoot, _circleSprite);
            SetAnchored(panel.rectTransform, position, new Vector2(86f, 86f));

            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;

            Image icon = CreateImage("Icon", panel.rectTransform, _weapons[i].Icon);
            SetCenteredSize(icon.rectTransform, 62f, 62f);
            icon.preserveAspect = true;

            Image marker = CreateImage("CurrentMarker", panel.rectTransform, _circleSprite);
            SetAnchored(marker.rectTransform, new Vector2(0f, -36f), new Vector2(9f, 9f));

            _slotVisuals.Add(new SlotVisual
            {
                Sector = sector,
                Panel = panel,
                Icon = icon,
                CurrentMarker = marker,
                Rect = panel.rectTransform
            });
        }

        _centerPanel.transform.SetAsLastSibling();
        RefreshHighlights();
    }

    private void ApplyPalette()
    {
        _scrim.color = new Color(0f, 0f, 0f, 0.34f);
        _wheelBackdrop.color = new Color(_colors.Panel.r, _colors.Panel.g, _colors.Panel.b, 0.28f);
        _centerPanel.color = _colors.Panel;
        _titleLabel.color = _colors.Accent;
        _nameLabel.color = _colors.Text;
        _hintLabel.color = _colors.Muted;
    }

    private void RefreshHighlights()
    {
        int currentSlot = Player.localCurrentInventorySlot;
        for (int i = 0; i < _slotVisuals.Count; i++)
        {
            bool selected = i == _selectedIndex;
            bool current = i < _weapons.Count && _weapons[i].Slot == currentSlot;
            SlotVisual visual = _slotVisuals[i];
            visual.Sector.color = selected
                ? _colors.AccentSoft
                : new Color(_colors.PanelSoft.r, _colors.PanelSoft.g, _colors.PanelSoft.b, _colors.PanelSoft.a * 0.66f);
            visual.Panel.color = selected
                ? new Color(_colors.Accent.r, _colors.Accent.g, _colors.Accent.b, 0.42f)
                : _colors.PanelSoft;
            visual.Icon.color = selected ? Color.white : new Color(1f, 1f, 1f, 0.78f);
            visual.CurrentMarker.color = _colors.Accent;
            visual.CurrentMarker.gameObject.SetActive(current);
            visual.Rect.localScale = selected ? Vector3.one * 1.16f : Vector3.one;
            Outline outline = visual.Panel.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = selected ? _colors.Accent : new Color(_colors.Muted.r, _colors.Muted.g, _colors.Muted.b, 0.45f);
            }
        }
    }

    private void UpdateSelectionText()
    {
        if (_selectedIndex >= 0 && _selectedIndex < _weapons.Count)
        {
            _nameLabel.text = _weapons[_selectedIndex].Name.ToUpperInvariant();
            _hintLabel.text = $"RELEASE  {ButtonHint}  TO  EQUIP\nB  CANCEL";
        }
        else if (_weapons.Count > 0)
        {
            _nameLabel.text = "SELECT ITEM";
            _hintLabel.text = "RIGHT STICK TO SELECT\nB  CANCEL";
        }
    }

    private void DisableInputMaps()
    {
        _disabledMaps.Clear();
        DisableMap(PlayerInputDispatcher.movementMap);
        DisableMap(PlayerInputDispatcher.inventoryMap);
        DisableMap(PlayerInputDispatcher.skillMap);
        DisableMap(PlayerInputDispatcher.drawingMap);
        DisableMap(PlayerInputDispatcher.placeableMap);
    }

    private void DisableMap(InputActionMap map)
    {
        if (map != null && map.enabled)
        {
            map.Disable();
            _disabledMaps.Add(map);
        }
    }

    private void RestoreInputMaps(bool restore)
    {
        if (restore)
        {
            for (int i = 0; i < _disabledMaps.Count; i++)
            {
                InputActionMap map = _disabledMaps[i];
                if (map != null)
                {
                    map.Enable();
                }
            }
        }

        _disabledMaps.Clear();
    }

    private void FadeOverlay()
    {
        if (!_built || _canvasGroup == null || !_canvasGroup.gameObject.activeSelf)
        {
            return;
        }

        _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, _targetAlpha, Time.unscaledDeltaTime * 9f);
        if (!_open && _canvasGroup.alpha <= 0.001f)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.gameObject.SetActive(false);
        }
    }

    private static string ButtonHint =>
        UtilityWheelPlugin.ControllerButton.Value == ControllerWheelButton.RightShoulder ? "RB" : "LB";

    private Sprite[] GetSectorSprites(int count)
    {
        if (_sectorSprites.TryGetValue(count, out Sprite[] sprites))
        {
            return sprites;
        }

        sprites = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            sprites[i] = CreateSectorSprite(count, i);
        }

        _sectorSprites[count] = sprites;
        return sprites;
    }

    private static Sprite CreateSectorSprite(int count, int index)
    {
        const int size = 256;
        float step = 360f / count;
        float centerAngle = index * step;
        float halfWidth = Mathf.Max(2f, (step * 0.5f) - 1.25f);
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = $"UtilityWheel_Sector_{count}_{index}",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color32[] pixels = new Color32[size * size];
        Vector2 center = new(size * 0.5f, size * 0.5f);
        float outer = (size * 0.5f) - 2f;
        float inner = outer * 0.39f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 delta = new((x + 0.5f) - center.x, (y + 0.5f) - center.y);
                float radius = delta.magnitude;
                float angle = Mathf.Repeat(Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg, 360f);
                float angularEdge = halfWidth - Mathf.Abs(Mathf.DeltaAngle(angle, centerAngle));
                float radialEdge = Mathf.Min(outer - radius, radius - inner);
                float alpha = Mathf.Clamp01(Mathf.Min(angularEdge * 0.85f, radialEdge));
                pixels[(y * size) + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateCircleSprite()
    {
        const int size = 64;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = "UtilityWheel_Circle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color32[] pixels = new Color32[size * size];
        Vector2 center = new(size * 0.5f, size * 0.5f);
        float radius = (size * 0.5f) - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float alpha = Mathf.Clamp01(radius + 0.75f - distance);
                pixels[(y * size) + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite)
    {
        GameObject gameObject = new(
            name,
            Il2CppType.Of<RectTransform>(),
            Il2CppType.Of<CanvasRenderer>(),
            Il2CppType.Of<Image>());
        gameObject.transform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, float size, FontStyles style)
    {
        GameObject gameObject = new(
            name,
            Il2CppType.Of<RectTransform>(),
            Il2CppType.Of<CanvasRenderer>(),
            Il2CppType.Of<TextMeshProUGUI>());
        gameObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.characterSpacing = 1.1f;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetCenteredSize(RectTransform rect, float width, float height)
    {
        SetAnchored(rect, Vector2.zero, new Vector2(width, height));
    }

    private static void SetAnchored(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
