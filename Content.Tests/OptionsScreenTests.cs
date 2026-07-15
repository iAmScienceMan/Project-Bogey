using System.Collections.Generic;
using System.Linq;
using Lattice.Renderer.Ui.Controls;
using Content.Renderer.Ui.Screens;
using Lattice.Shared.Configuration;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class OptionsScreenTests
{
    private static ConfigurationManager Config()
    {
        ConfigurationManager cfg = new();
        cfg.RegisterCVars(typeof(CVars));
        return cfg;
    }

    private static Button Button(Control root, string text)
        => root.SelfAndDescendants().OfType<Button>().First(b => b.Text == text);

    private static IReadOnlyList<GridContainer> Pages(Control root)
        => root.SelfAndDescendants().OfType<GridContainer>().ToList();

    private static Control WidgetAfterLabel(GridContainer page, string labelText)
    {
        IReadOnlyList<Control> children = page.Children;
        for (int i = 0; i < children.Count - 1; i++)
        {
            if (children[i] is Label label && label.Text == labelText)
            {
                return children[i + 1];
            }
        }

        throw new AssertionException($"No options row labelled '{labelText}'.");
    }

    [Test]
    public void Tabs_TogglePageVisibility()
    {
        OptionsScreen screen = new(Config());
        IReadOnlyList<GridContainer> pages = Pages(screen);
        GridContainer basic = pages[0];
        GridContainer advanced = pages[1];

        Assert.Multiple(() =>
        {
            Assert.That(basic.Visible, Is.True);
            Assert.That(advanced.Visible, Is.False);
        });

        Button(screen, "ADVANCED").Press();

        Assert.Multiple(() =>
        {
            Assert.That(basic.Visible, Is.False);
            Assert.That(advanced.Visible, Is.True);
        });

        Button(screen, "BASIC").Press();

        Assert.Multiple(() =>
        {
            Assert.That(basic.Visible, Is.True);
            Assert.That(advanced.Visible, Is.False);
        });
    }

    [Test]
    public void EditingField_AndApply_WritesParsedValueToConfig()
    {
        ConfigurationManager cfg = Config();
        OptionsScreen screen = new(cfg);

        LineEdit scale = (LineEdit)WidgetAfterLabel(Pages(screen)[0], "UI SCALE");
        scale.Text = "1.5";

        Button(screen, "APPLY").Press();

        Assert.That(cfg.GetCVar(CVars.UiScale), Is.EqualTo(1.5f));
    }

    [Test]
    public void Reset_RestoresDefaults_AndRefreshesFields()
    {
        ConfigurationManager cfg = Config();
        OptionsScreen screen = new(cfg);
        cfg.SetCVar(CVars.UiScale, 2.0f);

        Button(screen, "RESET").Press();

        LineEdit scale = (LineEdit)WidgetAfterLabel(Pages(screen)[0], "UI SCALE");

        Assert.Multiple(() =>
        {
            Assert.That(cfg.GetCVar(CVars.UiScale), Is.EqualTo(1f));
            Assert.That(scale.Text, Is.EqualTo("1"));
        });
    }
}
