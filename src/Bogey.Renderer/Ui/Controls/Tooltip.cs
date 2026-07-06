using System;
using System.Numerics;
using Bogey.Renderer.Gl;
using Bogey.Renderer.Text;

namespace Bogey.Renderer.Ui.Controls;

public sealed class Tooltip : Control
{
    private const float Padding = 6f;
    private const float Gap = 6f;

    public Button? Target { get; set; }

    public void PlaceWithin(UiRect screen)
    {
        if (!HasContent(out string tip))
        {
            Bounds = default;
            return;
        }

        float font = UiTheme.TooltipFontPx;
        float width = TextBatch.Measure(tip, font) + (Padding * 2f);
        float height = font + (Padding * 2f);

        float x = Target!.Bounds.X;
        float y = Target.Bounds.Bottom + Gap;
        if (y + height > screen.Bottom)
        {
            y = Target.Bounds.Y - height - Gap;
        }

        x = Math.Clamp(x, screen.X + 4f, MathF.Max(screen.X + 4f, screen.Right - width - 4f));
        Bounds = new UiRect(x, y, width, height);
    }

    public override void Draw(PrimitiveBatch prims, TextBatch text)
    {
        if (!Visible || !HasContent(out string tip))
        {
            return;
        }

        int previousPrimsLayer = prims.Layer;
        int previousTextLayer = text.Layer;
        prims.Layer = (int)RenderLayer.Ui + 50;
        text.Layer = (int)RenderLayer.Ui + 50;

        prims.FilledQuad(Bounds.Min, Bounds.Max, UiTheme.TooltipBackground);
        UiDraw.Box(prims, Bounds, UiTheme.Border);
        text.Text(new Vector2(Bounds.X + Padding, Bounds.Y + Padding), UiTheme.TooltipFontPx, UiTheme.Text, tip);

        prims.Layer = previousPrimsLayer;
        text.Layer = previousTextLayer;
    }

    private bool HasContent(out string tip)
    {
        tip = Target?.TooltipText ?? string.Empty;
        return tip.Length > 0;
    }
}
