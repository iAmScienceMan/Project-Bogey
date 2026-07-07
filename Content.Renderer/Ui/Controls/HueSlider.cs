using System;
using System.Numerics;
using Content.Shared;
using Lattice.Renderer.Gl;
using Lattice.Renderer.Text;
using Lattice.Renderer.Ui.Controls;

namespace Content.Renderer.Ui.Controls;

public sealed class HueSlider : Control
{
    private const int GradientSteps = 48;

    private float _value;

    public float MinWidth { get; set; } = 220f;

    public float BarHeight { get; set; } = 18f;

    public float Value
    {
        get => _value;
        set => _value = Math.Clamp(value, 0f, 1f);
    }

    public uint ColorRgb => ColorRgbUtil.FromHue(_value);

    public event Action? OnChanged;

    protected override bool IsOpaque => true;

    public override Vector2 Measure() => new(MinWidth, BarHeight + 8f);

    public void SetFromPosition(Vector2 point)
    {
        if (Bounds.W <= 1f)
        {
            return;
        }

        float previous = _value;
        Value = (point.X - Bounds.X) / Bounds.W;
        if (Math.Abs(previous - _value) > 1e-4f)
        {
            OnChanged?.Invoke();
        }
    }

    public override void Draw(PrimitiveBatch prims, TextBatch text)
    {
        if (!Visible)
        {
            return;
        }

        float barTop = Bounds.Y + 4f;
        float barBottom = barTop + BarHeight;
        float stepWidth = Bounds.W / GradientSteps;

        for (int i = 0; i < GradientSteps; i++)
        {
            (float r, float g, float b) = ColorRgbUtil.ToFloats(ColorRgbUtil.FromHue((i + 0.5f) / GradientSteps));
            float x = Bounds.X + (i * stepWidth);
            prims.FilledQuad(new Vector2(x, barTop), new Vector2(x + stepWidth + 0.5f, barBottom), new Rgba(r, g, b));
        }

        float handleX = Bounds.X + (_value * Bounds.W);
        prims.FilledQuad(
            new Vector2(handleX - 2f, Bounds.Y),
            new Vector2(handleX + 2f, barBottom + 4f),
            new Rgba(0.97f, 0.97f, 0.97f));
        prims.FilledQuad(
            new Vector2(handleX - 1f, Bounds.Y + 1f),
            new Vector2(handleX + 1f, barBottom + 3f),
            new Rgba(0.08f, 0.10f, 0.14f));
    }
}
