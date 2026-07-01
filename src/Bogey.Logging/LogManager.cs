using System;
using System.Collections.Generic;

namespace Bogey.Logging;

public sealed class LogManager : ILogManager
{
    private const string RootName = "root";

#if DEBUG
    private const LogLevel DefaultLevel = LogLevel.Debug;
#else
    private const LogLevel DefaultLevel = LogLevel.Info;
#endif

    private readonly Dictionary<string, Sawmill> _sawmills = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private readonly Sawmill _root;

    public LogManager()
    {
        _root = new Sawmill(RootName, parent: null)
        {
            Level = DefaultLevel,
        };
        _sawmills[RootName] = _root;
    }

    public ISawmill RootSawmill => _root;

    public ISawmill GetSawmill(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Sawmill name must not be empty.", nameof(name));
        }

        lock (_gate)
        {
            return GetOrCreate(name);
        }
    }

    public void AddHandler(ILogHandler handler) => _root.AddHandler(handler);

    private Sawmill GetOrCreate(string name)
    {
        if (_sawmills.TryGetValue(name, out Sawmill? existing))
        {
            return existing;
        }

        int split = name.LastIndexOf('.');
        Sawmill parent = split < 0 ? _root : GetOrCreate(name[..split]);

        Sawmill sawmill = new(name, parent);
        _sawmills[name] = sawmill;
        return sawmill;
    }
}
