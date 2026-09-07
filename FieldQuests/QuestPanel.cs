using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using Game.Data;
using Game.PlayerOperations;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace InfernoProtocol.FieldQuests;

internal sealed class QuestPanel : IDisposable
{
    private sealed class ActionBox
    {
        internal Image Image;
        internal TextMeshProUGUI Label;
        internal Action Click;
        internal bool Enabled = true;
        internal Color BaseColor;
    }
    private readonly Action<string, string> _send;
    private readonly List<ActionBox> _buttons = new();
    private readonly List<InputActionMap> _disabledMaps = new();
    private readonly List<InputAction> _previouslyDisabledActions = new();
    private readonly List<QuestRow> _visible = new();
    private readonly ActionBox[] _tabs = new ActionBox[5];
    private readonly ActionBox[] _rows = new ActionBox[6];
    private readonly Image[] _rowIcons = new Image[6];
    private readonly TextMeshProUGUI[] _rowStatus = new TextMeshProUGUI[6];
    private readonly string[] _filters = { "All assignments", "My journal", "Hunts", "Supplies", "Completed" };
    private GameObject _canvas, _window;
    private Image _scrim, _xpFill, _objectiveFill, _rewardIcon, _targetIcon;
    private TextMeshProUGUI _prompt, _level, _xp, _title, _description, _objective, _sources, _reward, _rewardXp, _status, _pagination, _listTitle;
    private ActionBox _primary, _abandon;
    private QuestView _view;
    private Color _accent = new(0.45f, 0.88f, 0.80f, 1);
    private readonly Color _ink = new(0.88f, 0.93f, 0.93f, 1);
    private readonly Color _muted = new(0.56f, 0.65f, 0.68f, 1);
    private readonly Color _panel = new(0.045f, 0.065f, 0.075f, 0.96f);
    private int _filter, _selection;
    private string _confirmation;
    private string _pendingClaim;
    private float _pendingSince, _receiptUntil, _displayXp = -1;
    private TextMeshProUGUI _receipt;
    private GameObject _levelHud;
    private TextMeshProUGUI _hudLevel;
    private Image _hudFill;
    private int _hudXp = -1;
    private float _nextHudTheme;
    private GameObject _dialog;
    private TextMeshProUGUI _dialogTitle, _dialogBody, _dialogReward;
    private Image _dialogIcon;
    private ActionBox _dialogConfirm, _dialogCancel;
    private bool _dialogResult, _dialogCancelSelected;
    private float _dialogOpened;
    private bool _cosmetics;
    private readonly Dictionary<int, Sprite> _finishPreviews = new();
    internal bool IsCosmetics => _cosmetics;
    private TextMeshProUGUI _brand, _sidebarTitle;
    private TextMeshProUGUI _welcome;
    private ActionBox _finishPrevious, _finishNext;
    private TextMeshProUGUI _briefingTitle, _finishPage, _collection;
    private Image _objectiveTrack, _levelTrack;
    private int _savedFinish, _savedEquipment;
    private bool _releasePending, _savedCursor, _lookWasEnabled;
    private CursorLockMode _savedLock;
    private float _openTime, _repeatAt, _messageUntil, _lastResponse, _nextAction;
    private int _navHeld;
    internal bool IsOpen { get; private set; }
    internal bool BlockingInput => IsOpen || _releasePending;
    internal QuestPanel(Action<string, string> send)
    {
        _send = send;
        Build();
    }
    internal void Open(bool cosmetics = false)
    {
        if (BlockingInput) return;
        _cosmetics = cosmetics;
        _filter = cosmetics ? _savedFinish : 0; _selection = cosmetics ? _savedEquipment : 0;
        _brand.text = cosmetics ? "FINISHES  /  EQUIPMENT COSMETICS" : "FIELD NOTES  /  QUEST BOARD";
        _sidebarTitle.text = cosmetics ? "CHOOSE A FINISH" : "THE QUEST BOARD";
        _welcome.gameObject.SetActive(!cosmetics);
        _finishPrevious.Image.gameObject.SetActive(cosmetics);
        _finishNext.Image.gameObject.SetActive(cosmetics);
        LayoutFinishes(cosmetics);
        LoadTheme();
        _savedCursor = Cursor.visible;
        _savedLock = Cursor.lockState;
        _disabledMaps.Clear();
        _previouslyDisabledActions.Clear();
        _lookWasEnabled = PlayerInputDispatcher.actions?.look?.enabled == true;
        var maps = PlayerInputDispatcher.inputActionsAsset?.actionMaps;
        if (maps != null)
            foreach (InputActionMap map in maps)
                if (map.enabled)
                {
                    foreach (InputAction action in map.actions)
                        if (!action.enabled) _previouslyDisabledActions.Add(action);
                    _disabledMaps.Add(map); map.Disable();
                }
        PlayerInputDispatcher.SetEnableLookInput(false);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        _window.SetActive(true); _scrim.gameObject.SetActive(true);
        _levelHud.SetActive(false);
        _prompt.gameObject.SetActive(false);
        IsOpen = true; _confirmation = null; _openTime = Time.unscaledTime;
        _pendingClaim = null;
        _dialog.SetActive(false);
        _receiptUntil = 0;
        _displayXp = _view?.Xp ?? 0;
        _lastResponse = Time.unscaledTime;
        _status.text = "Loading quest board...";
        Refresh();
    }
    internal void Close()
    {
        if (!IsOpen) return;
        if (_cosmetics) { _savedFinish = _filter; _savedEquipment = _selection; }
        IsOpen = false; _releasePending = true;
        _window.SetActive(false); _scrim.gameObject.SetActive(false);
        _confirmation = null;
        _dialog.SetActive(false);
    }
    private void RestoreInput()
    {
        foreach (InputActionMap map in _disabledMaps) map?.Enable();
        _disabledMaps.Clear();
        foreach (InputAction action in _previouslyDisabledActions) action?.Disable();
        _previouslyDisabledActions.Clear();
        PlayerInputDispatcher.SetEnableLookInput(_lookWasEnabled);
        Cursor.lockState = _savedLock; Cursor.visible = _savedCursor;
        _releasePending = false;
    }
    internal void Receive(QuestView view)
    {
        if (view == null) return;
        if (view.ProgressOnly || view.Rows.Count > 0 || _cosmetics) UpdateLevelHud(view.Xp);
        if (view.ProgressOnly) return;
        bool wasPending = _pendingClaim != null;
        string selectedId = Selected?.Id;
        Quest completed = _view == null || view.Xp <= _view.Xp ? null : QuestCatalog.All.FirstOrDefault(q =>
            _view.Rows.Any(r => r.Id == q.Id && r.State != "COMPLETED") &&
            view.Rows.Any(r => r.Id == q.Id && r.State == "COMPLETED"));
        if (completed != null && IsOpen)
        {
            int before = QuestCatalog.Level(_view.Xp), after = QuestCatalog.Level(view.Xp);
            _receipt.text = $"REWARD RECEIVED  /  {Name(completed.Reward)} x1  /  +{completed.Xp} XP" +
                (after > before ? $"  /  LEVEL UP: {after}" : "");
            _receipt.color = _accent;
            _receiptUntil = Time.unscaledTime + 6f;
            if (SkinCatalog.RewardFor(completed.Id).Length > 0) _receipt.text += "  /  " + SkinCatalog.RewardFor(completed.Id);
        }
        if (_pendingClaim != null && (view.Rows.Any(r => r.Id == _pendingClaim && r.State == "COMPLETED") || !string.IsNullOrEmpty(view.Message)))
            _pendingClaim = null;
        _view = view; _lastResponse = Time.unscaledTime;
        if (IsOpen && wasPending && _pendingClaim == null)
        {
            _dialogResult = true;
            _dialogTitle.text = completed != null ? "ASSIGNMENT COMPLETE" : "TURN-IN UPDATE";
            _dialogBody.text = completed != null ? completed.Title + "\nYour reward has been delivered." : view.Message;
            if (completed != null) _dialogReward.text = Name(completed.Reward) + $" x1   /   +{completed.Xp} XP";
            else { _dialogReward.text = "No success confirmed. Check your assignment before retrying."; _dialogIcon.enabled = false; }
            if (completed != null && SkinCatalog.RewardFor(completed.Id).Length > 0) _dialogReward.text += "\n" + SkinCatalog.RewardFor(completed.Id);
            _dialogConfirm.Enabled = false; _dialogConfirm.Image.gameObject.SetActive(false);
            _dialogCancel.Label.text = "CONTINUE  /  B or Esc";
            _dialog.SetActive(true);
        }
        if (!string.IsNullOrEmpty(view.Message))
        {
            _status.text = view.Message; _messageUntil = Time.unscaledTime + 8f;
            if (_cosmetics)
            {
                _receipt.text = view.Message; _receipt.color = _accent;
                _receiptUntil = Time.unscaledTime + 6;
            }
        }
        Refresh();
        // Keep the same selected contract when polling changes the filtered list.
        int retained = _visible.FindIndex(r => r.Id == selectedId);
        if (retained >= 0 && retained != _selection) { _selection = retained; Refresh(); }
    }
    internal void SetPrompt(bool visible, Vector3 boardPosition, bool riding, string talkButton,
        bool boardReady, string placementStatus, bool gameplay, bool cosmetics = false)
    {
        _levelHud.SetActive(gameplay && !BlockingInput && _hudXp >= 0);
        if (gameplay && Time.unscaledTime >= _nextHudTheme)
        {
            _nextHudTheme = Time.unscaledTime + 5;
            LoadTheme();
        }
        _prompt.gameObject.SetActive(visible && !BlockingInput);
        if (visible) _prompt.text = riding ? "QUEST BOARD\nDismount your carpet to read the board" :
            cosmetics ? $"FINISHES  /  COSMETIC STATION\n[{talkButton}]  Customize equipment" : $"QUEST BOARD  /  FIELD ASSIGNMENTS\n[{talkButton}]  Read board  /  Browse quests";
    }
    internal void Tick()
    {
        if (!Application.isFocused || !Player.isLocalPlayerLoaded || Player.localPlayer == null || Player.localPlayer.isDead)
            _levelHud.SetActive(false);
        if (_releasePending)
        {
            if (Released()) RestoreInput();
            return;
        }
        if (!IsOpen) return;
        if (_pendingClaim != null && Time.unscaledTime - _pendingSince > 8f)
        {
            _pendingClaim = null;
            if (_dialog.activeSelf)
            {
                _dialogResult = true; _dialogTitle.text = "CONFIRMATION DELAYED";
                _dialogBody.text = "The host has not confirmed the turn-in. Check the refreshed quest state before retrying.";
                _dialogConfirm.Image.gameObject.SetActive(false);
                _dialogCancel.Label.text = "BACK";
            }
            _status.text = "No confirmation received. Refresh the board before retrying.";
            _messageUntil = Time.unscaledTime + 6;
            Refresh();
        }
        if (Time.unscaledTime > _receiptUntil)
        {
            _receipt.text = _cosmetics ? "Earn it through quests. Save your look. Equip it whenever you like." : "A safer camp. A stronger survivor. One assignment at a time.";
            _receipt.color = _muted;
        }
        else _receipt.color = Color.Lerp(_muted, _accent, Mathf.Clamp01((_receiptUntil - Time.unscaledTime) / .6f));
        if (_view != null)
        {
            _displayXp = _displayXp < 0 ? _view.Xp : Mathf.MoveTowards(_displayXp, _view.Xp, Math.Max(120, Math.Abs(_view.Xp - _displayXp) * 5) * Time.unscaledDeltaTime);
            int animatedLevel = QuestCatalog.Level((int)_displayXp);
            int floor = QuestCatalog.Threshold(animatedLevel), ceiling = QuestCatalog.Threshold(animatedLevel + 1);
            _xpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((_displayXp - floor) / (ceiling - floor)), 1);
            _level.text = $"LEVEL {animatedLevel:00}";
            _xp.text = $"{(int)_displayXp - floor:N0} / {ceiling - floor:N0} XP\n{_view.Rows.Count(r => r.State == "COMPLETED")} assignments completed";
        }
        if (!Application.isFocused) { Close(); return; }
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        PlayerInputDispatcher.SetEnableLookInput(false);
        foreach (InputActionMap map in _disabledMaps) if (map.enabled) map.Disable();
        if (Time.unscaledTime - _lastResponse > 6)
            _status.text = "Waiting for the host. The host must have ContentPlus installed.";
        else if (Time.unscaledTime > _messageUntil && _confirmation == null)
            _status.text = _cosmetics ? (Gamepad.current != null ?
                "D-pad left/right: finish  |  Up/down: equipment  |  LB/RB: equipment page  |  A: apply  |  B: close" :
                "Click to select  |  Wheel: equipment  |  Left/right: finish  |  Up/down: equipment  |  Enter: apply  |  Esc: close") : Gamepad.current != null ?
                "D-pad: browse / filter  |  A: action  |  X: abandon  |  LB/RB: page  |  B: back" :
                "Click: select / action  |  Wheel: browse  |  Arrow keys: browse / filter  |  Enter: action  |  Esc: back";
        if (Time.unscaledTime - _openTime < 0.25f) return;
        Keyboard k = Keyboard.current;
        Gamepad g = Gamepad.current;
        if (_dialog.activeSelf) { TickDialog(k, g); return; }
        if (k?.escapeKey.wasPressedThisFrame == true || g?.buttonEast.wasPressedThisFrame == true)
        {
            if (_confirmation != null) { _confirmation = null; Refresh(); }
            else Close();
            return;
        }
        int filterCount = _cosmetics ? SkinCatalog.Names.Length : _filters.Length;
        if (g?.dpad.left.wasPressedThisFrame == true || k?.leftArrowKey.wasPressedThisFrame == true) Filter((_filter + filterCount - 1) % filterCount);
        if (g?.dpad.right.wasPressedThisFrame == true || k?.rightArrowKey.wasPressedThisFrame == true || k?.tabKey.wasPressedThisFrame == true) Filter((_filter + 1) % filterCount);
        int nav = (g?.dpad.down.isPressed == true || k?.downArrowKey.isPressed == true ? 1 : 0) -
                  (g?.dpad.up.isPressed == true || k?.upArrowKey.isPressed == true ? 1 : 0);
        if (nav != 0 && (nav != _navHeld || Time.unscaledTime >= _repeatAt))
        {
            Select(_selection + nav);
            _repeatAt = Time.unscaledTime + (nav != _navHeld ? 0.32f : 0.12f);
        }
        _navHeld = nav;
        if (g?.leftShoulder.wasPressedThisFrame == true || k?.pageUpKey.wasPressedThisFrame == true) Select(_selection - 6);
        if (g?.rightShoulder.wasPressedThisFrame == true || k?.pageDownKey.wasPressedThisFrame == true) Select(_selection + 6);
        if (g?.buttonSouth.wasPressedThisFrame == true || k?.enterKey.wasPressedThisFrame == true) Primary();
        if (_dialog.activeSelf) return;
        if (g?.buttonWest.wasPressedThisFrame == true || k?.deleteKey.wasPressedThisFrame == true) Abandon();
        if (_dialog.activeSelf) return;
        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 point = mouse.position.ReadValue();
        if (Mathf.Abs(mouse.scroll.ReadValue().y) > 0.1f)
            Select(_selection + (mouse.scroll.ReadValue().y < 0 ? 1 : -1));
        foreach (ActionBox button in _buttons)
        {
            if (!button.Image.gameObject.activeInHierarchy || !button.Enabled) continue;
            bool hit = RectTransformUtility.RectangleContainsScreenPoint(button.Image.rectTransform, point, null);
            button.Image.color = hit ? Color.Lerp(button.BaseColor, _accent, .18f) : button.BaseColor;
            if (hit && mouse.leftButton.wasPressedThisFrame) { button.Click(); break; }
        }
    }
    private static bool Released()
    {
        Keyboard k = Keyboard.current; Gamepad g = Gamepad.current;
        return !(k?.anyKey.isPressed == true || Mouse.current?.leftButton.isPressed == true ||
            Mouse.current?.rightButton.isPressed == true || Mouse.current?.middleButton.isPressed == true ||
            g?.buttonSouth.isPressed == true || g?.buttonEast.isPressed == true || g?.buttonWest.isPressed == true ||
            g?.buttonNorth.isPressed == true || g?.dpad.ReadValue().sqrMagnitude > 0.01f ||
            g?.leftShoulder.isPressed == true || g?.rightShoulder.isPressed == true || g?.selectButton.isPressed == true ||
            g?.leftTrigger.isPressed == true || g?.rightTrigger.isPressed == true || g?.startButton.isPressed == true);
    }
    private void Filter(int filter) { _filter = filter; if (!_cosmetics) _selection = 0; _confirmation = null; Refresh(); }
    private void Select(int selected)
    {
        _selection = Mathf.Clamp(selected, 0, Math.Max(0, (_cosmetics ? SkinCatalog.Items.Length : _visible.Count) - 1));
        _confirmation = null; Refresh();
    }
    private QuestRow Selected => _visible.Count > _selection ? _visible[_selection] : null;
    private void Primary()
    {
        if (_pendingClaim != null || Time.unscaledTime < _nextAction) return;
        _nextAction = Time.unscaledTime + .35f;
        if (_cosmetics)
        {
            int applied = 0;
            _view?.Skins?.TryGetValue(SkinCatalog.Items[_selection], out applied);
            if (_primary.Enabled && applied != _filter && _view?.SkinUnlocks?.Contains(_filter) == true)
                _send("skin", SkinCatalog.Items[_selection] + "|" + _filter);
            return;
        }
        QuestRow row = Selected;
        if (row == null) return;
        if (row.State == "AVAILABLE") { _send("accept", row.Id); return; }
        if (row.State != "READY") return;
        if (_confirmation != "claim:" + row.Id)
        {
            _confirmation = "claim:" + row.Id;
            ShowDialog(row, false);
            Quest q = QuestCatalog.Find(row.Id);
            _status.text = q.Hunt ? "Confirm completion to receive the displayed reward and XP." :
                $"Consume {q.Amount} {Name(q.Target)}: {Math.Min(q.Amount, row.Carried)} carried + {Math.Max(0, q.Amount - row.Carried)} stored. Confirm turn-in?";
            _messageUntil = Time.unscaledTime + 30;
            Refresh(); return;
        }
        _confirmation = null;
        _pendingClaim = row.Id; _pendingSince = Time.unscaledTime;
        _status.text = "Turning in assignment... waiting for host confirmation.";
        _messageUntil = Time.unscaledTime + 8;
        Refresh();
        _send("claim", row.Id);
    }
    private void Abandon()
    {
        if (_cosmetics) return;
        if (_pendingClaim != null || Time.unscaledTime < _nextAction) return;
        _nextAction = Time.unscaledTime + .35f;
        QuestRow row = Selected;
        if (row == null || (row.State != "ACTIVE" && row.State != "READY" && row.State != "UNAVAILABLE")) return;
        if (_confirmation != "abandon:" + row.Id)
        {
            _confirmation = "abandon:" + row.Id;
            ShowDialog(row, true);
            _status.text = "Abandon this assignment? Hunt progress will reset. Press Abandon again to confirm.";
            _messageUntil = Time.unscaledTime + 30;
            Refresh(); return;
        }
        _confirmation = null; _send("abandon", row.Id);
    }
    private void CancelDialog()
    {
        _confirmation = null;
        _dialog.SetActive(false);
        _nextAction = Time.unscaledTime + .3f;
        Refresh();
    }
    private void ShowDialog(QuestRow row, bool abandon)
    {
        Quest q = QuestCatalog.Find(row.Id);
        _dialogResult = false; _dialogCancelSelected = abandon;
        _dialogOpened = Time.unscaledTime;
        _dialogTitle.text = abandon ? "ABANDON ASSIGNMENT?" : "READY TO TURN IN?";
        _dialogBody.text = q.Title + "\n\n" + (abandon ?
            "This removes the assignment from your journal. Hunt progress will reset. No items will be taken." :
            q.Hunt ? "Hunt complete. No items will be consumed." :
            $"DELIVER  {q.Amount} {Name(q.Target)}\n{Math.Min(q.Amount, row.Carried)} carried  +  {Math.Max(0, q.Amount - row.Carried)} from built storage\nThese materials will be consumed.");
        _dialogReward.text = abandon ? "You can accept this assignment again later." : $"YOU RECEIVE\n{Name(q.Reward)} x1   /   +{q.Xp} XP";
        if (!abandon && SkinCatalog.RewardFor(q.Id).Length > 0) _dialogReward.text += "\n" + SkinCatalog.RewardFor(q.Id);
        SetIcon(_dialogIcon, q.Reward); _dialogIcon.enabled = !abandon;
        _dialogConfirm.Image.gameObject.SetActive(true); _dialogConfirm.Enabled = true;
        _dialogConfirm.Label.text = abandon ? "ABANDON" : "CONFIRM TURN-IN";
        _dialogCancel.Label.text = "CANCEL  /  B or Esc";
        _dialog.SetActive(true);
    }
    private void ConfirmDialog()
    {
        if (_dialogResult || _pendingClaim != null || _confirmation == null || Time.unscaledTime - _dialogOpened < .3f) return;
        string[] action = _confirmation.Split(':');
        QuestRow row = _view?.Rows.FirstOrDefault(r => r.Id == action[1]);
        bool claim = action[0] == "claim";
        if (row == null || (claim ? row.State != "READY" : row.State != "ACTIVE" && row.State != "READY" && row.State != "UNAVAILABLE"))
        {
            _dialogResult = true; _confirmation = null;
            _dialogTitle.text = "ASSIGNMENT CHANGED";
            _dialogBody.text = "This action is no longer available. Return to the board to review the current requirements.";
            _dialogConfirm.Image.gameObject.SetActive(false); _dialogCancel.Label.text = "BACK";
            return;
        }
        _confirmation = null;
        if (claim)
        {
            _pendingClaim = row.Id; _pendingSince = Time.unscaledTime;
            _dialogTitle.text = "TURNING IN...";
            _dialogBody.text = "Waiting for the host to confirm your materials and deliver your reward.";
            _dialogConfirm.Enabled = false; _dialogConfirm.Image.gameObject.SetActive(false);
            _dialogCancel.Label.text = "BACK (does not cancel request)";
        }
        else _dialog.SetActive(false);
        _nextAction = Time.unscaledTime + .35f;
        Refresh();
        _send(action[0], row.Id);
    }
    private void TickDialog(Keyboard k, Gamepad g)
    {
        if (k?.escapeKey.wasPressedThisFrame == true || g?.buttonEast.wasPressedThisFrame == true) { CancelDialog(); return; }
        if (Time.unscaledTime - _dialogOpened < .3f) return;
        if (g?.dpad.left.wasPressedThisFrame == true || k?.leftArrowKey.wasPressedThisFrame == true) _dialogCancelSelected = true;
        if (g?.dpad.right.wasPressedThisFrame == true || k?.rightArrowKey.wasPressedThisFrame == true) _dialogCancelSelected = false;
        if (k?.tabKey.wasPressedThisFrame == true) _dialogCancelSelected = !_dialogCancelSelected;
        if (g?.buttonSouth.wasPressedThisFrame == true || k?.enterKey.wasPressedThisFrame == true)
        {
            if (_dialogCancelSelected || _dialogResult || !_dialogConfirm.Enabled) CancelDialog(); else ConfirmDialog();
            return;
        }
        Mouse mouse = Mouse.current;
        for (int i = 0; i < 2; i++)
        {
            ActionBox b = i == 0 ? _dialogCancel : _dialogConfirm;
            if (!b.Image.gameObject.activeSelf) continue;
            bool hover = mouse != null && RectTransformUtility.RectangleContainsScreenPoint(b.Image.rectTransform, mouse.position.ReadValue(), null);
            bool focused = b == (_dialogCancelSelected ? _dialogCancel : _dialogConfirm);
            b.Image.color = Color.Lerp(_panel, _accent, hover || focused ? .38f : .14f);
            if (hover && mouse.leftButton.wasPressedThisFrame && b.Enabled) { b.Click(); return; }
        }
    }
    private void RefreshSkins()
    {
        _visible.Clear();
        _finishPage.text = $"{_filter / 5 + 1} / {(SkinCatalog.Names.Length + 4) / 5}";
        _collection.text = $"{Math.Max(0, (_view?.SkinUnlocks?.Count ?? 1) - 1)} / {SkinCatalog.Names.Length - 1} FINISHES UNLOCKED\nEarn new designs through quests.";
        _selection = Mathf.Clamp(_selection, 0, SkinCatalog.Items.Length - 1);
        for (int i = 0; i < _tabs.Length; i++)
        {
            int finish = _filter / 5 * 5 + i;
            _tabs[i].Image.gameObject.SetActive(finish < SkinCatalog.Names.Length);
            if (finish >= SkinCatalog.Names.Length) continue;
            bool unlocked = _view?.SkinUnlocks?.Contains(finish) == true;
            _tabs[i].Click = () => Filter(finish);
            _tabs[i].Label.text = (finish == _filter ? ">  " : "") + SkinCatalog.Names[finish] + (unlocked ? "" : "\nLocked");
            _tabs[i].Image.color = finish == _filter ? Color.Lerp(_panel, SkinVisuals.ColorFor(finish), .3f) : _panel;
            _tabs[i].Label.color = unlocked ? _ink : _muted;
        }
        int page = _selection / 6;
        _pagination.text = $"{page + 1} / {(SkinCatalog.Items.Length + 5) / 6}";
        _listTitle.text = "2  /  CHOOSE EQUIPMENT";
        for (int i = 0; i < _rows.Length; i++)
        {
            int index = page * 6 + i;
            _rows[i].Image.gameObject.SetActive(index < SkinCatalog.Items.Length);
            if (index >= SkinCatalog.Items.Length) continue;
            string item = SkinCatalog.Items[index];
            int applied = 0; _view?.Skins?.TryGetValue(item, out applied);
            applied = Mathf.Clamp(applied, 0, SkinCatalog.Names.Length - 1);
            _rows[i].Label.text = Name(item); _rows[i].Label.color = _ink;
            _rowStatus[i].text = "Current: " + SkinCatalog.Names[applied];
            _rowStatus[i].color = applied == _filter ? _accent : _muted;
            SetIcon(_rowIcons[i], item);
            _rows[i].Image.color = index == _selection ? Color.Lerp(_panel, _accent, .22f) : _panel;
            int capture = index; _rows[i].Click = () => Select(capture);
        }
        string selected = SkinCatalog.Items[_selection];
        bool available = _view?.SkinUnlocks?.Contains(_filter) == true;
        int current = 0; _view?.Skins?.TryGetValue(selected, out current);
        current = Mathf.Clamp(current, 0, SkinCatalog.Names.Length - 1);
        bool alreadyApplied = current == _filter;
        _title.text = SkinCatalog.Names[_filter];
        _description.text = _filter switch
        {
            1 => "Layered olive, sand and dark woodland camouflage. Original texture detail remains underneath.",
            2 => "Deep-blue panels with diagonal enamel bands and pale technical pinstripes.",
            3 => "Charcoal fragments split by ember-orange fissures. Painted cracks, not added glow or bloom.",
            4 => "An interlocking brass diamond lattice over dark brown panels, with recessed-looking borders.",
            5 => "Stepped snow-white, slate and ice-blue digital camouflage blocks.",
            6 => "Industrial yellow-and-black diagonal warning bands with pale divider rails.",
            7 => "Broken, winding dark tiger stripes with sand edging over burnt orange.",
            8 => "Crimson racing panels, cream pinstripes and a narrow checkered lane.",
            9 => "Rolling turquoise and navy wave bands edged with pale seafoam.",
            10 => "Fine map-like contour lines tracing irregular green terrain fields.",
            11 => "Interlaced gold ribbons over a finely textured royal-purple ground.",
            12 => "Angular sand, ochre and dark-earth splinter camouflage.",
            _ => "Restore the original game materials. No items are required to save an appearance."
        };
        _objective.text = (alreadyApplied ? "CURRENT FINISH" : available ? "READY TO APPLY" : "LOCKED FINISH") + "\nPattern swatch";
        _sources.text = available ? "No item needed. Applies when you equip it.\nSaved in this world; visible only to you." :
            "Complete this quest to unlock:\n" + (QuestCatalog.Find(SkinCatalog.Quests[_filter])?.Title ?? "Waiting for quest data");
        _reward.text = Name(selected);
        _rewardXp.text = "Current: " + SkinCatalog.Names[current];
        _rewardXp.fontSizeMax = 17;
        _rewardXp.fontSizeMin = 14;
        SetIcon(_rewardIcon, selected);
        SetFinishPreview(_targetIcon, _filter);
        if (_filter == 0) SetIcon(_targetIcon, selected);
        _objectiveFill.rectTransform.anchorMax = new Vector2(available ? 1 : 0, 1);
        _primary.Enabled = available && !alreadyApplied;
        _primary.Label.text = !available ? "UNLOCK THROUGH QUESTS" : alreadyApplied ? "APPLIED" : _filter == 0 ? "RESTORE ORIGINAL" : "APPLY TO THIS EQUIPMENT";
        _primary.Image.color = _primary.Enabled ? _accent : _panel;
        _primary.Label.color = _primary.Enabled ? new Color(.025f, .06f, .065f) : _muted;
        _abandon.Image.gameObject.SetActive(false);
        foreach (ActionBox button in _buttons) button.BaseColor = button.Image.color;
    }
    private void SetFinishPreview(Image image, int finish)
    {
        image.enabled = finish > 0;
        if (finish == 0) return;
        if (!_finishPreviews.TryGetValue(finish, out Sprite sprite))
        {
            Texture2D texture = FinishTextures.Create(finish);
            try { sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f)); }
            catch { UnityEngine.Object.Destroy(texture); throw; }
            _finishPreviews[finish] = sprite;
        }
        image.sprite = sprite; image.color = Color.white; image.preserveAspect = true;
    }
    private void LayoutFinishes(bool enabled)
    {
        _sidebarTitle.text = enabled ? "1  /  CHOOSE FINISH" : "THE QUEST BOARD";
        _briefingTitle.text = enabled ? "3  /  PREVIEW & APPLY" : "ASSIGNMENT BRIEF";
        _finishPage.gameObject.SetActive(enabled);
        _collection.gameObject.SetActive(enabled);
        _level.gameObject.SetActive(!enabled); _xp.gameObject.SetActive(!enabled);
        _levelTrack.gameObject.SetActive(!enabled); _objectiveTrack.gameObject.SetActive(!enabled);
        _rewardXp.fontSizeMax = 23;
        _rewardXp.fontSizeMin = 18;
        Place(_targetIcon.rectTransform, .06f, enabled ? .46f : .51f, enabled ? .43f : .20f, enabled ? .70f : .64f);
        Place(_objective.rectTransform, enabled ? .48f : .24f, enabled ? .46f : .505f, .94f, enabled ? .70f : .64f);
        Place(_description.rectTransform, .06f, enabled ? .71f : .65f, .94f, .82f);
        Place(_sources.rectTransform, .06f, .35f, .94f, enabled ? .445f : .477f);
    }
    private void Refresh()
    {
        if (_cosmetics) { RefreshSkins(); return; }
        _visible.Clear();
        if (_view != null)
            foreach (QuestRow r in _view.Rows)
            {
                Quest q = QuestCatalog.Find(r.Id);
                if (q == null) continue;
                if (_filter == 0 && r.State != "COMPLETED" ||
                    _filter == 1 && (r.State == "ACTIVE" || r.State == "READY" || r.State == "UNAVAILABLE") ||
                    _filter == 2 && q.Hunt && r.State != "COMPLETED" ||
                    _filter == 3 && !q.Hunt && r.State != "COMPLETED" ||
                    _filter == 4 && r.State == "COMPLETED") _visible.Add(r);
            }
        _selection = Mathf.Clamp(_selection, 0, Math.Max(0, _visible.Count - 1));
        int xp = _view?.Xp ?? 0;
        int level = QuestCatalog.Level(xp);
        int floor = QuestCatalog.Threshold(level), ceiling = QuestCatalog.Threshold(level + 1);
        _level.text = $"LEVEL {level:00}";
        _xp.text = $"{xp - floor:N0} / {ceiling - floor:N0} XP\n{_view?.Rows.Count(r => r.State == "COMPLETED") ?? 0} assignments completed";
        for (int i = 0; i < _tabs.Length; i++)
        {
            _tabs[i].Image.gameObject.SetActive(true);
            int tab = i; _tabs[i].Click = () => Filter(tab);
            _tabs[i].Label.text = _filters[i];
            _tabs[i].Image.color = i == _filter ? new Color(_accent.r * 0.23f, _accent.g * 0.23f, _accent.b * 0.23f, 1) : _panel;
            _tabs[i].Label.color = i == _filter ? _accent : _muted;
        }
        int page = _selection / 6;
        _pagination.text = _visible.Count == 0 ? "NO ASSIGNMENTS" : $"{page + 1} / {(_visible.Count + 5) / 6}";
        _listTitle.text = _filters[_filter].ToUpperInvariant() + $"  /  {_visible.Count}";
        for (int i = 0; i < _rows.Length; i++)
        {
            int index = page * 6 + i;
            _rows[i].Image.gameObject.SetActive(index < _visible.Count);
            if (index >= _visible.Count) continue;
            QuestRow r = _visible[index]; Quest q = QuestCatalog.Find(r.Id);
            _rows[i].Label.text = q.Title;
            _rows[i].Label.color = r.State == "LOCKED" ? _muted : _ink;
            _rows[i].Image.color = index == _selection ? new Color(_accent.r * 0.21f, _accent.g * 0.21f, _accent.b * 0.21f, 1) : _panel;
            _rowStatus[i].text = $"{r.State}  /  LV {q.Level}" + (r.State == "ACTIVE" || r.State == "READY" ? $"  /  {Math.Min(r.Count, q.Amount)}/{q.Amount}" : "");
            _rowStatus[i].color = r.State == "READY" ? _accent : _muted;
            SetIcon(_rowIcons[i], q.Reward);
            int capture = index;
            _rows[i].Click = () => Select(capture);
        }
        QuestRow row = Selected;
        if (row == null)
        {
            _title.text = _filter == 1 ? "Your next adventure awaits" : "Nothing here yet";
            _description.text = "Browse All assignments and accept a contract to begin. Every assignment rewards experience.";
            _objective.text = ""; _sources.text = ""; _reward.text = ""; _rewardXp.text = "";
            _rewardIcon.enabled = false; _targetIcon.enabled = false;
            _primary.Enabled = false; _primary.Label.text = "Select an assignment";
            _primary.Image.color = _panel; _primary.Label.color = _muted;
            _objectiveFill.rectTransform.anchorMax = new Vector2(0, 1);
            _abandon.Image.gameObject.SetActive(false);
            foreach (ActionBox button in _buttons) button.BaseColor = button.Image.color;
            return;
        }
        Quest quest = QuestCatalog.Find(row.Id);
        _title.text = quest.Title;
        _description.text = quest.Description;
        if (row.State == "UNAVAILABLE") _description.text = "This assignment needs an item or creature missing from this game build. You can abandon it to free a journal slot; no materials will be taken.";
        _objective.text = $"{(quest.Hunt ? "DEFEAT" : "DELIVER")}  {Name(quest.Target)}\n{Math.Min(row.Count, quest.Amount)} / {quest.Amount}";
        _objectiveFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(row.Count / (float)quest.Amount), 1);
        _sources.text = quest.Hunt ? "Your confirmed kills after accepting this quest." :
            $"CARRIED  {row.Carried}      BUILT STORAGE  {row.Stored}\nCarried items first; then eligible loaded storage.\nHost: shared built storage. Guests: own placements.";
        _reward.text = Name(quest.Reward) + "  x1";
        _rewardXp.text = $"+{quest.Xp} XP";
        string skinReward = SkinCatalog.RewardFor(quest.Id);
        _rewardXp.fontSize = skinReward.Length != 0 ? 18 : 23;
        if (skinReward.Length != 0) _rewardXp.text += "\nSKIN: " + SkinCatalog.Names[Array.IndexOf(SkinCatalog.Quests, quest.Id)];
        SetIcon(_rewardIcon, quest.Reward);
        SetIcon(_targetIcon, quest.Hunt ? quest.Reward : quest.Target);
        _targetIcon.enabled = !quest.Hunt;
        _primary.Enabled = _pendingClaim == null && (row.State == "AVAILABLE" || row.State == "READY");
        _primary.Label.text = row.State switch
        {
            "AVAILABLE" => "ACCEPT ASSIGNMENT",
            "READY" => _confirmation == "claim:" + row.Id ? "CONFIRM TURN-IN" : "COMPLETE & COLLECT",
            "LOCKED" => $"UNLOCKS AT LEVEL {quest.Level}",
            "COMPLETED" => "REWARD COLLECTED",
            "UNAVAILABLE" => "UNAVAILABLE IN THIS GAME BUILD",
            _ => "IN PROGRESS"
        };
        if (_pendingClaim != null) _primary.Label.text = "PROCESSING TURN-IN...";
        _primary.Image.color = _primary.Enabled ? _accent : _panel;
        _primary.Label.color = _primary.Enabled ? new Color(0.025f, 0.06f, 0.065f, 1) : _muted;
        _abandon.Image.gameObject.SetActive(row.State == "ACTIVE" || row.State == "READY" || row.State == "UNAVAILABLE");
        _abandon.Label.text = _confirmation == "abandon:" + row.Id ? "CONFIRM ABANDON" : "Abandon assignment";
        foreach (ActionBox button in _buttons) button.BaseColor = button.Image.color;
    }
    private static string Name(string item)
    {
        if (Enum.TryParse(item, out Items value) && ItemDatabase.GetItemInfo(value) != null)
        {
            string name = ItemDatabase.GetItemName(value);
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }
        return System.Text.RegularExpressions.Regex.Replace(item, "([a-z])([A-Z])", "$1 $2");
    }
    private static void SetIcon(Image image, string item)
    {
        image.sprite = Enum.TryParse(item, out Items value) ? ItemDatabase.GetItemInfo(value)?.icon : null;
        image.enabled = image.sprite != null;
        image.color = Color.white; image.preserveAspect = true;
    }
    private void LoadTheme()
    {
        try
        {
            string path = Path.Combine(Paths.ConfigPath, "com.holden.infernoprotocol.betterui.cfg");
            if (File.Exists(path))
                foreach (string line in File.ReadAllLines(path))
                    if (line.TrimStart().StartsWith("ColorPalette =", StringComparison.Ordinal) && int.TryParse(line.Split('=')[1].Trim(), out int palette))
                        _accent = palette switch
                        {
                            0 => new Color(0.43f, 1, 0.52f), 1 => new Color(0.43f, 0.68f, 1),
                            2 => new Color(0.35f, 0.91f, 0.93f), 3 => new Color(0.77f, 0.57f, 1),
                            _ => new Color(1, 0.76f, 0.39f)
                        };
        }
        catch { }
        _xpFill.color = _accent; _objectiveFill.color = _accent; _rewardXp.color = _accent;
        _hudFill.color = _accent; _hudLevel.color = _ink;
    }
    private void UpdateLevelHud(int xp)
    {
        if (xp < 0 || xp > 10000000 || xp == _hudXp) return;
        _hudXp = xp;
        int level = QuestCatalog.Level(xp);
        int floor = QuestCatalog.Threshold(level), ceiling = QuestCatalog.Threshold(level + 1);
        _hudLevel.text = "LV " + level;
        _hudFill.rectTransform.anchorMax = new Vector2(level >= 100 ? 1 : Mathf.Clamp01((xp - floor) / (float)(ceiling - floor)), 1);
    }
    private void Build()
    {
        _canvas = new GameObject("FieldQuests_Canvas", Il2CppType.Of<RectTransform>(), Il2CppType.Of<Canvas>(), Il2CppType.Of<CanvasScaler>());
        Canvas canvas = _canvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
        CanvasScaler scaler = _canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        _scrim = Box("Backdrop", _canvas.transform, 0, 0, 1, 1, new Color(0, 0, 0, 0.54f));
        Image window = Box("Journal", _canvas.transform, 0.04f, 0.055f, 0.96f, 0.945f, new Color(0.027f, 0.04f, 0.052f, 0.98f));
        _window = window.gameObject;
        _brand = Label("Brand", window.transform, .025f, .91f, .85f, .98f, "FIELD NOTES  /  QUEST BOARD", 31, _ink, true);
        _receipt = Label("Subtitle", window.transform, .026f, .858f, .975f, .906f, "A safer camp. A stronger survivor. One assignment at a time.", 19, _muted, true);
        Button("Close", window.transform, .88f, .90f, .975f, .965f, "CLOSE  [B]", Close);
        Box("Divider", window.transform, .025f, .85f, .975f, .852f, _muted * .4f);
        Image left = Box("Sidebar", window.transform, .02f, .13f, .235f, .825f, _panel);
        _sidebarTitle = Label("Agent", left.transform, .09f, .84f, .91f, .96f, "THE QUEST BOARD", 21, _ink, true);
        _welcome = Label("Welcome", left.transform, .09f, .69f, .91f, .84f, "Bring what the camp needs.\nTake what keeps you alive.", 18, _muted);
        _finishPrevious = Button("PreviousFinishes", left.transform, .045f, .71f, .30f, .79f, "<", () => Filter(SkinCatalog.PageStart(_filter, -1)));
        _finishNext = Button("NextFinishes", left.transform, .70f, .71f, .955f, .79f, ">", () => Filter(SkinCatalog.PageStart(_filter, 1)));
        _finishPage = Label("FinishPage", left.transform, .32f, .71f, .68f, .79f, "", 17, _muted);
        _finishPage.alignment = TextAlignmentOptions.Center;
        _collection = Label("Collection", left.transform, .09f, .025f, .91f, .18f, "", 16, _muted);
        _finishPrevious.Image.gameObject.SetActive(false); _finishNext.Image.gameObject.SetActive(false);
        for (int i = 0; i < 5; i++)
        {
            int tab = i;
            _tabs[i] = Button("Filter" + i, left.transform, .045f, .58f - .09f * i, .955f, .66f - .09f * i, _filters[i], () => Filter(tab));
        }
        _level = Label("Level", left.transform, .09f, .12f, .9f, .2f, "LEVEL 01", 25, _ink, true);
        Image xpTrack = Box("XPTrack", left.transform, .09f, .092f, .91f, .105f, new Color(.16f, .2f, .22f));
        _levelTrack = xpTrack;
        _xpFill = Box("XPFill", xpTrack.transform, 0, 0, 0, 1, _accent);
        _xp = Label("XPText", left.transform, .09f, .01f, .95f, .085f, "", 15, _muted);
        Image list = Box("Assignments", window.transform, .248f, .13f, .598f, .825f, Color.clear);
        _listTitle = Label("ListTitle", list.transform, .025f, .91f, .99f, 1, "ALL ASSIGNMENTS", 20, _ink, true);
        for (int i = 0; i < 6; i++)
        {
            _rows[i] = Button("Quest" + i, list.transform, .01f, .79f - .132f * i, .99f, .912f - .132f * i, "", () => { });
            Place(_rows[i].Label.rectTransform, .17f, .42f, .97f, .89f);
            _rows[i].Label.alignment = TextAlignmentOptions.MidlineLeft;
            _rows[i].Label.fontSize = 21;
            _rowStatus[i] = Label("State", _rows[i].Image.transform, .17f, .08f, .97f, .40f, "", 13, _muted);
            _rowIcons[i] = Box("RewardPreview", _rows[i].Image.transform, .015f, .12f, .145f, .88f, Color.white);
        }
        Button("Previous", list.transform, .02f, .005f, .22f, .08f, "< PREV", () => Select(_selection - 6));
        _pagination = Label("Pages", list.transform, .28f, .005f, .72f, .08f, "", 15, _muted);
        _pagination.alignment = TextAlignmentOptions.Center;
        Button("Next", list.transform, .78f, .005f, .98f, .08f, "NEXT >", () => Select(_selection + 6));
        Image detail = Box("Briefing", window.transform, .615f, .13f, .98f, .825f, _panel);
        _briefingTitle = Label("BriefingTitle", detail.transform, .06f, .92f, .94f, .99f, "ASSIGNMENT BRIEF", 14, _accent, true);
        _title = Label("Title", detail.transform, .06f, .82f, .94f, .925f, "", 29, _ink, true);
        _description = Label("Description", detail.transform, .06f, .65f, .94f, .82f, "", 19, _muted);
        _targetIcon = Box("TargetIcon", detail.transform, .06f, .51f, .20f, .64f, Color.white);
        _objective = Label("Objective", detail.transform, .24f, .505f, .94f, .64f, "", 21, _ink, true);
        Image track = Box("ObjectiveTrack", detail.transform, .06f, .482f, .94f, .492f, new Color(.16f, .2f, .22f));
        _objectiveTrack = track;
        _objectiveFill = Box("Progress", track.transform, 0, 0, 0, 1, _accent);
        _sources = Label("Sources", detail.transform, .06f, .35f, .94f, .477f, "", 16, _muted);
        Image rewardCard = Box("Reward", detail.transform, .045f, .205f, .955f, .34f, new Color(.065f, .10f, .12f));
        _rewardIcon = Box("Icon", rewardCard.transform, .035f, .06f, .20f, .94f, Color.white);
        _reward = Label("Name", rewardCard.transform, .23f, .45f, .98f, .95f, "", 19, _ink, true);
        _rewardXp = Label("Experience", rewardCard.transform, .23f, .05f, .98f, .48f, "", 23, _accent, true);
        _primary = Button("Primary", detail.transform, .06f, .095f, .94f, .183f, "", Primary);
        _abandon = Button("Abandon", detail.transform, .15f, .012f, .85f, .082f, "", Abandon);
        _status = Label("Status", window.transform, .025f, .018f, .975f, .10f, "", 17, _muted);
        _prompt = Label("Prompt", _canvas.transform, .3f, .18f, .7f, .27f, "", 22, _ink, true);
        _prompt.alignment = TextAlignmentOptions.Center;
        Image shade = Box("ConfirmationShade", window.transform, 0, 0, 1, 1, new Color(0, 0, 0, .75f));
        _dialog = shade.gameObject;
        Image card = Box("ConfirmationCard", shade.transform, .25f, .20f, .75f, .80f, _panel);
        Box("AccentRule", card.transform, 0, .986f, 1, 1, _accent);
        _dialogTitle = Label("Heading", card.transform, .065f, .81f, .935f, .96f, "", 30, _ink, true);
        _dialogBody = Label("Details", card.transform, .065f, .40f, .935f, .80f, "", 23, _ink);
        Image reward = Box("Receipt", card.transform, .05f, .21f, .95f, .39f, new Color(.075f, .105f, .12f));
        _dialogIcon = Box("RewardIcon", reward.transform, .025f, .08f, .17f, .92f, Color.white);
        _dialogReward = Label("Reward", reward.transform, .20f, .06f, .97f, .94f, "", 22, _ink, true);
        _dialogCancel = Button("Cancel", card.transform, .065f, .055f, .475f, .17f, "CANCEL", CancelDialog);
        _dialogConfirm = Button("Confirm", card.transform, .505f, .055f, .935f, .17f, "CONFIRM", ConfirmDialog);
        _dialog.SetActive(false);
        Image hud = Box("ExplorationLevel", _canvas.transform, .025f, .911f, .10f, .947f, new Color(.025f, .04f, .052f, .28f));
        _levelHud = hud.gameObject;
        _hudLevel = Label("LevelNumber", hud.transform, .065f, .25f, .95f, .95f, "LV 1", 18, _ink, true);
        Image hudTrack = Box("Track", hud.transform, .07f, .15f, .93f, .20f, new Color(.15f, .19f, .21f, .65f));
        _hudFill = Box("Fill", hudTrack.transform, 0, 0, 0, 1, _accent);
        _levelHud.SetActive(false);
        _window.SetActive(false); _scrim.gameObject.SetActive(false); _prompt.gameObject.SetActive(false);
    }
    private ActionBox Button(string name, Transform parent, float x0, float y0, float x1, float y1, string text, Action click)
    {
        Image image = Box(name, parent, x0, y0, x1, y1, _panel);
        var button = new ActionBox { Image = image, Click = click, Label = Label("Text", image.transform, .035f, .06f, .965f, .94f, text, 17, _ink, true) };
        button.Label.alignment = TextAlignmentOptions.Center;
        _buttons.Add(button); return button;
    }
    private static Image Box(string name, Transform parent, float x0, float y0, float x1, float y1, Color color)
    {
        var go = new GameObject(name, Il2CppType.Of<RectTransform>(), Il2CppType.Of<CanvasRenderer>(), Il2CppType.Of<Image>());
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
        Place(image.rectTransform, x0, y0, x1, y1); return image;
    }
    private static TextMeshProUGUI Label(string name, Transform parent, float x0, float y0, float x1, float y1, string text, float size, Color color, bool bold = false)
    {
        var go = new GameObject(name, Il2CppType.Of<RectTransform>(), Il2CppType.Of<CanvasRenderer>(), Il2CppType.Of<TextMeshProUGUI>());
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.text = text; label.fontSize = size; label.fontSizeMax = size; label.fontSizeMin = size * .78f;
        label.enableAutoSizing = true; label.color = color; label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        label.enableWordWrapping = true; label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false; label.richText = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        Place(label.rectTransform, x0, y0, x1, y1); return label;
    }
    private static void Place(RectTransform rect, float x0, float y0, float x1, float y1)
    {
        rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    public void Dispose()
    {
        foreach (Sprite sprite in _finishPreviews.Values)
            if (sprite != null) { UnityEngine.Object.Destroy(sprite.texture); UnityEngine.Object.Destroy(sprite); }
        _finishPreviews.Clear();
        if (BlockingInput) RestoreInput();
        IsOpen = false;
        if (_canvas != null) UnityEngine.Object.Destroy(_canvas);
        _canvas = null;
    }
}
