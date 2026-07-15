using Lattice.Sim.Engine;

namespace Content.Shared.Components;

public enum SeekerType
{
    Ir,
    ActiveRadar,
    SemiActiveRadar,
    AntiRadiation,
    Optical,
    Gps,
}

[RegisterComponent]
public sealed class Seeker : Component
{
    [DataField]
    public SeekerType Kind { get; set; }

    [DataField]
    public float AcquisitionRangeKm { get; set; }

    [DataField]
    public float FovDegrees { get; set; } = 360f;

    [DataField]
    public bool Datalink { get; set; }

    [DataField]
    public bool TargetsMunitions { get; set; }

    public EntityUid LockedEntity { get; set; } = EntityUid.Invalid;

    public bool Locked => LockedEntity.Valid;
}
