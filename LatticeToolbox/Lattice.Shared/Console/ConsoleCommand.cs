using System;
using System.Collections.Generic;

namespace Lattice.Shared.Console;

public abstract class ConsoleCommand : IConsoleCommand
{
    public abstract string Command { get; }

    public virtual IReadOnlyList<string> Aliases => Array.Empty<string>();

    public virtual string Description => string.Empty;

    public virtual string Help => string.Empty;

    public abstract void Execute(IConsoleShell shell, string argStr, string[] args);
}
