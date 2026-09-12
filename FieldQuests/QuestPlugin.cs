using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Game.AI;
using Game.Combat;
using Game.PlayerOperations;
using HarmonyLib;
using SaveSystem;

namespace InfernoProtocol.FieldQuests;

[BepInPlugin("com.holden.infernoprotocol.fieldquests", "ContentPlus", "0.5.2")]
public sealed class QuestPlugin : BasePlugin
{
    internal static ManualLogSource LogSource;
    internal static QuestController Controller;
    private Harmony _harmony;
    public override void Load()
    {
        LogSource = Log;
        Controller = AddComponent<QuestController>();
        _harmony = new Harmony("com.holden.infernoprotocol.fieldquests");
        _harmony.PatchAll(typeof(Hooks));
        Log.LogInfo("ContentPlus 0.5.2 loaded. Quest board, XP progression and equipment finishes. Use Interact to read the board (F9 also works).");
    }
    public override bool Unload()
    {
        Controller?.Cleanup();
        if (Controller != null) UnityEngine.Object.Destroy(Controller);
        Controller = null;
        _harmony?.UnpatchSelf();
        return true;
    }
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
