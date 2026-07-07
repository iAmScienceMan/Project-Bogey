using System;
using System.Collections.Generic;
using Lattice.Renderer.Ui;
using Lattice.Renderer.Gl;
using Lattice.Renderer.Text;
using Lattice.Renderer.Ui.Controls;
using Lattice.Renderer.Ui.Xaml;
using Lattice.Shared.Configuration;
using Content.Shared.Configuration;

namespace Content.Renderer.Ui.Screens;

[GenerateTypedNameReferences]
public sealed partial class OptionsScreen : Control
{
    private static readonly Rgba ErrorColor = new(0.94f, 0.42f, 0.38f);

    private readonly IConfigurationManager _cfg;
    private readonly List<Action> _refresh = new();
    private readonly List<Action> _apply = new();
    private readonly List<CVarDef> _managed = new();
    private readonly List<string> _errors = new();
    private float _phase;

    public OptionsScreen(IConfigurationManager cfg)
    {
        _cfg = cfg;
        LatticeXaml.Load(this);

        BuildBasicPage();
        BuildAdvancedPage();

        BasicTab.OnPressed += () => ShowPage(basic: true);
        AdvancedTab.OnPressed += () => ShowPage(basic: false);
        ApplyButton.OnPressed += Apply;
        ResetButton.OnPressed += Reset;
        BackButton.OnPressed += () => OnBack?.Invoke();

        Refresh();
        ShowPage(basic: true);
    }

    public event Action? OnBack;

    public void Refresh()
    {
        foreach (Action action in _refresh)
        {
            action();
        }
    }

    public override void FrameUpdate(float dt)
    {
        base.FrameUpdate(dt);
        _phase += dt * 6f;
    }

    public override void Draw(PrimitiveBatch prims, TextBatch text)
    {
        if (!Visible)
        {
            return;
        }

        MenuBackground.Draw(prims, Bounds, _phase);
        base.Draw(prims, text);
    }

    private void BuildBasicPage()
    {
        AddTextRow(BasicPage, "UI SCALE", CVars.UiScale);
        AddTextRow(BasicPage, "TACTICAL ZOOM", CVars.RenderZoom);
        AddTextRow(BasicPage, "WINDOW WIDTH", CVars.RenderWidth);
        AddTextRow(BasicPage, "WINDOW HEIGHT", CVars.RenderHeight);
        AddToggleRow(BasicPage, "VSYNC", CVars.RenderVsync);
    }

    private void BuildAdvancedPage()
    {
        AddTextRow(AdvancedPage, "FONT PATH", CVars.RenderFontPath);
    }

    private void AddTextRow(GridContainer page, string label, CVarDef def)
    {
        page.AddChild(MakeLabel(label));
        LineEdit edit = new() { MinWidth = 220f, FontSize = 13f, Numeric = IsNumeric(def.Type) };
        page.AddChild(edit);
        _managed.Add(def);
        _refresh.Add(() => edit.Text = _cfg.GetCVarString(def.Name) ?? string.Empty);
        _apply.Add(() =>
        {
            if (!_cfg.TrySetCVar(def.Name, edit.Text.Trim(), out string? error))
            {
                _errors.Add(error ?? $"{def.Name} is invalid.");
            }
        });
    }

    private void AddToggleRow(GridContainer page, string label, CVarDef<bool> def)
    {
        page.AddChild(MakeLabel(label));
        Button toggle = new() { FontSize = 13f, MinWidth = 220f };
        bool[] state = { _cfg.GetCVar(def) };

        void Render()
        {
            toggle.Active = state[0];
            toggle.Text = state[0] ? "ON" : "OFF";
        }

        toggle.OnPressed += () =>
        {
            state[0] = !state[0];
            Render();
        };

        page.AddChild(toggle);
        _managed.Add(def);
        _refresh.Add(() =>
        {
            state[0] = _cfg.GetCVar(def);
            Render();
        });
        _apply.Add(() => _cfg.SetCVar(def, state[0]));
    }

    private void Apply()
    {
        _errors.Clear();
        foreach (Action action in _apply)
        {
            action();
        }

        if (_errors.Count == 0)
        {
            StatusLabel.Text = "Settings applied.";
            StatusLabel.Color = UiTheme.Subtle;
        }
        else
        {
            StatusLabel.Text = _errors[0];
            StatusLabel.Color = ErrorColor;
        }
    }

    private void Reset()
    {
        foreach (CVarDef def in _managed)
        {
            _cfg.ResetToDefault(def);
        }

        Refresh();
        StatusLabel.Text = "Reset to defaults.";
        StatusLabel.Color = UiTheme.Subtle;
    }

    private void ShowPage(bool basic)
    {
        BasicPage.Visible = basic;
        AdvancedPage.Visible = !basic;
        BasicTab.Active = basic;
        AdvancedTab.Active = !basic;
    }

    private static Label MakeLabel(string text)
        => new() { Text = text, FontSize = 13f, Color = UiTheme.Text };

    private static bool IsNumeric(Type type)
        => type == typeof(int) || type == typeof(long) || type == typeof(float) || type == typeof(double);

}
