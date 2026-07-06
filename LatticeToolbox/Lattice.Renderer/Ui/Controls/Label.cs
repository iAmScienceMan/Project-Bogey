using System.Numerics;
using Lattice.Renderer.Gl;
using Lattice.Renderer.Text;

namespace Lattice.Renderer.Ui.Controls;


public sealed class Label : Control
{
    public string Text { get; set; } = string.Empty;

    public float FontSize { get; set; } = UiTheme.TextBlockFontPx;

    public Rgba Color { get; set; } = UiTheme.Text;

    public override Vector2 Measure()
        => new(TextBatch.Measure(Text, FontSize), FontSize);

    public override void Draw(PrimitiveBatch prims, TextBatch text)
    {
        if (!Visible)
        {
            return;
        }

        text.Text(new Vector2(Bounds.X, Bounds.Y), FontSize, Color, Text);
    }
}
