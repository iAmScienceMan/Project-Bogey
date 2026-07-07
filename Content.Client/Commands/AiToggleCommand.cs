using Content.Renderer.App;
using Lattice.Shared.Console;
using AiRequest = Content.Shared.Commands.AiCommand;

namespace Content.Client.Commands;

public sealed class AiToggleCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "ai";

    public override string Description => "Enables or disables all AI on the server (admin only).";

    public override string Help => "ai <on|off>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_context.Session is not { } session)
        {
            shell.WriteError("Not connected to a server.");
            return;
        }

        if (args.Length != 1 || args[0].ToLowerInvariant() is not ("on" or "off"))
        {
            shell.WriteError("usage: " + Help);
            return;
        }

        session.Enqueue(new AiRequest { Enabled = args[0].ToLowerInvariant() == "on" });
        shell.WriteLine($"Requested ai {args[0].ToLowerInvariant()}.");
    }
}
