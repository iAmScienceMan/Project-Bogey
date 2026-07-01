namespace Bogey.Shared.Console;

public abstract class ConsoleCommand : IConsoleCommand
{
    public abstract string Command { get; }

    public virtual string Description => string.Empty;

    public virtual string Help => string.Empty;

    public abstract void Execute(IConsoleShell shell, string argStr, string[] args);
}
