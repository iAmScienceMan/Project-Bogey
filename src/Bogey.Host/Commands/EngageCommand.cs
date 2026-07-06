using System.Globalization;
using Bogey.Renderer.App;
using Bogey.Shared.Console;
using EngageRequest = Bogey.Shared.Commands.EngageCommand;

namespace Bogey.Host.Commands;

public sealed class EngageCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "engage";

    public override string Description => "Orders a unit to fire a weapon at a tracked contact.";

    public override string Help => "engage <unit> <trackId> <weapon> [count]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_context.Session is not { } session)
        {
            shell.WriteError("No simulation is running; deploy first.");
            return;
        }

        if (args.Length is not (3 or 4))
        {
            shell.WriteError("usage: " + Help);
            return;
        }

        if (!int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int trackId))
        {
            shell.WriteError($"'{args[1]}' is not a valid track id.");
            return;
        }

        int count = 1;
        if (args.Length == 4 && !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
        {
            shell.WriteError($"'{args[3]}' is not a valid count.");
            return;
        }

        session.Enqueue(new EngageRequest
        {
            UnitName = args[0],
            TrackId = trackId,
            Weapon = args[2],
            Count = count < 1 ? 1 : count,
        });

        shell.WriteLine($"Engagement ordered: {args[0]} → track {trackId} with {args[2]} x{(count < 1 ? 1 : count)}.");
    }
}
