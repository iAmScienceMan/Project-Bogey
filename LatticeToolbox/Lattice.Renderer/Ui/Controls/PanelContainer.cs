using System;
using System.Numerics;
using Lattice.Renderer.Gl;
using Lattice.Renderer.Text;

namespace Lattice.Renderer.Ui.Controls;

public sealed class PanelContainer : Control
{
    protected override bool IsOpaque => true;

    public override Vector2 Measure()
    {
        float width = 0f;
        float height = 0f;
        foreach (Control child in Children)
        {
            if (!child.Visible)
            {
                continue;
            }

            Vector2 size = child.Measure();
            width = MathF.Max(width, size.X);
            height = MathF.Max(height, size.Y);
        }

        return new Vector2(width, height);
    }

    public override void Draw(PrimitiveBatch prims, TextBatch text)
    {
        if (!Visible)
        {
            return;
        }

        prims.FilledQuad(Bounds.Min, Bounds.Max, UiTheme.PanelBackground);
        UiDraw.Box(prims, Bounds, UiTheme.Border);

        foreach (Control child in Children)
        {
            child.Draw(prims, text);
        }
    }
}
