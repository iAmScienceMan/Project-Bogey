using System;
using System.Linq;
using Content.Shared.Net;
using Lattice.Renderer.Ui.Controls;
using Content.Renderer.Ui.Screens;
using Lattice.Shared.Changelog;
using Lattice.Shared.Configuration;
using Content.Shared.Configuration;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class MainMenuScreenTests
{
    private static ConfigurationManager Config()
    {
        ConfigurationManager cfg = new();
        cfg.RegisterCVars(typeof(CVars));
        cfg.RegisterCVars(typeof(CCVars));
        return cfg;
    }

    private static MainMenuScreen Menu(ConfigurationManager cfg, IChangelogManager? changelog = null)
        => new(cfg, changelog ?? new FakeChangelog());

    private static Button Button(Control root, string text)
        => root.SelfAndDescendants().OfType<Button>().First(b => b.Text == text);

    private static LineEdit EditByPlaceholder(Control root, string placeholder)
        => root.SelfAndDescendants().OfType<LineEdit>().First(e => e.PlaceHolder == placeholder);

    [Test]
    public void Construct_SeedsFieldsFromConfig()
    {
        ConfigurationManager cfg = Config();
        cfg.SetCVar(CCVars.PlayerUsername, "ICEMAN");
        cfg.SetCVar(CCVars.ClientAddress, "10.0.0.5:9000");

        MainMenuScreen screen = Menu(cfg);

        Assert.Multiple(() =>
        {
            Assert.That(EditByPlaceholder(screen, "Bogeyman").Text, Is.EqualTo("ICEMAN"));
            Assert.That(EditByPlaceholder(screen, "ip or ip:port").Text, Is.EqualTo("10.0.0.5:9000"));
        });
    }

    [Test]
    public void Connect_WritesEditedValuesToConfig_AndRaisesEvent()
    {
        ConfigurationManager cfg = Config();
        MainMenuScreen screen = Menu(cfg);

        EditByPlaceholder(screen, "Bogeyman").Text = "  VIPER  ";
        EditByPlaceholder(screen, "ip or ip:port").Text = "192.168.1.4:9100";

        string? host = null;
        int port = 0;
        screen.OnConnect += (h, p) =>
        {
            host = h;
            port = p;
        };

        Button(screen, "CONNECT").Press();

        Assert.Multiple(() =>
        {
            Assert.That(host, Is.EqualTo("192.168.1.4"));
            Assert.That(port, Is.EqualTo(9100));
            Assert.That(cfg.GetCVar(CCVars.PlayerUsername), Is.EqualTo("VIPER"));
            Assert.That(cfg.GetCVar(CCVars.ClientAddress), Is.EqualTo("192.168.1.4:9100"));
        });
    }

    [Test]
    public void Connect_DefaultsPortWhenOmitted()
    {
        ConfigurationManager cfg = Config();
        MainMenuScreen screen = Menu(cfg);

        EditByPlaceholder(screen, "ip or ip:port").Text = "play.example.com";

        int port = 0;
        string? host = null;
        screen.OnConnect += (h, p) =>
        {
            host = h;
            port = p;
        };

        Button(screen, "CONNECT").Press();

        Assert.Multiple(() =>
        {
            Assert.That(host, Is.EqualTo("play.example.com"));
            Assert.That(port, Is.EqualTo(8712));
        });
    }

    [Test]
    public void Connect_RejectsInvalidAddress()
    {
        ConfigurationManager cfg = Config();
        MainMenuScreen screen = Menu(cfg);

        EditByPlaceholder(screen, "ip or ip:port").Text = "host:notaport";

        bool connected = false;
        screen.OnConnect += (_, _) => connected = true;

        Button(screen, "CONNECT").Press();

        Assert.That(connected, Is.False);
    }

    [TestCase("localhost", "localhost", 8712, true)]
    [TestCase("127.0.0.1:9000", "127.0.0.1", 9000, true)]
    [TestCase("", "", 0, false)]
    [TestCase(":9000", "", 0, false)]
    [TestCase("host:0", "", 0, false)]
    [TestCase("host:70000", "", 0, false)]
    public void TryParseAddress_HandlesFormats(string input, string expectedHost, int expectedPort, bool expectedOk)
    {
        bool ok = MainMenuScreen.TryParseAddress(input, out string host, out int port);

        Assert.That(ok, Is.EqualTo(expectedOk));
        if (expectedOk)
        {
            Assert.Multiple(() =>
            {
                Assert.That(host, Is.EqualTo(expectedHost));
                Assert.That(port, Is.EqualTo(expectedPort));
            });
        }
    }

    [Test]
    public void ChangelogButton_ShowsNewIndicator_AndRaisesEvent()
    {
        ConfigurationManager cfg = Config();
        FakeChangelog changelog = new(new[]
        {
            new ChangelogEntry { Id = 3, Author = "A", Time = DateTime.Now },
        }, lastReadId: 0);

        MainMenuScreen screen = Menu(cfg, changelog);

        bool opened = false;
        screen.OnChangelog += () => opened = true;

        Button(screen, "CHANGELOG (NEW)").Press();
        Assert.That(opened, Is.True);

        changelog.MarkAllRead();
        screen.RefreshChangelogButton();
        Assert.That(screen.SelfAndDescendants().OfType<Button>().Any(b => b.Text == "CHANGELOG"), Is.True);
    }

    [Test]
    public void SetServers_PopulatesRows_AndJoinConnectsSelected()
    {
        ConfigurationManager cfg = Config();
        MainMenuScreen screen = Menu(cfg);
        EditByPlaceholder(screen, "Bogeyman").Text = "MAVERICK";

        screen.SetServers(new[]
        {
            new ServerListing { Address = "10.0.0.1:8712", Name = "Alpha", Players = 2, MaxPlayers = 8 },
            new ServerListing { Address = "10.0.0.2:9001", Name = "Bravo", Players = 0, MaxPlayers = 4 },
        });

        string? host = null;
        int port = 0;
        screen.OnConnect += (h, p) =>
        {
            host = h;
            port = p;
        };

        Button(screen, "Bravo").Press();
        Button(screen, "JOIN").Press();

        Assert.Multiple(() =>
        {
            Assert.That(host, Is.EqualTo("10.0.0.2"));
            Assert.That(port, Is.EqualTo(9001));
            Assert.That(cfg.GetCVar(CCVars.PlayerUsername), Is.EqualTo("MAVERICK"));
        });
    }

    [Test]
    public void Join_WithoutSelection_DoesNothing()
    {
        MainMenuScreen screen = Menu(Config());
        screen.SetServers(new[] { new ServerListing { Address = "10.0.0.1:8712", Name = "Alpha", Players = 1, MaxPlayers = 8 } });

        bool connected = false;
        screen.OnConnect += (_, _) => connected = true;

        Button(screen, "JOIN").Press();

        Assert.That(connected, Is.False);
    }

    [Test]
    public void NotFullFilter_HidesFullServers()
    {
        MainMenuScreen screen = Menu(Config());
        screen.SetServers(new[]
        {
            new ServerListing { Address = "10.0.0.1:8712", Name = "Full", Players = 8, MaxPlayers = 8 },
            new ServerListing { Address = "10.0.0.2:8712", Name = "Open", Players = 2, MaxPlayers = 8 },
        });

        Button(screen, "NOT FULL").Press();

        Assert.Multiple(() =>
        {
            Assert.That(screen.SelfAndDescendants().OfType<Button>().Any(b => b.Text == "Open"), Is.True);
            Assert.That(screen.SelfAndDescendants().OfType<Button>().Any(b => b.Text == "Full"), Is.False);
        });
    }

    [Test]
    public void OptionsAndQuit_RaiseEvents()
    {
        MainMenuScreen screen = Menu(Config());

        bool options = false;
        bool quit = false;
        screen.OnOptions += () => options = true;
        screen.OnQuit += () => quit = true;

        Button(screen, "OPTIONS").Press();
        Button(screen, "QUIT").Press();

        Assert.Multiple(() =>
        {
            Assert.That(options, Is.True);
            Assert.That(quit, Is.True);
        });
    }
}
