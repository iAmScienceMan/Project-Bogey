using Lattice.Sim.Engine;

namespace Content.Shared.Components;

public enum WeaponPosture
{
    Hold,
    Defensive,
    Free,
}

[RegisterComponent]
public sealed class WeaponControl : Component
{
    public WeaponPosture Posture { get; set; } = WeaponPosture.Hold;

    public int LockedTarget { get; set; } = -1;
}
