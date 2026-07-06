using System;
using System.Collections.Generic;

namespace Lattice.Shared.Changelog;

public sealed class Changelog
{
    public string Name { get; set; } = "Changelog";

    public List<ChangelogEntry> Entries { get; set; } = new();
}

public sealed class ChangelogEntry
{
    public int Id { get; set; }

    public string Author { get; set; } = string.Empty;

    public DateTime Time { get; set; }

    public string? Url { get; set; }

    public List<ChangelogChange> Changes { get; set; } = new();
}

public sealed class ChangelogChange
{
    public ChangeType Type { get; set; }

    public string Message { get; set; } = string.Empty;
}
