using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Renderer.App;
using Lattice.Shared.Console;
using SpawnRequest = Content.Shared.Commands.SpawnCommand;

namespace Content.Client.Commands;

public sealed class SpawnCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "spawn";

    public override string Description => "Spawns an entity from a prototype at world coordinates.";

    public override string Help => "spawn <prototypeId> <x> <y> [vx vy]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_context.Session is not { } session)
        {
            shell.WriteError("No simulation is running; deploy first.");
            return;
        }

        if (args.Length is not (3 or 5))
        {
            shell.WriteError("usage: " + Help);
            return;
        }

        string prototypeId = args[0];
        if (!_context.Prototypes.Contains(prototypeId))
        {
            shell.WriteError($"Unknown prototype '{prototypeId}'. Known: {string.Join(", ", _context.Prototypes)}");
            return;
        }

        if (!TryParseCoord(args[1], out float x) || !TryParseCoord(args[2], out float y))
        {
            shell.WriteError("Coordinates must be numbers.");
            return;
        }

        Vector2 velocity = Vector2.Zero;
        if (args.Length == 5)
        {
            if (!TryParseCoord(args[3], out float vx) || !TryParseCoord(args[4], out float vy))
            {
                shell.WriteError("Velocity components must be numbers.");
                return;
            }

            velocity = new Vector2(vx, vy);
        }

        session.Enqueue(new SpawnRequest
        {
            PrototypeId = prototypeId,
            Position = new Vector2(x, y),
            Velocity = velocity,
        });

        shell.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Spawning '{prototypeId}' at ({x:0.##}, {y:0.##})."));
    }

    private static bool TryParseCoord(string raw, out float value) =>
        float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
