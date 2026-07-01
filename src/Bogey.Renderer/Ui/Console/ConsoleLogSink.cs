using Bogey.Logging;

namespace Bogey.Renderer.Ui.Console;

internal sealed class ConsoleLogSink : ILogHandler
{
    private readonly DevConsole _console;

    public ConsoleLogSink(DevConsole console) => _console = console;

    public void Log(in LogMessage message)
        => _console.WriteLine(message.Level, $"[{message.SawmillName}] {message.Message}");
}
