using System.Collections.Generic;
using System.Numerics;

namespace Bogey.Shared.Tracks;

public sealed record OwnUnitView
{
    public required string Name { get; init; }
    public required Vector2 Position { get; init; }
    public required float SensorRangeKm { get; init; }
}

public sealed record TrackPictureSnapshot
{
    public required int Tick { get; init; }
    public required IReadOnlyList<Track> Tracks { get; init; }
    public required IReadOnlyList<OwnUnitView> OwnUnits { get; init; }
}
