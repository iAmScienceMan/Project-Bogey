using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Bogey.Renderer.Text.FreeType;
using Silk.NET.OpenGL;

namespace Bogey.Renderer.Text;

public sealed class FreeTypeFont : IFont
{
    private readonly IntPtr _library;
    private readonly IntPtr _face;
    private readonly IntPtr _glyphSlot;
    private readonly IntPtr _size;
    private readonly GlyphAtlas _atlas;

    private readonly Dictionary<long, Glyph> _glyphs = new();
    private readonly Dictionary<int, SizeMetrics> _metrics = new();

    private int _currentSize = -1;

    public FreeTypeFont(GL gl, string path)
    {
        if (FreeTypeNative.FT_Init_FreeType(out _library) != 0)
        {
            throw new InvalidOperationException("Failed to initialize FreeType.");
        }

        if (FreeTypeNative.FT_New_Face(_library, path, new CLong(0), out _face) != 0)
        {
            FreeTypeNative.FT_Done_FreeType(_library);
            throw new InvalidOperationException($"FreeType could not load the font at '{path}'.");
        }

        FT_FaceRec faceRec = ReadFace();
        _glyphSlot = faceRec.Glyph;
        _size = faceRec.Size;
        _atlas = new GlyphAtlas(gl);
    }

    public Glyph GetGlyph(char c, int pixelSize)
    {
        long key = ((long)pixelSize << 21) | c;
        if (_glyphs.TryGetValue(key, out Glyph cached))
        {
            return cached;
        }

        SetSize(pixelSize);
        FreeTypeNative.FT_Load_Char(_face, new CULong((nuint)c), FreeTypeNative.LoadRender);

        FT_GlyphSlotRec slot = Marshal.PtrToStructure<FT_GlyphSlotRec>(_glyphSlot);
        int width = (int)slot.Bitmap.Width;
        int height = (int)slot.Bitmap.Rows;
        float advance = (long)slot.Advance.X.Value >> 6;

        Vector4 uv = default;
        bool hasPixels = _atlas.TryAdd(width, height, slot.Bitmap.Buffer, slot.Bitmap.Pitch, out uv);

        Glyph glyph = new(uv, width, height, slot.BitmapLeft, slot.BitmapTop, advance, hasPixels);
        _glyphs[key] = glyph;
        return glyph;
    }

    public float AdvancePx(int pixelSize) => Metrics(pixelSize).Advance;

    public float Ascent(int pixelSize) => Metrics(pixelSize).Ascent;

    public float LineHeight(int pixelSize) => Metrics(pixelSize).LineHeight;

    public void Bind(TextureUnit unit) => _atlas.Bind(unit);

    public void Dispose()
    {
        _atlas.Dispose();
        FreeTypeNative.FT_Done_Face(_face);
        FreeTypeNative.FT_Done_FreeType(_library);
    }

    private SizeMetrics Metrics(int pixelSize)
    {
        if (_metrics.TryGetValue(pixelSize, out SizeMetrics cached))
        {
            return cached;
        }

        SetSize(pixelSize);
        FT_SizeRec sizeRec = Marshal.PtrToStructure<FT_SizeRec>(_size);
        SizeMetrics metrics = new(
            (long)sizeRec.Metrics.MaxAdvance.Value >> 6,
            (long)sizeRec.Metrics.Ascender.Value >> 6,
            (long)sizeRec.Metrics.Height.Value >> 6);

        _metrics[pixelSize] = metrics;
        return metrics;
    }

    private void SetSize(int pixelSize)
    {
        if (pixelSize != _currentSize)
        {
            FreeTypeNative.FT_Set_Pixel_Sizes(_face, 0, (uint)pixelSize);
            _currentSize = pixelSize;
        }
    }

    private FT_FaceRec ReadFace() => Marshal.PtrToStructure<FT_FaceRec>(_face);

    private readonly record struct SizeMetrics(float Advance, float Ascent, float LineHeight);
}
