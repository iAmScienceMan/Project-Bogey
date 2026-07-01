using System;
using System.Numerics;
using Bogey.Renderer.Gl;
using Bogey.Renderer.Text;

namespace Bogey.Renderer.Ui.Controls;

public enum Orientation
{
    Horizontal,
    Vertical,
}

public sealed class BoxContainer : Control
{
    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    public float Separation { get; set; }

    public float Padding { get; set; }

    protected override bool IsOpaque => true;

    public override Vector2 Measure()
    {
        float main = 0f;
        float cross = 0f;
        int visible = 0;

        foreach (Control child in Children)
        {
            if (!child.Visible)
            {
                continue;
            }

            Vector2 size = child.Measure();
            visible++;

            if (Orientation == Orientation.Horizontal)
            {
                main += size.X;
                cross = MathF.Max(cross, size.Y);
            }
            else
            {
                main += size.Y;
                cross = MathF.Max(cross, size.X);
            }
        }

        if (visible > 1)
        {
            main += Separation * (visible - 1);
        }

        float width = Orientation == Orientation.Horizontal ? main : cross;
        float height = Orientation == Orientation.Horizontal ? cross : main;
        return new Vector2(width + (Padding * 2f), height + (Padding * 2f));
    }

    public override void Arrange(UiRect rect)
    {
        Bounds = rect;
        float x = rect.X + Padding;
        float y = rect.Y + Padding;

        foreach (Control child in Children)
        {
            if (!child.Visible)
            {
                continue;
            }

            Vector2 size = child.Measure();
            child.Arrange(new UiRect(x, y, size.X, size.Y));

            if (Orientation == Orientation.Horizontal)
            {
                x += size.X + Separation;
            }
            else
            {
                y += size.Y + Separation;
            }
        }
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
