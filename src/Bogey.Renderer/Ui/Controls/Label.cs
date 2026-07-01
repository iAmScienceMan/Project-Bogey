using System.Numerics;
using Bogey.Renderer.Gl;
using Bogey.Renderer.Text;

namespace Bogey.Renderer.Ui.Controls;


public sealed class Label : Control
{
    public string Text { get; set; } = string.Empty;

    public override Vector2 Measure()
        => new(TextBatch.Measure(Text, UiTheme.TextBlockFontPx), UiTheme.TextBlockFontPx);

    public override void Draw(PrimitiveBatch prims, TextBatch text)
    {
        if (!Visible)
        {
            return;
        }

        text.Text(new Vector2(Bounds.X, Bounds.Y), UiTheme.TextBlockFontPx, UiTheme.Text, Text);
    }
}
