using System;
using System.Globalization;
using Content.Renderer.App;
using Lattice.Shared.Console;
using Content.Shared.Tracks;

namespace Content.Host.Commands;

public sealed class LoadoutCommand : ConsoleCommand
{
    [Dependency]
    private readonly SimConsoleContext _context = null!;

    public override string Command => "loadout";

    public override string Description => "Shows a unit's weapons posture and magazines.";

    public override string Help => "loadout <unit>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_context.Session?.Current is not { } snapshot)
        {
            shell.WriteError("No simulation is running; deploy first.");
            return;
        }

        if (args.Length != 1)
        {
            shell.WriteError("usage: " + Help);
            return;
        }

        foreach (OwnUnitView unit in snapshot.OwnUnits)
        {
            if (!string.Equals(unit.Name, args[0], StringComparison.Ordinal))
            {
                continue;
            }

            shell.WriteLine($"{unit.Name}  posture: {unit.Posture}");
            if (unit.Weapons.Count == 0)
            {
                shell.WriteLine("  (unarmed)");
                return;
            }

            foreach (WeaponStatusView weapon in unit.Weapons)
            {
                string rounds = weapon.Rounds < 0 ? "∞" : weapon.Rounds.ToString(CultureInfo.InvariantCulture);
                string status = weapon.Ready ? "ready" : "reloading";
                shell.WriteLine($"  {weapon.Name,-12} rounds {rounds,-4} {status}{(weapon.PointDefense ? "  [PD]" : string.Empty)}");
            }

            return;
        }

        shell.WriteError($"No friendly unit named '{args[0]}'.");
    }
}
