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
    public ContactDomain Domain { get; set; }

    
    public string TypeName { get; set; } = string.Empty;
}
