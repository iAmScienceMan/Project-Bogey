using Bogey.Renderer.Gl;

namespace Bogey.Renderer.Ui;

public static class UiTheme
{
    public static readonly Rgba PanelBackground = new(0.07f, 0.09f, 0.13f, 0.80f);
    public static readonly Rgba ButtonBackground = new(0.16f, 0.19f, 0.25f, 0.95f);
    public static readonly Rgba ButtonHover = new(0.24f, 0.29f, 0.37f, 0.98f);
    public static readonly Rgba ButtonPressed = new(0.10f, 0.12f, 0.16f, 1.0f);
    public static readonly Rgba ButtonActive = new(0.18f, 0.40f, 0.54f, 1.0f);
    public static readonly Rgba ButtonDisabled = new(0.12f, 0.13f, 0.16f, 0.7f);
    public static readonly Rgba Border = new(0.42f, 0.49f, 0.60f, 0.85f);
    public static readonly Rgba ActiveBorder = new(0.45f, 0.85f, 1.0f, 1.0f);
    public static readonly Rgba Text = new(0.90f, 0.93f, 0.97f);

    
    public static readonly Rgba IconSlot = new(0.10f, 0.12f, 0.16f, 1.0f);
    public static readonly Rgba HotkeyText = new(0.62f, 0.68f, 0.78f);


    public static readonly Rgba TooltipBackground = new(0.05f, 0.06f, 0.09f, 0.96f);


    public static readonly Rgba Accent = new(0.45f, 0.85f, 1.0f, 1.0f);
    public static readonly Rgba Subtle = new(0.62f, 0.68f, 0.78f);
    public static readonly Rgba Negative = new(0.52f, 0.20f, 0.20f, 0.95f);
    public static readonly Rgba NegativeHover = new(0.70f, 0.28f, 0.27f, 0.98f);
    public static readonly Rgba MenuBackground = new(0.03f, 0.05f, 0.08f, 1.0f);
    public static readonly Rgba MenuGrid = new(0.12f, 0.20f, 0.28f, 0.45f);

    public const float FontPx = 13f;
    public const float TextBlockFontPx = 12f;
    public const float HotkeyFontPx = 9f;
    public const float TooltipFontPx = 12f;
    public const float TitleFontPx = 40f;
    public const float SubtitleFontPx = 13f;
    public const float MenuButtonFontPx = 16f;
}
