using System;
using Bogey.Logging;
using Bogey.Shared.Console;

namespace Bogey.Host.Commands;

public sealed class ThrowExceptionCommand : ConsoleCommand
{
    public override string Command => "throwexception";

    public override string Description => "Intentionally throws an exception for testing purposes.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        shell.WriteLine(LogLevel.Warning, "Throw exception command executed. Throwing exception...");

        throw new InvalidOperationException("Intentional exception triggered by 'throwexception' command.");
    }
}
