using System.Numerics;

using Lattice.Sim.Engine;

namespace Content.Shared.Components;

[RegisterComponent]
public sealed class Propulsion : Component
{
    [DataField]
    public float MaxSpeedKmPerTick { get; set; }

    [DataField]
    public float MaxTurnRateDegPerSecond { get; set; } = 90f;

    public Vector2? Waypoint { get; set; }
}
