using Lattice.Sim.Engine;

namespace Content.Shared.Components;

[RegisterComponent]
public sealed class Identity : Component
{
    [DataField]
    public string Name { get; set; } = string.Empty;
}
