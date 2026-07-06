using System.Globalization;
using Bogey.Renderer.App;
using Bogey.Shared.Console;
using Bogey.Shared.Tracks;

namespace Bogey.Host.Commands;

public sealed class ContactsCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "contacts";

    public override string Description => "Lists the current friendly track picture (id, state, domain, position).";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_context.Session?.Current is not { } snapshot)
        {
            shell.WriteError("No simulation is running; deploy first.");
            return;
        }

        if (snapshot.Tracks.Count == 0)
        {
            shell.WriteLine("No contacts.");
            return;
        }

        foreach (Track track in snapshot.Tracks)
        {
            shell.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"T{track.TrackId,-3} {track.State,-11} {track.DomainGuess,-10} ({track.EstimatedPosition.X:0}, {track.EstimatedPosition.Y:0})  conf {track.Confidence:0.00}"));
        }
    }
}
