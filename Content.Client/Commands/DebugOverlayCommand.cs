using Content.Renderer.App;
using Content.Shared.Configuration;
using Lattice.Shared.Configuration;
using Lattice.Shared.Console;

namespace Content.Client.Commands;

public sealed class DebugOverlayCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    [Dependency]
    private readonly IConfigurationManager _cfg = null!;

    public override string Command => "debugoverlay";

    public override string Description => "Toggles the ground-truth debug overlay (admin only).";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (SessionCommands.GameSession(_context) is not { } session)
        {
            shell.WriteError("Not connected to a server.");
            return;
        }

        bool enabled = !_cfg.GetCVar(CCVars.DebugOverlay);
        SessionCommands.WarnIfNotAdmin(shell, session);
        _cfg.SetCVar(CCVars.DebugOverlay, enabled);
        session.SetGroundTruth(enabled);
        shell.WriteLine("Debug overlay " + (enabled ? "on" : "off")
            + (enabled ? ". Right-click picks an entity, right-click again teleports it." : "."));
    }
}
