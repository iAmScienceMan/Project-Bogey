using System.Collections.Generic;

using Lattice.Sim.Engine;

namespace Content.Shared.Components;

public sealed class WeaponMount
{
    public string ProjectilePrototype { get; set; } = string.Empty;

    public int CooldownTicks { get; set; }

    public int TicksUntilReady { get; set; }

    public int MagazineCapacity { get; set; }

    public int RoundsRemaining { get; set; }

    public bool PointDefense { get; set; }

    public float PointDefensePk { get; set; }

    public float PointDefenseRangeKm { get; set; }
}

[RegisterComponent]
public sealed class Loadout : Component
{
    public List<WeaponMount> Mounts { get; set; } = new();
}
