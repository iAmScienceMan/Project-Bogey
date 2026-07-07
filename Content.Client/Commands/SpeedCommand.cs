using Content.Renderer.App;
using Lattice.Shared.Console;

namespace Content.Client.Commands;

public sealed class SpeedCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    private const int MinSpeed = 0;
    private const int MaxSpeed = 100;

    public override string Command => "speed";

    public override string Description => "Sets the simulation speed in ticks per second.";

    public override string Help => "speed <" + MinSpeed + "-" + MaxSpeed + ">";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_context.Session is not { } session)
        {
            shell.WriteError("No simulation is running; deploy first.");
            return;
        }

        if (args.Length != 1
            || !int.TryParse(args[0], out int speed)
            || speed < MinSpeed
            || speed > MaxSpeed)
        {
            shell.WriteError("usage: " + Help);
            return;
        }

        session.SetSpeed(speed);
        shell.WriteLine("Speed: " + speed + ".");
    }
}
