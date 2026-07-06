using Lattice.Sim.Engine;

namespace Content.Shared.Components;

[RegisterComponent]
public sealed class Sensor : Component
{
    
    [DataField]
    public float RangeKm { get; set; }

    
    [DataField]
    public float MaxDetectProbability { get; set; }

    [DataField]
    public float FalloffExponent { get; set; }
}
