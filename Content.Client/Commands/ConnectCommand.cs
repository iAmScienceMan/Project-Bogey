using Content.Renderer.App;
using Content.Renderer.Ui.Screens;
using Lattice.Shared.Console;

namespace Content.Client.Commands;

public sealed class ConnectCommand : ConsoleCommand
{
    [Dependency]
    private readonly TacticalWindow _window = null!;

    public override string Command => "connect";

    public override string Description => "Connects to a server.";

    public override string Help => "connect <ip[:port]>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError("usage: " + Help);
            return;
        }

        if (!MainMenuScreen.TryParseAddress(args[0], out string host, out int port))
        {
            shell.WriteError($"'{args[0]}' is not a valid address - use ip or ip:port.");
            return;
        }

        shell.WriteLine($"Connecting to {host}:{port}...");
        _window.ConnectTo(host, port);
    }
}
