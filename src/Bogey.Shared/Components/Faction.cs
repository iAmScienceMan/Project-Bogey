namespace Bogey.Shared.Components;


public enum FactionType
{
    Friendly,
    Hostile,
    Neutral,
}


public sealed class Faction
{
    public FactionType Side { get; set; }
}
