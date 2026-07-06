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
}
