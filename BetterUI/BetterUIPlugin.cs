using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Game.UI;
using HarmonyLib;
using UnityEngine;

namespace InfernoProtocol.BetterUI;

[BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
public sealed class BetterUIPlugin : BasePlugin
{
    private static BetterUIController _controller;
    private Harmony _harmony;

    internal static ManualLogSource ModLog { get; private set; }
    internal static ConfigEntry<bool> Enabled { get; private set; }
    internal static ConfigEntry<float> RefreshInterval { get; private set; }
    internal static ConfigEntry<int> ColorPalette { get; private set; }
    internal static float RefreshSeconds => Sanitize(RefreshInterval.Value, 0.5f, 0.2f, 5f);
    internal static int PaletteIndex => ColorPalette != null
        ? Mathf.Clamp(ColorPalette.Value, 0, BetterUITheme.PaletteCount - 1)
        : 0;

    public override void Load()
    {
        ModLog = Log;
        Enabled = Config.Bind("General", "Enabled", true,
            "Enables the BetterUI crafting menu. F8 pauses or resumes it for the current session.");
        RefreshInterval = Config.Bind(
            "General",
            "RefreshInterval",
            0.5f,
            new ConfigDescription(
                "Seconds between lightweight crafting-menu state refreshes.",
                new AcceptableValueRange<float>(0.2f, 5f)));
        ColorPalette = Config.Bind(
            "Crafting",
            "ColorPalette",
            0,
            new ConfigDescription(
                "Remembered crafting-terminal palette: 0 green, 1 blue, 2 cyan, 3 violet, 4 amber.",
                new AcceptableValueRange<int>(0, BetterUITheme.PaletteCount - 1)));

        _controller = AddComponent<BetterUIController>();
        _harmony = new Harmony(PluginInfo.Guid);
        _harmony.PatchAll(typeof(CraftingVisibilityPatches));
        Log.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} loaded");
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

    internal static void SetCraftingOpen(bool open)
    {
        _controller?.SetCraftingOpen(open);
    }

    internal static void CyclePalette()
    {
        if (ColorPalette == null)
        {
            return;
        }

        ColorPalette.Value = (PaletteIndex + 1) % BetterUITheme.PaletteCount;
        string[] names = { "green", "blue", "cyan", "violet", "amber" };
        ModLog.LogInfo($"BetterUI crafting palette changed to {names[PaletteIndex]}.");
    }

    private static float Sanitize(float value, float fallback, float minimum, float maximum)
    {
        return float.IsNaN(value) || float.IsInfinity(value)
            ? fallback
            : Mathf.Clamp(value, minimum, maximum);
    }

    [HarmonyPatch]
    private static class CraftingVisibilityPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CraftingTableUI), nameof(CraftingTableUI.Show))]
        private static void AfterShow()
        {
            SetCraftingOpen(true);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CraftingTableUI), nameof(CraftingTableUI.Hide))]
        private static void AfterHide()
        {
            SetCraftingOpen(false);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CraftingTableUI), nameof(CraftingTableUI.HideRaw))]
        private static void AfterHideRaw()
        {
            SetCraftingOpen(false);
        }
    }
}
