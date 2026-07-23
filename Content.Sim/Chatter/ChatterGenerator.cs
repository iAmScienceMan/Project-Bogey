using System;
using System.Text;
using Content.Shared.Chatter;

namespace Content.Sim.Chatter;

public static class ChatterGenerator
{
    private static readonly string[] Phonetic =
    {
        "ALPHA", "BRAVO", "CHARLIE", "DELTA", "ECHO", "FOXTROT", "GOLF", "HOTEL",
        "INDIA", "JULIET", "KILO", "LIMA", "MIKE", "NOVEMBER", "OSCAR", "PAPA",
        "QUEBEC", "ROMEO", "SIERRA", "TANGO", "UNIFORM", "VICTOR", "WHISKEY",
        "XRAY", "YANKEE", "ZULU",
    };

    private static readonly string[] Digits =
    {
        "ZERO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE",
    };

    private static readonly string[] RoutineWords =
    {
        "COPY", "WILCO", "ROGER", "STANDBY", "HOLDING", "ON STATION", "SAY AGAIN",
        "STATE GREEN", "CONTINUE", "PROCEED",
    };

    private static readonly string[] UrgentWords =
    {
        "CONTACT", "ENGAGING", "FOX TWO", "DEFENDING", "BINGO", "SPIKED", "BREAK",
        "MERGED", "THREAT", "COMMIT",
    };

    private static readonly string[] CalmWords =
    {
        "NEGATIVE CONTACT", "CLEAR", "STEADY", "NO JOY", "PADLOCKED", "VISUAL",
        "TALLY", "ON TRACK",
    };

    public static ChatterLine FromText(string text)
    {
        return new ChatterLine(text, StableHash(text));
    }

    private static int StableHash(string text)
    {
        uint hash = 2166136261u;
        foreach (char c in text)
        {
            hash = (hash ^ c) * 16777619u;
        }

        return (int)hash;
    }

    public static ChatterLine Generate(Random rng, in ChatterRequest request)
    {
        int target = Math.Max(4, request.LengthSymbols);
        string[] pool = request.Tone switch
        {
            ChatterTone.Urgent => UrgentWords,
            ChatterTone.Calm => CalmWords,
            _ => RoutineWords,
        };

        StringBuilder builder = new(target + 16);
        AppendCallsign(builder, rng);

        while (builder.Length < target)
        {
            builder.Append(", ");
            double roll = rng.NextDouble();
            if (roll < 0.25)
            {
                AppendCallsign(builder, rng);
            }
            else if (roll < 0.55)
            {
                AppendBearing(builder, rng);
            }
            else
            {
                builder.Append(pool[rng.Next(pool.Length)]);
            }
        }

        string text = Trim(builder.ToString(), target);
        int voiceSeed = rng.Next();
        return new ChatterLine(text, voiceSeed);
    }

    private static void AppendCallsign(StringBuilder builder, Random rng)
    {
        builder.Append(Phonetic[rng.Next(Phonetic.Length)]);
        int digits = 1 + rng.Next(2);
        for (int i = 0; i < digits; i++)
        {
            builder.Append('-').Append(Digits[rng.Next(Digits.Length)]);
        }
    }

    private static void AppendBearing(StringBuilder builder, Random rng)
    {
        builder.Append("BEARING");
        for (int i = 0; i < 3; i++)
        {
            builder.Append(' ').Append(Digits[rng.Next(Digits.Length)]);
        }
    }

    private static string Trim(string text, int target)
    {
        if (text.Length <= target)
        {
            return text;
        }

        int cut = text.LastIndexOf(' ', Math.Min(target, text.Length - 1));
        if (cut <= 0)
        {
            cut = target;
        }

        return text[..cut].TrimEnd(',', ' ');
    }
}
