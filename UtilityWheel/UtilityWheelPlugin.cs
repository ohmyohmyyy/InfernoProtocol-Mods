using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using FrankMods;
using Game.PlayerOperations;
using Game.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace InfernoProtocol.UtilityWheel;

[BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
[BepInDependency(FrankModsCorePlugin.Guid, FrankModsCorePlugin.Version)]
public sealed class UtilityWheelPlugin : BasePlugin
{
    private static UtilityWheelController _controller;
    private Harmony _harmony;

    internal static ManualLogSource ModLog { get; private set; }
    internal static ConfigEntry<bool> Enabled { get; private set; }
    internal static ConfigEntry<GamepadButton> ControllerButton { get; private set; }
    internal static ConfigEntry<bool> KeyboardFallback { get; private set; }
    internal static ConfigEntry<Key> KeyboardKey { get; private set; }
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
            GamepadButton.LeftShoulder,
            "Controller button held to open the utility wheel.");
        KeyboardFallback = Config.Bind("Input", "KeyboardTabFallback", true,
            "Also allows holding the configured keyboard shortcut to open the wheel.");
        KeyboardKey = Config.Bind("Input", "KeyboardKey", Key.Tab,
            "Keyboard shortcut held to open the utility wheel.");
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

        ModSettingsRegistry.RegisterKeyBinding(PluginInfo.Guid, PluginInfo.Name, "KeyboardKey",
            "Open utility wheel", Key.Tab, () => KeyboardKey.Value, key => KeyboardKey.Value = key);
        ModSettingsRegistry.RegisterGamepadBinding(PluginInfo.Guid, PluginInfo.Name, "ControllerButton",
            "Open utility wheel", GamepadButton.LeftShoulder, () => ControllerButton.Value,
            button => ControllerButton.Value = button);

        _controller = AddComponent<UtilityWheelController>();
        _harmony = new Harmony(PluginInfo.Guid);
        _harmony.PatchAll(typeof(UtilityWheelPatches));
        Log.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} loaded. Hold {ControllerButton.Value} to open it.");
    }

    public override bool Unload()
    {
        ModSettingsRegistry.UnregisterMod(PluginInfo.Guid);
        _harmony?.UnpatchSelf();
        _harmony = null;
        if (_controller != null)
        {
            Object.Destroy(_controller);
            _controller = null;
        }

        return true;
    }

    internal static bool KeyboardKeyHeld(Keyboard keyboard)
    {
        if (!KeyboardFallback.Value || keyboard == null || KeyboardKey == null || KeyboardKey.Value == Key.None) return false;
        try { return keyboard[KeyboardKey.Value].isPressed; }
        catch { return false; }
    }

    internal static bool KeyboardKeyPressed(Keyboard keyboard)
    {
        if (!KeyboardFallback.Value || keyboard == null || KeyboardKey == null || KeyboardKey.Value == Key.None) return false;
        try { return keyboard[KeyboardKey.Value].wasPressedThisFrame; }
        catch { return false; }
    }

    internal static bool ControllerButtonHeld(Gamepad gamepad)
    {
        if (gamepad == null || ControllerButton == null) return false;
        try { return gamepad[ControllerButton.Value].isPressed; }
        catch { return false; }
    }

    internal static bool ControllerButtonPressed(Gamepad gamepad)
    {
        if (gamepad == null || ControllerButton == null) return false;
        try { return gamepad[ControllerButton.Value].wasPressedThisFrame; }
        catch { return false; }
    }

    internal static string ControllerButtonName => ControllerButton?.Value switch
    {
        GamepadButton.South => "A", GamepadButton.East => "B", GamepadButton.West => "X", GamepadButton.North => "Y",
        GamepadButton.LeftShoulder => "LB", GamepadButton.RightShoulder => "RB",
        GamepadButton.LeftTrigger => "LT", GamepadButton.RightTrigger => "RT",
        GamepadButton.LeftStick => "L3", GamepadButton.RightStick => "R3",
        GamepadButton.Start => "MENU", GamepadButton.Select => "VIEW",
        GamepadButton.DpadUp => "D-PAD UP", GamepadButton.DpadDown => "D-PAD DOWN",
        GamepadButton.DpadLeft => "D-PAD LEFT", GamepadButton.DpadRight => "D-PAD RIGHT",
        _ => "LB"
    };

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
