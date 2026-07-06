using System.Numerics;
using Lattice.Renderer.Ui;
using Lattice.Renderer.Ui.Controls;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class ControlLayoutTests
{
    private static (BoxContainer Bar, Button A, Button B) TwoButtonBar()
    {
        Button a = new() { Text = "A" };
        Button b = new() { Text = "B" };
        BoxContainer bar = new() { Orientation = Orientation.Horizontal, Separation = 4f, Padding = 5f };
        bar.AddChild(a);
        bar.AddChild(b);
        return (bar, a, b);
    }

    [Test]
    public void BoxContainer_StacksChildrenWithPaddingAndSeparation()
    {
        (BoxContainer bar, Button a, Button b) = TwoButtonBar();

        Vector2 size = bar.Measure();
        bar.Arrange(new UiRect(10f, 20f, size.X, size.Y));

        
        Assert.Multiple(() =>
        {
            Assert.That(a.Bounds.X, Is.EqualTo(15f).Within(0.5f));
            Assert.That(a.Bounds.Y, Is.EqualTo(25f).Within(0.5f));
            Assert.That(b.Bounds.X, Is.EqualTo(15f + a.Bounds.W + 4f).Within(0.5f));
        });
    }

    [Test]
    public void Button_HitTestsAndFires()
    {
        (BoxContainer bar, Button a, _) = TwoButtonBar();
        bool fired = false;
        a.OnPressed += () => fired = true;

        Vector2 size = bar.Measure();
        bar.Arrange(new UiRect(0f, 0f, size.X, size.Y));
        Vector2 center = new(a.Bounds.X + (a.Bounds.W * 0.5f), a.Bounds.Y + (a.Bounds.H * 0.5f));

        Assert.That(bar.HitTest(center), Is.SameAs(a));
        a.Press();
        Assert.That(fired, Is.True);
    }

    [Test]
    public void HiddenControl_IsNotHitTested()
    {
        (BoxContainer bar, Button a, _) = TwoButtonBar();
        Vector2 size = bar.Measure();
        bar.Arrange(new UiRect(0f, 0f, size.X, size.Y));
        Vector2 center = new(a.Bounds.X + (a.Bounds.W * 0.5f), a.Bounds.Y + (a.Bounds.H * 0.5f));

        a.Visible = false;

        Assert.Multiple(() =>
        {
            Assert.That(bar.HitTest(center), Is.Null);
            Assert.That(bar.HitTestOpaque(center), Is.Not.SameAs(a));
        });
    }

    [Test]
    public void DisabledButton_DoesNotFire_ButStillSwallowsClicks()
    {
        Button disabled = new() { Text = "X", Disabled = true };
        BoxContainer bar = new() { Padding = 5f };
        bar.AddChild(disabled);
        bool fired = false;
        disabled.OnPressed += () => fired = true;

        Vector2 size = bar.Measure();
        bar.Arrange(new UiRect(0f, 0f, size.X, size.Y));
        Vector2 center = new(disabled.Bounds.X + (disabled.Bounds.W * 0.5f), disabled.Bounds.Y + (disabled.Bounds.H * 0.5f));

        disabled.Press();

        Assert.Multiple(() =>
        {
            Assert.That(fired, Is.False, "a disabled button ignores presses.");
            Assert.That(bar.HitTest(center), Is.Null, "a disabled button isn't a click target.");
            Assert.That(bar.HitTestOpaque(center), Is.Not.Null, "but the panel still swallows the click.");
        });
    }
}
