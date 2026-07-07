using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Content.Renderer.App;
using Lattice.Shared.Console;
using TeleportRequest = Content.Shared.Commands.TeleportCommand;

namespace Content.Client.Commands;

public sealed class TeleportCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "teleport";

    public override IReadOnlyList<string> Aliases => new[] { "tp" };

    public override string Description => "Moves an entity to the given world coordinates (admin only).";

    public override string Help => "teleport <entityId> <x> <y>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_context.Session is not { } session)
        {
            shell.WriteError("Not connected to a server.");
            return;
        }

        if (args.Length != 3)
        {
            shell.WriteError("usage: " + Help);
            return;
        }

        if (!int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int entityId))
        {
            shell.WriteError($"'{args[0]}' is not a valid entity id.");
            return;
        }

        if (!TryParseCoord(args[1], out float x) || !TryParseCoord(args[2], out float y))
        {
            shell.WriteError("Coordinates must be numbers.");
            return;
        }

        session.Enqueue(new TeleportRequest
        {
            EntityId = entityId,
            Position = new Vector2(x, y),
        });
        shell.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Teleport of entity #{entityId} to ({x:0.##}, {y:0.##}) requested."));
    }

    private static bool TryParseCoord(string raw, out float value) =>
        float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
