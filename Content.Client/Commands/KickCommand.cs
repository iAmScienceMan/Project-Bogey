using Content.Renderer.App;
using Lattice.Shared.Console;

namespace Content.Client.Commands;

public sealed class KickCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "kick";

    public override string Description => "Kicks a player from the server (admin only).";

    public override string Help => "kick <username>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (SessionCommands.GameSession(_context) is not { } session)
        {
            shell.WriteError("Not connected to a server.");
            return;
        }

        if (args.Length != 1)
        {
            shell.WriteError("usage: " + Help);
            return;
        }

        SessionCommands.WarnIfNotAdmin(shell, session);
        session.Kick(args[0]);
        shell.WriteLine($"Requested kick of '{args[0]}'.");
    }
}
