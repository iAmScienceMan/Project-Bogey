using System;
using System.Globalization;

namespace Bogey.Renderer.Gl;

public readonly record struct Rgba(float R, float G, float B, float A)
{
    public Rgba(float r, float g, float b)
        : this(r, g, b, 1f)
    {
    }


    public Rgba WithAlpha(float alpha) => new(R, G, B, alpha);


    public Rgba FadeBy(float factor) => new(R, G, B, A * factor);

    public static Rgba Parse(string hex)
    {
        ReadOnlySpan<char> digits = hex.AsSpan().Trim();
        if (digits.Length > 0 && digits[0] == '#')
        {
            digits = digits[1..];
        }

        if (digits.Length != 6 && digits.Length != 8)
        {
            throw new FormatException($"Color '{hex}' must be #RRGGBB or #RRGGBBAA.");
        }

        float r = Channel(digits[..2]);
        float g = Channel(digits.Slice(2, 2));
        float b = Channel(digits.Slice(4, 2));
        float a = digits.Length == 8 ? Channel(digits.Slice(6, 2)) : 1f;
        return new Rgba(r, g, b, a);
    }

    private static float Channel(ReadOnlySpan<char> pair)
        => byte.Parse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255f;
}
