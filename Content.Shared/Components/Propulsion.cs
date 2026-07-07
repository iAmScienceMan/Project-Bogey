using System.Numerics;

using Lattice.Sim.Engine;

namespace Content.Shared.Components;

[RegisterComponent]
[NetworkedComponent]
public sealed class Propulsion : Component
{
    [DataField]
    public float MaxSpeedKmPerTick { get; set; }

    
    public Vector2? Waypoint { get; set; }
}
