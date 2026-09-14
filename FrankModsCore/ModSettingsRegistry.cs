using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace FrankMods;

public static class ModSettingsRegistry
{
    private static readonly List<KeyBindingSetting> KeyBindings = new();
    private static readonly List<GamepadBindingSetting> GamepadBindings = new();
    private static ModSettingsController _controller;

    public static void RegisterKeyBinding(
        string pluginGuid,
        string modName,
        string settingId,
        string displayName,
        Key defaultKey,
        Func<Key> read,
        Action<Key> write)
    {
        if (string.IsNullOrWhiteSpace(pluginGuid) || string.IsNullOrWhiteSpace(modName) ||
            string.IsNullOrWhiteSpace(settingId) || string.IsNullOrWhiteSpace(displayName) ||
            read == null || write == null)
            throw new ArgumentException("A mod setting registration is incomplete.");

        KeyBindingSetting existing = KeyBindings.FirstOrDefault(entry =>
            string.Equals(entry.PluginGuid, pluginGuid, StringComparison.Ordinal) &&
            string.Equals(entry.SettingId, settingId, StringComparison.Ordinal));
        if (existing != null) KeyBindings.Remove(existing);
        KeyBindings.Add(new KeyBindingSetting(pluginGuid, modName, settingId, displayName, defaultKey, read, write));
        KeyBindings.Sort((left, right) =>
        {
            int mod = string.Compare(left.ModName, right.ModName, StringComparison.OrdinalIgnoreCase);
            return mod != 0 ? mod : string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
        });
        _controller?.RegistryChanged();
    }

    public static void RegisterGamepadBinding(
        string pluginGuid,
        string modName,
        string settingId,
        string displayName,
        GamepadButton defaultButton,
        Func<GamepadButton> read,
        Action<GamepadButton> write)
    {
        if (string.IsNullOrWhiteSpace(pluginGuid) || string.IsNullOrWhiteSpace(modName) ||
            string.IsNullOrWhiteSpace(settingId) || string.IsNullOrWhiteSpace(displayName) ||
            read == null || write == null)
            throw new ArgumentException("A mod setting registration is incomplete.");

        GamepadBindingSetting existing = GamepadBindings.FirstOrDefault(entry =>
            string.Equals(entry.PluginGuid, pluginGuid, StringComparison.Ordinal) &&
            string.Equals(entry.SettingId, settingId, StringComparison.Ordinal));
        if (existing != null) GamepadBindings.Remove(existing);
        GamepadBindings.Add(new GamepadBindingSetting(pluginGuid, modName, settingId, displayName, defaultButton, read, write));
        GamepadBindings.Sort((left, right) =>
        {
            int mod = string.Compare(left.ModName, right.ModName, StringComparison.OrdinalIgnoreCase);
            return mod != 0 ? mod : string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
        });
        _controller?.RegistryChanged();
    }

    public static void UnregisterMod(string pluginGuid)
    {
        if (string.IsNullOrWhiteSpace(pluginGuid)) return;
        KeyBindings.RemoveAll(entry => string.Equals(entry.PluginGuid, pluginGuid, StringComparison.Ordinal));
        GamepadBindings.RemoveAll(entry => string.Equals(entry.PluginGuid, pluginGuid, StringComparison.Ordinal));
        _controller?.RegistryChanged();
    }

    internal static IReadOnlyList<KeyBindingSetting> Bindings => KeyBindings;
    internal static IReadOnlyList<GamepadBindingSetting> ControllerBindings => GamepadBindings;
    internal static int Count => KeyBindings.Count + GamepadBindings.Count;
    internal static void SetController(ModSettingsController controller)
    {
        _controller = controller;
        _controller?.RegistryChanged();
    }
}

internal sealed class GamepadBindingSetting
{
    internal GamepadBindingSetting(string pluginGuid, string modName, string settingId, string displayName,
        GamepadButton defaultButton, Func<GamepadButton> read, Action<GamepadButton> write)
    {
        PluginGuid = pluginGuid;
        ModName = modName;
        SettingId = settingId;
        DisplayName = displayName;
        DefaultButton = defaultButton;
        Read = read;
        Write = write;
    }

    internal string PluginGuid { get; }
    internal string ModName { get; }
    internal string SettingId { get; }
    internal string DisplayName { get; }
    internal GamepadButton DefaultButton { get; }
    internal Func<GamepadButton> Read { get; }
    internal Action<GamepadButton> Write { get; }
}

internal sealed class KeyBindingSetting
{
    internal KeyBindingSetting(string pluginGuid, string modName, string settingId, string displayName,
        Key defaultKey, Func<Key> read, Action<Key> write)
    {
        PluginGuid = pluginGuid;
        ModName = modName;
        SettingId = settingId;
        DisplayName = displayName;
        DefaultKey = defaultKey;
        Read = read;
        Write = write;
    }

    internal string PluginGuid { get; }
    internal string ModName { get; }
    internal string SettingId { get; }
    internal string DisplayName { get; }
    internal Key DefaultKey { get; }
    internal Func<Key> Read { get; }
    internal Action<Key> Write { get; }
}
