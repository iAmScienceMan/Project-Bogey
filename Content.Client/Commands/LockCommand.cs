using System.Globalization;
using Content.Renderer.App;
using Lattice.Shared.Console;
using LockRequest = Content.Shared.Commands.LockCommand;

namespace Content.Client.Commands;

public sealed class LockCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "lock";

    public override string Description => "Locks a unit's fire-control radar onto a tracked contact, or releases it.";

    public override string Help => "lock <unit> <trackId|off>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_context.Session is not { } session)
        {
            shell.WriteError("No simulation is running; deploy first.");
            return;
        }

        if (args.Length != 2)
        {
            shell.WriteError("usage: " + Help);
            return;
        }

        if (string.Equals(args[1], "off", System.StringComparison.OrdinalIgnoreCase))
        {
            session.Enqueue(new LockRequest { UnitName = args[0], TrackId = null });
            shell.WriteLine($"Lock released: {args[0]}.");
            return;
        }

        if (!int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int trackId))
        {
            shell.WriteError($"'{args[1]}' is not a valid track id.");
            return;
        }

        session.Enqueue(new LockRequest { UnitName = args[0], TrackId = trackId });
        shell.WriteLine($"Lock ordered: {args[0]} → track {trackId}.");
    }
}
