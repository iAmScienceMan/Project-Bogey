using System.Collections.Generic;
using System.Linq;
using Content.Renderer.Ui.Screens;
using Content.Shared.Net;
using Lattice.Renderer.Ui.Controls;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class LobbyScreenTests
{
    private static LobbyStatus Status(RoundPhase phase, params LobbyPlayer[] players) => new()
    {
        ServerName = "Test Server",
        ScenarioName = "Meridian Patrol",
        Phase = phase,
        RoundStartSeconds = 15f,
        Players = players,
    };

    private static Button Button(Control root, string text)
        => root.SelfAndDescendants().OfType<Button>().First(b => b.Text == text);

    private static IEnumerable<Label> Labels(Control root)
        => root.SelfAndDescendants().OfType<Label>();

    [Test]
    public void Update_ShowsServerInfoAndPlayers()
    {
        LobbyScreen screen = new();

        screen.Update(
            Status(
                RoundPhase.Lobby,
                new LobbyPlayer { Username = "alice", ColorRgb = 0xFF0000, Ready = true },
                new LobbyPlayer { Username = "bob", ColorRgb = 0x00FF00 }),
            "bob");

        Assert.Multiple(() =>
        {
            Assert.That(Labels(screen).Any(l => l.Text == "TEST SERVER"), Is.True);
            Assert.That(Labels(screen).Any(l => l.Text == "Scenario: Meridian Patrol"), Is.True);
            Assert.That(Labels(screen).Any(l => l.Text == "alice  [ready]"), Is.True);
            Assert.That(Labels(screen).Any(l => l.Text == "bob (you)"), Is.True);
        });
    }

    [Test]
    public void ReadyButton_TogglesReadyInLobby()
    {
        LobbyScreen screen = new();
        screen.Update(
            Status(RoundPhase.Lobby, new LobbyPlayer { Username = "bob" }),
            "bob");

        bool? readyRequest = null;
        screen.OnReadyToggle += ready => readyRequest = ready;

        Button(screen, "READY UP").Press();
        Assert.That(readyRequest, Is.True);

        screen.Update(
            Status(RoundPhase.Lobby, new LobbyPlayer { Username = "bob", Ready = true }),
            "bob");

        Button(screen, "READY").Press();
        Assert.That(readyRequest, Is.False);
    }

    [Test]
    public void ReadyButton_JoinsWhenRoundRunning()
    {
        LobbyScreen screen = new();
        screen.Update(
            Status(RoundPhase.InRound, new LobbyPlayer { Username = "bob" }),
            "bob");

        bool joined = false;
        screen.OnJoin += () => joined = true;

        Button(screen, "JOIN GAME").Press();
        Assert.That(joined, Is.True);
    }

    [Test]
    public void ApplyColor_ReportsSliderColor()
    {
        LobbyScreen screen = new();
        screen.ColorHue = 0f;

        uint? applied = null;
        screen.OnApplyColor += rgb => applied = rgb;

        Button(screen, "APPLY").Press();
        Assert.That(applied, Is.EqualTo(0xFF0000));
    }

    [Test]
    public void ConnectingScreen_ShowsFailureStates()
    {
        ConnectingScreen screen = new();

        screen.ShowConnecting("10.0.0.1:8712");
        Assert.That(
            screen.SelfAndDescendants().OfType<Label>().Any(l => l.Text == "10.0.0.1:8712"),
            Is.True);

        screen.ShowFailure("The username 'bob' is already present on server!", wasConnected: false);
        Assert.That(
            screen.SelfAndDescendants().OfType<Label>()
                .Any(l => l.Text == "The username 'bob' is already present on server!"),
            Is.True);
    }
}
