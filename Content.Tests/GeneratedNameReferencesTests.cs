using System.Numerics;
using Lattice.Renderer.Ui;
using Lattice.Renderer.Ui.Controls;
using Content.Tests.Fixtures;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class GeneratedNameReferencesTests
{
    [Test]
    public void Load_PopulatesGeneratedNameFields()
    {
        SampleScreen screen = new();

        Assert.Multiple(() =>
        {
            Assert.That(screen.BarPanel, Is.Not.Null.And.TypeOf<BoxContainer>());
            Assert.That(screen.Ok, Is.Not.Null.And.TypeOf<Button>());
            Assert.That(screen.CaptionLabel, Is.Not.Null.And.TypeOf<Label>());
            Assert.That(screen.Ok.Text, Is.EqualTo("OK"));
            Assert.That(screen.Ok.TooltipText, Is.EqualTo("Confirm (Enter)"));
        });
    }

    [Test]
    public void ClickingAGeneratedButton_FiresCodeBehindHandler()
    {
        SampleScreen screen = new();
        screen.Arrange(new UiRect(0f, 0f, 400f, 200f));

        Vector2 center = new(
            screen.Ok.Bounds.X + (screen.Ok.Bounds.W * 0.5f),
            screen.Ok.Bounds.Y + (screen.Ok.Bounds.H * 0.5f));

        Assert.That(screen.HitTest(center), Is.SameAs(screen.Ok), "the named button should be hit-testable.");

        screen.Ok.Press();
        Assert.That(screen.Clicked, Is.True, "OnPressed wired in the code-behind should fire.");
    }
}
