using System;
using System.Collections.Generic;
using System.Reflection;
using Game.UI;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace FrankMods;

public sealed class ModSettingsController : MonoBehaviour
{
    internal const string PauseButtonName = "FrankMods_ModSettingsButton";
    private static Color CanvasColor;
    private static Color PanelColor;
    private static Color RowColor;
    private static Color Accent;
    private static Color TextColor;
    private static Color DimText;
    private readonly List<GameObject> _dynamicObjects = new();
    private readonly List<ClickTarget> _clickTargets = new();
    private RectTransform _overlay;
    private RectTransform _modList;
    private RectTransform _settingsList;
    private MazeButton _pauseButton;
    private string _selectedMod;
    private KeyBindingSetting _listeningKey;
    private GamepadBindingSetting _listeningGamepad;
    private TextMeshProUGUI _status;
    private RectTransform _captureScrim;
    private TextMeshProUGUI _captureTitle;
    private TextMeshProUGUI _captureAction;
    private int _palette = -1;
    private bool _built;
    private bool _open;
    private bool _previousCursorVisible;
    private CursorLockMode _previousCursorLock;

    public ModSettingsController(IntPtr pointer) : base(pointer) { }

    [HideFromIl2Cpp]
    internal void AttachPauseMenu(MainMenuVerticalButtons menu)
    {
        if (menu == null || menu.settingsButton == null || ModSettingsRegistry.Count == 0) return;
        if (_pauseButton != null && _pauseButton.transform.parent == menu.settingsButton.transform.parent) return;
        Transform parent = menu.settingsButton.transform.parent;
        Transform existing = parent.Find(PauseButtonName);
        if (existing != null)
        {
            _pauseButton = existing.GetComponent<MazeButton>();
        }
        else
        {
            GameObject clone = Instantiate(menu.settingsButton.gameObject, parent);
            clone.name = PauseButtonName;
            clone.transform.SetSiblingIndex(menu.settingsButton.transform.GetSiblingIndex() + 1);
            _pauseButton = clone.GetComponent<MazeButton>();
            clone.SetActive(true);
        }
        ApplyPauseButtonLabel();
    }

    [HideFromIl2Cpp]
    internal void RegistryChanged()
    {
        if (_pauseButton != null) _pauseButton.gameObject.SetActive(ModSettingsRegistry.Count > 0);
        if (_open) RebuildContent();
    }

    [HideFromIl2Cpp]
    internal void Open()
    {
        if (ModSettingsRegistry.Count == 0) return;
        RefreshTheme();
        EnsureUi();
        _previousCursorVisible = Cursor.visible;
        _previousCursorLock = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        _open = true;
        _listeningKey = null;
        _listeningGamepad = null;
        if (_captureScrim != null) _captureScrim.gameObject.SetActive(false);
        _overlay.gameObject.SetActive(true);
        RebuildContent();
    }

    [HideFromIl2Cpp]
    internal void Cleanup()
    {
        Close();
        if (_overlay != null) Destroy(_overlay.gameObject);
        _overlay = null;
        if (_pauseButton != null) Destroy(_pauseButton.gameObject);
        _pauseButton = null;
    }

    private void Update()
    {
        ApplyPauseButtonLabel();
        if (!_open) return;
        Keyboard keyboard = Keyboard.current;
        if (_listeningKey != null)
        {
            CaptureKey(keyboard);
            return;
        }
        if (_listeningGamepad != null)
        {
            CaptureGamepad(keyboard, Gamepad.current);
            return;
        }
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
        Vector2 position = mouse.position.ReadValue();
        for (int i = _clickTargets.Count - 1; i >= 0; i--)
        {
            ClickTarget target = _clickTargets[i];
            if (target.Rect != null && RectTransformUtility.RectangleContainsScreenPoint(target.Rect, position, null))
            {
                target.Action();
                return;
            }
        }
    }

    private void CaptureKey(Keyboard keyboard)
    {
        if (keyboard == null) return;
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            _listeningKey = null;
            RebuildContent("Remap cancelled.");
            return;
        }
        if (!keyboard.anyKey.wasPressedThisFrame) return;
        var keys = keyboard.allKeys;
        for (int i = 0; i < keys.Count; i++)
        {
            KeyControl control = keys[i];
            if (control == null || !control.wasPressedThisFrame || control.keyCode is Key.None or Key.Escape) continue;
            KeyBindingSetting binding = _listeningKey;
            binding.Write(control.keyCode);
            _listeningKey = null;
            RebuildContent(binding.DisplayName + " set to " + KeyName(control.keyCode) + ".");
            return;
        }
    }

    private void CaptureGamepad(Keyboard keyboard, Gamepad gamepad)
    {
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            _listeningGamepad = null;
            RebuildContent("Remap cancelled.");
            return;
        }
        if (gamepad == null) return;
        for (int i = 0; i < ControllerButtons.Length; i++)
        {
            GamepadButton button = ControllerButtons[i];
            try
            {
                if (!gamepad[button].wasPressedThisFrame) continue;
                GamepadBindingSetting binding = _listeningGamepad;
                binding.Write(button);
                _listeningGamepad = null;
                RebuildContent(binding.DisplayName + " set to " + GamepadButtonName(button) + ".");
                return;
            }
            catch { }
        }
    }

    [HideFromIl2Cpp]
    private void Close()
    {
        if (!_open) return;
        _open = false;
        _listeningKey = null;
        _listeningGamepad = null;
        if (_overlay != null) _overlay.gameObject.SetActive(false);
        Cursor.visible = _previousCursorVisible;
        Cursor.lockState = _previousCursorLock;
    }

    private void EnsureUi()
    {
        if (_built) return;
        GameObject canvasObject = new("FrankMods_SettingsCanvas", Il2CppType.Of<RectTransform>(),
            Il2CppType.Of<Canvas>(), Il2CppType.Of<CanvasScaler>());
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32600;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _overlay = canvasObject.GetComponent<RectTransform>();
        Stretch(_overlay);

        Image backdrop = Image("Backdrop", _overlay, new Color(0f, 0f, 0f, 0.76f));
        Stretch(backdrop.rectTransform);
        RectTransform frame = Image("Frame", _overlay, CanvasColor).rectTransform;
        Center(frame, Vector2.zero, new Vector2(1060f, 650f));
        AddOutline(frame.gameObject, Accent, 2f);
        Text("Title", frame, "FRANKMODS // MOD SETTINGS", 25f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft,
            TextColor, new Vector2(-18f, 278f), new Vector2(960f, 48f));
        Text("Subtitle", frame, "Installed FrankMods register their controls here automatically.", 12f,
            FontStyles.Normal, TextAlignmentOptions.MidlineLeft, DimText, new Vector2(-18f, 247f), new Vector2(960f, 26f));

        RectTransform left = Image("InstalledMods", frame, PanelColor).rectTransform;
        Center(left, new Vector2(-375f, -8f), new Vector2(270f, 470f));
        AddOutline(left.gameObject, new Color(0.18f, 0.39f, 0.24f, 1f), 1f);
        Text("ModsHeading", left, "INSTALLED MODS", 13f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft,
            DimText, new Vector2(0f, 205f), new Vector2(232f, 30f));
        _modList = Rect("ModRows", left);
        Center(_modList, new Vector2(0f, -18f), new Vector2(238f, 390f));

        RectTransform right = Image("Settings", frame, PanelColor).rectTransform;
        Center(right, new Vector2(151f, -8f), new Vector2(750f, 470f));
        AddOutline(right.gameObject, new Color(0.18f, 0.39f, 0.24f, 1f), 1f);
        _settingsList = Rect("SettingsRows", right);
        Stretch(_settingsList);

        RectTransform close = Button("Close", frame, "CLOSE", new Vector2(420f, -292f), new Vector2(132f, 40f), Close);
        _clickTargets.Add(new ClickTarget(close, Close));
        _status = Text("Status", frame, "Select a mod to configure its controls.", 11f, FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft, DimText, new Vector2(-116f, -292f), new Vector2(720f, 34f));

        _captureScrim = Image("KeyCaptureScrim", frame, new Color(0f, 0f, 0f, 0.82f)).rectTransform;
        Stretch(_captureScrim);
        RectTransform captureCard = Image("KeyCaptureCard", _captureScrim, CanvasColor).rectTransform;
        Center(captureCard, Vector2.zero, new Vector2(650f, 250f));
        AddOutline(captureCard.gameObject, Accent, 3f);
        _captureTitle = Text("CaptureTitle", captureCard, "PRESS A KEY", 34f, FontStyles.Bold, TextAlignmentOptions.Center,
            Accent, new Vector2(0f, 60f), new Vector2(590f, 58f));
        _captureAction = Text("CaptureAction", captureCard, string.Empty, 18f, FontStyles.Bold,
            TextAlignmentOptions.Center, TextColor, new Vector2(0f, 5f), new Vector2(590f, 42f));
        Text("CaptureHint", captureCard, "The next input becomes the new binding  //  ESCAPE CANCELS", 12f,
            FontStyles.Normal, TextAlignmentOptions.Center, DimText, new Vector2(0f, -65f), new Vector2(590f, 32f));
        _captureScrim.gameObject.SetActive(false);
        _overlay.gameObject.SetActive(false);
        _built = true;
    }

    [HideFromIl2Cpp]
    private void RebuildContent(string message = null)
    {
        if (_captureScrim != null) _captureScrim.gameObject.SetActive(false);
        for (int i = 0; i < _dynamicObjects.Count; i++) if (_dynamicObjects[i] != null) Destroy(_dynamicObjects[i]);
        _dynamicObjects.Clear();
        _clickTargets.RemoveAll(target => target.Dynamic);
        IReadOnlyList<KeyBindingSetting> bindings = ModSettingsRegistry.Bindings;
        IReadOnlyList<GamepadBindingSetting> controllerBindings = ModSettingsRegistry.ControllerBindings;
        var mods = new List<string>();
        for (int i = 0; i < bindings.Count; i++) if (!mods.Contains(bindings[i].ModName)) mods.Add(bindings[i].ModName);
        for (int i = 0; i < controllerBindings.Count; i++) if (!mods.Contains(controllerBindings[i].ModName)) mods.Add(controllerBindings[i].ModName);
        mods.Sort(StringComparer.OrdinalIgnoreCase);
        if (mods.Count == 0) { Close(); return; }
        if (string.IsNullOrEmpty(_selectedMod) || !mods.Contains(_selectedMod)) _selectedMod = mods[0];

        for (int i = 0; i < mods.Count; i++)
        {
            string mod = mods[i];
            bool selected = mod == _selectedMod;
            RectTransform row = Button("Mod_" + i, _modList, mod.ToUpperInvariant(),
                new Vector2(0f, 160f - (i * 54f)), new Vector2(220f, 44f), () => SelectMod(mod), selected);
            Track(row.gameObject, row, () => SelectMod(mod));
        }

        TextMeshProUGUI heading = Text("SelectedMod", _settingsList, _selectedMod.ToUpperInvariant(), 19f,
            FontStyles.Bold, TextAlignmentOptions.MidlineLeft, TextColor, new Vector2(0f, 202f), new Vector2(690f, 40f));
        _dynamicObjects.Add(heading.gameObject);
        TextMeshProUGUI keyboardHeading = Text("KeyboardHeading", _settingsList, "KEYBOARD", 12f,
            FontStyles.Bold, TextAlignmentOptions.MidlineLeft, DimText, new Vector2(0f, 163f), new Vector2(690f, 26f));
        _dynamicObjects.Add(keyboardHeading.gameObject);
        int rowIndex = 0;
        for (int i = 0; i < bindings.Count; i++)
        {
            KeyBindingSetting binding = bindings[i];
            if (binding.ModName != _selectedMod) continue;
            float y = 116f - (rowIndex++ * 66f);
            RectTransform row = Image("Setting_" + binding.SettingId, _settingsList, RowColor).rectTransform;
            Center(row, new Vector2(0f, y), new Vector2(690f, 62f));
            AddOutline(row.gameObject, new Color(0.13f, 0.29f, 0.18f, 1f), 1f);
            _dynamicObjects.Add(row.gameObject);
            Text("Label", row, binding.DisplayName, 14f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft,
                TextColor, new Vector2(-183f, 0f), new Vector2(290f, 34f));
            Text("Value", row, KeyName(binding.Read()), 14f, FontStyles.Bold, TextAlignmentOptions.Center,
                Accent, new Vector2(62f, 0f), new Vector2(100f, 34f));
            RectTransform remap = Button("Remap", row, "REMAP", new Vector2(190f, 0f), new Vector2(100f, 38f),
                () => BeginListening(binding));
            Track(remap.gameObject, remap, () => BeginListening(binding), false);
            RectTransform reset = Button("Reset", row, "RESET", new Vector2(292f, 0f), new Vector2(80f, 38f),
                () => ResetBinding(binding));
            Track(reset.gameObject, reset, () => ResetBinding(binding), false);
        }

        float controllerHeaderY = 116f - (rowIndex * 66f) - 41f;
        TextMeshProUGUI controllerHeading = Text("ControllerHeading", _settingsList, "CONTROLLER", 12f,
            FontStyles.Bold, TextAlignmentOptions.MidlineLeft, DimText, new Vector2(0f, controllerHeaderY), new Vector2(690f, 26f));
        _dynamicObjects.Add(controllerHeading.gameObject);
        int controllerRowIndex = 0;
        for (int i = 0; i < controllerBindings.Count; i++)
        {
            GamepadBindingSetting binding = controllerBindings[i];
            if (binding.ModName != _selectedMod) continue;
            float y = controllerHeaderY - 47f - (controllerRowIndex++ * 66f);
            RectTransform row = Image("ControllerSetting_" + binding.SettingId, _settingsList, RowColor).rectTransform;
            Center(row, new Vector2(0f, y), new Vector2(690f, 62f));
            AddOutline(row.gameObject, new Color(0.13f, 0.29f, 0.18f, 1f), 1f);
            _dynamicObjects.Add(row.gameObject);
            Text("Label", row, binding.DisplayName, 14f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft,
                TextColor, new Vector2(-183f, 0f), new Vector2(290f, 34f));
            Text("Value", row, GamepadButtonName(binding.Read()), 14f, FontStyles.Bold, TextAlignmentOptions.Center,
                Accent, new Vector2(62f, 0f), new Vector2(100f, 34f));
            RectTransform remap = Button("Remap", row, "REMAP", new Vector2(190f, 0f), new Vector2(100f, 38f),
                () => BeginControllerListening(binding));
            Track(remap.gameObject, remap, () => BeginControllerListening(binding), false);
            RectTransform reset = Button("Reset", row, "RESET", new Vector2(292f, 0f), new Vector2(80f, 38f),
                () => ResetControllerBinding(binding));
            Track(reset.gameObject, reset, () => ResetControllerBinding(binding), false);
        }
        if (controllerRowIndex == 0)
        {
            TextMeshProUGUI native = Text("NativeController", _settingsList,
                "Uses native game controls / no separate controller shortcut.", 12f, FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft, DimText, new Vector2(0f, controllerHeaderY - 43f), new Vector2(690f, 30f));
            _dynamicObjects.Add(native.gameObject);
        }
        _status.text = message ?? "Choose REMAP, then press the keyboard key or controller button you want.";
    }

    [HideFromIl2Cpp]
    private void SelectMod(string mod)
    {
        _selectedMod = mod;
        _listeningKey = null;
        _listeningGamepad = null;
        RebuildContent();
    }

    [HideFromIl2Cpp]
    private void BeginListening(KeyBindingSetting binding)
    {
        _listeningKey = binding;
        _listeningGamepad = null;
        _status.text = "Waiting for a new key for " + binding.DisplayName + "...  Escape cancels.";
        if (_captureTitle != null) _captureTitle.text = "PRESS A KEY";
        if (_captureAction != null) _captureAction.text = binding.DisplayName.ToUpperInvariant();
        if (_captureScrim != null) _captureScrim.gameObject.SetActive(true);
    }

    [HideFromIl2Cpp]
    private void ResetBinding(KeyBindingSetting binding)
    {
        binding.Write(binding.DefaultKey);
        _listeningKey = null;
        RebuildContent(binding.DisplayName + " reset to " + KeyName(binding.DefaultKey) + ".");
    }

    [HideFromIl2Cpp]
    private void BeginControllerListening(GamepadBindingSetting binding)
    {
        _listeningGamepad = binding;
        _listeningKey = null;
        _status.text = "Waiting for a controller button for " + binding.DisplayName + "...  Escape cancels.";
        if (_captureTitle != null) _captureTitle.text = "PRESS A CONTROLLER BUTTON";
        if (_captureAction != null) _captureAction.text = binding.DisplayName.ToUpperInvariant();
        if (_captureScrim != null) _captureScrim.gameObject.SetActive(true);
    }

    [HideFromIl2Cpp]
    private void ResetControllerBinding(GamepadBindingSetting binding)
    {
        binding.Write(binding.DefaultButton);
        _listeningGamepad = null;
        RebuildContent(binding.DisplayName + " reset to " + GamepadButtonName(binding.DefaultButton) + ".");
    }

    private void ApplyPauseButtonLabel()
    {
        if (_pauseButton != null && _pauseButton.buttonText != null && _pauseButton.buttonText.text != "MOD SETTINGS")
            _pauseButton.buttonText.text = "MOD SETTINGS";
    }

    [HideFromIl2Cpp]
    private void Track(GameObject gameObject, RectTransform rect, Action action, bool trackObject = true)
    {
        if (trackObject) _dynamicObjects.Add(gameObject);
        _clickTargets.Add(new ClickTarget(rect, action, true));
    }

    private static string KeyName(Key key)
    {
        string value = key.ToString();
        if (value.StartsWith("Digit", StringComparison.Ordinal)) return value.Substring(5);
        if (value.StartsWith("Numpad", StringComparison.Ordinal)) return "NUM " + value.Substring(6).ToUpperInvariant();
        return value.ToUpperInvariant();
    }

    private static readonly GamepadButton[] ControllerButtons =
    {
        GamepadButton.South, GamepadButton.East, GamepadButton.West, GamepadButton.North,
        GamepadButton.LeftShoulder, GamepadButton.RightShoulder,
        GamepadButton.LeftTrigger, GamepadButton.RightTrigger,
        GamepadButton.LeftStick, GamepadButton.RightStick,
        GamepadButton.Start, GamepadButton.Select,
        GamepadButton.DpadUp, GamepadButton.DpadDown, GamepadButton.DpadLeft, GamepadButton.DpadRight
    };

    internal static string GamepadButtonName(GamepadButton button) => button switch
    {
        GamepadButton.South => "A",
        GamepadButton.East => "B",
        GamepadButton.West => "X",
        GamepadButton.North => "Y",
        GamepadButton.LeftShoulder => "LB",
        GamepadButton.RightShoulder => "RB",
        GamepadButton.LeftTrigger => "LT",
        GamepadButton.RightTrigger => "RT",
        GamepadButton.LeftStick => "L3",
        GamepadButton.RightStick => "R3",
        GamepadButton.Start => "MENU",
        GamepadButton.Select => "VIEW",
        GamepadButton.DpadUp => "D-PAD UP",
        GamepadButton.DpadDown => "D-PAD DOWN",
        GamepadButton.DpadLeft => "D-PAD LEFT",
        GamepadButton.DpadRight => "D-PAD RIGHT",
        _ => button.ToString().ToUpperInvariant()
    };

    [HideFromIl2Cpp]
    private void RefreshTheme()
    {
        int palette = ReadBetterUIPalette();
        if (_palette == palette) return;
        _palette = palette;
        float shift = palette switch { 1 => .25f, 2 => .13f, 3 => .43f, 4 => -.23f, _ => 0f };
        CanvasColor = Shifted("03100A", .985f, shift);
        PanelColor = Shifted("071B10", 1f, shift);
        RowColor = Shifted("0A2515", 1f, shift);
        Accent = Shifted("57DB6E", 1f, shift);
        TextColor = Shifted("DBF2DE", 1f, shift);
        DimText = Shifted("7DA684", 1f, shift);
        if (!_built) return;
        _overlay.gameObject.SetActive(false);
        Destroy(_overlay.gameObject);
        _overlay = null;
        _modList = null;
        _settingsList = null;
        _captureScrim = null;
        _captureTitle = null;
        _captureAction = null;
        _status = null;
        _dynamicObjects.Clear();
        _clickTargets.Clear();
        _built = false;
    }

    private static int ReadBetterUIPalette()
    {
        try
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (!string.Equals(assemblies[i].GetName().Name, "InfernoProtocol.BetterUI", StringComparison.Ordinal)) continue;
                Type plugin = assemblies[i].GetType("InfernoProtocol.BetterUI.BetterUIPlugin", false);
                PropertyInfo property = plugin?.GetProperty("PaletteIndex", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (property?.GetValue(null) is int value) return Mathf.Clamp(value, 0, 4);
                break;
            }
        }
        catch (Exception exception)
        {
            FrankModsCorePlugin.ModLog?.LogDebug("Could not read BetterUI palette: " + exception.Message);
        }
        return 0;
    }

    private static Color Shifted(string hex, float alpha, float shift)
    {
        if (!ColorUtility.TryParseHtmlString("#" + hex, out Color color)) color = Color.white;
        if (Mathf.Abs(shift) > .001f)
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            color = Color.HSVToRGB(Mathf.Repeat(hue + shift, 1f), saturation, value);
        }
        color.a = alpha;
        return color;
    }

    [HideFromIl2Cpp]
    private static RectTransform Button(string name, Transform parent, string label, Vector2 position, Vector2 size,
        Action action, bool selected = false)
    {
        RectTransform rect = Image(name, parent, selected ? new Color(0.12f, 0.32f, 0.18f, 1f) : RowColor).rectTransform;
        Center(rect, position, size);
        AddOutline(rect.gameObject, selected ? Accent : new Color(0.18f, 0.39f, 0.24f, 1f), selected ? 2f : 1f);
        Text("Text", rect, label, 12f, FontStyles.Bold, TextAlignmentOptions.Center,
            selected ? Color.white : TextColor, Vector2.zero, size - new Vector2(12f, 6f));
        return rect;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        GameObject gameObject = new(name, Il2CppType.Of<RectTransform>());
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private static Image Image(string name, Transform parent, Color color)
    {
        GameObject gameObject = new(name, Il2CppType.Of<RectTransform>(), Il2CppType.Of<CanvasRenderer>(), Il2CppType.Of<Image>());
        gameObject.transform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, string value, float size, FontStyles style,
        TextAlignmentOptions alignment, Color color, Vector2 position, Vector2 dimensions)
    {
        GameObject gameObject = new(name, Il2CppType.Of<RectTransform>(), Il2CppType.Of<CanvasRenderer>(), Il2CppType.Of<TextMeshProUGUI>());
        gameObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        Center(text.rectTransform, position, dimensions);
        return text;
    }

    private static void AddOutline(GameObject gameObject, Color color, float distance)
    {
        Outline outline = gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(distance, -distance);
        outline.useGraphicAlpha = true;
    }

    private static void Center(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private sealed class ClickTarget
    {
        internal ClickTarget(RectTransform rect, Action action, bool dynamic = false)
        {
            Rect = rect;
            Action = action;
            Dynamic = dynamic;
        }
        internal RectTransform Rect { get; }
        internal Action Action { get; }
        internal bool Dynamic { get; }
    }
}
