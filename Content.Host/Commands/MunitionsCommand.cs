using Content.Renderer.App;
using Lattice.Shared.Console;

namespace Content.Host.Commands;

public sealed class MunitionsCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "munitions";

    public override string Description => "Lists in-flight munitions with seeker type, phase, and datum.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_context.Overlay is not { } overlay)
        {
            shell.WriteError("No debug overlay is active; deploy with --debug first.");
            return;
        }

        foreach (string line in overlay.DescribeMunitions())
        {
            shell.WriteLine(line);
        }
    }
}
