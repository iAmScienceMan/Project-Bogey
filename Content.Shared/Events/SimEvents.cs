using System.Numerics;
using Lattice.Sim.Engine;

namespace Content.Shared.Events;

public sealed record MoveOrderEvent
{
    public required Vector2 Destination { get; init; }
}

public sealed record ContactDetectedEvent
{
    public required string ObserverFaction { get; init; }

    public required Vector2 ObservedPosition { get; init; }

    public required float DetectionStrength { get; init; }

    public required int Tick { get; init; }
}

public sealed record TrackDroppedEvent
{
    public required string ObserverFaction { get; init; }

    public required EntityUid TruthEntityId { get; init; }
}

public sealed record DamageEvent
{
    public required float Amount { get; init; }

    public required EntityUid SourceEntity { get; init; }
}

public sealed record EntityDestroyedEvent
{
    public required EntityUid EntityId { get; init; }

    public required EntityUid KillerEntity { get; init; }
}

public sealed record EngagementOrderEvent
{
    public required EntityUid Shooter { get; init; }

    public required int TrackId { get; init; }

    public required string Weapon { get; init; }

    public required int Count { get; init; }
}

public sealed record WeaponFiredEvent
{
    public required EntityUid Shooter { get; init; }

    public required EntityUid Target { get; init; }

    public required string Weapon { get; init; }
}

public sealed record MunitionResolvedEvent
{
    public required EntityUid Munition { get; init; }

    public required EntityUid Target { get; init; }

    public required bool Hit { get; init; }
}
