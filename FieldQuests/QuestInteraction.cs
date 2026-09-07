using System;
using System.Collections.Generic;
using Game.PlayerOperations;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace InfernoProtocol.FieldQuests;

// Reads the actual rebound Interact buttons, while temporarily reserving those buttons
// for the nearby quest board. Other gameplay controls remain untouched.
internal sealed class QuestInteraction : IDisposable
{
    private readonly List<ButtonControl> _buttons = new();
    private readonly List<InputAction> _disabled = new();
    private bool _active;
    internal bool Pressed => (_active && _buttons.Exists(b => b.wasPressedThisFrame)) || Keyboard.current?.f9Key.wasPressedThisFrame == true;
    internal string Label
    {
        get
        {
            bool gamepad = PlayerInputDispatcher.playingWithGamepad;
            foreach (ButtonControl button in _buttons)
                if ((button.device.TryCast<Gamepad>() != null) == gamepad)
                    return button.displayName;
            return "F9";
        }
    }
    internal void Reserve()
    {
        if (_active) return;
        InputAction interact = PlayerInputDispatcher.actions?.interact;
        if (interact == null || !interact.enabled) return;
        _buttons.Clear();
        foreach (InputControl control in interact.controls)
        {
            ButtonControl button = control.TryCast<ButtonControl>();
            if (button != null) _buttons.Add(button);
        }
        var maps = PlayerInputDispatcher.inputActionsAsset?.actionMaps;
        if (maps != null)
            foreach (InputActionMap map in maps)
                foreach (InputAction action in map.actions)
                {
                    if (!action.enabled) continue;
                    bool overlaps = false;
                    foreach (InputControl control in action.controls)
                        if (_buttons.Exists(b => b.path == control.path)) { overlaps = true; break; }
                    if (overlaps) _disabled.Add(action);
                }
        foreach (InputAction action in _disabled) action.Disable();
        _active = true;
    }
    internal void Release(bool force = false)
    {
        // Don't hand a held talk/reload button to the game when the player walks away.
        if (!force && _buttons.Exists(b => b.isPressed)) return;
        foreach (InputAction action in _disabled)
            // A different menu may have disabled the entire map since we reserved Talk.
            // Leave it disabled; that menu owns re-enabling its map.
            if (action.actionMap == null || action.actionMap.enabled) action.Enable();
        _disabled.Clear(); _buttons.Clear(); _active = false;
    }
    public void Dispose() => Release(true);
}
