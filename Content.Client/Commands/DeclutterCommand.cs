using Content.Renderer.App;
using Lattice.Shared.Console;

namespace Content.Client.Commands;

public sealed class DeclutterCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "declutter";

    public override string Description => "Cycles the ground-truth overlay display mode (admin only).";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_context.Overlay is not { } overlay)
        {
            shell.WriteError("No debug overlay is available.");
            return;
        }

        if (SessionCommands.GameSession(_context) is { } session)
        {
            SessionCommands.WarnIfNotAdmin(shell, session);
        }

        shell.WriteLine("Ground truth: " + overlay.CycleDisplay() + ".");
    }
}
