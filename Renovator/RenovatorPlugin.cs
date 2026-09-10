using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Game.LevelOperations;
using Game.Effects;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using System;
using UnityEngine;

namespace InfernoProtocol.Renovator;

[BepInPlugin("com.holden.infernoprotocol.renovator","Renovator","0.5.0")]
public sealed class RenovatorPlugin:BasePlugin
{
    internal static ManualLogSource Logger;
    internal static ConfigEntry<bool> Enabled;
    internal static ConfigEntry<bool> MoveSnap;
    internal static ConfigEntry<int> MoveStepIndex;
    internal static ConfigEntry<int> AngleStepIndex;
    internal static RenovatorController Controller;
    private Harmony _harmony;
    public override void Load()
    {
        Logger=Log; Enabled=Config.Bind("General","Enabled",true,"Enable Renovator. No multiplayer synchronization.");
        MoveSnap=Config.Bind("Move tool","Stepped nudges",true,"Use exact increments instead of continuous movement for Renovator nudges. Native structure snapping remains automatic.");
        MoveStepIndex=Config.Bind("Move tool","Movement step",1,new ConfigDescription("Saved movement precision: 0=.05m, 1=.1m, 2=.25m, 3=.5m, 4=1m.",new AcceptableValueRange<int>(0,4)));
        AngleStepIndex=Config.Bind("Move tool","Rotation step",1,new ConfigDescription("Saved angle precision: 0=1, 1=5, 2=15, 3=45, 4=90 degrees.",new AcceptableValueRange<int>(0,4)));
        // Register before BasePlugin.AddComponent queries Il2CppType. Do not call
        // IsTypeRegisteredInIl2Cpp first: that query logs an error for a managed
        // assembly that has not yet had its first type injected.
        ClassInjector.RegisterTypeInIl2Cpp<RenovatorController>();
        Controller=AddComponent<RenovatorController>();
        _harmony=new Harmony("com.holden.infernoprotocol.renovator"); _harmony.PatchAll(typeof(Hooks));
        PatchOptionalEditorGate("InfernoProtocol.UtilityWheel.UtilityWheelController","CanOpen");
        PatchOptionalEditorGate("InfernoProtocol.UtilityWheel.UtilityWheelController","CanRemainOpen");
        PatchOptionalEditorGate("InfernoProtocol.BetterLights.BetterLightsController","CanTarget");
        PatchOptionalEditorGate("InfernoProtocol.BetterLights.BetterLightsController","CanRemainOpen");
        Log.LogInfo("Renovator 0.5.0 loaded. Post-placement object transforms and 27 surface finishes enabled.");
    }

    private void PatchOptionalEditorGate(string typeName,string methodName)
    {
        try
        {
            Type type=FindLoadedType(typeName);
            var method=type==null?null:AccessTools.Method(type,methodName);
            var prefix=AccessTools.Method(typeof(Hooks),nameof(Hooks.AllowOptionalEditor));
            if(method!=null&&prefix!=null) _harmony.Patch(method,prefix:new HarmonyMethod(prefix));
        }
        catch(Exception exception) { Log.LogDebug("Optional editor compatibility deferred: "+exception.Message); }
    }

    private static Type FindLoadedType(string fullName)
    {
        // AccessTools.TypeByName enumerates every type in every loaded Unity assembly.
        // Besides being costly, that scan produces thousands of avoidable IL2CPP loader
        // warnings on Unity 6. Assembly.GetType performs a direct lookup instead.
        foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type=assembly.GetType(fullName,false);
            if(type!=null) return type;
        }
        return null;
    }
    public override bool Unload()
    {
        _harmony?.UnpatchSelf();
        if(Controller!=null) { Controller.Cleanup(); UnityEngine.Object.Destroy(Controller); }
        Controller=null; return true;
    }
    [HarmonyPatch]
    private static class Hooks
    {
        internal static bool AllowOptionalEditor(ref bool __result)
        {
            if(Controller?.Editing!=true) return true;
            __result=false; return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(SimplePlacable),nameof(SimplePlacable.Start))]
        private static void Placed(SimplePlacable __instance) { Controller?.Register(__instance); }
        [HarmonyPrefix, HarmonyPatch(typeof(SimplePlacable),nameof(SimplePlacable.OnDestroy))]
        private static void Removed(SimplePlacable __instance) { Controller?.Forget(__instance); }

        [HarmonyPrefix, HarmonyPatch(typeof(EffectManager),nameof(EffectManager.HighlightRenderer),new[]{typeof(Renderer)})]
        private static bool RefineRendererHighlight(Renderer highligtObject) => Controller?.SuppressHammerHighlight(highligtObject)!=true;

        [HarmonyPrefix, HarmonyPatch(typeof(EffectManager),nameof(EffectManager.HighlightRenderer),new[]{typeof(IHighlightedInteractable)})]
        private static bool RefineInteractableHighlight(IHighlightedInteractable highligtObject) =>
            Controller?.SuppressHammerHighlight(highligtObject?.objectRenderer)!=true;

        [HarmonyPostfix, HarmonyPatch(typeof(Game.PlayerOperations.Player),nameof(Game.PlayerOperations.Player.UpdatePlacablePreview))]
        private static void ApplyRenovatorPlacementOffsets() => Controller?.AfterNativePlacementUpdate();
    }
}
