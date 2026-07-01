using System.Numerics;

namespace Bogey.Shared.Components;

public sealed class Propulsion
{
    public float MaxSpeedKmPerTick { get; set; }

    
    public Vector2? Waypoint { get; set; }
}
