using System;
using System.Collections.Generic;
using Bogey.Shared.Configuration;
using Bogey.Shared.Console;

namespace Bogey.Host.Commands;

public sealed class CVarCommand : ConsoleCommand
{
    [Dependency]
    private readonly IConfigurationManager _cfg = null!;

    public override string Command => "cvar";

    public override string Description => "Lists, reads, or sets configuration variables.";

    public override string Help =>
        "cvar               lists every cvar\n" +
        "cvar <name>        prints one cvar\n" +
        "cvar <name> <val>  sets a cvar";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            foreach (CVarDef def in Sorted())
            {
                shell.WriteLine($"{def.Name} = {_cfg.GetCVarString(def.Name)}");
            }

            return;
        }

        string name = args[0];
        if (!_cfg.IsRegistered(name))
        {
            shell.WriteError($"Unknown cvar '{name}'.");
            return;
        }

        if (args.Length == 1)
        {
            shell.WriteLine($"{name} = {_cfg.GetCVarString(name)}");
            return;
        }

        string value = argStr[(argStr.IndexOf(' ') + 1)..].Trim();
        if (_cfg.TrySetCVar(name, value, out string? error))
        {
            shell.WriteLine($"{name} = {_cfg.GetCVarString(name)}");
        }
        else
        {
            shell.WriteError(error ?? $"Failed to set '{name}'.");
        }
    }

    private IEnumerable<CVarDef> Sorted()
    {
        List<CVarDef> list = new(_cfg.Definitions);
        list.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
        return list;
    }
}
