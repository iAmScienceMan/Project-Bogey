using System.Collections.Generic;

namespace Lattice.Shared.Console;

public interface IConsoleCommand
{
    string Command { get; }

    IReadOnlyList<string> Aliases { get; }

    string Description { get; }

    string Help { get; }

    void Execute(IConsoleShell shell, string argStr, string[] args);
}
