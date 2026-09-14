using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using FrankMods;
using Game.LevelOperations;
using Game.Effects;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace InfernoProtocol.Renovator;

[BepInPlugin("com.holden.infernoprotocol.renovator","Renovator","0.6.2")]
[BepInDependency(FrankModsCorePlugin.Guid, FrankModsCorePlugin.Version)]
public sealed class RenovatorPlugin:BasePlugin
{
    internal const string Guid="com.holden.infernoprotocol.renovator";
    internal static ManualLogSource Logger;
    internal static ConfigEntry<bool> Enabled;
    internal static ConfigEntry<bool> MoveSnap;
    internal static ConfigEntry<int> MoveStepIndex;
    internal static ConfigEntry<int> AngleStepIndex;
    internal static ConfigEntry<Key> EditorKeyboardKey;
    internal static ConfigEntry<GamepadButton> EditorControllerButton;
    internal static RenovatorController Controller;
    private Harmony _harmony;
    public override void Load()
    {
        Logger=Log; Enabled=Config.Bind("General","Enabled",true,"Enable Renovator. Hosts synchronize committed finishes and object moves to Renovator clients.");
        EditorKeyboardKey=Config.Bind("Input","EditorKeyboardKey",Key.F6,"Keyboard shortcut used to open the Renovator editor.");
        EditorControllerButton=Config.Bind("Input","EditorControllerButton",GamepadButton.RightStick,"Controller shortcut used to open the Renovator editor.");
        ModSettingsRegistry.RegisterKeyBinding(Guid,"Renovator","EditorKeyboardKey","Open Renovator editor",Key.F6,()=>EditorKeyboardKey.Value,key=>EditorKeyboardKey.Value=key);
        ModSettingsRegistry.RegisterGamepadBinding(Guid,"Renovator","EditorControllerButton","Open Renovator editor",GamepadButton.RightStick,()=>EditorControllerButton.Value,button=>EditorControllerButton.Value=button);
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
        Log.LogInfo("Renovator 0.6.2 loaded. Host-authoritative finishes and committed moves enabled.");
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
        ModSettingsRegistry.UnregisterMod(Guid);
        _harmony?.UnpatchSelf();
        if(Controller!=null) { Controller.Cleanup(); UnityEngine.Object.Destroy(Controller); }
        Controller=null; return true;
    }
    internal static bool EditorKeyPressed(Keyboard keyboard)
    {
        if(keyboard==null||EditorKeyboardKey==null||EditorKeyboardKey.Value==Key.None) return false;
        try { return keyboard[EditorKeyboardKey.Value].wasPressedThisFrame; }
        catch { return false; }
    }
    internal static string EditorKeyName=>EditorKeyboardKey?.Value.ToString().ToUpperInvariant()??"F6";
    internal static bool EditorControllerPressed(Gamepad gamepad)
    {
        if(gamepad==null||EditorControllerButton==null) return false;
        try { return gamepad[EditorControllerButton.Value].wasPressedThisFrame; }
        catch { return false; }
    }
    internal static string EditorControllerName=>EditorControllerButton?.Value switch
    {
        GamepadButton.South=>"A",GamepadButton.East=>"B",GamepadButton.West=>"X",GamepadButton.North=>"Y",
        GamepadButton.LeftShoulder=>"LB",GamepadButton.RightShoulder=>"RB",GamepadButton.LeftTrigger=>"LT",GamepadButton.RightTrigger=>"RT",
        GamepadButton.LeftStick=>"L3",GamepadButton.RightStick=>"R3",GamepadButton.Start=>"MENU",GamepadButton.Select=>"VIEW",
        GamepadButton.DpadUp=>"D-PAD UP",GamepadButton.DpadDown=>"D-PAD DOWN",GamepadButton.DpadLeft=>"D-PAD LEFT",GamepadButton.DpadRight=>"D-PAD RIGHT",
        _=>"R3"
    };
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
        [HarmonyPostfix, HarmonyPatch(typeof(PlacableManager),nameof(PlacableManager.RegisterPlacable))]
        private static void Registered(APlacable __0) { Controller?.Register(__0); }
        [HarmonyPrefix, HarmonyPatch(typeof(PlacableManager),nameof(PlacableManager.UnregisterPlacable))]
        private static void Unregistering(PlacableManager __instance,ulong __0)
        {
            if(__instance!=null) Controller?.Forget(__instance.GetPlacable(__0));
        }

        [HarmonyPrefix, HarmonyPatch(typeof(EffectManager),nameof(EffectManager.HighlightRenderer),new[]{typeof(Renderer)})]
        private static bool RefineRendererHighlight(Renderer highligtObject) => Controller?.SuppressHammerHighlight(highligtObject)!=true;

        [HarmonyPrefix, HarmonyPatch(typeof(EffectManager),nameof(EffectManager.HighlightRenderer),new[]{typeof(IHighlightedInteractable)})]
        private static bool RefineInteractableHighlight(IHighlightedInteractable highligtObject) =>
            Controller?.SuppressHammerHighlight(highligtObject?.objectRenderer)!=true;

        [HarmonyPostfix, HarmonyPatch(typeof(Game.PlayerOperations.Player),nameof(Game.PlayerOperations.Player.UpdatePlacablePreview))]
        private static void ApplyRenovatorPlacementOffsets() => Controller?.AfterNativePlacementUpdate();
    }
}
