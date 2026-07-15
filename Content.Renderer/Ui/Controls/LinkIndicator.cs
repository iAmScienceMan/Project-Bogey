using System;
using System.Diagnostics;
using System.Numerics;
using Lattice.Renderer.Gl;
using Lattice.Renderer.Text;
using Lattice.Renderer.Ui;
using Lattice.Renderer.Ui.Controls;

namespace Content.Renderer.Ui.Controls;

public enum LinkState
{
    Searching,
    Lost,
}

public sealed class LinkIndicator : Control
{
    private const int RingCount = 3;
    private const float PingSpeed = 0.5f;

    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly Rgba Lost = new(0.780f, 0.604f, 0.635f);

    public LinkState State { get; set; } = LinkState.Searching;

    public float Size { get; set; } = 64f;

    public override Vector2 Measure() => new(Size, Size);

    public override void Draw(PrimitiveBatch prims, TextBatch text)
    {
        if (!Visible)
        {
            return;
        }

        Vector2 center = new(Bounds.X + (Bounds.W * 0.5f), Bounds.Y + (Bounds.H * 0.5f));
        float radius = (MathF.Min(Bounds.W, Bounds.H) * 0.5f) - 3f;

        DrawBezel(prims, center, radius);

        if (State == LinkState.Searching)
        {
            DrawPings(prims, center, radius);
            prims.FilledCircle(center, 3f, UiTheme.Accent);
        }
        else
        {
            prims.Ring(center, radius * 0.55f, Lost.FadeBy(0.75f));
            DrawCross(prims, center, radius * 0.3f);
        }
    }

    private static void DrawBezel(PrimitiveBatch prims, Vector2 center, float radius)
    {
        prims.Ring(center, radius, UiTheme.Border);

        for (int i = 0; i < 4; i++)
        {
            float angle = i * (MathF.PI * 0.5f);
            Vector2 dir = new(MathF.Cos(angle), MathF.Sin(angle));
            prims.Line(center + (dir * (radius - 5f)), center + (dir * radius), UiTheme.Border);
        }
    }

    private static void DrawPings(PrimitiveBatch prims, Vector2 center, float radius)
    {
        float phase = (float)Clock.Elapsed.TotalSeconds * PingSpeed;

        for (int i = 0; i < RingCount; i++)
        {
            float t = Frac(phase + (i / (float)RingCount));
            float r = t * radius;
            if (r < 1.5f)
            {
                continue;
            }

            prims.Ring(center, r, UiTheme.ActiveBorder.FadeBy((1f - t) * 0.85f));
        }
    }

    private static void DrawCross(PrimitiveBatch prims, Vector2 center, float r)
    {
        prims.Line(new Vector2(center.X - r, center.Y - r), new Vector2(center.X + r, center.Y + r), Lost);
        prims.Line(new Vector2(center.X - r, center.Y + r), new Vector2(center.X + r, center.Y - r), Lost);
    }

    private static float Frac(float value) => value - MathF.Floor(value);
}
