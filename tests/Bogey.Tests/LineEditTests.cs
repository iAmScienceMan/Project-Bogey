using System.Numerics;
using Bogey.Renderer.Ui;
using Bogey.Renderer.Ui.Controls;
using NUnit.Framework;
using Silk.NET.Input;

namespace Bogey.Tests;

[TestFixture]
public sealed class LineEditTests
{
    [Test]
    public void Insert_AppendsAndAdvancesCaret_AndRaisesChanged()
    {
        LineEdit edit = new();
        string? last = null;
        edit.OnTextChanged += t => last = t;

        edit.Insert('h');
        edit.Insert('i');

        Assert.Multiple(() =>
        {
            Assert.That(edit.Text, Is.EqualTo("hi"));
            Assert.That(last, Is.EqualTo("hi"));
        });
    }

    [Test]
    public void Backspace_RemovesCharacterBeforeCaret()
    {
        LineEdit edit = new();
        edit.Insert('a');
        edit.Insert('b');

        edit.HandleKey(Key.Backspace);

        Assert.That(edit.Text, Is.EqualTo("a"));
    }

    [Test]
    public void Numeric_RejectsLetters_AcceptsDigitsAndSigns()
    {
        LineEdit edit = new() { Numeric = true };

        edit.Insert('4');
        edit.Insert('a');
        edit.Insert('.');
        edit.Insert('2');

        Assert.That(edit.Text, Is.EqualTo("4.2"));
    }

    [Test]
    public void Submit_RaisesOnSubmit()
    {
        LineEdit edit = new();
        bool submitted = false;
        edit.OnSubmit += () => submitted = true;

        edit.HandleKey(Key.Enter);

        Assert.That(submitted, Is.True);
    }

    [Test]
    public void HitTestFocusable_ReturnsSelf_WhenPointInside()
    {
        LineEdit edit = new() { MinWidth = 100f };
        Vector2 size = edit.Measure();
        edit.Arrange(new UiRect(10f, 10f, size.X, size.Y));

        Vector2 inside = new(edit.Bounds.X + 5f, edit.Bounds.Y + 5f);
        Vector2 outside = new(edit.Bounds.Right + 50f, edit.Bounds.Y + 5f);

        Assert.Multiple(() =>
        {
            Assert.That(edit.HitTestFocusable(inside), Is.SameAs(edit));
            Assert.That(edit.HitTestFocusable(outside), Is.Null);
        });
    }

    [Test]
    public void Focus_SetsFocusedFlag()
    {
        LineEdit edit = new();
        Assert.That(edit.IsFocused, Is.False);

        edit.Focus();
        Assert.That(edit.IsFocused, Is.True);

        edit.Blur();
        Assert.That(edit.IsFocused, Is.False);
    }
}
