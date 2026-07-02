using Bogey.Logging;
using Bogey.Shared.Console;

namespace Bogey.Host.Commands;

public sealed class ExitCommand : ConsoleCommand
{
    [Dependency]
    private readonly IAppControl _app = null!;

    public override string Command => "exit";

    public override string Description => "Closes the game.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        shell.WriteLine(LogLevel.Info, "Exiting...");
        _app.Quit();
    }
}
