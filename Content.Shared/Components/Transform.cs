using System.Numerics;

using Lattice.Sim.Engine;

namespace Content.Shared.Components;

[RegisterComponent]
public sealed class Transform : Component
{
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
}
