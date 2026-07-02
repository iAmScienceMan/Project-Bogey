using System;

namespace Bogey.Logging;

public readonly struct LogMessage
{
    public readonly DateTime Timestamp;
    public readonly string LogbookName;
    public readonly LogLevel Level;
    public readonly string Message;

    public LogMessage(DateTime timestamp, string logbookName, LogLevel level, string message)
    {
        Timestamp = timestamp;
        LogbookName = logbookName;
        Level = level;
        Message = message;
    }
}
