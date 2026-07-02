using Bogey.Renderer.App;
using Bogey.Shared.Console;

namespace Bogey.Host.Commands;

public sealed class DeclutterCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "declutter";

    public override string Description => "Cycles the ground-truth overlay display mode.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_context.Overlay is not { } overlay)
        {
            shell.WriteError("No debug overlay is active; deploy with --debug first.");
            return;
        }

        shell.WriteLine("Ground truth: " + overlay.CycleDisplay() + ".");
    }
}
