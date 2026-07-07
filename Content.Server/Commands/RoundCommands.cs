using System.Collections.Generic;
using System.Globalization;
using Lattice.Shared.Console;

namespace Content.Server.Commands;

public sealed class StatusCommand : ConsoleCommand
{
    [Dependency]
    private readonly GameTicker _ticker = null!;

    public override string Command => "status";

    public override string Description => "Shows the server state.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
        => shell.WriteLine(_ticker.StatusLine());
}

public sealed class PlayersCommand : ConsoleCommand
{
    [Dependency]
    private readonly GameTicker _ticker = null!;

    public override string Command => "players";

    public override string Description => "Lists every player the server knows about.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        IReadOnlyList<string> lines = _ticker.PlayerLines();
        if (lines.Count == 0)
        {
            shell.WriteLine("No players have connected yet.");
            return;
        }

        foreach (string line in lines)
        {
            shell.WriteLine("  " + line);
        }
    }
}

public sealed class GoLobbyCommand : ConsoleCommand
{
    [Dependency]
    private readonly GameTicker _ticker = null!;

    public override string Command => "golobby";

    public override string Description => "Ends the round and returns everyone to the lobby.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_ticker.RoundRunning)
        {
            shell.WriteError("No round is running.");
            return;
        }

        _ticker.GoLobby("the server console");
        shell.WriteLine("Round ended; everyone returned to the lobby.");
    }
}

public sealed class SpeedCommand : ConsoleCommand
{
    [Dependency]
    private readonly GameTicker _ticker = null!;

    public override string Command => "speed";

    public override string Description => "Sets the simulation speed multiplier.";

    public override string Help => "speed <0-100>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1
            || !int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int speed))
        {
            shell.WriteError("usage: " + Help);
            return;
        }

        _ticker.SetSpeed(speed);
        shell.WriteLine(_ticker.RoundRunning
            ? $"Speed set to {_ticker.Speed}."
            : $"Speed set to {_ticker.Speed}; it applies once the round starts.");
    }
}

public sealed class AiCommand : ConsoleCommand
{
    [Dependency]
    private readonly GameTicker _ticker = null!;

    public override string Command => "ai";

    public override string Description => "Enables or disables all AI-controlled units.";

    public override string Help => "ai <on|off>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || args[0].ToLowerInvariant() is not ("on" or "off"))
        {
            shell.WriteError("usage: " + Help);
            return;
        }

        shell.WriteLine(_ticker.SetAi(args[0].ToLowerInvariant() == "on")
            ? $"AI {args[0].ToLowerInvariant()}."
            : "No round is running.");
    }
}

public sealed class LobbyTimeCommand : ConsoleCommand
{
    [Dependency]
    private readonly GameTicker _ticker = null!;

    public override string Command => "lobbytime";

    public override string Description => "Sets or adjusts the remaining lobby countdown in seconds.";

    public override string Help => "lobbytime <seconds|+seconds|-seconds>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_ticker.RoundRunning)
        {
            shell.WriteError("The round is already running.");
            return;
        }

        if (args.Length != 1 || !TryParse(args[0], out bool isDelta, out float seconds))
        {
            shell.WriteError("usage: " + Help);
            return;
        }

        _ticker.AdjustLobbyTime(isDelta, seconds);
        shell.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Round starts in {_ticker.CountdownRemaining:0.0}s."));
    }

    public static bool TryParse(string raw, out bool isDelta, out float seconds)
    {
        isDelta = raw.StartsWith('+') || raw.StartsWith('-');
        return float.TryParse(raw, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out seconds);
    }
}

public sealed class PauseTimerCommand : ConsoleCommand
{
    [Dependency]
    private readonly GameTicker _ticker = null!;

    public override string Command => "pausetimer";

    public override string Description => "Pauses or resumes the lobby countdown.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_ticker.RoundRunning)
        {
            shell.WriteError("The round is already running.");
            return;
        }

        _ticker.SetTimerPaused(!_ticker.TimerPaused);
        shell.WriteLine(_ticker.TimerPaused ? "Lobby countdown paused." : "Lobby countdown resumed.");
    }
}

public sealed class StartRoundCommand : ConsoleCommand
{
    [Dependency]
    private readonly GameTicker _ticker = null!;

    public override string Command => "startround";

    public override string Description => "Starts the round immediately without waiting for the countdown.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
        => shell.WriteLine(_ticker.ForceStartRound()
            ? "Round started."
            : "The round is already running.");
}

public sealed class StopCommand : ConsoleCommand
{
    [Dependency]
    private readonly GameTicker _ticker = null!;

    public override string Command => "stop";

    public override string Description => "Shuts the server down.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        shell.WriteLine("Stopping server.");
        _ticker.Stop();
    }
}
