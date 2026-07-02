using System.Numerics;

namespace Bogey.Renderer.Text;

public readonly record struct Glyph(
    Vector4 Uv,
    float Width,
    float Height,
    float BearingLeft,
    float BearingTop,
    float Advance,
    bool HasPixels);
