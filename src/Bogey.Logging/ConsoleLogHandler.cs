using System;
using System.Globalization;

namespace Bogey.Logging;

public sealed class ConsoleLogHandler : ILogHandler
{
    private static readonly object ConsoleGate = new();

    public void Log(in LogMessage message)
    {
        string timestamp = message.Timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        string line = $"[{timestamp}] [{Tag(message.Level)}] {message.SawmillName}: {message.Message}";

        lock (ConsoleGate)
        {
            ConsoleColor previous = Console.ForegroundColor;
            Console.ForegroundColor = ColorFor(message.Level);
            Console.Out.WriteLine(line);
            Console.ForegroundColor = previous;
        }
    }

    private static string Tag(LogLevel level) => level switch
    {
        LogLevel.Verbose => "VERB",
        LogLevel.Debug => "DEBG",
        LogLevel.Info => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERRO",
        LogLevel.Fatal => "FATL",
        _ => "????",
    };

    private static ConsoleColor ColorFor(LogLevel level) => level switch
    {
        LogLevel.Verbose => ConsoleColor.DarkGray,
        LogLevel.Debug => ConsoleColor.Gray,
        LogLevel.Info => ConsoleColor.Cyan,
        LogLevel.Warning => ConsoleColor.Yellow,
        LogLevel.Error => ConsoleColor.Red,
        LogLevel.Fatal => ConsoleColor.Magenta,
        _ => ConsoleColor.White,
    };
}
