using System;

namespace Bogey.Logging;

public readonly struct LogMessage
{
    public readonly DateTime Timestamp;
    public readonly string SawmillName;
    public readonly LogLevel Level;
    public readonly string Message;

    public LogMessage(DateTime timestamp, string sawmillName, LogLevel level, string message)
    {
        Timestamp = timestamp;
        SawmillName = sawmillName;
        Level = level;
        Message = message;
    }
}
