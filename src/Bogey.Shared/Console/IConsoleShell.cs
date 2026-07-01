using Bogey.Logging;

namespace Bogey.Shared.Console;

public interface IConsoleShell
{
    void WriteLine(string message);

    void WriteLine(LogLevel level, string message);

    void WriteError(string message);
}
