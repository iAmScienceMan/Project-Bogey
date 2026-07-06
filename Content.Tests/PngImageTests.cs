using System;
using System.IO;
using System.IO.Compression;
using Lattice.Renderer.Gl;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class PngImageTests
{
    [Test]
    public void Decode_TruecolorAlpha_ReadsPixelsTopToBottom()
    {
        byte[] scanlines =
        {
            0, 255, 0, 0, 255, 0, 255, 0, 128,
            0, 0, 0, 255, 255, 255, 255, 0, 255,
        };

        byte[] png = BuildPng(width: 2, height: 2, colorType: 6, scanlines);

        PngImage image = PngImage.Decode(png);

        Assert.That(image.Width, Is.EqualTo(2));
        Assert.That(image.Height, Is.EqualTo(2));

        byte[] rgba = image.Rgba.ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(rgba[0..4], Is.EqualTo(new byte[] { 255, 0, 0, 255 }));
            Assert.That(rgba[4..8], Is.EqualTo(new byte[] { 0, 255, 0, 128 }));
            Assert.That(rgba[8..12], Is.EqualTo(new byte[] { 0, 0, 255, 255 }));
            Assert.That(rgba[12..16], Is.EqualTo(new byte[] { 255, 255, 0, 255 }));
        });
    }

    [Test]
    public void Decode_SubFilteredTruecolor_ReconstructsOriginalPixels()
    {
        byte[] scanlines =
        {
            1, 10, 20, 30, 5, 5, 5,
        };

        byte[] png = BuildPng(width: 2, height: 1, colorType: 2, scanlines);

        PngImage image = PngImage.Decode(png);

        byte[] rgba = image.Rgba.ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(rgba[0..4], Is.EqualTo(new byte[] { 10, 20, 30, 255 }));
            Assert.That(rgba[4..8], Is.EqualTo(new byte[] { 15, 25, 35, 255 }));
        });
    }

    [Test]
    public void Decode_Rejects_NonPngData()
    {
        Assert.Throws<InvalidDataException>(() => PngImage.Decode(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
    }

    private static byte[] BuildPng(int width, int height, byte colorType, byte[] scanlines)
    {
        using MemoryStream stream = new();
        stream.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        byte[] ihdr = new byte[13];
        WriteBigEndian(ihdr, 0, width);
        WriteBigEndian(ihdr, 4, height);
        ihdr[8] = 8;
        ihdr[9] = colorType;
        WriteChunk(stream, "IHDR", ihdr);

        using MemoryStream compressed = new();
        using (ZLibStream zlib = new(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(scanlines);
        }

        WriteChunk(stream, "IDAT", compressed.ToArray());
        WriteChunk(stream, "IEND", Array.Empty<byte>());
        return stream.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        byte[] length = new byte[4];
        WriteBigEndian(length, 0, data.Length);
        stream.Write(length);
        foreach (char c in type)
        {
            stream.WriteByte((byte)c);
        }

        stream.Write(data);
        stream.Write(new byte[4]);
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)((value >> 24) & 0xFF);
        buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 3] = (byte)(value & 0xFF);
    }
}
