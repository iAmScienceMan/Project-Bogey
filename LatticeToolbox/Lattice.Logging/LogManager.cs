using System;
using System.Collections.Generic;

namespace Lattice.Logging;

public sealed class LogManager : ILogManager
{
    private const string RootName = "root";

#if DEBUG
    private const LogLevel DefaultLevel = LogLevel.Debug;
#else
    private const LogLevel DefaultLevel = LogLevel.Info;
#endif

    private readonly Dictionary<string, Logbook> _logbooks = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly Logbook _root;

    public LogManager()
    {
        _root = new Logbook(RootName, parent: null)
        {
            Level = DefaultLevel,
        };
        _logbooks[RootName] = _root;
    }

    public ILogbook RootLogbook => _root;

    public ILogbook GetLogbook(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Logbook name must not be empty.", nameof(name));
        }

        lock (_gate)
        {
            return GetOrCreate(name);
        }
    }

    public void AddHandler(ILogHandler handler) => _root.AddHandler(handler);

    private Logbook GetOrCreate(string name)
    {
        if (_logbooks.TryGetValue(name, out Logbook? existing))
        {
            return existing;
        }

        int split = name.LastIndexOf('.');
        Logbook parent = split < 0 ? _root : GetOrCreate(name[..split]);

        Logbook logbook = new(name, parent);
        _logbooks[name] = logbook;
        return logbook;
    }
}
