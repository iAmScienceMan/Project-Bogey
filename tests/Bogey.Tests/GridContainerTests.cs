using System.Numerics;
using Bogey.Renderer.Ui;
using Bogey.Renderer.Ui.Controls;
using NUnit.Framework;

namespace Bogey.Tests;

[TestFixture]
public sealed class GridContainerTests
{
    [Test]
    public void TwoColumns_ArrangesRowMajor_WithColumnAndRowSizing()
    {
        Button a = new() { Text = "A", MinWidth = 100f };
        Button b = new() { Text = "B", MinWidth = 120f };
        Button c = new() { Text = "C", MinWidth = 80f };
        Button d = new() { Text = "D", MinWidth = 60f };

        GridContainer grid = new() { Columns = 2, Separation = 4f, Padding = 5f };
        grid.AddChild(a);
        grid.AddChild(b);
        grid.AddChild(c);
        grid.AddChild(d);

        Vector2 size = grid.Measure();
        grid.Arrange(new UiRect(0f, 0f, size.X, size.Y));

        float rowHeight = a.Measure().Y;
        float col0Width = 100f;

        Assert.Multiple(() =>
        {
            Assert.That(a.Bounds.X, Is.EqualTo(5f).Within(0.5f));
            Assert.That(a.Bounds.Y, Is.EqualTo(5f).Within(0.5f));
            Assert.That(b.Bounds.X, Is.EqualTo(5f + col0Width + 4f).Within(0.5f));
            Assert.That(b.Bounds.Y, Is.EqualTo(5f).Within(0.5f));
            Assert.That(c.Bounds.X, Is.EqualTo(5f).Within(0.5f));
            Assert.That(c.Bounds.Y, Is.EqualTo(5f + rowHeight + 4f).Within(0.5f));
            Assert.That(d.Bounds.X, Is.EqualTo(5f + col0Width + 4f).Within(0.5f));
        });
    }

    [Test]
    public void Measure_AccountsForColumnWidthsSeparationAndPadding()
    {
        Button a = new() { Text = "A", MinWidth = 100f };
        Button b = new() { Text = "B", MinWidth = 120f };

        GridContainer grid = new() { Columns = 2, Separation = 4f, Padding = 5f };
        grid.AddChild(a);
        grid.AddChild(b);

        Vector2 size = grid.Measure();

        Assert.That(size.X, Is.EqualTo(5f + 100f + 4f + 120f + 5f).Within(0.5f));
    }
}
