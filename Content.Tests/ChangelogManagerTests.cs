using System;
using System.IO;
using Lattice.Shared.Changelog;
using Lattice.Shared.Configuration;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class ChangelogManagerTests
{
    private const string SampleYaml =
        "Entries:\n" +
        "- Id: 1\n" +
        "  Author: Alpha\n" +
        "  Time: '2026-01-01T00:00:00.0000000+00:00'\n" +
        "  Changes:\n" +
        "  - Type: Add\n" +
        "    Message: Added a thing.\n" +
        "  - Type: Fix\n" +
        "    Message: Fixed a thing.\n" +
        "- Id: 2\n" +
        "  Author: Bravo\n" +
        "  Time: '2026-01-02T00:00:00.0000000+00:00'\n" +
        "  Changes:\n" +
        "  - Type: Tweak\n" +
        "    Message: Tweaked a thing.\n";

    [Test]
    public void Parse_ReadsEntriesChangesAndTypes()
    {
        Changelog log = ChangelogManager.Parse(SampleYaml);

        Assert.That(log.Entries, Has.Count.EqualTo(2));

        ChangelogEntry first = log.Entries[0];
        Assert.Multiple(() =>
        {
            Assert.That(first.Id, Is.EqualTo(1));
            Assert.That(first.Author, Is.EqualTo("Alpha"));
            Assert.That(first.Changes, Has.Count.EqualTo(2));
            Assert.That(first.Changes[0].Type, Is.EqualTo(ChangeType.Add));
            Assert.That(first.Changes[0].Message, Is.EqualTo("Added a thing."));
            Assert.That(first.Changes[1].Type, Is.EqualTo(ChangeType.Fix));
            Assert.That(log.Entries[1].Changes[0].Type, Is.EqualTo(ChangeType.Tweak));
        });
    }

    [Test]
    public void LoadDirectory_SortsNewestFirst_AndTracksNewEntries()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bogey-changelog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "changelog.yml"), SampleYaml);

            ConfigurationManager cfg = new();
            cfg.RegisterCVars(typeof(CVars));

            ChangelogManager manager = new(cfg);
            manager.LoadDirectory(dir);

            Assert.Multiple(() =>
            {
                Assert.That(manager.Entries[0].Id, Is.EqualTo(2), "newest entry comes first");
                Assert.That(manager.MaxId, Is.EqualTo(2));
                Assert.That(manager.HasNewEntries, Is.True);
            });

            manager.MarkAllRead();

            Assert.Multiple(() =>
            {
                Assert.That(cfg.GetCVar(CVars.ChangelogLastReadId), Is.EqualTo(2));
                Assert.That(manager.HasNewEntries, Is.False);
            });
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public void LoadDirectory_MissingDirectory_IsEmpty()
    {
        ConfigurationManager cfg = new();
        cfg.RegisterCVars(typeof(CVars));

        ChangelogManager manager = new(cfg);
        manager.LoadDirectory(Path.Combine(Path.GetTempPath(), "bogey-missing-" + Guid.NewGuid().ToString("N")));

        Assert.Multiple(() =>
        {
            Assert.That(manager.Entries, Is.Empty);
            Assert.That(manager.HasNewEntries, Is.False);
        });
    }
}
