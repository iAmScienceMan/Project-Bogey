using Bogey.Logging;

namespace Bogey.Renderer.Ui.Console;

internal sealed class ConsoleLogSink : ILogHandler
{
    private readonly DevConsole _console;

    public ConsoleLogSink(DevConsole console) => _console = console;

    public void Log(in LogMessage message)
        => _console.WriteLog(message.Level, message.LogbookName, message.Message);
}
