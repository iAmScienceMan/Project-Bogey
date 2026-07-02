using System;
using System.Linq;
using Bogey.Renderer.Ui.Controls;
using Bogey.Renderer.Ui.Screens;
using Bogey.Shared.Changelog;
using NUnit.Framework;

namespace Bogey.Tests;

[TestFixture]
public sealed class ChangelogScreenTests
{
    private static ChangelogEntry Entry(int id, string author, ChangeType type, string message)
        => new()
        {
            Id = id,
            Author = author,
            Time = DateTime.Now,
            Changes = { new ChangelogChange { Type = type, Message = message } },
        };

    private static string[] LabelTexts(Control root)
        => root.SelfAndDescendants().OfType<Label>().Select(l => l.Text).ToArray();

    [Test]
    public void Populate_RendersMessages_AndNewSinceDivider()
    {
        FakeChangelog changelog = new(new[]
        {
            Entry(2, "Bravo", ChangeType.Add, "a brand new thing"),
            Entry(1, "Alpha", ChangeType.Fix, "an older fix"),
        }, lastReadId: 1);

        ChangelogScreen screen = new();
        screen.Populate(changelog);

        string[] texts = LabelTexts(screen);

        Assert.Multiple(() =>
        {
            Assert.That(texts, Has.Some.Contains("a brand new thing"));
            Assert.That(texts, Has.Some.Contains("an older fix"));
            Assert.That(texts, Has.Some.Contains("new since"));
        });
    }

    [Test]
    public void Populate_CollapsesRepeatedAuthorHeaders()
    {
        FakeChangelog changelog = new(new[]
        {
            Entry(3, "Alpha", ChangeType.Add, "third"),
            Entry(2, "Alpha", ChangeType.Add, "second"),
            Entry(1, "Alpha", ChangeType.Add, "first"),
        }, lastReadId: 3);

        ChangelogScreen screen = new();
        screen.Populate(changelog);

        int headers = LabelTexts(screen).Count(t => t.Contains("ALPHA changed:"));

        Assert.That(headers, Is.EqualTo(1));
    }

    [Test]
    public void Populate_Empty_ShowsPlaceholder()
    {
        ChangelogScreen screen = new();
        screen.Populate(new FakeChangelog());

        Assert.That(LabelTexts(screen), Has.Some.Contains("No changelog entries"));
    }

    [Test]
    public void Back_RaisesEvent()
    {
        ChangelogScreen screen = new();
        bool back = false;
        screen.OnBack += () => back = true;

        screen.SelfAndDescendants().OfType<Button>().First(b => b.Text == "BACK").Press();

        Assert.That(back, Is.True);
    }
}
