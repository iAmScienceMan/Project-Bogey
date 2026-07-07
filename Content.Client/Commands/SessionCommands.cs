using System;
using Content.Renderer.App;
using Content.Renderer.RealTime;
using Content.Shared.Net;
using Lattice.Shared.Console;

namespace Content.Client.Commands;

internal static class SessionCommands
{
    public static IGameSession? GameSession(SimConsoleContext context)
        => context.Session as IGameSession;

    public static bool IsSelfAdmin(IGameSession session)
    {
        if (session.Lobby is not { } lobby)
        {
            return false;
        }

        foreach (LobbyPlayer player in lobby.Players)
        {
            if (string.Equals(player.Username, session.Username, StringComparison.OrdinalIgnoreCase))
            {
                return player.IsAdmin;
            }
        }

        return false;
    }

    public static void WarnIfNotAdmin(IConsoleShell shell, IGameSession session)
    {
        if (!IsSelfAdmin(session))
        {
            shell.WriteLine("Note: you are not an admin on this server; the server will ignore this.");
        }
    }
}
