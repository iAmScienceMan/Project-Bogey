using System.Collections.Generic;

using Lattice.Sim.Engine;

namespace Content.Shared.Components;

public sealed class WeaponMount
{
    [DataField]
    public string ProjectilePrototype { get; set; } = string.Empty;

    [DataField]
    public int CooldownTicks { get; set; }

    public int TicksUntilReady { get; set; }

    [DataField]
    public int MagazineCapacity { get; set; }

    public int RoundsRemaining { get; set; }

    [DataField]
    public bool PointDefense { get; set; }

    [DataField]
    public float PointDefensePk { get; set; }

    [DataField]
    public float PointDefenseRangeKm { get; set; }
}

[RegisterComponent]
public sealed class Loadout : Component
{
    [DataField]
    public List<WeaponMount> Mounts { get; set; } = new();
}
