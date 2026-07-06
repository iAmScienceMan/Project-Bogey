using Lattice.Logging;
using Lattice.Shared.Console;

namespace Content.Host.Commands;

public sealed class TestCommand : ConsoleCommand
{
    public override string Command => "test";

    public override string Description => "Replies with a test message using a specified log level.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        // default behavior
        var level = LogLevel.Info;
        var message = "Test command works!";

        if (args.Length > 0)
        {
            var input = args[0].ToLowerInvariant();

            level = input switch
            {
                "verbose" => LogLevel.Verbose,
                "debug" => LogLevel.Debug,
                "info" => LogLevel.Info,
                "warn" or "warning" => LogLevel.Warning,
                "error" => LogLevel.Error,
                "fatal" => LogLevel.Fatal,
                _ => LogLevel.Info
            };

            if (args.Length > 1)
            {
                message = string.Join(' ', args[1..]);
            }
        }

        shell.WriteLine(level, message);
    }
}
