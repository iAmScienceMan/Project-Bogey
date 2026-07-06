using System;
using System.Numerics;
using Silk.NET.OpenGL;

namespace Lattice.Renderer.Text;

internal sealed class GlyphAtlas : IDisposable
{
    private const int AtlasSize = 1024;
    private const int Padding = 1;

    private readonly GL _gl;
    private readonly uint _texture;

    private int _penX = Padding;
    private int _penY = Padding;
    private int _shelfHeight;

    public GlyphAtlas(GL gl)
    {
        _gl = gl;
        _texture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, _texture);

        byte[] zeros = new byte[AtlasSize * AtlasSize * 4];
        ReadOnlySpan<byte> span = zeros;
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, AtlasSize, AtlasSize, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, span);

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    public unsafe bool TryAdd(int width, int height, IntPtr coverage, int pitch, out Vector4 uv)
    {
        uv = default;
        if (width <= 0 || height <= 0 || pitch <= 0)
        {
            return false;
        }

        if (_penX + width + Padding > AtlasSize)
        {
            _penX = Padding;
            _penY += _shelfHeight + Padding;
            _shelfHeight = 0;
        }

        if (_penY + height + Padding > AtlasSize)
        {
            return false;
        }

        _gl.BindTexture(TextureTarget.Texture2D, _texture);
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        _gl.PixelStore(PixelStoreParameter.UnpackRowLength, pitch);
        _gl.TexSubImage2D(TextureTarget.Texture2D, 0, _penX, _penY, (uint)width, (uint)height,
            PixelFormat.Red, PixelType.UnsignedByte, (void*)coverage);
        _gl.PixelStore(PixelStoreParameter.UnpackRowLength, 0);
        _gl.BindTexture(TextureTarget.Texture2D, 0);

        uv = new Vector4(
            (float)_penX / AtlasSize,
            (float)_penY / AtlasSize,
            (float)(_penX + width) / AtlasSize,
            (float)(_penY + height) / AtlasSize);

        _penX += width + Padding;
        _shelfHeight = Math.Max(_shelfHeight, height);
        return true;
    }

    public void Bind(TextureUnit unit)
    {
        _gl.ActiveTexture(unit);
        _gl.BindTexture(TextureTarget.Texture2D, _texture);
    }

    public void Dispose() => _gl.DeleteTexture(_texture);
}
