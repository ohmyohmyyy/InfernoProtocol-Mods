using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InfernoProtocol.BetterUI;

internal enum ButtonVariant
{
    Secondary,
    Primary,
    Danger
}

internal static class BetterUIStyler
{
    internal static Image Surface(RectTransform rect, Color fill, Color border, bool addShadow = true)
    {
        if (rect == null)
        {
            return null;
        }

        Image image = rect.GetComponent<Image>();
        if (image == null)
        {
            image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
        }

        RoundedImage(image, fill);
        Effects(image, border, addShadow);
        return image;
    }

    internal static void RoundedImage(Image image, Color color)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = BetterUITheme.RoundedSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
    }

    internal static void Tint(Image image, Color color)
    {
        if (image != null)
        {
            image.color = color;
        }
    }

    internal static void Effects(Graphic target, Color border, bool addShadow)
    {
        if (target == null)
        {
            return;
        }

        Outline outline = target.GetComponent<Outline>();
        if (outline == null)
        {
            outline = target.gameObject.AddComponent<Outline>();
        }

        outline.effectColor = border;
        outline.effectDistance = new Vector2(1.25f, -1.25f);
        outline.useGraphicAlpha = true;

        Shadow shadow = null;
        var shadows = target.GetComponents<Shadow>();
        for (int i = 0; i < shadows.Length; i++)
        {
            Shadow candidate = shadows[i];
            if (candidate != null && candidate.TryCast<Outline>() == null)
            {
                shadow = candidate;
                break;
            }
        }

        if (!addShadow)
        {
            if (shadow != null && ColorsMatch(shadow.effectColor, BetterUITheme.Shadow))
            {
                shadow.enabled = false;
            }

            return;
        }

        if (shadow == null)
        {
            shadow = target.gameObject.AddComponent<Shadow>();
        }

        shadow.enabled = true;
        shadow.effectColor = BetterUITheme.Shadow;
        shadow.effectDistance = new Vector2(0f, -4f);
        shadow.useGraphicAlpha = true;
    }

    private static bool ColorsMatch(Color left, Color right)
    {
        const float tolerance = 0.002f;
        return Mathf.Abs(left.r - right.r) < tolerance
            && Mathf.Abs(left.g - right.g) < tolerance
            && Mathf.Abs(left.b - right.b) < tolerance
            && Mathf.Abs(left.a - right.a) < tolerance;
    }

    internal static void Text(TMP_Text text, float size, Color color, FontStyles style, bool preserveSize = false)
    {
        if (text == null)
        {
            return;
        }

        text.enableAutoSizing = false;
        if (!preserveSize)
        {
            text.fontSize = size;
        }

        text.color = color;
        text.fontStyle = style;
        text.raycastTarget = false;
    }

    internal static void Heading(TMP_Text text, float size)
    {
        Text(text, size, BetterUITheme.Text, FontStyles.Bold);
        if (text == null)
        {
            return;
        }

        text.characterSpacing = 2.2f;
        text.outlineWidth = 0.12f;
        text.outlineColor = new Color32(0, 0, 0, 190);
    }

    internal static void Button(MazeButton button, ButtonVariant variant)
    {
        if (button == null)
        {
            return;
        }

        Color normal;
        Color hover;
        Color border;
        Color normalText;

        switch (variant)
        {
            case ButtonVariant.Primary:
                normal = new Color(BetterUITheme.Primary.r, BetterUITheme.Primary.g, BetterUITheme.Primary.b, 0.18f);
                hover = new Color(BetterUITheme.PrimaryHover.r, BetterUITheme.PrimaryHover.g, BetterUITheme.PrimaryHover.b, 0.34f);
                border = BetterUITheme.Primary;
                normalText = BetterUITheme.PrimaryHover;
                break;
            case ButtonVariant.Danger:
                normal = BetterUITheme.DangerSoft;
                hover = new Color(BetterUITheme.Danger.r, BetterUITheme.Danger.g, BetterUITheme.Danger.b, 0.34f);
                border = BetterUITheme.Danger;
                normalText = BetterUITheme.Text;
                break;
            default:
                normal = BetterUITheme.PanelSoft;
                hover = BetterUITheme.SlotHover;
                border = BetterUITheme.Border;
                normalText = BetterUITheme.TextMuted;
                break;
        }

        button.backgroundColor = normal;
        button.hoverColor = hover;
        button.textColor = normalText;
        button.hoverTextColor = BetterUITheme.Text;

        Graphic graphic = button.backgroundImage;
        Image image = graphic != null ? graphic.TryCast<Image>() : null;
        if (image != null)
        {
            RoundedImage(image, normal);
            Effects(image, border, false);
            Outline outline = image.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectDistance = new Vector2(0.65f, -0.65f);
            }
        }

        if (button.buttonText != null)
        {
            Text(button.buttonText, Mathf.Max(15f, button.buttonText.fontSize), normalText, FontStyles.Bold);
            button.buttonText.characterSpacing = 0.8f;
        }

        LayoutElement layout = button.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = button.gameObject.AddComponent<LayoutElement>();
        }

        layout.minHeight = 42f;
        button.UpdateVisualState(true);
    }

    internal static void ToggleButton(MazeButton button, bool active)
    {
        if (button == null)
        {
            return;
        }

        Color normal = active ? BetterUITheme.AccentSoft : BetterUITheme.PanelSoft;
        Color hover = active ? BetterUITheme.CraftingSelectedHover : BetterUITheme.SlotHover;
        Color border = active ? BetterUITheme.Accent : BetterUITheme.BorderSoft;
        Color text = active ? BetterUITheme.Text : BetterUITheme.TextMuted;

        button.backgroundColor = normal;
        button.hoverColor = hover;
        button.textColor = text;
        button.hoverTextColor = BetterUITheme.Text;

        Graphic graphic = button.backgroundImage;
        Image image = graphic != null ? graphic.TryCast<Image>() : null;
        if (image != null)
        {
            RoundedImage(image, normal);
            Effects(image, border, false);
            Outline outline = image.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectDistance = new Vector2(0.65f, -0.65f);
            }
        }

        if (button.buttonText != null)
        {
            Text(button.buttonText, 14f, text, FontStyles.Bold);
            button.buttonText.characterSpacing = 0.5f;
        }
    }

    internal static void InputField(TMP_InputField input)
    {
        if (input == null)
        {
            return;
        }

        Image background = input.GetComponent<Image>();
        if (background != null)
        {
            RoundedImage(background, BetterUITheme.PanelElevated);
            Effects(background, BetterUITheme.Border, false);
        }

        Text(input.textComponent, 16f, BetterUITheme.Text, FontStyles.Normal);
        TMP_Text placeholder = input.placeholder != null ? input.placeholder.TryCast<TMP_Text>() : null;
        Text(placeholder, 16f, BetterUITheme.TextDim, FontStyles.Italic);
        input.selectionColor = BetterUITheme.AccentSoft;
        input.caretColor = BetterUITheme.Accent;
        input.caretWidth = 2;
    }

    internal static void Scrollbars(Transform root)
    {
        if (root == null)
        {
            return;
        }

        var scrollbars = root.GetComponentsInChildren<Scrollbar>(true);
        for (int i = 0; i < scrollbars.Length; i++)
        {
            Scrollbar scrollbar = scrollbars[i];
            if (scrollbar == null)
            {
                continue;
            }

            Image track = scrollbar.GetComponent<Image>();
            if (track != null)
            {
                RoundedImage(track, new Color(BetterUITheme.PanelSoft.r, BetterUITheme.PanelSoft.g, BetterUITheme.PanelSoft.b, 0.62f));
            }

            Image handle = scrollbar.handleRect != null ? scrollbar.handleRect.GetComponent<Image>() : null;
            if (handle != null)
            {
                RoundedImage(handle, BetterUITheme.Accent);
                Effects(handle, new Color(1f, 1f, 1f, 0.24f), false);
            }

            var colors = scrollbar.colors;
            colors.normalColor = BetterUITheme.TextMuted;
            colors.highlightedColor = BetterUITheme.Text;
            colors.pressedColor = BetterUITheme.Accent;
            colors.disabledColor = BetterUITheme.TextDim;
            colors.fadeDuration = 0.08f;
            scrollbar.colors = colors;
        }
    }

    internal static ButtonVariant ClassifyButton(MazeButton button)
    {
        string label = button != null && button.buttonText != null
            ? button.buttonText.text.ToUpperInvariant()
            : string.Empty;

        if (label.Contains("CLOSE") || label.Contains("DROP") || label.Contains("REMOVE") || label.Contains("DELETE"))
        {
            return ButtonVariant.Danger;
        }

        if (label.Contains("CRAFT") || label.Contains("PACK") || label.Contains("CONFIRM") || label.Contains("APPLY") || label.Contains("PLACE"))
        {
            return ButtonVariant.Primary;
        }

        return ButtonVariant.Secondary;
    }
}
