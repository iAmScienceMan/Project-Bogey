using Content.Renderer.App;
using Content.Shared.Net;
using Lattice.Shared.Console;

namespace Content.Client.Commands;

public sealed class PlayersCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "players";

    public override string Description => "Lists the players connected to the server.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (SessionCommands.GameSession(_context) is not { Lobby: { } lobby })
        {
            shell.WriteLine("Not connected to a server.");
            return;
        }

        shell.WriteLine($"{lobby.Players.Count} player(s) connected:");
        foreach (LobbyPlayer player in lobby.Players)
        {
            string state = player.InGame ? "in game" : player.Ready ? "ready" : "in lobby";
            string admin = player.IsAdmin ? " [admin]" : string.Empty;
            shell.WriteLine($"  {player.Username}{admin} - {state}");
        }
    }
}
