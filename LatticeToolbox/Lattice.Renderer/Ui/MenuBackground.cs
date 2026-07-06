using System.Numerics;
using Lattice.Renderer.Gl;

namespace Lattice.Renderer.Ui;

public static class MenuBackground
{
    private const float Spacing = 48f;

    public static void Draw(PrimitiveBatch prims, UiRect rect, float offset)
    {
        prims.FilledQuad(rect.Min, rect.Max, UiTheme.MenuBackground);

        float start = offset % Spacing;
        for (float x = rect.X + start; x <= rect.Right; x += Spacing)
        {
            prims.Line(new Vector2(x, rect.Y), new Vector2(x, rect.Bottom), UiTheme.MenuGrid);
        }

        for (float y = rect.Y + start; y <= rect.Bottom; y += Spacing)
        {
            prims.Line(new Vector2(rect.X, y), new Vector2(rect.Right, y), UiTheme.MenuGrid);
        }
    }
}
