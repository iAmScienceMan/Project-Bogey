using Lattice.Sim.Engine;

namespace Content.Shared.Components;

[RegisterComponent]
public sealed class Sensor : Component
{
    
    public float RangeKm { get; set; }

    
    public float MaxDetectProbability { get; set; }

    public float FalloffExponent { get; set; }
}
