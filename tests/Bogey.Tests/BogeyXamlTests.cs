using System;
using Bogey.Renderer.Ui.Controls;
using Bogey.Renderer.Ui.Xaml;
using NUnit.Framework;

namespace Bogey.Tests;

[TestFixture]
public sealed class BogeyXamlTests
{
    private const string Sample =
        "<Control xmlns=\"bogey\">" +
        "  <BoxContainer Name=\"Bar\" Orientation=\"Vertical\" Separation=\"5\" Padding=\"4\">" +
        "    <Button Name=\"Go\" Text=\"Go\" ToolTip=\"do it\" Hotkey=\"G\" />" +
        "    <Label Text=\"hi\" />" +
        "  </BoxContainer>" +
        "</Control>";

    [Test]
    public void Parse_BuildsTree_WithTypesPropertiesAndNameScope()
    {
        Control root = BogeyXaml.Parse(Sample, out NameScope scope);

        Assert.That(root, Is.TypeOf<Control>());
        Assert.That(root.Children, Has.Count.EqualTo(1));

        BoxContainer bar = (BoxContainer)root.Children[0];
        Assert.Multiple(() =>
        {
            Assert.That(bar.Name, Is.EqualTo("Bar"));
            Assert.That(bar.Orientation, Is.EqualTo(Orientation.Vertical));
            Assert.That(bar.Separation, Is.EqualTo(5f));
            Assert.That(bar.Padding, Is.EqualTo(4f));
            Assert.That(bar.Children, Has.Count.EqualTo(2));
        });

        Button go = (Button)bar.Children[0];
        Assert.Multiple(() =>
        {
            Assert.That(go.Text, Is.EqualTo("Go"));
            Assert.That(go.Hotkey, Is.EqualTo("G"));
            Assert.That(go.TooltipText, Is.EqualTo("do it (G)"));
            Assert.That(scope.Lookup("Bar"), Is.SameAs(bar));
            Assert.That(scope.Lookup("Go"), Is.SameAs(go));
        });
    }

    [Test]
    public void Parse_UnknownElement_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            BogeyXaml.Parse("<Control xmlns=\"bogey\"><Widget /></Control>", out _));
    }

    [Test]
    public void Parse_UnknownAttribute_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            BogeyXaml.Parse("<Control xmlns=\"bogey\"><Button Bogus=\"1\" /></Control>", out _));
    }
}
