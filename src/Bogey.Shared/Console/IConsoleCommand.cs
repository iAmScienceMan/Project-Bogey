namespace Bogey.Shared.Console;

public interface IConsoleCommand
{
    string Command { get; }

    string Description { get; }

    string Help { get; }

    void Execute(IConsoleShell shell, string argStr, string[] args);
}
