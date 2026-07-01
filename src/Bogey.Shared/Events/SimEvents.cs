using System.Numerics;

namespace Bogey.Shared.Events;

public sealed record MoveOrderEvent
{
    
    public required Vector2 Destination { get; init; }
}

public sealed record ContactDetectedEvent
{
    
    public required Vector2 ObservedPosition { get; init; }

    public required float DetectionStrength { get; init; }

    public required int Tick { get; init; }
}

public sealed record TrackDroppedEvent
{
    
    public required int TruthEntityId { get; init; }
}
