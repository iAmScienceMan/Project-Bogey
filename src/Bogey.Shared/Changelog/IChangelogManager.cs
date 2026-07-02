using System.Collections.Generic;

namespace Bogey.Shared.Changelog;

public interface IChangelogManager
{
    IReadOnlyList<ChangelogEntry> Entries { get; }

    int MaxId { get; }

    int LastReadId { get; }

    bool HasNewEntries { get; }

    void MarkAllRead();
}
