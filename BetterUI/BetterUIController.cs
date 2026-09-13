using System;
using Game.UI;
using Game.UI.Trade;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InfernoProtocol.BetterUI;

/// <summary>
/// Styles crafting, building, trading, the explicitly opened genetics panel,
/// and the compact durability readout on the player's hotbar.
/// </summary>
public sealed class BetterUIController : MonoBehaviour
{
    private readonly CraftingMenuOverhaul _craftingMenu = new();
    private readonly BuildingMenuOverhaul _buildingMenu = new();
    private readonly TraderMenuOverhaul _traderMenu = new();
    private readonly GeneticsTree _genetics = new();
    private readonly HotbarDurabilityDisplay _hotbarDurability = new();
    private float _nextRefresh;
    private float _nextBuildingRefresh;
    private float _retryAt;
    private float _buildingRetryAt;
    private float _nextTraderRefresh;
    private float _traderRetryAt;
    private bool _sessionEnabled = true;
    private bool _craftingOpen;
    private bool _buildingOpen;
    private bool _traderOpen;
    private bool _failureReported;
    private bool _buildingFailureReported;
    private bool _traderFailureReported;

    public BetterUIController(IntPtr pointer) : base(pointer)
    {
    }

    private void Update()
    {
        HandleToggle();
        _genetics.Tick(_sessionEnabled && BetterUIPlugin.Enabled.Value);

        bool enabled = _sessionEnabled && BetterUIPlugin.Enabled.Value;
        _hotbarDurability.Tick(enabled);
        UpdateCrafting(enabled);
        UpdateBuilding(enabled);
        UpdateTrader(enabled);
    }

    private void UpdateTrader(bool enabled)
    {
        if (!enabled || !BetterUIPlugin.TraderEnabled.Value || !_traderOpen)
        {
            _traderMenu.SetVisible(false);
            return;
        }

        TradeUI trader = TradeUI.instance;
        if (trader == null)
        {
            _traderMenu.SetVisible(false);
            return;
        }

        _traderMenu.SetVisible(true);
        if (_traderMenu.HandleInput()) _nextTraderRefresh = 0f;
        float now = Time.unscaledTime;
        if (now < _nextTraderRefresh || now < _traderRetryAt) return;
        _nextTraderRefresh = now + BetterUIPlugin.RefreshSeconds;
        try
        {
            _traderMenu.Apply(trader);
            _traderRetryAt = 0f;
            _traderFailureReported = false;
        }
        catch (Exception exception)
        {
            _traderRetryAt = now + 2f;
            if (!_traderFailureReported)
            {
                _traderFailureReported = true;
                BetterUIPlugin.ModLog.LogWarning($"Could not style the trader menu; retrying: {exception}");
            }
        }
    }

    private void UpdateCrafting(bool enabled)
    {
        CraftingTableUI crafting = CraftingTableUI.instance;
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

    private void UpdateBuilding(bool enabled)
    {
        BuildTemplatesUI building = BuildTemplatesUI.instance;
        if (!enabled || !_buildingOpen || building == null)
        {
            _buildingMenu.SetVisible(false);
            return;
        }

        _buildingMenu.SetVisible(true);
        if (_buildingMenu.HandleInput())
        {
            _nextBuildingRefresh = 0f;
        }

        float now = Time.unscaledTime;
        if (now < _nextBuildingRefresh || now < _buildingRetryAt)
        {
            return;
        }

        _nextBuildingRefresh = now + BetterUIPlugin.RefreshSeconds;
        try
        {
            _buildingMenu.Apply(building);
            _buildingRetryAt = 0f;
            _buildingFailureReported = false;
        }
        catch (Exception exception)
        {
            _buildingRetryAt = now + 2f;
            if (!_buildingFailureReported)
            {
                _buildingFailureReported = true;
                BetterUIPlugin.ModLog.LogWarning($"Could not style the building menu; retrying: {exception}");
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
            _buildingMenu.SetVisible(false);
            _traderMenu.SetVisible(false);
        }

        BetterUIPlugin.ModLog.LogInfo(
            $"BetterUI menus {(_sessionEnabled ? "enabled" : "paused")} for this session");
        _nextRefresh = 0f;
        _nextBuildingRefresh = 0f;
        _nextTraderRefresh = 0f;
    }

    internal void SetBuildingOpen(bool open)
    {
        _buildingOpen = open;
        _nextBuildingRefresh = 0f;
        if (!open)
        {
            _buildingMenu.SetVisible(false);
        }
    }

    internal void PrepareBuildingMenu(BuildTemplatesUI building)
    {
        if (!_sessionEnabled || !BetterUIPlugin.Enabled.Value || building == null)
        {
            return;
        }

        try
        {
            _buildingMenu.Prepare(building);
        }
        catch (Exception exception)
        {
            if (!_buildingFailureReported)
            {
                _buildingFailureReported = true;
                BetterUIPlugin.ModLog.LogWarning($"Could not prepare the building menu: {exception}");
            }
        }
    }

    internal void SetBuildingFilter(Game.LevelOperations.PlacableType filter)
    {
        _buildingMenu.SetActiveFilter(filter);
        _nextBuildingRefresh = 0f;
    }

    internal void ResetBuildingFilter()
    {
        _buildingMenu.ResetActiveFilter();
        _nextBuildingRefresh = 0f;
    }

    internal void InvalidateBuilding()
    {
        _nextBuildingRefresh = 0f;
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

    internal void PrepareTraderMenu(TradeUI trader)
    {
        if (!_sessionEnabled || !BetterUIPlugin.Enabled.Value || !BetterUIPlugin.TraderEnabled.Value || trader == null)
            return;
        try
        {
            _traderMenu.Prepare(trader);
        }
        catch (Exception exception)
        {
            if (!_traderFailureReported)
            {
                _traderFailureReported = true;
                BetterUIPlugin.ModLog.LogWarning($"Could not prepare the trader menu: {exception}");
            }
        }
    }

    internal void SetTraderOpen(bool open)
    {
        _traderOpen = open;
        _nextTraderRefresh = 0f;
        if (!open) _traderMenu.SetVisible(false);
    }

    internal void InvalidateTrader()
    {
        _nextTraderRefresh = 0f;
    }

    private void OnDestroy()
    {
        _hotbarDurability.Dispose();
        _genetics.Dispose();
    }
}
