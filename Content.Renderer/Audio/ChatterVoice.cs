using System;
using System.Collections.Generic;
using Content.Shared.Chatter;

namespace Content.Renderer.Audio;

public static class ChatterVoice
{
    private const int DefaultRate = 44100;
    private const float MaxSeconds = 20f;

    private readonly struct Phone
    {
        public Phone(bool silence, bool voiced, float f1, float f2, float f3, float duration, float level)
        {
            Silence = silence;
            Voiced = voiced;
            F1 = f1;
            F2 = f2;
            F3 = f3;
            Duration = duration;
            Level = level;
        }

        public bool Silence { get; }

        public bool Voiced { get; }

        public float F1 { get; }

        public float F2 { get; }

        public float F3 { get; }

        public float Duration { get; }

        public float Level { get; }
    }

    public static short[] Synthesize(in ChatterLine line, int rate = DefaultRate)
    {
        Random rng = new(line.VoiceSeed);
        float tempo = 0.85f + (float)rng.NextDouble() * 0.4f;
        float f0Base = 82f + (float)rng.NextDouble() * 46f;
        float vibratoHz = 4.5f + (float)rng.NextDouble() * 2f;

        List<Phone> phones = Parse(line.Text, rng);
        if (phones.Count == 0)
        {
            return Array.Empty<short>();
        }

        int n = 0;
        foreach (Phone phone in phones)
        {
            n += Math.Max(1, (int)(phone.Duration * tempo * rate));
        }

        n = Math.Min(n, (int)(MaxSeconds * rate));
        if (n <= 0)
        {
            return Array.Empty<short>();
        }

        float[] f1 = new float[n];
        float[] f2 = new float[n];
        float[] f3 = new float[n];
        float[] amp = new float[n];
        float[] voiced = new float[n];
        LayOutPhones(phones, rng, tempo, rate, n, f1, f2, f3, amp, voiced);

        float[] source = BuildSource(rng, voiced, rate, f0Base, vibratoHz);

        float[] tract = new float[n];
        Biquad bp1 = default;
        Biquad bp2 = default;
        Biquad bp3 = default;
        float previous = 0f;
        for (int i = 0; i < n; i++)
        {
            bool isVoiced = voiced[i] > 0.5f;
            float excitation = source[i] - 0.95f * previous;
            previous = source[i];

            bp1.SetBandpass(f1[i], isVoiced ? 90f : 150f, rate);
            bp2.SetBandpass(f2[i], isVoiced ? 110f : 200f, rate);
            bp3.SetBandpass(f3[i], isVoiced ? 150f : 250f, rate);
            float y = bp1.Process(excitation) + 0.9f * bp2.Process(excitation) + 0.7f * bp3.Process(excitation);
            tract[i] = y * amp[i];
        }

        BandLimit(tract, rate);
        Normalize(tract, 0.9f);
        ApplyFades(tract, rate);

        float[] mixed = Assemble(rng, tract, rate);
        return ToPcm(mixed);
    }

    private static List<Phone> Parse(string text, Random rng)
    {
        List<Phone> phones = new();
        float jitter() => 0.88f + (float)rng.NextDouble() * 0.24f;

        string lower = text.ToLowerInvariant();
        for (int index = 0; index < lower.Length; index++)
        {
            char c = lower[index];
            char next = index + 1 < lower.Length ? lower[index + 1] : '\0';

            if (next == 'h' && (c is 'c' or 's' or 't' or 'p'))
            {
                switch (c)
                {
                    case 'c':
                        AddPlosive(phones, false, 400f, 1800f, 2600f);
                        AddCons(phones, false, 400f, 1950f, 2650f, 0.08f, 0.55f);
                        break;
                    case 's': AddCons(phones, false, 400f, 1900f, 2600f, 0.11f, 0.55f); break;
                    case 't': AddCons(phones, false, 400f, 1600f, 2500f, 0.09f, 0.45f); break;
                    case 'p': AddCons(phones, false, 400f, 1400f, 2400f, 0.10f, 0.5f); break;
                }

                index++;
                continue;
            }

            bool softC = next is 'e' or 'i' or 'y';
            switch (c)
            {
                case 'a': AddVowel(phones, 720f, 1240f, 2600f, jitter()); break;
                case 'e': AddVowel(phones, 530f, 1780f, 2480f, jitter()); break;
                case 'i':
                case 'y': AddVowel(phones, 360f, 2100f, 2900f, jitter()); break;
                case 'o': AddVowel(phones, 540f, 900f, 2400f, jitter()); break;
                case 'u': AddVowel(phones, 340f, 820f, 2350f, jitter()); break;

                case 'm': AddCons(phones, true, 300f, 1100f, 2400f, 0.09f, 0.7f); break;
                case 'n': AddCons(phones, true, 300f, 1600f, 2500f, 0.09f, 0.7f); break;
                case 'l': AddCons(phones, true, 360f, 1300f, 2600f, 0.08f, 0.7f); break;
                case 'r': AddCons(phones, true, 490f, 1350f, 1700f, 0.08f, 0.7f); break;
                case 'w': AddCons(phones, true, 350f, 900f, 2200f, 0.07f, 0.7f); break;

                case 'v': AddCons(phones, true, 320f, 1400f, 2400f, 0.09f, 0.55f); break;
                case 'z': AddCons(phones, true, 320f, 2600f, 3000f, 0.10f, 0.55f); break;
                case 'j': AddCons(phones, true, 320f, 2000f, 2600f, 0.10f, 0.55f); break;

                case 'f': AddCons(phones, false, 400f, 1400f, 2400f, 0.10f, 0.5f); break;
                case 's': AddCons(phones, false, 400f, 2600f, 3000f, 0.11f, 0.55f); break;
                case 'h': AddCons(phones, false, 500f, 1500f, 2500f, 0.09f, 0.4f); break;
                case 'x': AddCons(phones, false, 400f, 2400f, 2900f, 0.11f, 0.55f); break;

                case 'c':
                    if (softC)
                    {
                        AddCons(phones, false, 400f, 2600f, 3000f, 0.11f, 0.55f);
                    }
                    else
                    {
                        AddPlosive(phones, false, 400f, 1600f, 2200f);
                    }

                    break;

                case 'p': AddPlosive(phones, false, 400f, 1000f, 2200f); break;
                case 'b': AddPlosive(phones, true, 320f, 1000f, 2200f); break;
                case 't': AddPlosive(phones, false, 400f, 1800f, 2600f); break;
                case 'd': AddPlosive(phones, true, 320f, 1800f, 2600f); break;
                case 'k':
                case 'q': AddPlosive(phones, false, 400f, 1600f, 2200f); break;
                case 'g': AddPlosive(phones, true, 320f, 1600f, 2200f); break;

                case ' ': AddPause(phones, 0.09f); break;
                case ',':
                case ';':
                case ':':
                case '-': AddPause(phones, 0.14f); break;
                case '.':
                case '!':
                case '?': AddPause(phones, 0.22f); break;

                default:
                    if (char.IsLetterOrDigit(c))
                    {
                        AddVowel(phones, 500f, 1450f, 2500f, 0.7f);
                    }

                    break;
            }
        }

        return phones;
    }

    private static void AddVowel(List<Phone> phones, float f1, float f2, float f3, float scale)
    {
        phones.Add(new Phone(false, true, f1, f2, f3, 0.14f * scale, 1f));
    }

    private static void AddCons(List<Phone> phones, bool voiced, float f1, float f2, float f3, float dur, float level)
    {
        phones.Add(new Phone(false, voiced, f1, f2, f3, dur, level));
    }

    private static void AddPlosive(List<Phone> phones, bool voiced, float f1, float f2, float f3)
    {
        phones.Add(new Phone(true, false, f1, f2, f3, 0.035f, 0f));
        phones.Add(new Phone(false, voiced, f1, f2, f3, 0.016f, 0.85f));
    }

    private static void AddPause(List<Phone> phones, float dur)
    {
        phones.Add(new Phone(true, false, 500f, 1450f, 2500f, dur, 0f));
    }

    private static void LayOutPhones(
        List<Phone> phones,
        Random rng,
        float tempo,
        int rate,
        int n,
        float[] f1,
        float[] f2,
        float[] f3,
        float[] amp,
        float[] voiced)
    {
        Array.Fill(f1, 500f);
        Array.Fill(f2, 1450f);
        Array.Fill(f3, 2500f);

        (float F1, float F2, float F3) prev = (500f, 1450f, 2500f);
        int pos = 0;

        foreach (Phone phone in phones)
        {
            int length = Math.Max(1, (int)(phone.Duration * tempo * rate));
            int i0 = pos;
            int i1 = Math.Min(pos + length, n);
            pos = i1;
            length = i1 - i0;
            if (length <= 0)
            {
                break;
            }

            if (phone.Silence)
            {
                for (int i = 0; i < length; i++)
                {
                    f1[i0 + i] = prev.F1;
                    f2[i0 + i] = prev.F2;
                    f3[i0 + i] = prev.F3;
                }

                continue;
            }

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / length;
                f1[i0 + i] = Lerp(prev.F1, phone.F1, t);
                f2[i0 + i] = Lerp(prev.F2, phone.F2, t);
                f3[i0 + i] = Lerp(prev.F3, phone.F3, t);
                voiced[i0 + i] = phone.Voiced ? 1f : 0f;
            }

            prev = (phone.F1, phone.F2, phone.F3);

            int fade = Math.Min((int)(0.012 * rate), length / 2);
            float gain = phone.Level * (0.9f + (float)rng.NextDouble() * 0.1f);
            for (int i = 0; i < length; i++)
            {
                float env = 1f;
                if (fade > 0)
                {
                    if (i < fade)
                    {
                        env = (float)i / fade;
                    }
                    else if (i >= length - fade)
                    {
                        env = (float)(length - 1 - i) / fade;
                    }
                }

                amp[i0 + i] = env * gain;
            }
        }
    }

    private static float[] BuildSource(Random rng, float[] voiced, int rate, float f0Base, float vibratoHz)
    {
        int n = voiced.Length;
        float[] source = new float[n];
        double phase = 0.0;
        double drift = 0.0;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float declination = -0.16f * i / n;
            drift += 0.06 * NextGaussian(rng) * 0.003;
            float vibrato = 0.02f * MathF.Sin(2f * MathF.PI * vibratoHz * t);
            float f0 = Math.Clamp(f0Base * (1f + declination + (float)drift + vibrato), 55f, 200f);

            phase += f0 / rate;
            float buzz = 0f;
            for (int h = 1; h < 26; h++)
            {
                buzz += (1f / h) * MathF.Sin(2f * MathF.PI * (float)phase * h);
            }

            float noise = (float)NextGaussian(rng);
            float v = voiced[i];
            source[i] = buzz * 0.5f * v + noise * (1f - v) * 0.8f + noise * 0.02f;
        }

        return source;
    }

    private static float[] Assemble(Random rng, float[] voice, int rate)
    {
        float[] head = KeyingClick(rng, rate);
        float[] tail = KeyingClick(rng, rate);
        int gap = (int)(0.02 * rate);

        int total = head.Length + gap + voice.Length + gap + tail.Length;
        float[] mix = new float[total];

        int offset = 0;
        Array.Copy(head, 0, mix, offset, head.Length);
        offset += head.Length + gap;
        Array.Copy(voice, 0, mix, offset, voice.Length);
        offset += voice.Length + gap;
        Array.Copy(tail, 0, mix, offset, tail.Length);

        Biquad hissHigh = default;
        hissHigh.SetHighpass(320f, rate);
        Biquad hissLow = default;
        hissLow.SetLowpass(3000f, rate);
        for (int i = 0; i < total; i++)
        {
            float hiss = hissLow.Process(hissHigh.Process((float)NextGaussian(rng)));
            mix[i] = MathF.Tanh((mix[i] + hiss * 0.02f) * 1.2f);
        }

        Normalize(mix, 0.92f);
        return mix;
    }

    private static void BandLimit(float[] signal, int rate)
    {
        Biquad highPass = default;
        highPass.SetHighpass(300f, rate);
        Biquad lowPass = default;
        lowPass.SetLowpass(3000f, rate);
        for (int i = 0; i < signal.Length; i++)
        {
            signal[i] = lowPass.Process(highPass.Process(signal[i]));
        }
    }

    private static float[] KeyingClick(Random rng, int rate)
    {
        double decay = Range(rng, 0.0008, 0.003);
        int length = (int)(Math.Min(0.014, decay * 8) * rate);
        if (length <= 0)
        {
            return Array.Empty<float>();
        }

        float ringHz = (float)Range(rng, 1400.0, 3800.0);
        float sign = rng.NextDouble() < 0.5 ? -1f : 1f;

        float[] click = new float[length];
        float prev = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / rate;
            float white = (float)NextGaussian(rng);
            float edge = white - prev;
            prev = white;
            float ring = MathF.Sin(2f * MathF.PI * ringHz * t) * 0.35f;
            float env = MathF.Exp(-t / (float)decay);
            click[i] = (edge * 0.6f + ring) * env * sign * 0.4f;
        }

        return click;
    }

    private static short[] ToPcm(float[] signal)
    {
        short[] pcm = new short[signal.Length];
        for (int i = 0; i < signal.Length; i++)
        {
            pcm[i] = (short)Math.Clamp((int)(signal[i] * 32767f), short.MinValue, short.MaxValue);
        }

        return pcm;
    }

    private static void Normalize(float[] signal, float peak)
    {
        float max = 1e-9f;
        for (int i = 0; i < signal.Length; i++)
        {
            max = MathF.Max(max, MathF.Abs(signal[i]));
        }

        float scale = peak / max;
        for (int i = 0; i < signal.Length; i++)
        {
            signal[i] *= scale;
        }
    }

    private static void ApplyFades(float[] signal, int rate)
    {
        int fade = Math.Min((int)(0.02 * rate), signal.Length / 2);
        for (int i = 0; i < fade; i++)
        {
            float k = (float)i / fade;
            signal[i] *= k;
            signal[signal.Length - 1 - i] *= k;
        }
    }

    private static double Range(Random rng, double lo, double hi) => lo + rng.NextDouble() * (hi - lo);

    private static double NextGaussian(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private struct Biquad
    {
        private float _b0;
        private float _b1;
        private float _b2;
        private float _a1;
        private float _a2;
        private float _x1;
        private float _x2;
        private float _y1;
        private float _y2;

        public void SetBandpass(float centerHz, float bandwidthHz, int rate)
        {
            float q = Math.Clamp(centerHz / bandwidthHz, 1f, 12f);
            float w0 = 2f * MathF.PI * centerHz / rate;
            float cos = MathF.Cos(w0);
            float alpha = MathF.Sin(w0) / (2f * q);
            float a0 = 1f + alpha;

            _b0 = alpha / a0;
            _b1 = 0f;
            _b2 = -alpha / a0;
            _a1 = -2f * cos / a0;
            _a2 = (1f - alpha) / a0;
        }

        public void SetLowpass(float cutoffHz, int rate)
        {
            const float q = 0.707f;
            float w0 = 2f * MathF.PI * cutoffHz / rate;
            float cos = MathF.Cos(w0);
            float alpha = MathF.Sin(w0) / (2f * q);
            float a0 = 1f + alpha;

            _b0 = (1f - cos) / 2f / a0;
            _b1 = (1f - cos) / a0;
            _b2 = _b0;
            _a1 = -2f * cos / a0;
            _a2 = (1f - alpha) / a0;
        }

        public void SetHighpass(float cutoffHz, int rate)
        {
            const float q = 0.707f;
            float w0 = 2f * MathF.PI * cutoffHz / rate;
            float cos = MathF.Cos(w0);
            float alpha = MathF.Sin(w0) / (2f * q);
            float a0 = 1f + alpha;

            _b0 = (1f + cos) / 2f / a0;
            _b1 = -(1f + cos) / a0;
            _b2 = _b0;
            _a1 = -2f * cos / a0;
            _a2 = (1f - alpha) / a0;
        }

        public float Process(float x)
        {
            float y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
            _x2 = _x1;
            _x1 = x;
            _y2 = _y1;
            _y1 = y;
            return y;
        }
    }
}
