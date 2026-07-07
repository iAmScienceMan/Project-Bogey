using Content.Renderer.App;
using Lattice.Shared.Console;

namespace Content.Client.Commands;

public sealed class MunitionsCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "munitions";

    public override string Description => "Lists in-flight munitions with seeker type, phase, and datum (admin only).";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_context.Overlay is not { } overlay || SessionCommands.GameSession(_context) is not { } session)
        {
            shell.WriteError("No debug overlay is available.");
            return;
        }

        if (session.GroundTruth is not { } truth)
        {
            shell.WriteError("No ground-truth data streaming; enable the overlay first (debugoverlay or seekers).");
            return;
        }

        foreach (string line in overlay.DescribeMunitions(truth.Munitions))
        {
            shell.WriteLine(line);
        }
    }
}
