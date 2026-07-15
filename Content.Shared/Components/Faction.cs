using Lattice.Sim.Engine;

namespace Content.Shared.Components;


public enum FactionType
{
    Friendly,
    Hostile,
    Neutral,
}


[RegisterComponent]
public sealed class Faction : Component
{
    [DataField]
    public FactionType Side { get; set; }

    [DataField]
    public string? Id { get; set; }

    public string EffectiveId => Id ?? DefaultIdFor(Side);

    public static string DefaultIdFor(FactionType side) => side switch
    {
        FactionType.Friendly => "friendly",
        FactionType.Hostile => "hostile",
        _ => "neutral",
    };
}
