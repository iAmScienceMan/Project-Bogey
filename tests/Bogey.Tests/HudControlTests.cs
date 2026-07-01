using System.Numerics;
using Bogey.Renderer.Gl;
using Bogey.Renderer.Ui;
using Bogey.Renderer.Ui.Controls;
using Bogey.Renderer.Ui.Xaml;
using NUnit.Framework;

namespace Bogey.Tests;

[TestFixture]
public sealed class HudControlTests
{
    [Test]
    public void Rgba_Parse_ReadsHexWithAndWithoutAlpha()
    {
        Rgba opaque = Rgba.Parse("#CCDBEB");
        Rgba translucent = Rgba.Parse("#CCDBEBBF");

        Assert.Multiple(() =>
        {
            Assert.That(opaque.R, Is.EqualTo(204f / 255f).Within(0.001f));
            Assert.That(opaque.G, Is.EqualTo(219f / 255f).Within(0.001f));
            Assert.That(opaque.B, Is.EqualTo(235f / 255f).Within(0.001f));
            Assert.That(opaque.A, Is.EqualTo(1f));
            Assert.That(translucent.A, Is.EqualTo(191f / 255f).Within(0.001f));
        });
    }

    [Test]
    public void Label_ParsesFontSizeAndColorFromMarkup()
    {
        const string markup =
            "<Control xmlns=\"bogey\">" +
            "  <Label Name=\"Readout\" FontSize=\"15\" Color=\"#CCDBEB\" Text=\"TICK 0\" />" +
            "</Control>";

        Control root = BogeyXaml.Parse(markup, out NameScope scope);
        Label readout = (Label)scope.Lookup("Readout");

        Assert.Multiple(() =>
        {
            Assert.That(readout.FontSize, Is.EqualTo(15f));
            Assert.That(readout.Color, Is.EqualTo(Rgba.Parse("#CCDBEB")));
            Assert.That(readout.Measure().Y, Is.EqualTo(15f));
        });
    }

    [Test]
    public void TransparentBoxContainer_DoesNotSwallowClicks()
    {
        BoxContainer readout = new() { Orientation = Orientation.Vertical, DrawBackground = false };
        readout.AddChild(new Label { Text = "TICK 0", FontSize = 15f });

        Vector2 size = readout.Measure();
        readout.Arrange(new UiRect(0f, 0f, size.X, size.Y));
        Vector2 inside = new(readout.Bounds.X + 1f, readout.Bounds.Y + 1f);

        Assert.That(readout.HitTestOpaque(inside), Is.Null);
    }

    [Test]
    public void Tooltip_AnchorsBelowItsTargetAndClampsToScreen()
    {
        Button target = new() { ToolTip = "Pause", Hotkey = "Space" };
        target.Arrange(new UiRect(100f, 40f, 30f, 30f));

        Tooltip tooltip = new() { Target = target };
        tooltip.PlaceWithin(new UiRect(0f, 0f, 800f, 600f));

        Assert.Multiple(() =>
        {
            Assert.That(tooltip.Bounds.Y, Is.GreaterThan(target.Bounds.Bottom));
            Assert.That(tooltip.Bounds.Right, Is.LessThanOrEqualTo(800f));
            Assert.That(tooltip.Bounds.W, Is.GreaterThan(0f));
        });
    }

    [Test]
    public void Tooltip_WithoutTarget_HasNoBounds()
    {
        Tooltip tooltip = new();
        tooltip.PlaceWithin(new UiRect(0f, 0f, 800f, 600f));

        Assert.That(tooltip.Bounds, Is.EqualTo(default(UiRect)));
    }
}
