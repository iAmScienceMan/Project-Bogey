using System;
using System.Globalization;
using System.Text;
using Content.Renderer.App;
using Content.Renderer.Ui.Controls;
using Content.Shared;
using Content.Shared.Net;
using Lattice.Renderer.Gl;
using Lattice.Renderer.Text;
using Lattice.Renderer.Ui;
using Lattice.Renderer.Ui.Controls;
using Lattice.Renderer.Ui.Xaml;

namespace Content.Renderer.Ui.Screens;

[GenerateTypedNameReferences]
public sealed partial class LobbyScreen : Control
{
    private readonly HueSlider _colorSlider = new();
    private float _sinceStatus;
    private LobbyStatus? _status;
    private string _username = string.Empty;
    private string _playerListKey = string.Empty;

    public LobbyScreen()
    {
        LatticeXaml.Load(this);

        ColorRow.AddChild(_colorSlider);

        ReadyButton.OnPressed += PressReady;
        OptionsButton.OnPressed += () => OnOptions?.Invoke();
        LeaveButton.OnPressed += () => OnLeave?.Invoke();
        ApplyColorButton.OnPressed += () => OnApplyColor?.Invoke(_colorSlider.ColorRgb);
    }

    public event Action<bool>? OnReadyToggle;

    public event Action? OnJoin;

    public event Action? OnLeave;

    public event Action? OnOptions;

    public event Action<uint>? OnApplyColor;

    public float ColorHue
    {
        get => _colorSlider.Value;
        set => _colorSlider.Value = value;
    }

    public void Update(LobbyStatus? status, string username)
    {
        if (!ReferenceEquals(status, _status))
        {
            _sinceStatus = 0f;
        }

        _status = status;
        _username = username;
        RefreshStatic();
        RefreshPlayers();
    }

    public override void FrameUpdate(float dt)
    {
        base.FrameUpdate(dt);
        _sinceStatus += dt;
        RefreshCountdown();
    }

    public override void Draw(PrimitiveBatch prims, TextBatch text)
    {
        if (!Visible)
        {
            return;
        }

        MenuBackground.Draw(prims, Bounds);
        base.Draw(prims, text);
    }

    private void PressReady()
    {
        if (_status is null)
        {
            return;
        }

        if (_status.Phase == RoundPhase.InRound)
        {
            OnJoin?.Invoke();
        }
        else
        {
            OnReadyToggle?.Invoke(!IsSelfReady());
        }
    }

    private bool IsSelfReady()
    {
        if (_status is null)
        {
            return false;
        }

        foreach (LobbyPlayer player in _status.Players)
        {
            if (string.Equals(player.Username, _username, StringComparison.OrdinalIgnoreCase))
            {
                return player.Ready;
            }
        }

        return false;
    }

    private void RefreshStatic()
    {
        if (_status is null)
        {
            ServerNameLabel.Text = "CONNECTED";
            ScenarioLabel.Text = "Scenario: -";
            return;
        }

        ServerNameLabel.Text = _status.ServerName.ToUpperInvariant();
        ScenarioLabel.Text = "Scenario: " + _status.ScenarioName;

        if (_status.Phase == RoundPhase.InRound)
        {
            RoundStateLabel.Text = "Round: in progress (tick "
                + _status.RoundTick.ToString(CultureInfo.InvariantCulture) + ")";
            ReadyButton.Text = "JOIN GAME";
            ReadyButton.Active = false;
        }
        else
        {
            RoundStateLabel.Text = "Round: not started";
            bool ready = IsSelfReady();
            ReadyButton.Text = ready ? "READY" : "READY UP";
            ReadyButton.Active = ready;
        }
    }

    private void RefreshCountdown()
    {
        if (_status is null)
        {
            StartTimeLabel.Text = "Waiting for server...";
            return;
        }

        if (_status.Phase == RoundPhase.InRound)
        {
            StartTimeLabel.Text = "Round is in progress.";
            return;
        }

        if (_status.CountdownPaused)
        {
            StartTimeLabel.Text = string.Create(
                CultureInfo.InvariantCulture,
                $"Round start paused at {TimeSpan.FromSeconds(_status.RoundStartSeconds):m\\:ss}");
            return;
        }

        float remaining = MathF.Max(0f, _status.RoundStartSeconds - _sinceStatus);
        TimeSpan span = TimeSpan.FromSeconds(remaining);
        StartTimeLabel.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"Round starts in: {(int)span.TotalMinutes}:{span.Seconds:00}");
    }

    private void RefreshPlayers()
    {
        string key = BuildPlayerListKey();
        if (string.Equals(key, _playerListKey, StringComparison.Ordinal))
        {
            return;
        }

        _playerListKey = key;
        PlayerList.ClearChildren();

        if (_status is null)
        {
            return;
        }

        foreach (LobbyPlayer player in _status.Players)
        {
            (float r, float g, float b) = ColorRgbUtil.ToFloats(player.ColorRgb);
            string suffix = player.InGame ? "  [in game]" : player.Ready ? "  [ready]" : string.Empty;
            string admin = player.IsAdmin ? "  [admin]" : string.Empty;
            string self = string.Equals(player.Username, _username, StringComparison.OrdinalIgnoreCase)
                ? " (you)"
                : string.Empty;

            PlayerList.AddChild(new Label
            {
                Text = player.Username + self + admin + suffix,
                FontSize = 13f,
                Color = new Rgba(r, g, b),
            });
        }
    }

    private string BuildPlayerListKey()
    {
        if (_status is null)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        foreach (LobbyPlayer player in _status.Players)
        {
            builder.Append(player.Username).Append(':')
                .Append(player.ColorRgb).Append(':')
                .Append(player.Ready ? '1' : '0')
                .Append(player.InGame ? 'g' : '-')
                .Append(player.IsAdmin ? 'a' : '-')
                .Append(';');
        }

        return builder.ToString();
    }
}
