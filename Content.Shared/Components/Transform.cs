using System.Numerics;

using Lattice.Sim.Engine;

namespace Content.Shared.Components;

[RegisterComponent]
[NetworkedComponent]
public sealed class Transform : Component
{
    [DataField]
    public Vector2 Position { get; set; }
    [DataField]
    public Vector2 Velocity { get; set; }
}
