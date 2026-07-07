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
[NetworkedComponent]
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

    public int LockedEntity { get; set; } = -1;

    public bool Locked => LockedEntity >= 0;
}
