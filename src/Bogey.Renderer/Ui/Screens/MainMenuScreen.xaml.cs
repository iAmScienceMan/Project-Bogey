using System;
using System.Collections.Generic;
using System.Globalization;
using Bogey.Renderer.App;
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
    private readonly IReadOnlyList<ScenarioInfo> _scenarios;
    private int _scenarioIndex;
    private float _phase;

    public MainMenuScreen(IConfigurationManager cfg, IChangelogManager changelog, IReadOnlyList<ScenarioInfo> scenarios)
    {
        _cfg = cfg;
        _changelog = changelog;
        _scenarios = scenarios;
        BogeyXaml.Load(this);

        CallsignEdit.Text = cfg.GetCVar(CVars.PlayerCallsign);
        SeedEdit.Text = cfg.GetCVar(CVars.GameSeed).ToString(CultureInfo.InvariantCulture);

        _scenarioIndex = FindScenarioIndex(cfg.GetCVar(CVars.GameScenario));
        RefreshScenarioButton();

        DeployButton.OnPressed += Deploy;
        ScenarioButton.OnPressed += CycleScenario;
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

    private int FindScenarioIndex(string id)
    {
        for (int i = 0; i < _scenarios.Count; i++)
        {
            if (string.Equals(_scenarios[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return 0;
    }

    private void CycleScenario()
    {
        if (_scenarios.Count == 0)
        {
            return;
        }

        _scenarioIndex = (_scenarioIndex + 1) % _scenarios.Count;
        _cfg.SetCVar(CVars.GameScenario, _scenarios[_scenarioIndex].Id);
        RefreshScenarioButton();
    }

    private void RefreshScenarioButton()
        => ScenarioButton.Text = _scenarios.Count == 0 ? "-" : _scenarios[_scenarioIndex].Name;

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
