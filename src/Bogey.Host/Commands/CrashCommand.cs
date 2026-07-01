using System;
using Bogey.Logging;
using Bogey.Shared.Console;

namespace Bogey.Host.Commands;

public sealed class CrashCommand : ConsoleCommand
{
    public override string Command => "crash";

    public override string Description => "Intentionally crashes the client for testing purposes.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        shell.WriteLine(LogLevel.Warning, "Crash command executed. Crashing...");

        Environment.FailFast("Intentional crash test");
    }
}
