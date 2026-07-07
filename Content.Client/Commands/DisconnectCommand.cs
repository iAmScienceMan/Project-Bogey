using Content.Renderer.App;
using Lattice.Shared.Console;

namespace Content.Client.Commands;

public sealed class DisconnectCommand : ConsoleCommand
{
    [Dependency]
    private readonly TacticalWindow _window = null!;

    public override string Command => "disconnect";

    public override string Description => "Disconnects from the current server.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_window.HasSession)
        {
            shell.WriteError("Not connected to a server.");
            return;
        }

        shell.WriteLine("Disconnected.");
        _window.DisconnectFromServer();
    }
}
