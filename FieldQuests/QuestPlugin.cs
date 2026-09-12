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

[BepInPlugin("com.holden.infernoprotocol.fieldquests", "ContentPlus", "0.5.1")]
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
        Log.LogInfo("ContentPlus 0.5.1 loaded. Quest board, XP progression and equipment finishes. Use Interact to read the board (F9 also works).");
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
        [HarmonyPostfix, HarmonyPatch(typeof(ANetworkEntity), nameof(ANetworkEntity.ValidateDamageRequestOnServerSide))]
        private static void ValidatedDamage(ANetworkEntity __instance, ulong __1, bool __result)
        {
            try
            {
                ANPC npc = __instance?.TryCast<ANPC>();
                if (npc != null) Controller?.Service?.ValidateDamage(npc, __1, __result);
            }
            catch (Exception e) { LogSource.LogWarning("Quest damage attribution: " + e.Message); }
        }
        [HarmonyPrefix, HarmonyPatch(typeof(ANetworkEntity), nameof(ANetworkEntity.DamageServer))]
        private static void BeginServerDamage(ANetworkEntity __instance, ulong __1)
        {
            try
            {
                ANPC npc = __instance?.TryCast<ANPC>();
                if (npc != null) Controller?.Service?.BeginDamage(npc, __1);
            }
            catch (Exception e) { LogSource.LogWarning("Quest server damage attribution: " + e.Message); }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(ANetworkEntity), nameof(ANetworkEntity.DamageServer))]
        private static void EndServerDamage(ANetworkEntity __instance, ulong __1)
        {
            try
            {
                ANPC npc = __instance?.TryCast<ANPC>();
                if (npc != null) Controller?.Service?.EndDamage(npc, __1);
            }
            catch (Exception e) { LogSource.LogWarning("Quest server damage cleanup: " + e.Message); }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(ANPC), nameof(ANPC.OnNetworkSpawn))]
        private static void Spawn(ANPC __instance) => Controller?.Service?.Respawn(__instance);
        [HarmonyPrefix, HarmonyPatch(typeof(Player), nameof(Player.ApplyMovement))]
        private static bool Move() => Controller == null || !Controller.BlockingInput;
    }
}
