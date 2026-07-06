using System;
using System.Numerics;
using Lattice.Renderer.Gl;
using Lattice.Renderer.Text;
using Silk.NET.Input;

namespace Lattice.Renderer.Ui.Controls;

public sealed class LineEdit : Control
{
    private const float HorizontalPadding = 8f;
    private const float VerticalPadding = 5f;
    private const float CaretBlinkSeconds = 0.53f;
    private const float CaretWidth = 1.5f;

    private int _caret;
    private float _blinkTimer;
    private bool _caretVisible = true;

    public string Text { get; set; } = string.Empty;

    public string? PlaceHolder { get; set; }

    public float FontSize { get; set; } = UiTheme.FontPx;

    public float MinWidth { get; set; } = 120f;

    public bool Numeric { get; set; }

    public bool IsFocused { get; private set; }

    public event Action<string>? OnTextChanged;

    public event Action? OnSubmit;

    protected override bool IsOpaque => true;

    public void Focus()
    {
        IsFocused = true;
        _caret = Text.Length;
        ResetBlink();
    }

    public void Blur() => IsFocused = false;

    public override Control? HitTestFocusable(Vector2 point)
        => Visible && Bounds.Contains(point) ? this : null;

    public void Insert(char c)
    {
        if (c < 0x20 || c == 0x7F)
        {
            return;
        }

        if (Numeric && !char.IsDigit(c) && c != '-' && c != '.')
        {
            return;
        }

        Text = Text.Insert(_caret, c.ToString());
        _caret++;
        ResetBlink();
        OnTextChanged?.Invoke(Text);
    }

    public void HandleKey(Key key)
    {
        switch (key)
        {
            case Key.Enter:
            case Key.KeypadEnter:
                OnSubmit?.Invoke();
                break;
            case Key.Backspace:
                if (_caret > 0)
                {
                    Text = Text.Remove(_caret - 1, 1);
                    _caret--;
                    OnTextChanged?.Invoke(Text);
                }

                break;
            case Key.Delete:
                if (_caret < Text.Length)
                {
                    Text = Text.Remove(_caret, 1);
                    OnTextChanged?.Invoke(Text);
                }

                break;
            case Key.Left:
                _caret = Math.Max(0, _caret - 1);
                break;
            case Key.Right:
                _caret = Math.Min(Text.Length, _caret + 1);
                break;
            case Key.Home:
                _caret = 0;
                break;
            case Key.End:
                _caret = Text.Length;
                break;
        }

        ResetBlink();
    }

    public override Vector2 Measure()
    {
        float content = TextBatch.Measure(Text, FontSize) + (HorizontalPadding * 2f);
        float width = MathF.Max(MinWidth, content);
        return new Vector2(width, FontSize + (VerticalPadding * 2f));
    }

    public override void FrameUpdate(float dt)
    {
        base.FrameUpdate(dt);

        if (!IsFocused)
        {
            return;
        }

        _blinkTimer += dt;
        if (_blinkTimer >= CaretBlinkSeconds)
        {
            _blinkTimer -= CaretBlinkSeconds;
            _caretVisible = !_caretVisible;
        }
    }

    public override void Draw(PrimitiveBatch prims, TextBatch text)
    {
        if (!Visible)
        {
            return;
        }

        prims.FilledQuad(Bounds.Min, Bounds.Max, UiTheme.IconSlot);
        UiDraw.Box(prims, Bounds, IsFocused ? UiTheme.ActiveBorder : UiTheme.Border);

        float textX = Bounds.X + HorizontalPadding;
        float textY = Bounds.Y + ((Bounds.H - FontSize) * 0.5f);

        if (Text.Length > 0)
        {
            text.Text(new Vector2(textX, textY), FontSize, UiTheme.Text, Text);
        }
        else if (!string.IsNullOrEmpty(PlaceHolder))
        {
            text.Text(new Vector2(textX, textY), FontSize, UiTheme.HotkeyText, PlaceHolder);
        }

        if (IsFocused && _caretVisible)
        {
            _caret = Math.Clamp(_caret, 0, Text.Length);
            float caretX = textX + TextBatch.Measure(Text[.._caret], FontSize);
            prims.FilledQuad(
                new Vector2(caretX, textY),
                new Vector2(caretX + CaretWidth, textY + FontSize),
                UiTheme.Text);
        }
    }

    private void ResetBlink()
    {
        _blinkTimer = 0f;
        _caretVisible = true;
    }
}
