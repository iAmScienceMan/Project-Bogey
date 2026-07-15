using Lattice.Sim.Engine;

namespace Content.Shared.Components;


public enum ContactDomain
{
    Unknown,
    Air,
    Surface,
    Subsurface,
    Munition,
}

[RegisterComponent]
public sealed class ClassificationProfile : Component
{
    [DataField]
    public ContactDomain Domain { get; set; }

    
    [DataField]
    public string TypeName { get; set; } = string.Empty;
}
