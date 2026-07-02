using System;
using System.Collections.Generic;
using System.Linq;

namespace Bogey.Shared.Console;

public sealed class HelpCommand : ConsoleCommand
{
    [Dependency]
    private readonly ConsoleCommandRegistry _registry = null!;

    public override string Command => "help";

    public override string Description => "Displays a list of commands, or information about a specific command.";

    public override string Help => "help [command]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        switch (args.Length)
        {
            case 0:
                ListCommands(shell);
                break;
            case 1:
                DescribeCommand(shell, args[0]);
                break;
            default:
                shell.WriteError("usage: " + Help);
                break;
        }
    }

    private void ListCommands(IConsoleShell shell)
    {
        List<IConsoleCommand> commands = _registry.Commands.Values
            .Distinct()
            .OrderBy(command => command.Command, StringComparer.OrdinalIgnoreCase)
            .ToList();

        int width = commands.Max(command => command.Command.Length);

        shell.WriteLine($"{commands.Count} commands available. Type 'help <command>' for more details.");

        foreach (IConsoleCommand command in commands)
        {
            string name = command.Command.PadRight(width);
            shell.WriteLine(command.Description.Length == 0
                ? name
                : $"{name}  {command.Description}");
        }
    }

    private void DescribeCommand(IConsoleShell shell, string name)
    {
        if (!_registry.Commands.TryGetValue(name, out IConsoleCommand? command))
        {
            shell.WriteError($"Unknown command: {name}");
            return;
        }

        shell.WriteLine(command.Command);

        if (command.Aliases.Count > 0)
        {
            shell.WriteLine("Aliases: " + string.Join(", ", command.Aliases));
        }

        if (command.Description.Length > 0)
        {
            shell.WriteLine(command.Description);
        }

        if (command.Help.Length > 0)
        {
            shell.WriteLine("Usage: " + command.Help);
        }
    }
}
