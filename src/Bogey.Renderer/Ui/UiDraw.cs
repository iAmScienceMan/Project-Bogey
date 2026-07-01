using System.Numerics;
using Bogey.Renderer.Gl;

namespace Bogey.Renderer.Ui;

internal static class UiDraw
{
    public static void Box(PrimitiveBatch prims, UiRect rect, Rgba color)
    {
        Vector2 tl = new(rect.X, rect.Y);
        Vector2 tr = new(rect.Right, rect.Y);
        Vector2 br = new(rect.Right, rect.Bottom);
        Vector2 bl = new(rect.X, rect.Bottom);
        prims.Line(tl, tr, color);
        prims.Line(tr, br, color);
        prims.Line(br, bl, color);
        prims.Line(bl, tl, color);
    }
}
