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
    public SeekerType Type { get; set; }

    public float AcquisitionRangeKm { get; set; }

    public float FovDegrees { get; set; } = 360f;

    public bool Datalink { get; set; }

    public bool TargetsMunitions { get; set; }

    public int LockedEntity { get; set; } = -1;

    public bool Locked => LockedEntity >= 0;
}
