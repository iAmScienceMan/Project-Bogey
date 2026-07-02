using System;
using System.Globalization;
using Bogey.Renderer.Gl;
using Bogey.Renderer.Text;
using Bogey.Renderer.Ui.Controls;
using Bogey.Renderer.Ui.Xaml;
using Bogey.Shared.Changelog;
using Bogey.Shared.Configuration;

namespace Bogey.Renderer.Ui.Screens;

[GenerateTypedNameReferences]
public sealed partial class MainMenuScreen : Control
{
    private readonly IConfigurationManager _cfg;
    private readonly IChangelogManager _changelog;
    private float _phase;

    public MainMenuScreen(IConfigurationManager cfg, IChangelogManager changelog)
    {
        _cfg = cfg;
        _changelog = changelog;
        BogeyXaml.Load(this);

        CallsignEdit.Text = cfg.GetCVar(CVars.PlayerCallsign);
        SeedEdit.Text = cfg.GetCVar(CVars.GameSeed).ToString(CultureInfo.InvariantCulture);

        DeployButton.OnPressed += Deploy;
        CallsignEdit.OnSubmit += Deploy;
        SeedEdit.OnSubmit += Deploy;
        ChangelogButton.OnPressed += () => OnChangelog?.Invoke();
        OptionsButton.OnPressed += () => OnOptions?.Invoke();
        QuitButton.OnPressed += () => OnQuit?.Invoke();

        RefreshChangelogButton();
    }

    public event Action? OnDeploy;

    public event Action? OnChangelog;

    public event Action? OnOptions;

    public event Action? OnQuit;

    public void RefreshChangelogButton()
        => ChangelogButton.Text = _changelog.HasNewEntries ? "CHANGELOG (NEW)" : "CHANGELOG";

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

    private void Deploy()
    {
        string callsign = CallsignEdit.Text.Trim();
        _cfg.SetCVar(CVars.PlayerCallsign, callsign.Length == 0 ? CVars.PlayerCallsign.DefaultValue : callsign);

        if (int.TryParse(SeedEdit.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
        {
            _cfg.SetCVar(CVars.GameSeed, seed);
        }

        OnDeploy?.Invoke();
    }
}
