using System;
using System.Globalization;

namespace Content.Shared;

public static class ColorRgbUtil
{
    public static uint ParseHex(string? text, uint fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        string trimmed = text.Trim().TrimStart('#');
        return trimmed.Length == 6 && uint.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value)
            ? value
            : fallback;
    }

    public static string ToHex(uint rgb) => "#" + (rgb & 0xFFFFFF).ToString("X6", CultureInfo.InvariantCulture);

    public static (float R, float G, float B) ToFloats(uint rgb) => (
        ((rgb >> 16) & 0xFF) / 255f,
        ((rgb >> 8) & 0xFF) / 255f,
        (rgb & 0xFF) / 255f);

    public static uint FromFloats(float r, float g, float b)
        => ((uint)Math.Clamp((int)(r * 255f + 0.5f), 0, 255) << 16)
           | ((uint)Math.Clamp((int)(g * 255f + 0.5f), 0, 255) << 8)
           | (uint)Math.Clamp((int)(b * 255f + 0.5f), 0, 255);

    public static uint FromHue(float hue01)
    {
        float h = (hue01 - MathF.Floor(hue01)) * 6f;
        float x = 1f - MathF.Abs(h % 2f - 1f);

        (float r, float g, float b) = (int)h switch
        {
            0 => (1f, x, 0f),
            1 => (x, 1f, 0f),
            2 => (0f, 1f, x),
            3 => (0f, x, 1f),
            4 => (x, 0f, 1f),
            _ => (1f, 0f, x),
        };

        return FromFloats(r, g, b);
    }

    public static float ToHue(uint rgb)
    {
        (float r, float g, float b) = ToFloats(rgb);
        float max = MathF.Max(r, MathF.Max(g, b));
        float min = MathF.Min(r, MathF.Min(g, b));
        float delta = max - min;

        if (delta < 1e-5f)
        {
            return 0f;
        }

        float hue;
        if (max == r)
        {
            hue = (g - b) / delta % 6f;
        }
        else if (max == g)
        {
            hue = (b - r) / delta + 2f;
        }
        else
        {
            hue = (r - g) / delta + 4f;
        }

        hue /= 6f;
        return hue < 0f ? hue + 1f : hue;
    }
}
