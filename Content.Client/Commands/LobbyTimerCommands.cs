using System.Globalization;
using Content.Renderer.App;
using Lattice.Shared.Console;

namespace Content.Client.Commands;

public sealed class LobbyTimeCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "lobbytime";

    public override string Description => "Sets or adjusts the lobby countdown in seconds (admin only).";

    public override string Help => "lobbytime <seconds|+seconds|-seconds>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (SessionCommands.GameSession(_context) is not { } session)
        {
            shell.WriteError("Not connected to a server.");
            return;
        }

        if (args.Length != 1
            || !float.TryParse(args[0], NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out float seconds))
        {
            shell.WriteError("usage: " + Help);
            return;
        }

        bool isDelta = args[0].StartsWith('+') || args[0].StartsWith('-');
        SessionCommands.WarnIfNotAdmin(shell, session);
        session.AdjustLobbyTime(isDelta, seconds);
        shell.WriteLine(isDelta
            ? $"Requested countdown adjustment of {seconds:+0.#;-0.#}s."
            : $"Requested countdown set to {seconds:0.#}s.");
    }
}

public sealed class PauseTimerCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "pausetimer";

    public override string Description => "Pauses or resumes the lobby countdown (admin only).";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (SessionCommands.GameSession(_context) is not { } session)
        {
            shell.WriteError("Not connected to a server.");
            return;
        }

        SessionCommands.WarnIfNotAdmin(shell, session);
        session.PauseTimer();
        shell.WriteLine("Requested a countdown pause toggle.");
    }
}

public sealed class StartRoundCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "startround";

    public override string Description => "Starts the round immediately, skipping the countdown (admin only).";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (SessionCommands.GameSession(_context) is not { } session)
        {
            shell.WriteError("Not connected to a server.");
            return;
        }

        SessionCommands.WarnIfNotAdmin(shell, session);
        session.StartRound();
        shell.WriteLine("Requested an immediate round start.");
    }
}
