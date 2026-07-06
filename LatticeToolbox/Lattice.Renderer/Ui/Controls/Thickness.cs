using System;
using System.Globalization;

namespace Lattice.Renderer.Ui.Controls;

public readonly record struct Thickness(float Left, float Top, float Right, float Bottom)
{
    public Thickness(float uniform)
        : this(uniform, uniform, uniform, uniform)
    {
    }

    public static Thickness Parse(string raw)
    {
        string[] parts = raw.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return new Thickness(Number(parts[0]));
        }

        if (parts.Length == 4)
        {
            return new Thickness(Number(parts[0]), Number(parts[1]), Number(parts[2]), Number(parts[3]));
        }

        throw new FormatException($"Thickness must be 1 or 4 numbers, got '{raw}'.");
    }

    private static float Number(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
}
