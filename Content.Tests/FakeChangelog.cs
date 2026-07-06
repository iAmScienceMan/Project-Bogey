using System.Collections.Generic;
using System.Linq;
using Lattice.Shared.Changelog;

namespace Content.Tests;

internal sealed class FakeChangelog : IChangelogManager
{
    private readonly List<ChangelogEntry> _entries;

    public FakeChangelog(IEnumerable<ChangelogEntry>? entries = null, int lastReadId = 0)
    {
        _entries = (entries ?? Enumerable.Empty<ChangelogEntry>()).ToList();
        _entries.Sort(static (a, b) => b.Id.CompareTo(a.Id));
        LastReadId = lastReadId;
        MaxId = _entries.Count > 0 ? _entries.Max(static e => e.Id) : 0;
    }

    public IReadOnlyList<ChangelogEntry> Entries => _entries;

    public int MaxId { get; }

    public int LastReadId { get; private set; }

    public bool HasNewEntries => MaxId > LastReadId;

    public void MarkAllRead() => LastReadId = MaxId;
}
