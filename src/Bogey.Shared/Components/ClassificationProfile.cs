namespace Bogey.Shared.Components;


public enum ContactDomain
{
    Unknown,
    Air,
    Surface,
    Subsurface,
}

public sealed class ClassificationProfile
{
    public ContactDomain Domain { get; set; }

    
    public string TypeName { get; set; } = string.Empty;
}
