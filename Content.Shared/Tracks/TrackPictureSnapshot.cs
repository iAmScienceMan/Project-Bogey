using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Components;

namespace Content.Shared.Tracks;

public sealed record WeaponStatusView
{
    public required string Name { get; init; }
    public required int Rounds { get; init; }
    public required bool Ready { get; init; }
    public required bool PointDefense { get; init; }
}

public sealed record OwnUnitView
{
    public required string Name { get; init; }
    public required Vector2 Position { get; init; }
    public required float SensorRangeKm { get; init; }
    public WeaponPosture Posture { get; init; }
    public float HullCurrent { get; init; }
    public float HullMax { get; init; }
    public int? LockedTrackId { get; init; }
    public IReadOnlyList<WeaponStatusView> Weapons { get; init; } = new List<WeaponStatusView>();
}

public sealed record MunitionView
{
    public required int Id { get; init; }
    public required Vector2 Position { get; init; }
    public required float HeadingRadians { get; init; }
    public required FactionType Faction { get; init; }
    public required SeekerType Seeker { get; init; }
    public required bool Locked { get; init; }
}

public sealed record TrackPictureSnapshot
{
    public required int Tick { get; init; }
    public required IReadOnlyList<Track> Tracks { get; init; }
    public required IReadOnlyList<OwnUnitView> OwnUnits { get; init; }
    public IReadOnlyList<MunitionView> Munitions { get; init; } = new List<MunitionView>();
}
