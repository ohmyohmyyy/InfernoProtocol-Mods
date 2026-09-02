using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace InfernoProtocol.UtilityWheel;

internal readonly struct WheelColors
{
    internal WheelColors(Color accent, Color accentSoft, Color panel, Color panelSoft, Color text, Color muted)
    {
        Accent = accent;
        AccentSoft = accentSoft;
        Panel = panel;
        PanelSoft = panelSoft;
        Text = text;
        Muted = muted;
    }

    internal Color Accent { get; }
    internal Color AccentSoft { get; }
    internal Color Panel { get; }
    internal Color PanelSoft { get; }
    internal Color Text { get; }
    internal Color Muted { get; }
}

internal static class UtilityWheelTheme
{
    internal static WheelColors Load()
    {
        int palette = Mathf.Clamp(UtilityWheelPlugin.FallbackPalette.Value, 0, 4);
        if (UtilityWheelPlugin.MatchBetterUI.Value)
        {
            palette = ReadBetterUIPalette(palette);
        }

        float shift = palette switch
        {
            1 => 0.25f,
            2 => 0.13f,
            3 => 0.43f,
            4 => -0.23f,
            _ => 0f
        };
        float opacity = Mathf.Clamp(UtilityWheelPlugin.Opacity.Value, 0.35f, 0.95f);
        return new WheelColors(
            ShiftedHex("6DFF83", 1f, shift),
            ShiftedHex("6DFF83", Mathf.Min(0.52f, opacity), shift),
            ShiftedHex("020805", opacity * 0.78f, shift),
            ShiftedHex("0C2113", opacity, shift),
            ShiftedHex("D9FFD7", 1f, shift),
            ShiftedHex("8FBC91", 1f, shift));
    }

    private static int ReadBetterUIPalette(int fallback)
    {
        try
        {
            string path = Path.Combine(Paths.ConfigPath, "com.holden.infernoprotocol.betterui.cfg");
            if (!File.Exists(path))
            {
                return fallback;
            }

            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (!line.StartsWith("ColorPalette", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int equals = line.IndexOf('=');
                if (equals >= 0 && int.TryParse(line.Substring(equals + 1).Trim(), out int value))
                {
                    return Mathf.Clamp(value, 0, 4);
                }
            }
        }
        catch (Exception exception)
        {
            UtilityWheelPlugin.ModLog.LogDebug($"Could not read BetterUI palette: {exception.Message}");
        }

        return fallback;
    }

    private static Color ShiftedHex(string hex, float alpha, float shift)
    {
        Color color = Hex(hex, alpha);
        if (Mathf.Abs(shift) < 0.001f)
        {
            return color;
        }

        Color.RGBToHSV(color, out float hue, out float saturation, out float value);
        Color shifted = Color.HSVToRGB(Mathf.Repeat(hue + shift, 1f), saturation, value);
        shifted.a = alpha;
        return shifted;
    }

    private static Color Hex(string value, float alpha)
    {
        if (!ColorUtility.TryParseHtmlString("#" + value, out Color color))
        {
            color = Color.white;
        }

        color.a = alpha;
        return color;
    }
}
