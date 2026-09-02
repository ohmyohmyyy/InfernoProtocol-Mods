using UnityEngine;

namespace InfernoProtocol.BetterUI;

internal static class BetterUITheme
{
    private sealed class Palette
    {
        internal Color Scrim;
        internal Color Panel;
        internal Color PanelElevated;
        internal Color PanelSoft;
        internal Color CraftingCanvas;
        internal Color CraftingWell;
        internal Color CraftingReady;
        internal Color CraftingSelected;
        internal Color CraftingSelectedHover;
        internal Color Slot;
        internal Color SlotHover;
        internal Color SlotSelected;
        internal Color Border;
        internal Color BorderSoft;
        internal Color CardBorder;
        internal Color Accent;
        internal Color AccentSoft;
        internal Color Primary;
        internal Color PrimaryHover;
        internal Color PrimarySoft;
        internal Color Success;
        internal Color SuccessSoft;
        internal Color Text;
        internal Color TextMuted;
        internal Color TextUnavailable;
        internal Color TextDim;
        internal Color Shadow;
    }

    internal const int PaletteCount = 5;
    internal static readonly Color Danger = Hex("FFB45C", 1f);
    internal static readonly Color DangerSoft = Hex("FFB45C", 0.18f);
    internal static readonly Color CountBackground = Hex("010302", 0.94f);

    internal static Color Scrim => Current.Scrim;
    internal static Color Panel => Current.Panel;
    internal static Color PanelElevated => Current.PanelElevated;
    internal static Color PanelSoft => Current.PanelSoft;
    internal static Color CraftingCanvas => Current.CraftingCanvas;
    internal static Color CraftingWell => Current.CraftingWell;
    internal static Color CraftingReady => Current.CraftingReady;
    internal static Color CraftingSelected => Current.CraftingSelected;
    internal static Color CraftingSelectedHover => Current.CraftingSelectedHover;
    internal static Color Slot => Current.Slot;
    internal static Color SlotHover => Current.SlotHover;
    internal static Color SlotSelected => Current.SlotSelected;
    internal static Color Border => Current.Border;
    internal static Color BorderSoft => Current.BorderSoft;
    internal static Color CardBorder => Current.CardBorder;
    internal static Color Accent => Current.Accent;
    internal static Color AccentSoft => Current.AccentSoft;
    internal static Color Primary => Current.Primary;
    internal static Color PrimaryHover => Current.PrimaryHover;
    internal static Color PrimarySoft => Current.PrimarySoft;
    internal static Color Success => Current.Success;
    internal static Color SuccessSoft => Current.SuccessSoft;
    internal static Color Text => Current.Text;
    internal static Color TextMuted => Current.TextMuted;
    internal static Color TextUnavailable => Current.TextUnavailable;
    internal static Color TextDim => Current.TextDim;
    internal static Color Shadow => Current.Shadow;

    private static readonly Texture2D[] CategoryIcons = new Texture2D[5];
    private static int _cachedPaletteIndex = -1;
    private static Palette _current;
    private static Sprite _roundedSprite;
    private static Texture2D _scanlineTexture;
    private static Texture2D _colorWheelTexture;

    private static Palette Current
    {
        get
        {
            int index = BetterUIPlugin.PaletteIndex;
            if (_current == null || _cachedPaletteIndex != index)
            {
                _cachedPaletteIndex = index;
                _current = BuildPalette(index);
            }

            return _current;
        }
    }

    internal static Texture2D ScanlineTexture
    {
        get
        {
            if (_scanlineTexture == null)
            {
                _scanlineTexture = CreateScanlineTexture();
            }

            return _scanlineTexture;
        }
    }

    internal static Texture2D ColorWheelTexture
    {
        get
        {
            if (_colorWheelTexture == null)
            {
                _colorWheelTexture = CreateColorWheelTexture();
            }

            return _colorWheelTexture;
        }
    }

    internal static Sprite RoundedSprite
    {
        get
        {
            if (_roundedSprite == null)
            {
                _roundedSprite = CreateRoundedSprite();
            }

            return _roundedSprite;
        }
    }

    internal static Texture2D GetCategoryIconTexture(string label)
    {
        string normalized = label?.ToUpperInvariant() ?? string.Empty;
        int iconIndex = normalized.Contains("WEAPON") || normalized.Contains("COMBAT")
            ? 0
            : normalized.Contains("MED") || normalized.Contains("HEALTH")
                ? 1
                : normalized.Contains("TOOL") || normalized.Contains("FOOD")
                    ? 2
                    : normalized.Contains("CLOTH")
                        ? 3
                        : 4;
        if (CategoryIcons[iconIndex] == null)
        {
            CategoryIcons[iconIndex] = CreateCategoryIcon(iconIndex);
        }

        return CategoryIcons[iconIndex];
    }

    private static Palette BuildPalette(int index)
    {
        float shift;
        switch (Mathf.Clamp(index, 0, PaletteCount - 1))
        {
            case 1: shift = 0.25f; break; // blue
            case 2: shift = 0.13f; break; // cyan
            case 3: shift = 0.43f; break; // violet
            case 4: shift = -0.23f; break; // amber
            default: shift = 0f; break; // phosphor green
        }

        return new Palette
        {
            Scrim = ShiftedHex("020704", 0.72f, shift),
            Panel = ShiftedHex("040C07", 0.88f, shift),
            PanelElevated = ShiftedHex("08140C", 0.90f, shift),
            PanelSoft = ShiftedHex("0C2113", 0.86f, shift),
            CraftingCanvas = ShiftedHex("020805", 0.80f, shift),
            CraftingWell = ShiftedHex("010403", 0.84f, shift),
            CraftingReady = ShiftedHex("07190C", 0.90f, shift),
            CraftingSelected = ShiftedHex("103A1C", 1f, shift),
            CraftingSelectedHover = ShiftedHex("165326", 1f, shift),
            Slot = ShiftedHex("07130B", 0.99f, shift),
            SlotHover = ShiftedHex("102B18", 1f, shift),
            SlotSelected = ShiftedHex("174923", 1f, shift),
            Border = ShiftedHex("2D6B3B", 0.90f, shift),
            BorderSoft = ShiftedHex("1C4327", 0.76f, shift),
            CardBorder = ShiftedHex("16331E", 0.68f, shift),
            Accent = ShiftedHex("6DFF83", 1f, shift),
            AccentSoft = ShiftedHex("6DFF83", 0.20f, shift),
            Primary = ShiftedHex("B8FF78", 1f, shift),
            PrimaryHover = ShiftedHex("DFFF9D", 1f, shift),
            PrimarySoft = ShiftedHex("B8FF78", 0.15f, shift),
            Success = ShiftedHex("66E97A", 1f, shift),
            SuccessSoft = ShiftedHex("66E97A", 0.18f, shift),
            Text = ShiftedHex("D9FFD7", 1f, shift),
            TextMuted = ShiftedHex("8FBC91", 1f, shift),
            TextUnavailable = ShiftedHex("688B6C", 1f, shift),
            TextDim = ShiftedHex("4F7556", 1f, shift),
            Shadow = ShiftedHex("000D03", 0.66f, shift)
        };
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

    private static Sprite CreateRoundedSprite()
    {
        const int textureSize = 64;
        const float radius = 13f;
        var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "BetterUI_RoundedSurface",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color32[textureSize * textureSize];
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float nearestX = Mathf.Clamp(x + 0.5f, radius, textureSize - radius);
                float nearestY = Mathf.Clamp(y + 0.5f, radius, textureSize - radius);
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(nearestX, nearestY));
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius + 0.75f - distance) * 255f);
                pixels[(y * textureSize) + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Object.DontDestroyOnLoad(texture);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(16f, 16f, 16f, 16f));
        sprite.name = "BetterUI_RoundedSurfaceSprite";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(sprite);
        return sprite;
    }

    private static Texture2D CreateScanlineTexture()
    {
        var texture = new Texture2D(2, 4, TextureFormat.RGBA32, false)
        {
            name = "BetterUI_TerminalScanlines",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
            hideFlags = HideFlags.HideAndDontSave
        };

        var clear = new Color32(255, 255, 255, 0);
        var line = new Color32(0, 0, 0, 255);
        texture.SetPixels32(new[]
        {
            clear, clear,
            clear, clear,
            line, line,
            clear, clear
        });
        texture.Apply(false, true);
        Object.DontDestroyOnLoad(texture);
        return texture;
    }

    private static Texture2D CreateColorWheelTexture()
    {
        const int size = 32;
        var texture = NewIconTexture("BetterUI_ColorWheel", size);
        var pixels = new Color32[size * size];
        Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 delta = new Vector2(x, y) - center;
                float radius = delta.magnitude;
                if (radius < 7f || radius > 14.5f)
                {
                    pixels[(y * size) + x] = new Color32(255, 255, 255, 0);
                    continue;
                }

                float hue = Mathf.Repeat(Mathf.Atan2(delta.y, delta.x) / (Mathf.PI * 2f), 1f);
                pixels[(y * size) + x] = Color.HSVToRGB(hue, 0.86f, 1f);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Object.DontDestroyOnLoad(texture);
        return texture;
    }

    private static Texture2D CreateCategoryIcon(int iconIndex)
    {
        const int size = 32;
        var texture = NewIconTexture($"BetterUI_CategoryIcon_{iconIndex}", size);
        var pixels = new Color32[size * size];
        var white = new Color32(255, 255, 255, 255);

        switch (iconIndex)
        {
            case 0: // crossed weapons
                DrawLine(pixels, size, 7, 6, 25, 25, 2, white);
                DrawLine(pixels, size, 25, 6, 7, 25, 2, white);
                break;
            case 1: // medical cross
                FillRect(pixels, size, 13, 5, 19, 27, white);
                FillRect(pixels, size, 5, 13, 27, 19, white);
                break;
            case 2: // hammer
                DrawLine(pixels, size, 9, 25, 21, 11, 3, white);
                FillRect(pixels, size, 15, 6, 27, 12, white);
                break;
            case 3: // shirt
                FillRect(pixels, size, 10, 10, 22, 27, white);
                FillRect(pixels, size, 5, 10, 12, 17, white);
                FillRect(pixels, size, 20, 10, 27, 17, white);
                FillRect(pixels, size, 13, 7, 19, 12, new Color32(255, 255, 255, 0));
                break;
            default: // misc grid
                FillRect(pixels, size, 6, 6, 13, 13, white);
                FillRect(pixels, size, 19, 6, 26, 13, white);
                FillRect(pixels, size, 6, 19, 13, 26, white);
                FillRect(pixels, size, 19, 19, 26, 26, white);
                break;
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Object.DontDestroyOnLoad(texture);
        return texture;
    }

    private static Texture2D NewIconTexture(string name, int size)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private static void FillRect(Color32[] pixels, int size, int left, int bottom, int right, int top, Color32 color)
    {
        for (int y = Mathf.Max(0, bottom); y < Mathf.Min(size, top); y++)
        {
            for (int x = Mathf.Max(0, left); x < Mathf.Min(size, right); x++)
            {
                pixels[(y * size) + x] = color;
            }
        }
    }

    private static void DrawLine(
        Color32[] pixels,
        int size,
        int startX,
        int startY,
        int endX,
        int endY,
        int thickness,
        Color32 color)
    {
        int steps = Mathf.Max(Mathf.Abs(endX - startX), Mathf.Abs(endY - startY));
        for (int i = 0; i <= steps; i++)
        {
            float amount = steps > 0 ? i / (float)steps : 0f;
            int x = Mathf.RoundToInt(Mathf.Lerp(startX, endX, amount));
            int y = Mathf.RoundToInt(Mathf.Lerp(startY, endY, amount));
            FillRect(pixels, size, x - thickness, y - thickness, x + thickness + 1, y + thickness + 1, color);
        }
    }

    private static Color Hex(string hex, float alpha)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color color);
        color.a = alpha;
        return color;
    }
}
