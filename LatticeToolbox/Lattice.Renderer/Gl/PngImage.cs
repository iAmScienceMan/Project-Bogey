using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Lattice.Renderer.Gl;

public sealed class PngImage
{
    private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    private readonly byte[] _rgba;

    private PngImage(int width, int height, byte[] rgba)
    {
        Width = width;
        Height = height;
        _rgba = rgba;
    }

    public int Width { get; }

    public int Height { get; }

    public ReadOnlySpan<byte> Rgba => _rgba;

    public static PngImage Load(string path) => Decode(File.ReadAllBytes(path));

    public static PngImage Decode(byte[] bytes)
    {
        if (bytes.Length < 8 || !bytes.AsSpan(0, 8).SequenceEqual(Signature))
        {
            throw new InvalidDataException("File is not a PNG image.");
        }

        int width = 0;
        int height = 0;
        int bitDepth = 0;
        int colorType = 0;
        int interlace = 0;
        byte[]? palette = null;
        byte[]? paletteAlpha = null;
        bool sawHeader = false;

        using MemoryStream compressed = new();

        int offset = 8;
        while (offset + 12 <= bytes.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4));
            int dataStart = offset + 8;
            if (length < 0 || dataStart + length + 4 > bytes.Length)
            {
                throw new InvalidDataException("PNG chunk extends past the end of the file.");
            }

            string type = Encoding.ASCII.GetString(bytes, offset + 4, 4);
            ReadOnlySpan<byte> data = bytes.AsSpan(dataStart, length);

            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(data);
                    height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                    bitDepth = data[8];
                    colorType = data[9];
                    interlace = data[12];
                    sawHeader = true;
                    break;
                case "PLTE":
                    palette = data.ToArray();
                    break;
                case "tRNS":
                    paletteAlpha = data.ToArray();
                    break;
                case "IDAT":
                    compressed.Write(data);
                    break;
                case "IEND":
                    offset = bytes.Length;
                    continue;
            }

            offset = dataStart + length + 4;
        }

        if (!sawHeader)
        {
            throw new InvalidDataException("PNG is missing its IHDR header chunk.");
        }

        if (bitDepth != 8)
        {
            throw new NotSupportedException($"Only 8-bit-per-channel PNG images are supported (got bit depth {bitDepth}).");
        }

        if (interlace != 0)
        {
            throw new NotSupportedException("Interlaced PNG images are not supported.");
        }

        byte[] raw = Inflate(compressed);
        byte[] rgba = ToRgba(raw, width, height, colorType, palette, paletteAlpha);
        return new PngImage(width, height, rgba);
    }

    private static byte[] Inflate(MemoryStream compressed)
    {
        compressed.Position = 0;
        using ZLibStream zlib = new(compressed, CompressionMode.Decompress);
        using MemoryStream output = new();
        zlib.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] ToRgba(byte[] raw, int width, int height, int colorType, byte[]? palette, byte[]? paletteAlpha)
    {
        int channels = colorType switch
        {
            0 => 1,
            2 => 3,
            3 => 1,
            4 => 2,
            6 => 4,
            _ => throw new NotSupportedException($"Unsupported PNG color type {colorType}."),
        };

        if (colorType == 3 && palette is null)
        {
            throw new InvalidDataException("Indexed PNG is missing its PLTE palette chunk.");
        }

        int stride = width * channels;
        long required = (long)(stride + 1) * height;
        if (raw.Length < required)
        {
            throw new InvalidDataException("PNG pixel data is shorter than the declared image size.");
        }

        byte[] pixels = Unfilter(raw, height, channels, stride);
        byte[] rgba = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            int rowStart = y * stride;
            for (int x = 0; x < width; x++)
            {
                int src = rowStart + (x * channels);
                int dst = ((y * width) + x) * 4;

                byte r;
                byte g;
                byte b;
                byte a;
                switch (colorType)
                {
                    case 0:
                        r = g = b = pixels[src];
                        a = 255;
                        break;
                    case 2:
                        r = pixels[src];
                        g = pixels[src + 1];
                        b = pixels[src + 2];
                        a = 255;
                        break;
                    case 3:
                        int index = pixels[src];
                        int entry = index * 3;
                        r = palette![entry];
                        g = palette[entry + 1];
                        b = palette[entry + 2];
                        a = paletteAlpha is not null && index < paletteAlpha.Length ? paletteAlpha[index] : (byte)255;
                        break;
                    case 4:
                        r = g = b = pixels[src];
                        a = pixels[src + 1];
                        break;
                    default:
                        r = pixels[src];
                        g = pixels[src + 1];
                        b = pixels[src + 2];
                        a = pixels[src + 3];
                        break;
                }

                rgba[dst] = r;
                rgba[dst + 1] = g;
                rgba[dst + 2] = b;
                rgba[dst + 3] = a;
            }
        }

        return rgba;
    }

    private static byte[] Unfilter(byte[] raw, int height, int bytesPerPixel, int stride)
    {
        byte[] output = new byte[height * stride];
        int rawPos = 0;

        for (int y = 0; y < height; y++)
        {
            int filter = raw[rawPos++];
            int rowStart = y * stride;
            int priorRow = rowStart - stride;

            for (int i = 0; i < stride; i++)
            {
                int value = raw[rawPos++];
                int left = i >= bytesPerPixel ? output[rowStart + i - bytesPerPixel] : 0;
                int up = y > 0 ? output[priorRow + i] : 0;
                int upperLeft = y > 0 && i >= bytesPerPixel ? output[priorRow + i - bytesPerPixel] : 0;

                int reconstructed = filter switch
                {
                    0 => value,
                    1 => value + left,
                    2 => value + up,
                    3 => value + ((left + up) / 2),
                    4 => value + Paeth(left, up, upperLeft),
                    _ => throw new InvalidDataException($"Unknown PNG scanline filter {filter}."),
                };

                output[rowStart + i] = (byte)(reconstructed & 0xFF);
            }
        }

        return output;
    }

    private static int Paeth(int left, int up, int upperLeft)
    {
        int prediction = left + up - upperLeft;
        int distLeft = Math.Abs(prediction - left);
        int distUp = Math.Abs(prediction - up);
        int distUpperLeft = Math.Abs(prediction - upperLeft);

        if (distLeft <= distUp && distLeft <= distUpperLeft)
        {
            return left;
        }

        return distUp <= distUpperLeft ? up : upperLeft;
    }
}
