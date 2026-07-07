using Content.Renderer.App;
using Lattice.Shared.Console;

namespace Content.Client.Commands;

public sealed class SeekersCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "seekers";

    public override string Description => "Toggles the debug seeker overlay (FOV cones, acquisition rings, datum lines; admin only).";

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

        shell.WriteLine("Seeker overlay: " + overlay.ToggleSeekers() + ".");
    }
}
