using System.Numerics;
using Content.Shared.Components;

namespace Content.Sim;

public sealed record MunitionDebug
{
    public required int Id { get; init; }
    public required FactionType Faction { get; init; }
    public required Vector2 Position { get; init; }
    public required float HeadingRadians { get; init; }
    public required SeekerType Seeker { get; init; }
    public required float FovDegrees { get; init; }
    public required float AcquisitionRangeKm { get; init; }
    public required bool Locked { get; init; }
    public required Vector2 Datum { get; init; }
    public required bool DatumPassed { get; init; }
    public Vector2? TargetPosition { get; init; }
}
