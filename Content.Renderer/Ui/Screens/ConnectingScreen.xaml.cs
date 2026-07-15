using System;
using System.Numerics;
using Content.Renderer.Ui.Controls;
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
    private readonly LinkIndicator _indicator = new() { Size = 64f };
    private float _dots;

    public ConnectingScreen()
    {
        LatticeXaml.Load(this);

        IndicatorSlot.AddChild(_indicator);

        CancelButton.OnPressed += () => OnCancel?.Invoke();
        RetryButton.OnPressed += () => OnRetry?.Invoke();
        BackButton.OnPressed += () => OnCancel?.Invoke();
    }

    public event Action? OnCancel;

    public event Action? OnRetry;

    public void ShowConnecting(string address)
    {
        AddressLabel.Text = address;
        SetPage(ConnectingPage.Connecting);
    }

    public void ShowFailure(string? reason, bool wasConnected)
    {
        StatusLabel.Text = string.IsNullOrWhiteSpace(reason) ? "Connection failed." : reason;
        SetPage(wasConnected ? ConnectingPage.Disconnected : ConnectingPage.ConnectFailed);
    }

    private void SetPage(ConnectingPage page)
    {
        bool searching = page == ConnectingPage.Connecting;

        _indicator.State = searching ? LinkState.Searching : LinkState.Lost;
        SearchingButtons.Visible = searching;
        LostButtons.Visible = !searching;

        TitleLabel.Text = page switch
        {
            ConnectingPage.Connecting => "ESTABLISHING LINK",
            ConnectingPage.ConnectFailed => "LINK FAILED",
            _ => "LINK LOST",
        };
    }

    public override void FrameUpdate(float dt)
    {
        base.FrameUpdate(dt);

        if (SearchingButtons.Visible)
        {
            _dots += dt;
            int count = 1 + ((int)(_dots * 2f) % 3);
            StatusLabel.Text = "Establishing connection" + new string('.', count);
        }
    }

    public override Vector2 Measure() => Root.Measure();

    public override void Arrange(UiRect rect)
    {
        Bounds = rect;
        Root.Arrange(rect);
    }
}
