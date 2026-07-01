using System;
using System.Numerics;
using Bogey.Renderer.Gl;
using Bogey.Renderer.Text;

namespace Bogey.Renderer.Ui.Controls;

public sealed class Button : Control
{
    private const float HorizontalPadding = 8f;
    private const float VerticalPadding = 7f;
    private const float IconSlotSize = 22f;
    private const float IconGlyphPx = 12f;
    private const float IconToHotkeyGap = 3f;

    public string? Text { get; set; }

    public string? Icon { get; set; }

    public string? Hotkey { get; set; }

    public string? ToolTip { get; set; }

    public bool Disabled { get; set; }

    public bool Active { get; set; }

    public bool IsHovered { get; set; }

    public bool IsPressed { get; set; }

    public event Action? OnPressed;

    protected override bool IsOpaque => true;

    private bool IconMode => Icon is not null || Hotkey is not null;

    
    public string TooltipText
    {
        get
        {
            string? body = ToolTip ?? Text;
            if (string.IsNullOrEmpty(body))
            {
                return Hotkey is null ? string.Empty : $"({Hotkey})";
            }

            return Hotkey is null ? body : $"{body} ({Hotkey})";
        }
    }

    public void Press()
    {
        if (!Disabled)
        {
            OnPressed?.Invoke();
        }
    }

    public override Vector2 Measure()
    {
        if (!IconMode)
        {
            float textWidth = TextBatch.Measure(Text ?? string.Empty, UiTheme.FontPx) + (HorizontalPadding * 2f);
            float textHeight = UiTheme.FontPx + (VerticalPadding * 2f);
            return new Vector2(MathF.Max(textWidth, textHeight), textHeight);
        }

        float hotkeyWidth = Hotkey is null ? 0f : TextBatch.Measure(Hotkey, UiTheme.HotkeyFontPx);
        float contentWidth = MathF.Max(IconSlotSize, hotkeyWidth) + (HorizontalPadding * 2f);
        float hotkeyBlock = Hotkey is null ? 0f : IconToHotkeyGap + UiTheme.HotkeyFontPx;
        float height = IconSlotSize + hotkeyBlock + (VerticalPadding * 2f);
        return new Vector2(MathF.Max(contentWidth, height), height);
    }

    public override Button? HitTest(Vector2 point)
        => Visible && !Disabled && Bounds.Contains(point) ? this : null;

    public override void Draw(PrimitiveBatch prims, TextBatch text)
    {
        if (!Visible)
        {
            return;
        }

        Rgba background = Disabled ? UiTheme.ButtonDisabled
            : IsPressed ? UiTheme.ButtonPressed
            : Active ? UiTheme.ButtonActive
            : IsHovered ? UiTheme.ButtonHover
            : UiTheme.ButtonBackground;

        prims.FilledQuad(Bounds.Min, Bounds.Max, background);
        UiDraw.Box(prims, Bounds, Active ? UiTheme.ActiveBorder : UiTheme.Border);

        if (IconMode)
        {
            DrawIconAndHotkey(prims, text);
        }
        else
        {
            DrawCenteredText(text);
        }
    }

    private void DrawCenteredText(TextBatch text)
    {
        string label = Text ?? string.Empty;
        float width = TextBatch.Measure(label, UiTheme.FontPx);
        float x = Bounds.X + ((Bounds.W - width) * 0.5f);
        float y = Bounds.Y + ((Bounds.H - UiTheme.FontPx) * 0.5f);
        text.Text(new Vector2(x, y), UiTheme.FontPx, UiTheme.Text, label);
    }

    private void DrawIconAndHotkey(PrimitiveBatch prims, TextBatch text)
    {
        float centerX = Bounds.X + (Bounds.W * 0.5f);

        float iconX = centerX - (IconSlotSize * 0.5f);
        float iconY = Bounds.Y + VerticalPadding;
        UiRect iconSlot = new(iconX, iconY, IconSlotSize, IconSlotSize);
        prims.FilledQuad(iconSlot.Min, iconSlot.Max, UiTheme.IconSlot);
        UiDraw.Box(prims, iconSlot, UiTheme.Border);

        if (!string.IsNullOrEmpty(Icon))
        {
            float iconWidth = TextBatch.Measure(Icon, IconGlyphPx);
            text.Text(
                new Vector2(centerX - (iconWidth * 0.5f), iconY + ((IconSlotSize - IconGlyphPx) * 0.5f)),
                IconGlyphPx, UiTheme.Text, Icon);
        }

        if (Hotkey is not null)
        {
            float keyWidth = TextBatch.Measure(Hotkey, UiTheme.HotkeyFontPx);
            text.Text(
                new Vector2(centerX - (keyWidth * 0.5f), iconSlot.Bottom + IconToHotkeyGap),
                UiTheme.HotkeyFontPx, UiTheme.HotkeyText, Hotkey);
        }
    }
}
