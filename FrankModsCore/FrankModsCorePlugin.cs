using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace FrankMods;

[BepInPlugin(Guid, Name, Version)]
public sealed class FrankModsCorePlugin : BasePlugin
{
    public const string Guid = "com.holden.infernoprotocol.frankmodscore";
    public const string Name = "FrankMods Core";
    public const string Version = "1.0.0";

    internal static ManualLogSource ModLog { get; private set; }
    internal static ModSettingsController Controller { get; private set; }
    private Harmony _harmony;

    public override void Load()
    {
        ModLog = Log;
        ClassInjector.RegisterTypeInIl2Cpp<ModSettingsController>();
        Controller = AddComponent<ModSettingsController>();
        ModSettingsRegistry.SetController(Controller);
        _harmony = new Harmony(Guid);
        _harmony.PatchAll(typeof(PauseMenuPatches));
        Log.LogInfo($"{Name} {Version} loaded");
    }

    public override bool Unload()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
        ModSettingsRegistry.SetController(null);
        if (Controller != null)
        {
            Controller.Cleanup();
            Object.Destroy(Controller);
            Controller = null;
        }
        return true;
    }
}

[HarmonyPatch]
internal static class PauseMenuPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Game.UI.MainMenuVerticalButtons), "Start")]
    private static void MainMenuStarted(Game.UI.MainMenuVerticalButtons __instance)
    {
        FrankModsCorePlugin.Controller?.AttachPauseMenu(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Game.UI.MazeButton), nameof(Game.UI.MazeButton.OnPointerClick))]
    private static bool ModSettingsClicked(Game.UI.MazeButton __instance)
    {
        if (__instance == null || __instance.gameObject.name != ModSettingsController.PauseButtonName) return true;
        FrankModsCorePlugin.Controller?.Open();
        return false;
    }
}
