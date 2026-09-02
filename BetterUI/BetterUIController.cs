using System;
using Game.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InfernoProtocol.BetterUI;

/// <summary>
/// Keeps BetterUI deliberately scoped to the crafting screen. In particular,
/// this controller never scans or modifies the always-present gameplay,
/// inventory, storage, hotbar, pause-menu, or tooltip hierarchies.
/// </summary>
public sealed class BetterUIController : MonoBehaviour
{
    private readonly CraftingMenuOverhaul _craftingMenu = new();
    private float _nextRefresh;
    private float _retryAt;
    private bool _sessionEnabled = true;
    private bool _craftingOpen;
    private bool _failureReported;

    public BetterUIController(IntPtr pointer) : base(pointer)
    {
    }

    private void Update()
    {
        HandleToggle();

        CraftingTableUI crafting = CraftingTableUI.instance;
        bool enabled = _sessionEnabled && BetterUIPlugin.Enabled.Value;
        if (!enabled || !_craftingOpen || crafting == null)
        {
            _craftingMenu.SetVisible(false);
            return;
        }

        _craftingMenu.SetVisible(true);
        if (_craftingMenu.HandleInput())
        {
            _nextRefresh = 0f;
        }

        // Progress needs frame-by-frame updates so the in-panel fill animation is
        // smooth even though the rest of the crafting screen refreshes slowly.
        _craftingMenu.RefreshCraftingProgress(crafting);

        float now = Time.unscaledTime;
        if (now < _nextRefresh || now < _retryAt)
        {
            return;
        }

        _nextRefresh = now + BetterUIPlugin.RefreshSeconds;
        try
        {
            _craftingMenu.Apply(crafting);
            _retryAt = 0f;
            _failureReported = false;
        }
        catch (Exception exception)
        {
            _retryAt = now + 2f;
            if (!_failureReported)
            {
                _failureReported = true;
                BetterUIPlugin.ModLog.LogWarning($"Could not style the crafting menu; retrying: {exception}");
            }
        }
    }

    private void HandleToggle()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.f8Key.wasPressedThisFrame)
        {
            return;
        }

        _sessionEnabled = !_sessionEnabled;
        if (!_sessionEnabled)
        {
            _craftingMenu.SetVisible(false);
        }

        BetterUIPlugin.ModLog.LogInfo(
            $"BetterUI crafting menu {(_sessionEnabled ? "enabled" : "paused")} for this session");
        _nextRefresh = 0f;
    }

    internal void SetCraftingOpen(bool open)
    {
        _craftingOpen = open;
        _nextRefresh = 0f;
        if (!open)
        {
            _craftingMenu.SetVisible(false);
        }
    }
}
