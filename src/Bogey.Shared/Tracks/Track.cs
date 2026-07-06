using System.Numerics;
using Bogey.Shared.Components;

namespace Bogey.Shared.Tracks;


public enum TrackState
{
    Detected,    
    Classifying, 
    Identified,  
    Stale,       
    Dropped,     
}

public sealed record Track
{
    public required int TrackId { get; init; }

    public required Vector2 EstimatedPosition { get; init; }

    public Vector2 EstimatedVelocity { get; init; }

    
    public required float PositionalErrorKm { get; init; }

    
    public required float Confidence { get; init; }

    public ContactDomain DomainGuess { get; init; } = ContactDomain.Unknown;

    public string? TypeGuess { get; init; }

    public required int LastUpdatedTick { get; init; }

    public required TrackState State { get; init; }
}
