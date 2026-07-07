using System;
using System.Globalization;
using System.Numerics;
using Lattice.Network;
using Lattice.Renderer.Gl;
using Lattice.Renderer.Text;

namespace Content.Renderer.App;

public sealed class NetGraph
{
    private const int SampleCount = 180;
    private const float PanelWidth = 280f;
    private const float GraphHeight = 56f;
    private const float TextHeight = 34f;
    private const float Margin = 14f;

    private static readonly Rgba PanelColor = new(0.03f, 0.05f, 0.08f, 0.82f);
    private static readonly Rgba InColor = new(0.35f, 0.85f, 0.45f, 0.9f);
    private static readonly Rgba OutColor = new(0.95f, 0.65f, 0.25f, 0.9f);
    private static readonly Rgba RttColor = new(0.95f, 0.9f, 0.35f);
    private static readonly Rgba TextColor = new(0.82f, 0.86f, 0.92f);

    private readonly float[] _inRates = new float[SampleCount];
    private readonly float[] _outRates = new float[SampleCount];
    private readonly float[] _rtts = new float[SampleCount];
    private int _head;

    private NetworkStats _last;
    private bool _hasLast;
    private float _sinceSample;

    public void Update(NetworkStats stats, float dt)
    {
        if (!_hasLast)
        {
            _last = stats;
            _hasLast = true;
            return;
        }

        _sinceSample += dt;
        if (_sinceSample < 1f / 30f)
        {
            return;
        }

        float inRate = (stats.ReceivedBytes - _last.ReceivedBytes) / _sinceSample;
        float outRate = (stats.SentBytes - _last.SentBytes) / _sinceSample;

        _inRates[_head] = MathF.Max(0f, inRate);
        _outRates[_head] = MathF.Max(0f, outRate);
        _rtts[_head] = stats.RttSeconds * 1000f;
        _head = (_head + 1) % SampleCount;

        _last = stats;
        _sinceSample = 0f;
    }

    public void Draw(PrimitiveBatch prims, TextBatch text, Vector2 viewport, NetworkStats stats)
    {
        float panelHeight = GraphHeight + TextHeight;
        Vector2 min = new(viewport.X - PanelWidth - Margin, viewport.Y - panelHeight - Margin);
        Vector2 max = new(viewport.X - Margin, viewport.Y - Margin);
        prims.FilledQuad(min, max, PanelColor);

        float graphBottom = min.Y + GraphHeight;
        float maxRate = 1024f;
        float maxRtt = 50f;
        for (int i = 0; i < SampleCount; i++)
        {
            maxRate = MathF.Max(maxRate, MathF.Max(_inRates[i], _outRates[i]));
            maxRtt = MathF.Max(maxRtt, _rtts[i]);
        }

        float step = PanelWidth / SampleCount;
        for (int i = 0; i < SampleCount; i++)
        {
            int index = (_head + i) % SampleCount;
            float x = min.X + (i * step);

            float inHeight = _inRates[index] / maxRate * (GraphHeight - 4f);
            if (inHeight > 0.5f)
            {
                prims.FilledQuad(
                    new Vector2(x, graphBottom - inHeight),
                    new Vector2(x + MathF.Max(1f, step), graphBottom),
                    InColor.WithAlpha(0.55f));
            }

            float outHeight = _outRates[index] / maxRate * (GraphHeight - 4f);
            if (outHeight > 0.5f)
            {
                prims.FilledQuad(
                    new Vector2(x, graphBottom - outHeight),
                    new Vector2(x + MathF.Max(1f, step * 0.5f), graphBottom),
                    OutColor.WithAlpha(0.75f));
            }

            float rttY = graphBottom - (_rtts[index] / maxRtt * (GraphHeight - 4f));
            prims.FilledQuad(new Vector2(x, rttY - 1f), new Vector2(x + MathF.Max(1f, step), rttY), RttColor.WithAlpha(0.8f));
        }

        float rttMs = stats.RttSeconds * 1000f;
        string header = string.Create(
            CultureInfo.InvariantCulture,
            $"rtt {rttMs:0} ms   resent {stats.ResentMessages}");
        string rates = string.Create(
            CultureInfo.InvariantCulture,
            $"in {LatestRate(_inRates) / 1024f:0.0} KB/s   out {LatestRate(_outRates) / 1024f:0.0} KB/s");

        text.Text(new Vector2(min.X + 6f, graphBottom + 4f), 11f, TextColor, header);
        text.Text(new Vector2(min.X + 6f, graphBottom + 18f), 11f, TextColor, rates);
    }

    private float LatestRate(float[] samples)
        => samples[(_head + SampleCount - 1) % SampleCount];
}
