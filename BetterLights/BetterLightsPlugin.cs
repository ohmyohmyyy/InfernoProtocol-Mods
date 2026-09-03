using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

namespace InfernoProtocol.BetterLights;


[BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
public sealed class BetterLightsPlugin : BasePlugin
{
    private static readonly Dictionary<string, string> TorchColors = new(StringComparer.Ordinal);
    private static BetterLightsController _controller;
    private static ConfigEntry<string> _savedTorchColors;

    internal static ManualLogSource ModLog { get; private set; }
    internal static ConfigEntry<bool> Enabled { get; private set; }
    internal static ConfigEntry<float> InteractionRange { get; private set; }
    internal static ConfigEntry<float> AimThreshold { get; private set; }
    internal static ConfigEntry<float> StickDeadzone { get; private set; }
    internal static ConfigEntry<bool> KeyboardFallback { get; private set; }

    public override void Load()
    {
        ModLog = Log;
        Enabled = Config.Bind("General", "Enabled", true, "Enables BetterLights torch and lantern customization.");
        InteractionRange = Config.Bind(
            "Interaction",
            "Range",
            3f,
            new ConfigDescription("Maximum distance for the light color prompt.", new AcceptableValueRange<float>(1.5f, 6f)));
        // Migrate the original local-test default so existing testers receive the
        // closer prompt distance instead of retaining 4.25 metres indefinitely.
        if (Mathf.Approximately(InteractionRange.Value, 4.25f))
        {
            InteractionRange.Value = 3f;
        }
        AimThreshold = Config.Bind(
            "Interaction",
            "AimThreshold",
            0.78f,
            new ConfigDescription("How closely the camera must face a torch, from 0 to 1.", new AcceptableValueRange<float>(0.45f, 0.95f)));
        StickDeadzone = Config.Bind(
            "Interaction",
            "StickDeadzone",
            0.16f,
            new ConfigDescription("Right-stick distance before the color changes.", new AcceptableValueRange<float>(0.08f, 0.5f)));
        KeyboardFallback = Config.Bind(
            "Interaction",
            "KeyboardFallback",
            true,
            "Allows F7 and arrow-key control in addition to the controller controls.");
        _savedTorchColors = Config.Bind(
            "Persistence",
            "TorchColors",
            string.Empty,
            "Per-light colors managed by BetterLights. Editing this value manually is not recommended.");

        LoadSavedColors();
        _controller = AddComponent<BetterLightsController>();
        Log.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} loaded");
    }

    public override bool Unload()
    {
        if (_controller != null)
        {
            UnityEngine.Object.Destroy(_controller);
            _controller = null;
        }

        return true;
    }

    internal static string GetTorchKey(APlacable torch)
    {
        if (torch == null)
        {
            return string.Empty;
        }

        if (torch.placableId != 0)
        {
            return $"{torch.placableItem}-{torch.interiorIndex}-{torch.placableId}";
        }

        Vector3 position = torch.transform.position;
        return $"{torch.placableItem}-{torch.interiorIndex}-P{Mathf.RoundToInt(position.x * 10f)}_{Mathf.RoundToInt(position.y * 10f)}_{Mathf.RoundToInt(position.z * 10f)}";
    }

    internal static string GetSceneLightKey(string type, Transform root)
    {
        if (root == null)
        {
            return string.Empty;
        }

        Vector3 position = root.position;
        return $"{type}-P{Mathf.RoundToInt(position.x * 10f)}_{Mathf.RoundToInt(position.y * 10f)}_{Mathf.RoundToInt(position.z * 10f)}";
    }

    internal static bool TryGetTorchColor(string key, out Color color)
    {
        color = default;
        return !string.IsNullOrEmpty(key) && TorchColors.TryGetValue(key, out string html) &&
               ColorUtility.TryParseHtmlString(html, out color);
    }

    internal static void SaveTorchColor(string key, Color color)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        TorchColors[key] = "#" + ColorUtility.ToHtmlStringRGB(color);
        SaveColors();
    }

    internal static void RemoveTorchColor(string key)
    {
        if (!string.IsNullOrEmpty(key) && TorchColors.Remove(key))
        {
            SaveColors();
        }
    }

    private static void LoadSavedColors()
    {
        TorchColors.Clear();
        string value = _savedTorchColors?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string[] entries = value.Split(';', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < entries.Length; i++)
        {
            int separator = entries[i].IndexOf('=');
            if (separator <= 0 || separator >= entries[i].Length - 1)
            {
                continue;
            }

            string key = entries[i].Substring(0, separator);
            string html = entries[i].Substring(separator + 1);
            if (ColorUtility.TryParseHtmlString(html, out _))
            {
                TorchColors[key] = html;
            }
        }
    }

    private static void SaveColors()
    {
        var keys = new List<string>(TorchColors.Keys);
        keys.Sort(StringComparer.Ordinal);
        var entries = new string[keys.Count];
        for (int i = 0; i < keys.Count; i++)
        {
            entries[i] = keys[i] + "=" + TorchColors[keys[i]];
        }

        _savedTorchColors.Value = string.Join(";", entries);
    }
}
