using System;

namespace Bogey.Logging;

public interface ILogbook
{
    string Name { get; }

    LogLevel? Level { get; set; }

    void AddHandler(ILogHandler handler);

    void Log(LogLevel level, string message);

    void Log(LogLevel level, string format, params object?[] args);

    void Verbose(string message);

    void Verbose(string format, params object?[] args);

    void Debug(string message);

    void Debug(string format, params object?[] args);

    void Info(string message);

    void Info(string format, params object?[] args);

    void Warning(string message);

    void Warning(string format, params object?[] args);

    void Error(string message);

    void Error(string format, params object?[] args);

    void Fatal(string message);

    void Fatal(string format, params object?[] args);

    void Error(Exception exception, string message);
}
