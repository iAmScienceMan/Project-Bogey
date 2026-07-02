using System;
using System.Collections.Generic;
using System.Numerics;
using Bogey.Logging;
using Bogey.Renderer.Gl;
using Bogey.Renderer.Text;
using Bogey.Renderer.Ui.Controls;
using Bogey.Shared.Console;
using Silk.NET.Input;

namespace Bogey.Renderer.Ui.Console;

public sealed class DevConsole : Control
{
    private const float OpenHeightFraction = 0.45f;
    private const float FontPx = 14f;
    private const float LineHeight = 17f;
    private const float Pad = 8f;
    private const float InputHeight = 24f;
    private const float CaretBlinkSeconds = 0.53f;
    private const int MaxLines = 1000;
    private const string Prompt = "> ";

    private static readonly Rgba Background = new(0.03f, 0.04f, 0.06f, 0.92f);
    private static readonly Rgba InputBackground = new(0.06f, 0.08f, 0.11f, 0.97f);
    private static readonly Rgba BorderColor = new(0.30f, 0.36f, 0.45f, 0.9f);
    private static readonly Rgba CaretColor = new(0.85f, 0.90f, 0.97f);
    private static readonly Rgba PromptColor = new(0.86f, 0.74f, 0.33f);
    private static readonly Rgba NormalColor = new(0.82f, 0.86f, 0.92f);
    private static readonly Rgba CategoryColor = new(0.50f, 0.62f, 0.72f);

    private static readonly Rgba VerboseColor = new(0.55f, 0.58f, 0.62f);
    private static readonly Rgba DebugColor = new(0.60f, 0.72f, 0.62f);
    private static readonly Rgba InfoColor = new(0.44f, 0.72f, 0.92f);
    private static readonly Rgba WarningColor = new(0.92f, 0.78f, 0.30f);
    private static readonly Rgba ErrorColor = new(0.94f, 0.42f, 0.38f);
    private static readonly Rgba FatalColor = new(0.90f, 0.44f, 0.90f);

    private readonly object _gate = new();
    private readonly List<Run[]> _lines = new();
    private readonly List<string> _history = new();
    private readonly ConsoleCommandRegistry _registry;
    private readonly IConsoleShell _shell;

    private string _input = string.Empty;
    private int _caret;
    private int _scroll;
    private int _historyIndex;
    private float _blinkTimer;
    private bool _caretVisible = true;
    private int _lastVisibleRows = 1;
    private int _lastTotalRows;

    public DevConsole(ILogManager logManager, IReadOnlyList<object> services)
    {
        logManager.AddHandler(new ConsoleLogSink(this));
        _shell = new LocalConsoleShell(this);
        _registry = new ConsoleCommandRegistry(logManager.GetLogbook("console"), services);

        WriteLine(LogLevel.Info, "Developer console ready. Type a command and press ENTER. ` to close.");
    }

    public bool IsOpen { get; private set; }

    public void RunCommand(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return;
        }

        AddLine(new[]
        {
            new Run(Prompt, PromptColor),
            new Run(commandLine, NormalColor),
        });

        _registry.Execute(commandLine, _shell);
    }

    public void Toggle()
    {
        IsOpen = !IsOpen;
        _blinkTimer = 0f;
        _caretVisible = true;
        if (IsOpen)
        {
            _scroll = 0;
        }
    }

    public void WriteLine(LogLevel level, string message)
    {
        foreach (string part in message.Split('\n'))
        {
            AddLine(new[]
            {
                new Run("[", NormalColor),
                new Run(Tag(level), ColorFor(level)),
                new Run("] ", NormalColor),
                new Run(part.TrimEnd('\r'), NormalColor),
            });
        }
    }

    public void WriteLog(LogLevel level, string category, string message)
    {
        foreach (string part in message.Split('\n'))
        {
            AddLine(new[]
            {
                new Run("[", NormalColor),
                new Run(Tag(level), ColorFor(level)),
                new Run("] [", NormalColor),
                new Run(category, CategoryColor),
                new Run("] ", NormalColor),
                new Run(part.TrimEnd('\r'), NormalColor),
            });
        }
    }

    public void HandleChar(char c)
    {
        if (!IsOpen || c == '`' || c < 0x20 || c == 0x7F)
        {
            return;
        }

        _input = _input.Insert(_caret, c.ToString());
        _caret++;
        ResetBlink();
    }

    public bool HandleKey(Key key)
    {
        if (!IsOpen)
        {
            return false;
        }

        switch (key)
        {
            case Key.Enter:
            case Key.KeypadEnter:
                Submit();
                return true;
            case Key.Backspace:
                if (_caret > 0)
                {
                    _input = _input.Remove(_caret - 1, 1);
                    _caret--;
                }

                ResetBlink();
                return true;
            case Key.Delete:
                if (_caret < _input.Length)
                {
                    _input = _input.Remove(_caret, 1);
                }

                ResetBlink();
                return true;
            case Key.Left:
                _caret = Math.Max(0, _caret - 1);
                ResetBlink();
                return true;
            case Key.Right:
                _caret = Math.Min(_input.Length, _caret + 1);
                ResetBlink();
                return true;
            case Key.Home:
                _caret = 0;
                ResetBlink();
                return true;
            case Key.End:
                _caret = _input.Length;
                ResetBlink();
                return true;
            case Key.Up:
                RecallHistory(-1);
                return true;
            case Key.Down:
                RecallHistory(1);
                return true;
            case Key.PageUp:
                ScrollBy(_lastVisibleRows - 1);
                return true;
            case Key.PageDown:
                ScrollBy(-(_lastVisibleRows - 1));
                return true;
            case Key.Escape:
                Toggle();
                return true;
            default:
                return true;
        }
    }

    public void HandleScroll(float wheelY) => ScrollBy((int)MathF.Round(wheelY) * 3);

    public override void Arrange(UiRect rect) => Bounds = rect;

    public override void FrameUpdate(float dt)
    {
        if (!IsOpen)
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
        if (!IsOpen)
        {
            return;
        }

        UiRect panel = new(0f, 0f, Bounds.W, Bounds.H * OpenHeightFraction);
        prims.FilledQuad(panel.Min, panel.Max, Background);

        UiRect input = new(panel.X, panel.Bottom - InputHeight, panel.W, InputHeight);
        prims.FilledQuad(input.Min, input.Max, InputBackground);
        prims.Line(new Vector2(input.X, input.Y), new Vector2(input.Right, input.Y), BorderColor);
        prims.Line(new Vector2(panel.X, panel.Bottom), new Vector2(panel.Right, panel.Bottom), BorderColor);

        DrawLog(text, panel, input);
        DrawInput(prims, text, input);
    }

    private void DrawLog(TextBatch text, UiRect panel, UiRect input)
    {
        float charWidth = TextBatch.CharWidth(FontPx);
        float logWidth = panel.W - (Pad * 2f);
        float logHeight = input.Y - panel.Y - (Pad * 2f);
        int maxCols = Math.Max(1, (int)(logWidth / charWidth));
        int visibleRows = Math.Max(1, (int)(logHeight / LineHeight));

        List<List<Fragment>> rows = BuildRows(maxCols);

        _lastTotalRows = rows.Count;
        _lastVisibleRows = visibleRows;

        int maxScroll = Math.Max(0, rows.Count - visibleRows);
        _scroll = Math.Clamp(_scroll, 0, maxScroll);

        int startRow = Math.Max(0, rows.Count - visibleRows - _scroll);
        float logX = panel.X + Pad;
        float y = panel.Y + Pad;

        for (int i = startRow; i < rows.Count && i < startRow + visibleRows; i++)
        {
            foreach (Fragment fragment in rows[i])
            {
                text.Text(new Vector2(logX + (fragment.Col * charWidth), y), FontPx, fragment.Color, fragment.Text);
            }

            y += LineHeight;
        }
    }

    private void DrawInput(PrimitiveBatch prims, TextBatch text, UiRect input)
    {
        float charWidth = TextBatch.CharWidth(FontPx);
        float textY = input.Y + ((InputHeight - FontPx) * 0.5f);

        text.Text(new Vector2(input.X + Pad, textY), FontPx, PromptColor, Prompt);
        text.Text(new Vector2(input.X + Pad + (Prompt.Length * charWidth), textY), FontPx, NormalColor, _input);

        if (_caretVisible)
        {
            float caretX = input.X + Pad + ((Prompt.Length + _caret) * charWidth);
            prims.FilledQuad(
                new Vector2(caretX, textY),
                new Vector2(caretX + 1.5f, textY + FontPx),
                CaretColor);
        }
    }

    private List<List<Fragment>> BuildRows(int maxCols)
    {
        List<List<Fragment>> rows = new();

        lock (_gate)
        {
            foreach (Run[] line in _lines)
            {
                List<Fragment> row = new();
                int col = 0;

                foreach (Run run in line)
                {
                    int i = 0;
                    while (i < run.Text.Length)
                    {
                        if (col >= maxCols)
                        {
                            rows.Add(row);
                            row = new List<Fragment>();
                            col = 0;
                        }

                        int take = Math.Min(maxCols - col, run.Text.Length - i);
                        row.Add(new Fragment(col, run.Text.Substring(i, take), run.Color));
                        col += take;
                        i += take;
                    }
                }

                rows.Add(row);
            }
        }

        return rows;
    }

    private void AddLine(Run[] runs)
    {
        lock (_gate)
        {
            _lines.Add(runs);
            int excess = _lines.Count - MaxLines;
            if (excess > 0)
            {
                _lines.RemoveRange(0, excess);
            }
        }
    }

    private void Submit()
    {
        string text = _input;
        _input = string.Empty;
        _caret = 0;
        _scroll = 0;
        ResetBlink();

        if (text.Trim().Length == 0)
        {
            return;
        }

        if (_history.Count == 0 || !string.Equals(_history[^1], text, StringComparison.Ordinal))
        {
            _history.Add(text);
        }

        _historyIndex = _history.Count;
        RunCommand(text);
    }

    private void RecallHistory(int direction)
    {
        if (_history.Count == 0)
        {
            return;
        }

        _historyIndex = Math.Clamp(_historyIndex + direction, 0, _history.Count);
        _input = _historyIndex == _history.Count ? string.Empty : _history[_historyIndex];
        _caret = _input.Length;
        ResetBlink();
    }

    private void ScrollBy(int rows)
    {
        int maxScroll = Math.Max(0, _lastTotalRows - _lastVisibleRows);
        _scroll = Math.Clamp(_scroll + rows, 0, maxScroll);
    }

    private void ResetBlink()
    {
        _blinkTimer = 0f;
        _caretVisible = true;
    }

    private static string Tag(LogLevel level) => level switch
    {
        LogLevel.Verbose => "VERB",
        LogLevel.Debug => "DEBG",
        LogLevel.Info => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERRO",
        LogLevel.Fatal => "FATL",
        _ => "????",
    };

    private static Rgba ColorFor(LogLevel level) => level switch
    {
        LogLevel.Verbose => VerboseColor,
        LogLevel.Debug => DebugColor,
        LogLevel.Info => InfoColor,
        LogLevel.Warning => WarningColor,
        LogLevel.Error => ErrorColor,
        LogLevel.Fatal => FatalColor,
        _ => NormalColor,
    };

    private readonly record struct Run(string Text, Rgba Color);

    private readonly record struct Fragment(int Col, string Text, Rgba Color);
}
