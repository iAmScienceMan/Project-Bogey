using Bogey.Shared.Components;

namespace Bogey.Sim.Systems;

public static class Factions
{
    public static readonly FactionType[] InOrder =
    {
        FactionType.Friendly,
        FactionType.Hostile,
        FactionType.Neutral,
    };

    public static bool AreHostile(FactionType a, FactionType b)
        => a != FactionType.Neutral && b != FactionType.Neutral && a != b;
}
