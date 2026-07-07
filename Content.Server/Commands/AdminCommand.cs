using Lattice.Shared.Console;

namespace Content.Server.Commands;

public sealed class AdminCommand : ConsoleCommand
{
    [Dependency]
    private readonly GameTicker _ticker = null!;

    public override string Command => "admin";

    public override string Description => "Grants admin to a username.";

    public override string Help => "admin <username>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError("usage: " + Help);
            return;
        }

        _ticker.GrantAdmin(args[0]);
        shell.WriteLine($"'{args[0]}' is now an admin.");
    }
}

public sealed class UnadminCommand : ConsoleCommand
{
    [Dependency]
    private readonly GameTicker _ticker = null!;

    public override string Command => "unadmin";

    public override string Description => "Revokes admin from a username.";

    public override string Help => "unadmin <username>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError("usage: " + Help);
            return;
        }

        shell.WriteLine(_ticker.RevokeAdmin(args[0])
            ? $"'{args[0]}' is no longer an admin."
            : $"'{args[0]}' was not an admin.");
    }
}

public sealed class KickCommand : ConsoleCommand
{
    [Dependency]
    private readonly GameTicker _ticker = null!;

    public override string Command => "kick";

    public override string Description => "Kicks a connected player.";

    public override string Help => "kick <username>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError("usage: " + Help);
            return;
        }

        shell.WriteLine(_ticker.Kick(args[0], "the server console")
            ? $"Kicked '{args[0]}'."
            : $"No connected player named '{args[0]}'.");
    }
}
