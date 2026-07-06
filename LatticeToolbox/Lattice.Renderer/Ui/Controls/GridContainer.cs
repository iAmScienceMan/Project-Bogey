using System;
using System.Numerics;

namespace Lattice.Renderer.Ui.Controls;

public sealed class GridContainer : Control
{
    public int Columns { get; set; } = 1;

    public float Separation { get; set; }

    public float Padding { get; set; }

    public override Vector2 Measure()
    {
        (float[] widths, float[] heights) = ComputeGrid();
        int cols = Math.Max(1, Columns);
        int usedCols = Math.Min(cols, Math.Max(1, VisibleCount()));

        float width = Padding * 2f + Separation * Math.Max(0, usedCols - 1);
        foreach (float w in widths)
        {
            width += w;
        }

        float height = Padding * 2f + Separation * Math.Max(0, heights.Length - 1);
        foreach (float h in heights)
        {
            height += h;
        }

        return new Vector2(width, height);
    }

    public override void Arrange(UiRect rect)
    {
        Bounds = rect;
        (float[] widths, float[] heights) = ComputeGrid();
        int cols = Math.Max(1, Columns);

        float[] colX = new float[cols];
        float x = rect.X + Padding;
        for (int c = 0; c < cols; c++)
        {
            colX[c] = x;
            x += widths[c] + Separation;
        }

        float[] rowY = new float[heights.Length];
        float y = rect.Y + Padding;
        for (int r = 0; r < heights.Length; r++)
        {
            rowY[r] = y;
            y += heights[r] + Separation;
        }

        int index = 0;
        foreach (Control child in Children)
        {
            if (!child.Visible)
            {
                continue;
            }

            int col = index % cols;
            int row = index / cols;
            Vector2 size = child.Measure();
            child.Arrange(new UiRect(colX[col], rowY[row], size.X, size.Y));
            index++;
        }
    }

    private (float[] Widths, float[] Heights) ComputeGrid()
    {
        int cols = Math.Max(1, Columns);
        int visible = VisibleCount();
        int rows = Math.Max(1, (visible + cols - 1) / cols);

        float[] widths = new float[cols];
        float[] heights = new float[rows];

        int index = 0;
        foreach (Control child in Children)
        {
            if (!child.Visible)
            {
                continue;
            }

            int col = index % cols;
            int row = index / cols;
            Vector2 size = child.Measure();
            widths[col] = MathF.Max(widths[col], size.X);
            heights[row] = MathF.Max(heights[row], size.Y);
            index++;
        }

        return (widths, heights);
    }

    private int VisibleCount()
    {
        int count = 0;
        foreach (Control child in Children)
        {
            if (child.Visible)
            {
                count++;
            }
        }

        return count;
    }
}
