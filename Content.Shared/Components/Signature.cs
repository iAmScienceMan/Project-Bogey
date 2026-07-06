using Lattice.Sim.Engine;

namespace Content.Shared.Components;

[RegisterComponent]
public sealed class Signature : Component
{
    [DataField]
    public float Value { get; set; }
}
