using System;
using System.Collections.Generic;
using Game.PlayerOperations;
using Game.UI;
using Il2CppInterop.Runtime;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Unity.Netcode;

namespace InfernoProtocol.BetterLights;

public sealed class BetterLightsController : MonoBehaviour
{
    private static readonly Color[] PresetColors =
    {
        new(1f, 0.43f, 0.12f, 1f),
        new(1f, 0.12f, 0.09f, 1f),
        new(0.12f, 0.95f, 0.32f, 1f),
        new(0.12f, 0.68f, 1f, 1f),
        new(0.48f, 0.2f, 1f, 1f),
        new(1f, 0.28f, 0.78f, 1f),
        new(1f, 1f, 1f, 1f)
    };

    private readonly List<LightTarget> _targets = new();
    private readonly List<LightTarget> _nearbyTargets = new();
    private readonly Dictionary<int, LightTarget> _targetsByRoot = new();
    private readonly Dictionary<string, Color> _defaultColors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Color> _networkColors = new(StringComparer.Ordinal);
    private readonly List<InputActionMap> _disabledMaps = new();
    private readonly HashSet<ulong> _compatibleClients = new();
    private readonly HashSet<ulong> _pendingSnapshotClients = new();

    private const string HelloMessage = "BetterLights.Hello.v1";
    private const string ColorMessage = "BetterLights.Color.v1";
    private const string AckMessage = "BetterLights.Ack.v1";
    private const byte SyncProtocol = 1;

    private RectTransform _canvasRect;
    private RectTransform _promptRoot;
    private RectTransform _pickerRoot;
    private CanvasGroup _promptGroup;
    private CanvasGroup _pickerGroup;
    private Image _promptPanel;
    private Image _promptSwatch;
    private Image _pickerPanel;
    private Image _selectedSwatch;
    private RawImage _colorWheel;
    private RectTransform _selectorOuter;
    private RectTransform _selectorInner;
    private Image _saveButton;
    private Image _originalButton;
    private Image _cancelButton;
    private TextMeshProUGUI _promptTitle;
    private TextMeshProUGUI _promptName;
    private TextMeshProUGUI _pickerName;
    private TextMeshProUGUI _hexLabel;
    private TextMeshProUGUI _detailLabel;
    private TextMeshProUGUI _hintLabel;
    private Sprite _circleSprite;
    private readonly List<Image> _presetDots = new();
    private LightTarget _target;
    private string _targetKey;
    private Color _currentColor;
    private Color _originalColor;
    private float _hue;
    private float _saturation;
    private float _nextWorldScan;
    private float _nextNearbyScan;
    private float _nextTargetScan;
    private int _lastWorldTargetCount = -1;
    private int _stableWorldScans;
    private bool _worldRegistryReady;
    private bool _built;
    private bool _open;
    private bool _resetPending;
    private bool _mouseMode;
    private bool _mouseDraggingWheel;
    private bool _cursorStateCaptured;
    private bool _cursorWasVisible;
    private CursorLockMode _cursorLockMode;
    private int _presetIndex;
    private int _lastLoggedTargetCount = -1;
    private float _confirmationUntil;
    private string _confirmationText;
    private NetworkManager _networkManager;
    private CustomMessagingManager _messaging;
    private CustomMessagingManager.HandleNamedMessageDelegate _helloHandler;
    private CustomMessagingManager.HandleNamedMessageDelegate _colorHandler;
    private CustomMessagingManager.HandleNamedMessageDelegate _ackHandler;
    private float _nextNetworkCheck;
    private float _nextHelloAttempt;
    private bool _syncAcknowledged;
    private bool _networkFailureReported;

    public BetterLightsController(IntPtr pointer) : base(pointer)
    {
    }

    private void Update()
    {
        if (!_built)
        {
            BuildUi();
        }

        if (!BetterLightsPlugin.Enabled.Value)
        {
            if (_open)
            {
                ClosePicker(true);
            }

            HideUi();
            return;
        }

        UpdateNetworkSync();
        UpdateLightRegistry();

        if (_open)
        {
            UpdatePicker();
            return;
        }

        UpdateTargeting();
    }

    private void UpdateTargeting()
    {
        if (!CanTarget())
        {
            _target = null;
            _promptGroup.alpha = 0f;
            return;
        }

        float now = Time.unscaledTime;
        if (now >= _nextNearbyScan)
        {
            _nextNearbyScan = now + 0.45f;
            RefreshNearbyTargets();
        }
        if (now >= _nextTargetScan)
        {
            _nextTargetScan = now + 0.14f;
            _target = FindTarget();
            RefreshPrompt();
        }

        if (_target == null)
        {
            _promptGroup.alpha = 0f;
            return;
        }

        PositionPrompt(_target);
        _promptGroup.alpha = Mathf.MoveTowards(_promptGroup.alpha, 1f, Time.unscaledDeltaTime * 10f);
        if (TryGetActivation(out bool mouseMode))
        {
            OpenPicker(mouseMode);
        }
    }

    private void UpdateLightRegistry()
    {
        if (!Player.isLocalPlayerLoaded) return;
        float now = Time.unscaledTime;
        if (!_worldRegistryReady)
        {
            if (now < _nextWorldScan) return;
            _nextWorldScan = now + 2f;
            RefreshTorches();
            if (_targets.Count > 0 && _targets.Count == _lastWorldTargetCount) _stableWorldScans++;
            else _stableWorldScans = 0;
            _lastWorldTargetCount = _targets.Count;
            _worldRegistryReady = _stableWorldScans >= 1;
            return;
        }

        // Placed torches are discovered directly by the targeting ray and remote
        // color messages request a refresh only when their target is genuinely new.
    }

    private void RefreshTorches()
    {
        _targets.Clear();
        _nearbyTargets.Clear();
        _targetsByRoot.Clear();
        APlacable[] found = UnityEngine.Object.FindObjectsOfType<APlacable>(true);
        for (int i = 0; found != null && i < found.Length; i++)
        {
            LightTarget target = TorchColorService.CreateTorchTarget(found[i]);
            if (target == null)
            {
                continue;
            }

            AddTarget(target);
        }

        Light[] lights = UnityEngine.Object.FindObjectsOfType<Light>(true);
        for (int i = 0; lights != null && i < lights.Length; i++)
        {
            LightTarget target = TorchColorService.CreateLanternTarget(lights[i]);
            if (target == null)
            {
                continue;
            }

            AddTarget(target);
        }

        if (_targets.Count != _lastLoggedTargetCount)
        {
            _lastLoggedTargetCount = _targets.Count;
            BetterLightsPlugin.ModLog.LogInfo($"Found {_targets.Count} customizable torch and lantern lights.");
        }

        FlushPendingSnapshots();
    }

    private bool AddTarget(LightTarget target)
    {
        if (target?.Root == null) return false;
        int rootId = target.Root.GetInstanceID();
        if (_targetsByRoot.ContainsKey(rootId)) return false;
        _targetsByRoot.Add(rootId, target);
        _targets.Add(target);
        if (_networkColors.TryGetValue(target.Key, out Color networkColor))
        {
            RememberDefaultColor(target);
            TorchColorService.Apply(target, networkColor);
        }
        else if (!IsRemoteClient() && BetterLightsPlugin.TryGetTorchColor(target.Key, out Color savedColor))
        {
            RememberDefaultColor(target);
            TorchColorService.Apply(target, savedColor);
        }
        return true;
    }

    private void RememberDefaultColor(LightTarget target)
    {
        if (!_defaultColors.ContainsKey(target.Key)) _defaultColors[target.Key] = TorchColorService.ReadColor(target);
    }

    private void RefreshNearbyTargets()
    {
        _nearbyTargets.Clear();
        Camera camera = Player.mainCamera;
        if (camera == null) return;
        Vector3 origin = camera.transform.position;
        float radius = BetterLightsPlugin.InteractionRange.Value + 1.5f;
        float radiusSquared = radius * radius;
        for (int i = 0; i < _targets.Count; i++)
        {
            LightTarget target = _targets[i];
            if (target == null || !target.IsValid) continue;
            Vector3 offset = TorchColorService.GetVisualPosition(target) - origin;
            if (offset.sqrMagnitude <= radiusSquared) _nearbyTargets.Add(target);
        }
    }

    private LightTarget FindTarget()
    {
        Camera camera = Player.mainCamera;
        Transform playerTransform = Player.localTransform;
        if (camera == null || playerTransform == null)
        {
            return null;
        }

        float maximumDistance = BetterLightsPlugin.InteractionRange.Value;
        float minimumDot = BetterLightsPlugin.AimThreshold.Value;
        float bestScore = float.MaxValue;
        LightTarget best = null;
        Ray ray = new(camera.transform.position, camera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maximumDistance))
        {
            APlacable placable = hit.collider != null ? hit.collider.GetComponentInParent<APlacable>() : null;
            LightTarget direct = null;
            if (TorchColorService.IsSupported(placable))
            {
                int rootId = placable.transform.GetInstanceID();
                if (!_targetsByRoot.TryGetValue(rootId, out direct)) direct = TorchColorService.CreateTorchTarget(placable);
            }
            if (direct != null)
            {
                AddTarget(direct);
                best = direct;
                bestScore = Vector3.Distance(camera.transform.position, TorchColorService.GetVisualPosition(best));
            }
        }
        for (int i = 0; i < _nearbyTargets.Count; i++)
        {
            LightTarget target = _nearbyTargets[i];
            if (target == null || !target.IsValid)
            {
                continue;
            }

            Vector3 position = TorchColorService.GetVisualPosition(target);
            Vector3 offset = position - camera.transform.position;
            float distance = offset.magnitude;
            if (distance <= 0.01f || distance > maximumDistance)
            {
                continue;
            }

            float dot = Vector3.Dot(camera.transform.forward, offset / distance);
            if (dot < minimumDot)
            {
                continue;
            }

            Vector3 viewport = camera.WorldToViewportPoint(position);
            if (viewport.z <= 0f || viewport.x < 0.04f || viewport.x > 0.96f || viewport.y < 0.05f || viewport.y > 0.95f)
            {
                continue;
            }

            float score = distance + ((1f - dot) * 5f);
            if (score < bestScore)
            {
                bestScore = score;
                best = target;
            }
        }

        return best;
    }

    private void RefreshPrompt()
    {
        if (_target == null)
        {
            return;
        }

        Color color = TorchColorService.ReadColor(_target);
        _promptSwatch.color = color;
        _promptPanel.GetComponent<Outline>().effectColor = WithAlpha(color, 0.76f);
        _promptTitle.text = Time.unscaledTime < _confirmationUntil
            ? _confirmationText
            : "R3 / F7 / MMB   LIGHT COLOR";
        float distance = Player.mainCamera != null
            ? Vector3.Distance(Player.mainCamera.transform.position, TorchColorService.GetVisualPosition(_target))
            : 0f;
        _promptName.text = $"{_target.Name}  //  {distance:0.0}m";
    }

    private void PositionPrompt(LightTarget target)
    {
        Camera camera = Player.mainCamera;
        if (camera == null)
        {
            return;
        }

        Vector3 screen = camera.WorldToScreenPoint(TorchColorService.GetVisualPosition(target));
        float x = Mathf.Clamp(screen.x + 116f, 130f, Screen.width - 130f);
        float y = Mathf.Clamp(screen.y + 28f, 40f, Screen.height - 40f);
        _promptRoot.position = new Vector3(x, y, 0f);
    }

    private void OpenPicker(bool mouseMode)
    {
        if (_target == null)
        {
            return;
        }

        _targetKey = _target.Key;
        _originalColor = TorchColorService.ReadColor(_target);
        _currentColor = _originalColor;
        Color.RGBToHSV(_currentColor, out _hue, out _saturation, out _);
        _presetIndex = FindNearestPreset(_currentColor);
        _resetPending = false;
        _mouseMode = false;
        _mouseDraggingWheel = false;
        _open = true;
        _promptGroup.alpha = 0f;
        _pickerRoot.gameObject.SetActive(true);
        _pickerGroup.alpha = 1f;
        PositionPicker(_target);
        DisableInputMaps();
        if (mouseMode)
        {
            EnableMouseMode();
        }
        else if (_hintLabel != null)
        {
            _hintLabel.text = "RIGHT STICK COLOR  //  D-PAD PRESETS";
        }

        RefreshPickerVisuals();
    }

    private void UpdatePicker()
    {
        if (_target == null || !CanRemainOpen())
        {
            ClosePicker(true);
            return;
        }

        Gamepad gamepad = Gamepad.current;
        Keyboard keyboard = Keyboard.current;
        if (HandleMouseInput())
        {
            return;
        }

        Vector2 stick = gamepad != null ? gamepad.rightStick.ReadValue() : Vector2.zero;
        float deadzone = BetterLightsPlugin.StickDeadzone.Value;
        bool changed = false;
        if (stick.magnitude >= deadzone)
        {
            _hue = Mathf.Repeat(Mathf.Atan2(stick.y, stick.x) / (Mathf.PI * 2f), 1f);
            _saturation = Mathf.Clamp01((stick.magnitude - deadzone) / Mathf.Max(0.01f, 1f - deadzone));
            changed = true;
        }

        if (BetterLightsPlugin.KeyboardFallback.Value && keyboard != null)
        {
            float hueDirection = (keyboard.rightArrowKey.isPressed ? 1f : 0f) -
                                 (keyboard.leftArrowKey.isPressed ? 1f : 0f);
            float saturationDirection = (keyboard.upArrowKey.isPressed ? 1f : 0f) -
                                        (keyboard.downArrowKey.isPressed ? 1f : 0f);
            if (Mathf.Abs(hueDirection) > 0.01f || Mathf.Abs(saturationDirection) > 0.01f)
            {
                _hue = Mathf.Repeat(_hue + (hueDirection * Time.unscaledDeltaTime * 0.32f), 1f);
                _saturation = Mathf.Clamp01(_saturation + (saturationDirection * Time.unscaledDeltaTime * 0.7f));
                changed = true;
            }
        }

        if (changed)
        {
            _resetPending = false;
            _presetIndex = -1;
            _currentColor = Color.HSVToRGB(_hue, _saturation, 1f);
            TorchColorService.Apply(_target, _currentColor);
            RefreshPickerVisuals();
        }

        if (gamepad != null && (gamepad.dpad.left.wasPressedThisFrame || gamepad.dpad.right.wasPressedThisFrame))
        {
            int direction = gamepad.dpad.right.wasPressedThisFrame ? 1 : -1;
            CyclePreset(direction);
        }

        if ((gamepad != null && gamepad.leftStickButton.wasPressedThisFrame))
        {
            ResetToOriginalColor();
        }

        if ((gamepad != null && gamepad.buttonEast.wasPressedThisFrame) ||
            (keyboard != null && keyboard.escapeKey.wasPressedThisFrame))
        {
            ClosePicker(true);
            return;
        }

        if ((gamepad != null && gamepad.rightStickButton.wasPressedThisFrame) ||
            (BetterLightsPlugin.KeyboardFallback.Value && keyboard != null && keyboard.f7Key.wasPressedThisFrame))
        {
            ConfirmPicker();
        }
    }

    private bool HandleMouseInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return false;
        }

        if (!_mouseMode && mouse.delta.ReadValue().sqrMagnitude > 2f)
        {
            EnableMouseMode();
        }

        if (!_mouseMode)
        {
            return false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Vector2 pointer = mouse.position.ReadValue();
        bool pressed = mouse.leftButton.wasPressedThisFrame;
        UpdateMouseButtonVisual(_saveButton, pointer);
        UpdateMouseButtonVisual(_originalButton, pointer);
        UpdateMouseButtonVisual(_cancelButton, pointer);

        if (pressed && ContainsPoint(_saveButton != null ? _saveButton.rectTransform : null, pointer))
        {
            ConfirmPicker();
            return true;
        }

        if (pressed && ContainsPoint(_originalButton != null ? _originalButton.rectTransform : null, pointer))
        {
            ResetToOriginalColor();
            return false;
        }

        if (pressed && ContainsPoint(_cancelButton != null ? _cancelButton.rectTransform : null, pointer))
        {
            ClosePicker(true);
            return true;
        }

        if (pressed)
        {
            for (int i = 0; i < _presetDots.Count; i++)
            {
                Image dot = _presetDots[i];
                if (dot != null && ContainsPoint(dot.rectTransform, pointer, 6f))
                {
                    SelectPreset(i);
                    return false;
                }
            }
        }

        if (pressed && IsPointInsideWheel(pointer))
        {
            _mouseDraggingWheel = true;
        }

        if (_mouseDraggingWheel && mouse.leftButton.isPressed)
        {
            SetColorFromPointer(pointer);
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            _mouseDraggingWheel = false;
        }

        return false;
    }

    private void EnableMouseMode()
    {
        if (!_cursorStateCaptured)
        {
            _cursorWasVisible = Cursor.visible;
            _cursorLockMode = Cursor.lockState;
            _cursorStateCaptured = true;
        }

        _mouseMode = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (_hintLabel != null)
        {
            _hintLabel.text = "CLICK + DRAG THE WHEEL  //  CLICK A PRESET";
        }
    }

    private void RestoreCursorState()
    {
        if (_cursorStateCaptured)
        {
            Cursor.lockState = _cursorLockMode;
            Cursor.visible = _cursorWasVisible;
        }

        _cursorStateCaptured = false;
        _mouseMode = false;
    }

    private bool IsPointInsideWheel(Vector2 pointer)
    {
        if (_colorWheel == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _colorWheel.rectTransform,
                pointer,
                null,
                out Vector2 local))
        {
            return false;
        }

        float radius = Mathf.Min(_colorWheel.rectTransform.rect.width, _colorWheel.rectTransform.rect.height) * 0.5f;
        return local.magnitude <= radius;
    }

    private void SetColorFromPointer(Vector2 pointer)
    {
        if (_colorWheel == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _colorWheel.rectTransform,
                pointer,
                null,
                out Vector2 local))
        {
            return;
        }

        float radius = Mathf.Min(_colorWheel.rectTransform.rect.width, _colorWheel.rectTransform.rect.height) * 0.46f;
        float magnitude = Mathf.Min(local.magnitude, radius);
        _hue = magnitude > 0.5f
            ? Mathf.Repeat(Mathf.Atan2(local.y, local.x) / (Mathf.PI * 2f), 1f)
            : _hue;
        _saturation = Mathf.Clamp01(magnitude / Mathf.Max(1f, radius));
        _resetPending = false;
        _presetIndex = -1;
        _currentColor = Color.HSVToRGB(_hue, _saturation, 1f);
        TorchColorService.Apply(_target, _currentColor);
        RefreshPickerVisuals();
    }

    private void SelectPreset(int index)
    {
        if (index < 0 || index >= PresetColors.Length)
        {
            return;
        }

        _presetIndex = index;
        _currentColor = PresetColors[index];
        Color.RGBToHSV(_currentColor, out _hue, out _saturation, out _);
        _resetPending = false;
        TorchColorService.Apply(_target, _currentColor);
        RefreshPickerVisuals();
    }

    private static bool ContainsPoint(RectTransform rect, Vector2 pointer, float padding = 0f)
    {
        if (rect == null)
        {
            return false;
        }

        if (padding <= 0f)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(rect, pointer, null);
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, pointer, null, out Vector2 local))
        {
            return false;
        }

        Rect bounds = rect.rect;
        bounds.xMin -= padding;
        bounds.xMax += padding;
        bounds.yMin -= padding;
        bounds.yMax += padding;
        return bounds.Contains(local);
    }

    private void UpdateMouseButtonVisual(Image button, Vector2 pointer)
    {
        if (button == null)
        {
            return;
        }

        bool hovered = ContainsPoint(button.rectTransform, pointer);
        button.color = hovered
            ? WithAlpha(_currentColor, 0.42f)
            : new Color(0.045f, 0.06f, 0.052f, 0.92f);
        Outline outline = button.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = hovered ? Color.white : WithAlpha(_currentColor, 0.55f);
        }
    }

    private void ResetToOriginalColor()
    {
        if (!_defaultColors.TryGetValue(_targetKey, out _currentColor))
        {
            _currentColor = _originalColor;
        }

        _currentColor.a = 1f;
        Color.RGBToHSV(_currentColor, out _hue, out _saturation, out _);
        _resetPending = true;
        _presetIndex = FindNearestPreset(_currentColor);
        TorchColorService.Apply(_target, _currentColor);
        RefreshPickerVisuals();
    }

    private void CyclePreset(int direction)
    {
        if (_presetIndex < 0)
        {
            _presetIndex = FindNearestPreset(_currentColor);
        }

        SelectPreset((_presetIndex + direction + PresetColors.Length) % PresetColors.Length);
    }

    private static int FindNearestPreset(Color color)
    {
        int nearest = 0;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < PresetColors.Length; i++)
        {
            Color preset = PresetColors[i];
            float distance = ((preset.r - color.r) * (preset.r - color.r)) +
                             ((preset.g - color.g) * (preset.g - color.g)) +
                             ((preset.b - color.b) * (preset.b - color.b));
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = i;
            }
        }

        return nearest;
    }

    private void ConfirmPicker()
    {
        if (_resetPending)
        {
            BetterLightsPlugin.RemoveTorchColor(_targetKey);
        }
        else
        {
            BetterLightsPlugin.SaveTorchColor(_targetKey, _currentColor);
        }

        _confirmationText = _resetPending ? "ORIGINAL COLOR RESTORED" : $"SAVED  {_hexLabel.text}";
        _confirmationUntil = Time.unscaledTime + 1.35f;
        BetterLightsPlugin.ModLog.LogInfo($"Saved {_target.Name} color {_hexLabel.text}.");
        BroadcastColor(_targetKey, _currentColor);
        ClosePicker(false);
    }

    private void ClosePicker(bool restoreOriginal)
    {
        if (restoreOriginal && _target != null)
        {
            TorchColorService.Apply(_target, _originalColor);
        }

        _open = false;
        _mouseDraggingWheel = false;
        _pickerGroup.alpha = 0f;
        _pickerRoot.gameObject.SetActive(false);
        RestoreCursorState();
        RestoreInputMaps();
        _nextTargetScan = 0f;
    }

    private void RefreshPickerVisuals()
    {
        _selectedSwatch.color = _currentColor;
        _pickerPanel.GetComponent<Outline>().effectColor = WithAlpha(_currentColor, 0.84f);
        _pickerName.text = _target != null ? _target.Name : "LIGHT";
        _hexLabel.text = "#" + ColorUtility.ToHtmlStringRGB(_currentColor);
        _detailLabel.text = $"HUE  {Mathf.RoundToInt(_hue * 360f):000}°    SAT  {Mathf.RoundToInt(_saturation * 100f):000}%";

        float angle = _hue * Mathf.PI * 2f;
        Vector2 position = new(Mathf.Cos(angle) * _saturation * 113f, Mathf.Sin(angle) * _saturation * 113f);
        _selectorOuter.anchoredPosition = position;
        _selectorInner.anchoredPosition = Vector2.zero;
        _selectorInner.GetComponent<Image>().color = _currentColor;
        for (int i = 0; i < _presetDots.Count; i++)
        {
            RectTransform rect = _presetDots[i].rectTransform;
            rect.localScale = i == _presetIndex ? Vector3.one * 1.3f : Vector3.one;
            Outline outline = _presetDots[i].GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = i == _presetIndex ? Color.white : new Color(1f, 1f, 1f, 0.2f);
            }
        }
    }

    private void PositionPicker(LightTarget target)
    {
        Camera camera = Player.mainCamera;
        Vector3 screen = camera != null
            ? camera.WorldToScreenPoint(TorchColorService.GetVisualPosition(target))
            : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        float x = Mathf.Clamp(screen.x + 215f, 195f, Screen.width - 195f);
        float y = Mathf.Clamp(screen.y, 228f, Screen.height - 228f);
        _pickerRoot.position = new Vector3(x, y, 0f);
    }

    private bool CanTarget()
    {
        try
        {
            return !IsRemoteClient() && Player.isLocalPlayerLoaded && Player.localPlayer != null && Player.mainCamera != null &&
                   !Cursor.visible && !PlayerInventoryUI.isOpen && !SkillWheelManager.isOpen &&
                   GameObject.Find("Renovator_InputBlocker") == null;
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
            return Player.localPlayer != null && !PlayerInventoryUI.isOpen &&
                   GameObject.Find("Renovator_InputBlocker") == null;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetActivation(out bool mouseMode)
    {
        mouseMode = false;
        Gamepad gamepad = Gamepad.current;
        if (gamepad != null && gamepad.rightStickButton.wasPressedThisFrame)
        {
            return true;
        }

        Keyboard keyboard = Keyboard.current;
        if (BetterLightsPlugin.KeyboardFallback.Value && keyboard != null && keyboard.f7Key.wasPressedThisFrame)
        {
            mouseMode = Mouse.current != null;
            return true;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.middleButton.wasPressedThisFrame)
        {
            mouseMode = true;
            return true;
        }

        return false;
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

    private void RestoreInputMaps()
    {
        for (int i = 0; i < _disabledMaps.Count; i++)
        {
            InputActionMap map = _disabledMaps[i];
            if (map != null)
            {
                map.Enable();
            }
        }

        _disabledMaps.Clear();
    }

    private void UpdateNetworkSync()
    {
        float now = Time.unscaledTime;
        if (now < _nextNetworkCheck)
        {
            return;
        }

        _nextNetworkCheck = now + 0.75f;
        try
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening || manager.CustomMessagingManager == null)
            {
                ResetNetworkSync();
                return;
            }

            if (_networkManager == null || _networkManager.GetInstanceID() != manager.GetInstanceID())
            {
                ResetNetworkSync();
                _networkManager = manager;
                _messaging = manager.CustomMessagingManager;
                _helloHandler = DelegateSupport.ConvertDelegate<CustomMessagingManager.HandleNamedMessageDelegate>(
                    new Action<ulong, FastBufferReader>(OnHelloMessage));
                _colorHandler = DelegateSupport.ConvertDelegate<CustomMessagingManager.HandleNamedMessageDelegate>(
                    new Action<ulong, FastBufferReader>(OnColorMessage));
                _ackHandler = DelegateSupport.ConvertDelegate<CustomMessagingManager.HandleNamedMessageDelegate>(
                    new Action<ulong, FastBufferReader>(OnAckMessage));
                _messaging.RegisterNamedMessageHandler(HelloMessage, _helloHandler);
                _messaging.RegisterNamedMessageHandler(ColorMessage, _colorHandler);
                _messaging.RegisterNamedMessageHandler(AckMessage, _ackHandler);
                _networkFailureReported = false;
                BetterLightsPlugin.ModLog.LogInfo("BetterLights multiplayer color sync layer ready.");
            }

            if (manager.IsClient && !manager.IsServer && manager.IsConnectedClient &&
                !_syncAcknowledged && now >= _nextHelloAttempt)
            {
                SendHello();
            }
        }
        catch (Exception exception)
        {
            if (!_networkFailureReported)
            {
                _networkFailureReported = true;
                BetterLightsPlugin.ModLog.LogWarning($"BetterLights multiplayer sync is unavailable: {exception.Message}");
            }
        }
    }

    private void SendHello()
    {
        if (_messaging == null)
        {
            return;
        }

        FastBufferWriter writer = new(1, Allocator.Temp, 1);
        try
        {
            writer.WriteByte(SyncProtocol);
            _messaging.SendNamedMessage(
                HelloMessage,
                NetworkManager.ServerClientId,
                writer,
                NetworkDelivery.ReliableSequenced);
            _nextHelloAttempt = Time.unscaledTime + 2f;
        }
        finally
        {
            writer.Dispose();
        }
    }

    private void OnHelloMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (_networkManager == null || !_networkManager.IsServer || senderClientId == _networkManager.LocalClientId)
        {
            return;
        }

        if (reader.Length < 1) return;
        reader.ReadByte(out byte protocol);
        if (protocol != SyncProtocol) return;

        SendAck(senderClientId);
        if (_compatibleClients.Add(senderClientId))
        {
            BetterLightsPlugin.ModLog.LogInfo($"BetterLights client {senderClientId} joined light color sync.");
        }

        if (_targets.Count > 0)
        {
            SendSnapshot(senderClientId);
        }
        else
        {
            _pendingSnapshotClients.Add(senderClientId);
        }
    }

    private void SendAck(ulong clientId)
    {
        if (_messaging == null) return;
        FastBufferWriter writer = new(1, Allocator.Temp, 1);
        try
        {
            writer.WriteByte(SyncProtocol);
            _messaging.SendNamedMessage(AckMessage, clientId, writer, NetworkDelivery.ReliableSequenced);
        }
        finally
        {
            writer.Dispose();
        }
    }

    private void OnAckMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (_networkManager == null || !_networkManager.IsClient || _networkManager.IsServer ||
            senderClientId != NetworkManager.ServerClientId || reader.Length < 1) return;
        reader.ReadByte(out byte protocol);
        if (protocol != SyncProtocol || _syncAcknowledged) return;
        _syncAcknowledged = true;
        BetterLightsPlugin.ModLog.LogInfo("Connected to host BetterLights color sync.");
    }

    private void OnColorMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (_networkManager == null || !_networkManager.IsClient || _networkManager.IsServer ||
            senderClientId != NetworkManager.ServerClientId)
        {
            return;
        }

        reader.ReadValue(out string key, true);
        reader.ReadValue(out Color color);
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        color.a = 1f;
        _networkColors[key] = color;
        if (!ApplyNetworkColor(key, color) && _worldRegistryReady)
        {
            _worldRegistryReady = false;
            _stableWorldScans = 0;
            _lastWorldTargetCount = -1;
            _nextWorldScan = 0f;
        }
    }

    private void SendSnapshot(ulong clientId)
    {
        for (int i = 0; i < _targets.Count; i++)
        {
            LightTarget target = _targets[i];
            if (target != null && target.IsValid)
            {
                if (BetterLightsPlugin.TryGetTorchColor(target.Key, out Color savedColor))
                    SendColor(clientId, target.Key, savedColor);
            }
        }
    }

    private void FlushPendingSnapshots()
    {
        if (_targets.Count == 0 || _pendingSnapshotClients.Count == 0)
        {
            return;
        }

        foreach (ulong clientId in _pendingSnapshotClients)
        {
            SendSnapshot(clientId);
        }

        _pendingSnapshotClients.Clear();
    }

    private void BroadcastColor(string key, Color color)
    {
        UpdateNetworkSync();
        if (_networkManager == null || !_networkManager.IsServer || _messaging == null)
        {
            return;
        }

        var disconnected = new List<ulong>();
        foreach (ulong clientId in _compatibleClients)
        {
            if (!SendColor(clientId, key, color))
            {
                disconnected.Add(clientId);
            }
        }

        for (int i = 0; i < disconnected.Count; i++)
        {
            _compatibleClients.Remove(disconnected[i]);
        }
    }

    private bool SendColor(ulong clientId, string key, Color color)
    {
        if (_messaging == null || string.IsNullOrEmpty(key))
        {
            return false;
        }

        int capacity = Mathf.Max(256, (key.Length * 2) + 64);
        FastBufferWriter writer = new(capacity, Allocator.Temp, capacity);
        try
        {
            writer.WriteValue(key, true);
            writer.WriteValue(ref color);
            _messaging.SendNamedMessage(
                ColorMessage,
                clientId,
                writer,
                NetworkDelivery.ReliableSequenced);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            writer.Dispose();
        }
    }

    private bool ApplyNetworkColor(string key, Color color)
    {
        bool applied = false;
        for (int i = 0; i < _targets.Count; i++)
        {
            LightTarget target = _targets[i];
            if (target != null && target.IsValid && string.Equals(target.Key, key, StringComparison.Ordinal))
            {
                TorchColorService.Apply(target, color);
                applied = true;
            }
        }
        return applied;
    }

    private bool IsRemoteClient()
    {
        try
        {
            NetworkManager manager = NetworkManager.Singleton;
            return manager != null && manager.IsListening && manager.IsClient && !manager.IsServer;
        }
        catch
        {
            return false;
        }
    }

    private void ResetNetworkSync()
    {
        if (_messaging != null)
        {
            try
            {
                _messaging.UnregisterNamedMessageHandler(HelloMessage);
                _messaging.UnregisterNamedMessageHandler(ColorMessage);
                _messaging.UnregisterNamedMessageHandler(AckMessage);
            }
            catch
            {
                // The owning NetworkManager may already be shutting down.
            }
        }

        _networkManager = null;
        _messaging = null;
        _helloHandler = null;
        _colorHandler = null;
        _ackHandler = null;
        _nextHelloAttempt = 0f;
        _syncAcknowledged = false;
        _compatibleClients.Clear();
        _pendingSnapshotClients.Clear();
        _targets.Clear();
        _nearbyTargets.Clear();
        _targetsByRoot.Clear();
        _worldRegistryReady = false;
        _stableWorldScans = 0;
        _lastWorldTargetCount = -1;
        _nextWorldScan = 0f;
        _nextNearbyScan = 0f;
        if (_networkColors.Count > 0)
        {
            _networkColors.Clear();
            _nextWorldScan = 0f;
        }
    }

    private void BuildUi()
    {
        GameObject canvasObject = new(
            "BetterLights_Canvas",
            Il2CppType.Of<RectTransform>(),
            Il2CppType.Of<Canvas>(),
            Il2CppType.Of<CanvasScaler>());
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31800;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _canvasRect = canvasObject.GetComponent<RectTransform>();
        Stretch(_canvasRect);
        _circleSprite = CreateCircleSprite();

        _promptPanel = CreateImage("Prompt", _canvasRect, null);
        _promptRoot = _promptPanel.rectTransform;
        SetCenteredSize(_promptRoot, 284f, 54f);
        _promptPanel.color = new Color(0.015f, 0.025f, 0.02f, 0.76f);
        Outline promptOutline = _promptPanel.gameObject.AddComponent<Outline>();
        promptOutline.effectDistance = new Vector2(1f, -1f);
        promptOutline.useGraphicAlpha = true;
        _promptGroup = _promptPanel.gameObject.AddComponent<CanvasGroup>();
        _promptGroup.alpha = 0f;
        _promptGroup.interactable = false;
        _promptGroup.blocksRaycasts = false;

        _promptSwatch = CreateImage("Swatch", _promptRoot, _circleSprite);
        SetAnchored(_promptSwatch.rectTransform, new Vector2(-115f, 0f), new Vector2(28f, 28f));
        _promptTitle = CreateText("Action", _promptRoot, 13f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        SetAnchored(_promptTitle.rectTransform, new Vector2(18f, 9f), new Vector2(218f, 22f));
        _promptTitle.color = Color.white;
        _promptName = CreateText("Name", _promptRoot, 10f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        SetAnchored(_promptName.rectTransform, new Vector2(18f, -11f), new Vector2(218f, 18f));
        _promptName.color = new Color(0.72f, 0.78f, 0.74f, 1f);

        _pickerPanel = CreateImage("Picker", _canvasRect, null);
        _pickerRoot = _pickerPanel.rectTransform;
        SetCenteredSize(_pickerRoot, 370f, 444f);
        _pickerPanel.color = new Color(0.01f, 0.018f, 0.014f, 0.86f);
        Outline pickerOutline = _pickerPanel.gameObject.AddComponent<Outline>();
        pickerOutline.effectDistance = new Vector2(1.5f, -1.5f);
        pickerOutline.useGraphicAlpha = true;
        _pickerGroup = _pickerPanel.gameObject.AddComponent<CanvasGroup>();
        _pickerGroup.interactable = false;
        _pickerGroup.blocksRaycasts = false;

        TextMeshProUGUI title = CreateText("Title", _pickerRoot, 14f, FontStyles.Bold, TextAlignmentOptions.Center);
        title.text = "BETTERLIGHTS // COLOR CONTROL";
        title.color = new Color(0.88f, 0.94f, 0.9f, 1f);
        SetAnchored(title.rectTransform, new Vector2(0f, 198f), new Vector2(338f, 28f));
        _pickerName = CreateText("TorchName", _pickerRoot, 11f, FontStyles.Normal, TextAlignmentOptions.Center);
        _pickerName.color = new Color(0.64f, 0.71f, 0.66f, 1f);
        SetAnchored(_pickerName.rectTransform, new Vector2(0f, 174f), new Vector2(320f, 20f));

        _colorWheel = CreateRawImage("ColorWheel", _pickerRoot, CreateColorWheelTexture());
        SetAnchored(_colorWheel.rectTransform, new Vector2(0f, 42f), new Vector2(246f, 246f));
        _selectorOuter = CreateImage("Selector", _colorWheel.rectTransform, _circleSprite).rectTransform;
        SetAnchored(_selectorOuter, Vector2.zero, new Vector2(20f, 20f));
        _selectorOuter.GetComponent<Image>().color = Color.white;
        _selectorInner = CreateImage("SelectorColor", _selectorOuter, _circleSprite).rectTransform;
        SetAnchored(_selectorInner, Vector2.zero, new Vector2(12f, 12f));

        _selectedSwatch = CreateImage("SelectedColor", _pickerRoot, _circleSprite);
        SetAnchored(_selectedSwatch.rectTransform, new Vector2(-55f, -102f), new Vector2(30f, 30f));
        _hexLabel = CreateText("Hex", _pickerRoot, 15f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        _hexLabel.color = Color.white;
        SetAnchored(_hexLabel.rectTransform, new Vector2(30f, -102f), new Vector2(124f, 28f));
        _detailLabel = CreateText("ColorDetails", _pickerRoot, 10f, FontStyles.Normal, TextAlignmentOptions.Center);
        _detailLabel.color = new Color(0.62f, 0.7f, 0.65f, 1f);
        SetAnchored(_detailLabel.rectTransform, new Vector2(0f, -128f), new Vector2(318f, 20f));

        for (int i = 0; i < PresetColors.Length; i++)
        {
            Image dot = CreateImage("Preset_" + i, _pickerRoot, _circleSprite);
            SetAnchored(dot.rectTransform, new Vector2((i - 3) * 27f, -154f), new Vector2(19f, 19f));
            dot.color = PresetColors[i];
            Outline outline = dot.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;
            _presetDots.Add(dot);
        }

        _hintLabel = CreateText("Hints", _pickerRoot, 11f, FontStyles.Normal, TextAlignmentOptions.Center);
        _hintLabel.text = "RIGHT STICK COLOR  //  D-PAD PRESETS";
        _hintLabel.color = new Color(0.72f, 0.78f, 0.74f, 1f);
        SetAnchored(_hintLabel.rectTransform, new Vector2(0f, -177f), new Vector2(338f, 20f));

        _originalButton = CreateActionButton("OriginalButton", _pickerRoot, "L3  ORIGINAL", -112f);
        _saveButton = CreateActionButton("SaveButton", _pickerRoot, "R3  SAVE", 0f);
        _cancelButton = CreateActionButton("CancelButton", _pickerRoot, "B  CANCEL", 112f);

        _pickerRoot.gameObject.SetActive(false);
        _built = true;
    }

    private void HideUi()
    {
        if (_promptGroup != null)
        {
            _promptGroup.alpha = 0f;
        }

        if (_pickerRoot != null)
        {
            _pickerRoot.gameObject.SetActive(false);
        }
    }

    private Image CreateActionButton(string name, Transform parent, string label, float x)
    {
        Image button = CreateImage(name, parent, null);
        SetAnchored(button.rectTransform, new Vector2(x, -204f), new Vector2(102f, 30f));
        button.color = new Color(0.045f, 0.06f, 0.052f, 0.92f);
        Outline outline = button.gameObject.AddComponent<Outline>();
        outline.effectDistance = new Vector2(1f, -1f);
        outline.effectColor = new Color(1f, 1f, 1f, 0.24f);
        outline.useGraphicAlpha = true;

        TextMeshProUGUI text = CreateText(
            name + "Text",
            button.rectTransform,
            10f,
            FontStyles.Bold,
            TextAlignmentOptions.Center);
        text.text = label;
        text.color = new Color(0.88f, 0.94f, 0.9f, 1f);
        Stretch(text.rectTransform);
        return button;
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

    private static RawImage CreateRawImage(string name, Transform parent, Texture texture)
    {
        GameObject gameObject = new(
            name,
            Il2CppType.Of<RectTransform>(),
            Il2CppType.Of<CanvasRenderer>(),
            Il2CppType.Of<RawImage>());
        gameObject.transform.SetParent(parent, false);
        RawImage image = gameObject.GetComponent<RawImage>();
        image.texture = texture;
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        float size,
        FontStyles style,
        TextAlignmentOptions alignment)
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
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.characterSpacing = 0.7f;
        return text;
    }

    private static Texture2D CreateColorWheelTexture()
    {
        const int size = 256;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = "BetterLights_ColorWheel",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color32[] pixels = new Color32[size * size];
        Vector2 center = new(size * 0.5f, size * 0.5f);
        float radius = (size * 0.5f) - 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 offset = new((x + 0.5f) - center.x, (y + 0.5f) - center.y);
                float saturation = offset.magnitude / radius;
                if (saturation > 1.02f)
                {
                    pixels[(y * size) + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float hue = Mathf.Repeat(Mathf.Atan2(offset.y, offset.x) / (Mathf.PI * 2f), 1f);
                Color color = Color.HSVToRGB(hue, Mathf.Clamp01(saturation), 1f);
                float alpha = Mathf.Clamp01((1.02f - saturation) * 50f);
                pixels[(y * size) + x] = new Color(color.r, color.g, color.b, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Sprite CreateCircleSprite()
    {
        const int size = 64;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = "BetterLights_Circle",
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

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static void SetCenteredSize(RectTransform rect, float width, float height)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = Vector2.zero;
    }

    private static void SetAnchored(RectTransform rect, Vector2 position, Vector2 size)
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

    private void OnDisable()
    {
        if (_open)
        {
            ClosePicker(true);
        }
    }

    private void OnDestroy()
    {
        RestoreCursorState();
        RestoreInputMaps();
        ResetNetworkSync();
    }
}
