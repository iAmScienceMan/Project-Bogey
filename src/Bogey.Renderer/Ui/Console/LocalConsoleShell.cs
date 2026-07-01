using Bogey.Logging;
using Bogey.Shared.Console;

namespace Bogey.Renderer.Ui.Console;

internal sealed class LocalConsoleShell : IConsoleShell
{
    private readonly DevConsole _console;

    public LocalConsoleShell(DevConsole console) => _console = console;

    public void WriteLine(string message) => _console.WriteLine(LogLevel.Info, message);

    public void WriteLine(LogLevel level, string message) => _console.WriteLine(level, message);

    public void WriteError(string message) => _console.WriteLine(LogLevel.Error, message);
}
