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
    [DataField]
    public AiBehavior Behavior { get; set; } = AiBehavior.Aggressive;
}
