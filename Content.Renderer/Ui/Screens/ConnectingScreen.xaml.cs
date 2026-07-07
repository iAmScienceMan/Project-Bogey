using System;
using Content.Renderer.App;
using Lattice.Renderer.Gl;
using Lattice.Renderer.Text;
using Lattice.Renderer.Ui;
using Lattice.Renderer.Ui.Controls;
using Lattice.Renderer.Ui.Xaml;

namespace Content.Renderer.Ui.Screens;

public enum ConnectingPage
{
    Connecting,
    ConnectFailed,
    Disconnected,
}

[GenerateTypedNameReferences]
public sealed partial class ConnectingScreen : Control
{
    private float _phase;
    private float _dots;

    public ConnectingScreen()
    {
        LatticeXaml.Load(this);

        CancelButton.OnPressed += () => OnCancel?.Invoke();
        RetryButton.OnPressed += () => OnRetry?.Invoke();
        ReconnectButton.OnPressed += () => OnRetry?.Invoke();
        BackButton.OnPressed += () => OnCancel?.Invoke();
        DisconnectedBackButton.OnPressed += () => OnCancel?.Invoke();
    }

    public event Action? OnCancel;

    public event Action? OnRetry;

    public void ShowConnecting(string address)
    {
        ConnectingLabel.Text = $"Connecting to {address}...";
        SetPage(ConnectingPage.Connecting);
    }

    public void ShowFailure(string? reason, bool wasConnected)
    {
        string text = string.IsNullOrWhiteSpace(reason) ? "Connection failed." : reason;
        if (wasConnected)
        {
            DisconnectReason.Text = text;
            SetPage(ConnectingPage.Disconnected);
        }
        else
        {
            ConnectFailReason.Text = text;
            SetPage(ConnectingPage.ConnectFailed);
        }
    }

    private void SetPage(ConnectingPage page)
    {
        ConnectingStatus.Visible = page == ConnectingPage.Connecting;
        ConnectFail.Visible = page == ConnectingPage.ConnectFailed;
        Disconnected.Visible = page == ConnectingPage.Disconnected;
    }

    public override void FrameUpdate(float dt)
    {
        base.FrameUpdate(dt);
        _phase += dt * 6f;

        if (ConnectingStatus.Visible)
        {
            _dots += dt;
            int count = 1 + ((int)(_dots * 2f) % 3);
            ConnectStatus.Text = "Establishing connection" + new string('.', count);
        }
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
}
