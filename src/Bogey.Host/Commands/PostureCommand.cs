using System;
using Bogey.Renderer.App;
using Bogey.Shared.Components;
using Bogey.Shared.Console;
using PostureRequest = Bogey.Shared.Commands.PostureCommand;

namespace Bogey.Host.Commands;

public sealed class PostureCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "posture";

    public override string Description => "Sets a unit's weapons posture (hold, defensive, free).";

    public override string Help => "posture <unit> <hold|defensive|free>";

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

        if (!Enum.TryParse(args[1], ignoreCase: true, out WeaponPosture posture))
        {
            shell.WriteError("Posture must be one of: hold, defensive, free.");
            return;
        }

        session.Enqueue(new PostureRequest
        {
            UnitName = args[0],
            Posture = posture,
        });

        shell.WriteLine($"Posture ordered: {args[0]} → {posture}.");
    }
}
