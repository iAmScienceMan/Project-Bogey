using System;
using Silk.NET.OpenGL;

namespace Lattice.Renderer.Text;

public interface IFont : IDisposable
{
    Glyph GetGlyph(char c, int pixelSize);

    float AdvancePx(int pixelSize);

    float Ascent(int pixelSize);

    float LineHeight(int pixelSize);

    void Bind(TextureUnit unit);
}
