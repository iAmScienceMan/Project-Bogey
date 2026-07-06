using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lattice.Logging;
using Lattice.Shared.Configuration;
using YamlDotNet.Serialization;

namespace Lattice.Shared.Changelog;

public sealed class ChangelogManager : IChangelogManager
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    private readonly IConfigurationManager _cfg;
    private readonly ILogbook? _log;
    private readonly List<ChangelogEntry> _entries = new();

    public ChangelogManager(IConfigurationManager cfg, ILogbook? log = null)
    {
        _cfg = cfg;
        _log = log;
    }

    public IReadOnlyList<ChangelogEntry> Entries => _entries;

    public int MaxId { get; private set; }

    public int LastReadId => _cfg.GetCVar(CVars.ChangelogLastReadId);

    public bool HasNewEntries => MaxId > LastReadId;

    public void MarkAllRead() => _cfg.SetCVar(CVars.ChangelogLastReadId, MaxId);

    public void LoadDirectory(string directory)
    {
        _entries.Clear();
        MaxId = 0;

        if (!Directory.Exists(directory))
        {
            _log?.Warning($"Changelog directory not found: {directory}");
            return;
        }

        foreach (string path in Directory.GetFiles(directory, "*.yml").OrderBy(static p => p, StringComparer.Ordinal))
        {
            try
            {
                _entries.AddRange(Parse(File.ReadAllText(path)).Entries);
            }
            catch (Exception ex) when (ex is IOException or YamlDotNet.Core.YamlException)
            {
                _log?.Error($"Failed to read changelog '{path}': {ex.Message}");
            }
        }

        _entries.Sort(static (a, b) => b.Id.CompareTo(a.Id));
        MaxId = _entries.Count > 0 ? _entries.Max(static e => e.Id) : 0;
    }

    public static Changelog Parse(string yaml)
        => Deserializer.Deserialize<Changelog>(yaml) ?? new Changelog();
}
