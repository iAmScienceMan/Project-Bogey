using Bogey.Renderer.App;
using Bogey.Renderer.RealTime;
using Bogey.Shared.Console;

namespace Bogey.Host.Commands;

public sealed class SpeedCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "speed";

    public override string Description => "Sets the simulation speed.";

    public override string Help => "speed <paused|normal|fast>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_context.Session is not { } session)
        {
            shell.WriteError("No simulation is running; deploy first.");
            return;
        }

        if (args.Length != 1 || !TryParseSpeed(args[0], out SimSpeed speed))
        {
            shell.WriteError("usage: " + Help);
            return;
        }

        session.SetSpeed(speed);
        shell.WriteLine("Speed: " + speed.ToString().ToLowerInvariant() + ".");
    }

    private static bool TryParseSpeed(string raw, out SimSpeed speed)
    {
        switch (raw.ToLowerInvariant())
        {
            case "paused":
            case "pause":
                speed = SimSpeed.Paused;
                return true;
            case "normal":
            case "1":
                speed = SimSpeed.Normal;
                return true;
            case "fast":
            case "2":
                speed = SimSpeed.Fast;
                return true;
            default:
                speed = SimSpeed.Normal;
                return false;
        }
    }
}
