using System;
using System.Collections.Generic;
using System.Globalization;

namespace Lattice.Logging;

public sealed class Logbook : ILogbook
{
    private readonly Logbook? _parent;
    private readonly List<ILogHandler> _handlers = new();
    private readonly object _gate = new();

    public Logbook(string name, Logbook? parent)
    {
        Name = name;
        _parent = parent;
    }

    public string Name { get; }

    public LogLevel? Level { get; set; }

    public void AddHandler(ILogHandler handler)
    {
        lock (_gate)
        {
            _handlers.Add(handler);
        }
    }

    public void Log(LogLevel level, string message)
    {
        if (level < EffectiveLevel())
        {
            return;
        }

        LogMessage entry = new(DateTime.Now, Name, level, message);
        Dispatch(in entry);
    }

    public void Log(LogLevel level, string format, params object?[] args)
    {
        if (level < EffectiveLevel())
        {
            return;
        }

        Log(level, string.Format(CultureInfo.InvariantCulture, format, args));
    }

    public void Verbose(string message) => Log(LogLevel.Verbose, message);

    public void Verbose(string format, params object?[] args) => Log(LogLevel.Verbose, format, args);

    public void Debug(string message) => Log(LogLevel.Debug, message);

    public void Debug(string format, params object?[] args) => Log(LogLevel.Debug, format, args);

    public void Info(string message) => Log(LogLevel.Info, message);

    public void Info(string format, params object?[] args) => Log(LogLevel.Info, format, args);

    public void Warning(string message) => Log(LogLevel.Warning, message);

    public void Warning(string format, params object?[] args) => Log(LogLevel.Warning, format, args);

    public void Error(string message) => Log(LogLevel.Error, message);

    public void Error(string format, params object?[] args) => Log(LogLevel.Error, format, args);

    public void Fatal(string message) => Log(LogLevel.Fatal, message);

    public void Fatal(string format, params object?[] args) => Log(LogLevel.Fatal, format, args);

    public void Error(Exception exception, string message) => Log(LogLevel.Error, $"{message}\n{exception}");

    private LogLevel EffectiveLevel()
    {
        for (Logbook? current = this; current is not null; current = current._parent)
        {
            if (current.Level is { } level)
            {
                return level;
            }
        }

        return LogLevel.Info;
    }

    private void Dispatch(in LogMessage entry)
    {
        for (Logbook? current = this; current is not null; current = current._parent)
        {
            lock (current._gate)
            {
                foreach (ILogHandler handler in current._handlers)
                {
                    handler.Log(in entry);
                }
            }
        }
    }
}
