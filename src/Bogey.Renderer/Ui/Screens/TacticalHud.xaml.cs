using System;
using System.Globalization;
using Bogey.Renderer.App;
using Bogey.Renderer.RealTime;
using Bogey.Renderer.Ui.Controls;
using Bogey.Renderer.Ui.Xaml;

namespace Bogey.Renderer.Ui.Screens;

[GenerateTypedNameReferences]
public sealed partial class TacticalHud : Control
{
    private readonly ISimSession _session;
    private readonly Action<string> _runCommand;
    private readonly bool _debug;

    public TacticalHud(ISimSession session, IDebugOverlay? debugOverlay, Action recenter, Action<string> runCommand)
    {
        _session = session;
        _runCommand = runCommand;
        _debug = debugOverlay is not null;

        BogeyXaml.Load(this);

        PauseButton.OnPressed += TogglePause;
        NormalButton.OnPressed += () => _runCommand("speed normal");
        FastButton.OnPressed += () => _runCommand("speed fast");
        RecenterButton.OnPressed += () => recenter();
        DeclutterButton.OnPressed += () => _runCommand("declutter");

        DebugPanel.Visible = _debug;
    }

    public string? SelectedUnit { get; set; }

    public Button? HoveredButton
    {
        get => Tooltip.Target;
        set => Tooltip.Target = value;
    }

    public override void FrameUpdate(float dt)
    {
        base.FrameUpdate(dt);

        bool hasPicture = _session.Current is not null;
        WaitingLabel.Visible = !hasPicture;
        StatusReadout.Visible = hasPicture;

        TickLabel.Text = "TICK " + _session.Tick.ToString(CultureInfo.InvariantCulture);
        SpeedLabel.Text = "SPEED " + SpeedLabelText(_session.Speed);
        SelectedLabel.Text = "SELECTED " + (SelectedUnit ?? "--");

        PauseButton.Active = _session.Speed == SimSpeed.Paused;
        NormalButton.Active = _session.Speed == SimSpeed.Normal;
        FastButton.Active = _session.Speed == SimSpeed.Fast;
        DebugPanel.Visible = _debug;
    }

    public override void Arrange(UiRect rect)
    {
        base.Arrange(rect);
        Tooltip.PlaceWithin(rect);
    }

    private void TogglePause()
        => _runCommand(_session.Speed == SimSpeed.Paused ? "speed normal" : "speed paused");

    private static string SpeedLabelText(SimSpeed speed) => speed switch
    {
        SimSpeed.Paused => "PAUSED",
        SimSpeed.Normal => "1x",
        SimSpeed.Fast => "FAST",
        _ => "?",
    };
}
