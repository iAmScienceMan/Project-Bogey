using Bogey.Renderer.App;
using Bogey.Shared.Console;

namespace Bogey.Host.Commands;

public sealed class SeekersCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "seekers";

    public override string Description => "Toggles the debug seeker overlay (FOV cones, acquisition rings, datum lines).";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_context.Overlay is not { } overlay)
        {
            shell.WriteError("No debug overlay is active; deploy with --debug first.");
            return;
        }

        shell.WriteLine("Seeker overlay: " + overlay.ToggleSeekers() + ".");
    }
}
