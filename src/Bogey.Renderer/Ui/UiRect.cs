using System.Numerics;

namespace Bogey.Renderer.Ui;


public readonly record struct UiRect(float X, float Y, float W, float H)
{
    public float Right => X + W;
    public float Bottom => Y + H;

    public bool Contains(Vector2 p) => p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;

    public Vector2 Min => new(X, Y);
    public Vector2 Max => new(Right, Bottom);
}
