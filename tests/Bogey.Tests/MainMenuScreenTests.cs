using System;
using System.Collections.Generic;
using System.Linq;
using Bogey.Renderer.Ui.Controls;
using Bogey.Renderer.Ui.Screens;
using Bogey.Shared.Changelog;
using Bogey.Shared.Configuration;
using NUnit.Framework;

namespace Bogey.Tests;

[TestFixture]
public sealed class MainMenuScreenTests
{
    private static ConfigurationManager Config()
    {
        ConfigurationManager cfg = new();
        cfg.RegisterCVars(typeof(CVars));
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
        cfg.SetCVar(CVars.PlayerCallsign, "ICEMAN");
        cfg.SetCVar(CVars.GameSeed, 4242);

        MainMenuScreen screen = Menu(cfg);

        Assert.Multiple(() =>
        {
            Assert.That(EditByPlaceholder(screen, "your callsign").Text, Is.EqualTo("ICEMAN"));
            Assert.That(EditByPlaceholder(screen, "scenario seed").Text, Is.EqualTo("4242"));
        });
    }

    [Test]
    public void Deploy_WritesEditedValuesToConfig_AndRaisesEvent()
    {
        ConfigurationManager cfg = Config();
        MainMenuScreen screen = Menu(cfg);

        EditByPlaceholder(screen, "your callsign").Text = "  VIPER  ";
        EditByPlaceholder(screen, "scenario seed").Text = "88";

        bool deployed = false;
        screen.OnDeploy += () => deployed = true;

        Button(screen, "DEPLOY").Press();

        Assert.Multiple(() =>
        {
            Assert.That(deployed, Is.True);
            Assert.That(cfg.GetCVar(CVars.PlayerCallsign), Is.EqualTo("VIPER"));
            Assert.That(cfg.GetCVar(CVars.GameSeed), Is.EqualTo(88));
        });
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
