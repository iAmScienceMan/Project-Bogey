using System;
using Bogey.Renderer.App;
using Bogey.Renderer.RealTime;
using Bogey.Renderer.Ui.Controls;
using Bogey.Renderer.Ui.Xaml;

namespace Bogey.Renderer.Ui.Screens;

[GenerateTypedNameReferences]
public sealed partial class TacticalHud : Control
{
    private readonly ISimSession _session;
    private readonly bool _debug;

    public TacticalHud(ISimSession session, IDebugOverlay? debugOverlay, Action recenter)
    {
        _session = session;
        _debug = debugOverlay is not null;

        BogeyXaml.Load(this);

        PauseButton.OnPressed += TogglePause;
        NormalButton.OnPressed += () => _session.SetSpeed(SimSpeed.Normal);
        FastButton.OnPressed += () => _session.SetSpeed(SimSpeed.Fast);
        RecenterButton.OnPressed += () => recenter();
        DeclutterButton.OnPressed += () => debugOverlay?.CycleDisplay();

        DebugPanel.Visible = _debug;
    }

    public override void FrameUpdate(float dt)
    {
        base.FrameUpdate(dt);

        
        PauseButton.Active = _session.Speed == SimSpeed.Paused;
        NormalButton.Active = _session.Speed == SimSpeed.Normal;
        FastButton.Active = _session.Speed == SimSpeed.Fast;
        DebugPanel.Visible = _debug;
    }

    private void TogglePause()
        => _session.SetSpeed(_session.Speed == SimSpeed.Paused ? SimSpeed.Normal : SimSpeed.Paused);
}
