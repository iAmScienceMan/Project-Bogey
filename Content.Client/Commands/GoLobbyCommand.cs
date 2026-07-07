using Content.Renderer.App;
using Lattice.Shared.Console;

namespace Content.Client.Commands;

public sealed class GoLobbyCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "golobby";

    public override string Description => "Ends the round and returns everyone to the lobby (admin only).";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (SessionCommands.GameSession(_context) is not { } session)
        {
            shell.WriteError("Not connected to a server.");
            return;
        }

        SessionCommands.WarnIfNotAdmin(shell, session);
        session.GoLobby();
        shell.WriteLine("Requested a return to the lobby.");
    }
}
