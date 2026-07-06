namespace Bogey.Shared.Components;

public enum AiBehavior
{
    Hold,
    Aggressive,
}

public sealed class Ai
{
    public AiBehavior Behavior { get; set; } = AiBehavior.Aggressive;
}
