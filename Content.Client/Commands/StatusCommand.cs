using System.Globalization;
using Content.Renderer.App;
using Content.Renderer.RealTime;
using Lattice.Network;
using Lattice.Shared.Console;

namespace Content.Client.Commands;

public sealed class StatusCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "status";

    public override string Description => "Shows connection state, ping and network statistics.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (SessionCommands.GameSession(_context) is not { } session)
        {
            shell.WriteLine("Not connected to a server.");
            return;
        }

        NetworkStats stats = session.Stats;
        shell.WriteLine($"phase: {session.Phase}, username: {session.Username}, protocol: {NetworkProtocol.Version}");
        shell.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"ping: {stats.RttSeconds * 1000f:0} ms, resent messages: {stats.ResentMessages}"));
        shell.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"received: {stats.ReceivedBytes / 1024f:0.0} KB in {stats.ReceivedPackets} packets, "
            + $"sent: {stats.SentBytes / 1024f:0.0} KB in {stats.SentPackets} packets"));

        if (session.Lobby is { } lobby)
        {
            shell.WriteLine($"server: {lobby.ServerName}, scenario: {lobby.ScenarioName}, "
                + $"round: {(lobby.Phase == Content.Shared.Net.RoundPhase.InRound ? "in progress" : "not started")}");
        }
    }
}
