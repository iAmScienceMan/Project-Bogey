using Lattice.Sim.Engine;

namespace Content.Shared.Components;

public enum AiBehavior
{
    Hold,
    Aggressive,
}

[RegisterComponent]
public sealed class Ai : Component
{
    public AiBehavior Behavior { get; set; } = AiBehavior.Aggressive;
}
