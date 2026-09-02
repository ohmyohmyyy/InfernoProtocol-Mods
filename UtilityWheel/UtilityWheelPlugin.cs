using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Game.PlayerOperations;
using Game.UI;
using HarmonyLib;
using UnityEngine;

namespace InfernoProtocol.UtilityWheel;

internal enum ControllerWheelButton
{
    LeftShoulder,
    RightShoulder
}

[BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
public sealed class UtilityWheelPlugin : BasePlugin
{
    private static UtilityWheelController _controller;
    private Harmony _harmony;

    internal static ManualLogSource ModLog { get; private set; }
    internal static ConfigEntry<bool> Enabled { get; private set; }
    internal static ConfigEntry<ControllerWheelButton> ControllerButton { get; private set; }
    internal static ConfigEntry<bool> KeyboardFallback { get; private set; }
    internal static ConfigEntry<bool> MatchBetterUI { get; private set; }
    internal static ConfigEntry<int> FallbackPalette { get; private set; }
    internal static ConfigEntry<float> Opacity { get; private set; }
    internal static ConfigEntry<float> StickDeadzone { get; private set; }

    public override void Load()
    {
        ModLog = Log;
        Enabled = Config.Bind("General", "Enabled", true, "Enables the controller utility wheel.");
        ControllerButton = Config.Bind(
            "Input",
            "ControllerButton",
            ControllerWheelButton.LeftShoulder,
            "Button held to open the wheel: LeftShoulder or RightShoulder.");
        KeyboardFallback = Config.Bind("Input", "KeyboardTabFallback", true,
            "Also allows holding Tab to open the wheel for testing or keyboard play.");
        MatchBetterUI = Config.Bind("Appearance", "MatchBetterUIPalette", true,
            "Reads BetterUI's remembered crafting color when BetterUI is installed.");
        FallbackPalette = Config.Bind(
            "Appearance",
            "FallbackPalette",
            0,
            new ConfigDescription(
                "Palette used without BetterUI: 0 green, 1 blue, 2 cyan, 3 violet, 4 amber.",
                new AcceptableValueRange<int>(0, 4)));
        Opacity = Config.Bind(
            "Appearance",
            "WheelOpacity",
            0.72f,
            new ConfigDescription("Opacity of the radial panels.", new AcceptableValueRange<float>(0.35f, 0.95f)));
        StickDeadzone = Config.Bind(
            "Input",
            "StickDeadzone",
            0.32f,
            new ConfigDescription("Right-stick distance required to change selection.", new AcceptableValueRange<float>(0.15f, 0.75f)));

        _controller = AddComponent<UtilityWheelController>();
        _harmony = new Harmony(PluginInfo.Guid);
        _harmony.PatchAll(typeof(UtilityWheelPatches));
        Log.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} loaded. Hold {ControllerButton.Value} to open it.");
    }

    public override bool Unload()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
        if (_controller != null)
        {
            Object.Destroy(_controller);
            _controller = null;
        }

        return true;
    }

    internal static bool ShouldSuppressNativeSelection =>
        Enabled != null && Enabled.Value && UtilityWheelController.ActivationControlIsPressed &&
        !UtilityWheelController.IsCommittingSelection;

    [HarmonyPatch]
    private static class UtilityWheelPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.OnInventorySlotSelected))]
        private static bool BeforeNativeSlotSelection()
        {
            return !ShouldSuppressNativeSelection;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SkillWheelManager), nameof(SkillWheelManager.OpenWheel))]
        private static bool BeforeSkillWheelOpen()
        {
            return !(Enabled != null && Enabled.Value && UtilityWheelController.ActivationControlIsPressed);
        }
    }
}
