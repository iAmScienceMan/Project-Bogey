using System;
using System.Buffers.Binary;
using System.IO;

namespace Content.Renderer.Audio;

public readonly struct WavData
{
    public WavData(short[] samples, int channels, int sampleRate)
    {
        Samples = samples;
        Channels = channels;
        SampleRate = sampleRate;
    }

    public short[] Samples { get; }

    public int Channels { get; }

    public int SampleRate { get; }
}

public static class WavFile
{
    public static WavData Load(string path)
    {
        ReadOnlySpan<byte> span = File.ReadAllBytes(path);

        if (span.Length < 12
            || !span[..4].SequenceEqual("RIFF"u8)
            || !span.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            throw new InvalidDataException($"'{path}' is not a RIFF/WAVE file.");
        }

        ushort format = 0;
        int channels = 0;
        int sampleRate = 0;
        int bitsPerSample = 0;
        ReadOnlySpan<byte> data = default;

        int offset = 12;
        while (offset + 8 <= span.Length)
        {
            ReadOnlySpan<byte> id = span.Slice(offset, 4);
            int size = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset + 4, 4));
            int body = offset + 8;
            if (size < 0 || body + size > span.Length)
            {
                size = span.Length - body;
            }

            if (id.SequenceEqual("fmt "u8))
            {
                format = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(body, 2));
                channels = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(body + 2, 2));
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(body + 4, 4));
                bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(body + 14, 2));
            }
            else if (id.SequenceEqual("data"u8))
            {
                data = span.Slice(body, size);
            }

            offset = body + size + (size & 1);
        }

        if (format != 1 || bitsPerSample != 16)
        {
            throw new InvalidDataException(
                $"'{path}' must be 16-bit PCM (found format {format}, {bitsPerSample}-bit).");
        }

        if (channels <= 0 || sampleRate <= 0 || data.IsEmpty)
        {
            throw new InvalidDataException($"'{path}' is missing a valid fmt/data chunk.");
        }

        int count = data.Length / 2;
        short[] samples = new short[count];
        for (int i = 0; i < count; i++)
        {
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(i * 2, 2));
        }

        return new WavData(samples, channels, sampleRate);
    }
}
