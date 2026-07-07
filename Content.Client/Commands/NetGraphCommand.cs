using Content.Shared.Configuration;
using Lattice.Shared.Configuration;
using Lattice.Shared.Console;

namespace Content.Client.Commands;

public sealed class NetGraphCommand : ConsoleCommand
{
    [Dependency]
    private readonly IConfigurationManager _cfg = null!;

    public override string Command => "net_graph";

    public override string Description => "Toggles the network statistics graph overlay.";

    public override string Help => "net_graph [0|1]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        bool enabled = args.Length == 1
            ? args[0] is "1" or "on" or "true"
            : !_cfg.GetCVar(CCVars.NetGraph);

        _cfg.SetCVar(CCVars.NetGraph, enabled);
        shell.WriteLine("net_graph " + (enabled ? "on" : "off") + ".");
    }
}
