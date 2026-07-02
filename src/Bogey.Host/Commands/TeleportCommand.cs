using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Bogey.Renderer.App;
using Bogey.Shared.Console;

namespace Bogey.Host.Commands;

public sealed class TeleportCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "teleport";

    public override IReadOnlyList<string> Aliases => new[] { "tp" };

    public override string Description => "Moves an entity to the given world coordinates.";

    public override string Help => "teleport <entityId> <x> <y>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 3)
        {
            shell.WriteError("usage: " + Help);
            return;
        }

        if (_context.Overlay is not { } overlay)
        {
            shell.WriteError("No debug overlay is active; deploy with --debug first.");
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

        Vector2 position = new(x, y);
        if (overlay.Teleport(entityId, position))
        {
            shell.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Teleported entity #{entityId} to ({x:0.##}, {y:0.##})."));
        }
        else
        {
            shell.WriteError($"No entity with id {entityId}.");
        }
    }

    private static bool TryParseCoord(string raw, out float value) =>
        float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
