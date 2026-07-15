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
    [DataField]
    public WeaponPosture Posture { get; set; } = WeaponPosture.Hold;

    public EntityUid LockedTarget { get; set; } = EntityUid.Invalid;
}
