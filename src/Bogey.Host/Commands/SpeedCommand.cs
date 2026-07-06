using Bogey.Renderer.App;
using Bogey.Shared.Console;

namespace Bogey.Host.Commands;

public sealed class SpeedCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "speed";

    public override string Description => "Sets the simulation speed in ticks per second.";

    public override string Help => "speed <" + SimSession.MinSpeed + "-" + SimSession.MaxSpeed + ">";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_context.Session is not { } session)
        {
            shell.WriteError("No simulation is running; deploy first.");
            return;
        }

        if (args.Length != 1
            || !int.TryParse(args[0], out int speed)
            || speed < SimSession.MinSpeed
            || speed > SimSession.MaxSpeed)
        {
            shell.WriteError("usage: " + Help);
            return;
        }

        session.SetSpeed(speed);
        shell.WriteLine("Speed: " + speed + ".");
    }
}
