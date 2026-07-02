using System;
using System.Runtime.InteropServices;

namespace Bogey.Renderer.Text.FreeType;

[StructLayout(LayoutKind.Sequential)]
internal struct FT_Generic
{
    public IntPtr Data;
    public IntPtr Finalizer;
}

[StructLayout(LayoutKind.Sequential)]
internal struct FT_Vector
{
    public CLong X;
    public CLong Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct FT_BBox
{
    public CLong XMin;
    public CLong YMin;
    public CLong XMax;
    public CLong YMax;
}

[StructLayout(LayoutKind.Sequential)]
internal struct FT_Glyph_Metrics
{
    public CLong Width;
    public CLong Height;
    public CLong HoriBearingX;
    public CLong HoriBearingY;
    public CLong HoriAdvance;
    public CLong VertBearingX;
    public CLong VertBearingY;
    public CLong VertAdvance;
}

[StructLayout(LayoutKind.Sequential)]
internal struct FT_Bitmap
{
    public uint Rows;
    public uint Width;
    public int Pitch;
    public IntPtr Buffer;
    public ushort NumGrays;
    public byte PixelMode;
    public byte PaletteMode;
    public IntPtr Palette;
}

[StructLayout(LayoutKind.Sequential)]
internal struct FT_Size_Metrics
{
    public ushort XPpem;
    public ushort YPpem;
    public CLong XScale;
    public CLong YScale;
    public CLong Ascender;
    public CLong Descender;
    public CLong Height;
    public CLong MaxAdvance;
}

[StructLayout(LayoutKind.Sequential)]
internal struct FT_SizeRec
{
    public IntPtr Face;
    public FT_Generic Generic;
    public FT_Size_Metrics Metrics;
}

[StructLayout(LayoutKind.Sequential)]
internal struct FT_GlyphSlotRec
{
    public IntPtr Library;
    public IntPtr Face;
    public IntPtr Next;
    public uint GlyphIndex;
    public FT_Generic Generic;
    public FT_Glyph_Metrics Metrics;
    public CLong LinearHoriAdvance;
    public CLong LinearVertAdvance;
    public FT_Vector Advance;
    public int Format;
    public FT_Bitmap Bitmap;
    public int BitmapLeft;
    public int BitmapTop;
}

[StructLayout(LayoutKind.Sequential)]
internal struct FT_FaceRec
{
    public CLong NumFaces;
    public CLong FaceIndex;
    public CLong FaceFlags;
    public CLong StyleFlags;
    public CLong NumGlyphs;
    public IntPtr FamilyName;
    public IntPtr StyleName;
    public int NumFixedSizes;
    public IntPtr AvailableSizes;
    public int NumCharmaps;
    public IntPtr Charmaps;
    public FT_Generic Generic;
    public FT_BBox Bbox;
    public ushort UnitsPerEm;
    public short Ascender;
    public short Descender;
    public short Height;
    public short MaxAdvanceWidth;
    public short MaxAdvanceHeight;
    public short UnderlinePosition;
    public short UnderlineThickness;
    public IntPtr Glyph;
    public IntPtr Size;
    public IntPtr Charmap;
}
