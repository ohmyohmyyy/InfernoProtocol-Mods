using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using FrankMods;
using Game.AI;
using Game.Combat;
using Game.PlayerOperations;
using HarmonyLib;
using SaveSystem;
using UnityEngine.InputSystem;

namespace InfernoProtocol.FieldQuests;

[BepInPlugin("com.holden.infernoprotocol.fieldquests", "ContentPlus", "0.5.3")]
[BepInDependency(FrankModsCorePlugin.Guid, FrankModsCorePlugin.Version)]
public sealed class QuestPlugin : BasePlugin
{
    internal const string Guid = "com.holden.infernoprotocol.fieldquests";
    internal static ManualLogSource LogSource;
    internal static QuestController Controller;
    internal static ConfigEntry<Key> BoardKeyboardKey;
    private Harmony _harmony;
    public override void Load()
    {
        LogSource = Log;
        BoardKeyboardKey = Config.Bind("Input", "BoardKeyboardKey", Key.F9,
            "Keyboard shortcut used near the quest board in addition to the native Interact binding.");
        ModSettingsRegistry.RegisterKeyBinding(Guid, "ContentPlus", "BoardKeyboardKey",
            "Open quest board", Key.F9, () => BoardKeyboardKey.Value, key => BoardKeyboardKey.Value = key);
        Controller = AddComponent<QuestController>();
        _harmony = new Harmony("com.holden.infernoprotocol.fieldquests");
        _harmony.PatchAll(typeof(Hooks));
        Log.LogInfo($"ContentPlus 0.5.3 loaded. Quest board, XP progression and equipment finishes. Use Interact to read the board ({BoardKeyName} also works).");
    }
    public override bool Unload()
    {
        ModSettingsRegistry.UnregisterMod(Guid);
        Controller?.Cleanup();
        if (Controller != null) UnityEngine.Object.Destroy(Controller);
        Controller = null;
        _harmony?.UnpatchSelf();
        return true;
    }
    internal static bool BoardKeyPressed(Keyboard keyboard)
    {
        if (keyboard == null || BoardKeyboardKey == null || BoardKeyboardKey.Value == Key.None) return false;
        try { return keyboard[BoardKeyboardKey.Value].wasPressedThisFrame; }
        catch { return false; }
    }
    internal static string BoardKeyName => BoardKeyboardKey?.Value.ToString().ToUpperInvariant() ?? "F9";
    [HarmonyPatch]
    private static class Hooks
    {
        [HarmonyPostfix, HarmonyPatch(typeof(ANPC), nameof(ANPC.OnDeathServer_InternalServer))]
        private static void Death(ANPC __instance, ref DamageData __0)
        {
            try { Controller?.Service?.Death(__instance, __0); }
            catch (Exception e) { LogSource.LogWarning("Quest kill tracking: " + e.Message); }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(ANPC), nameof(ANPC.OnNetworkSpawn))]
        private static void Spawn(ANPC __instance) => Controller?.Service?.Respawn(__instance);
        [HarmonyPrefix, HarmonyPatch(typeof(Player), nameof(Player.ApplyMovement))]
        private static bool Move() => Controller == null || !Controller.BlockingInput;
    }
}
