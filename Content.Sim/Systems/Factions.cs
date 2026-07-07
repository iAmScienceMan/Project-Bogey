using System;
using Content.Shared.Components;

namespace Content.Sim.Systems;

public static class Factions
{
    public static bool AreHostile(Faction a, Faction b)
        => a.Side != FactionType.Neutral
           && b.Side != FactionType.Neutral
           && !string.Equals(a.EffectiveId, b.EffectiveId, StringComparison.Ordinal);

    public static bool IsHostileTo(string observerFactionId, Faction other)
        => other.Side != FactionType.Neutral
           && !string.Equals(other.EffectiveId, observerFactionId, StringComparison.Ordinal);
}
