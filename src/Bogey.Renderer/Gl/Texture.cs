using System;
using Silk.NET.OpenGL;

namespace Bogey.Renderer.Gl;

public sealed class Texture : IDisposable
{
    private readonly GL _gl;
    private readonly uint _handle;

    public Texture(GL gl, int width, int height, ReadOnlySpan<byte> rgba)
    {
        _gl = gl;
        Width = width;
        Height = height;

        _handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _handle);
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);

        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, rgba);

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    public int Width { get; }

    public int Height { get; }

    public static Texture FromFile(GL gl, string path)
    {
        PngImage image = PngImage.Load(path);
        return new Texture(gl, image.Width, image.Height, image.Rgba);
    }

    public void Bind(TextureUnit unit)
    {
        _gl.ActiveTexture(unit);
        _gl.BindTexture(TextureTarget.Texture2D, _handle);
    }

    public void Dispose() => _gl.DeleteTexture(_handle);
}
