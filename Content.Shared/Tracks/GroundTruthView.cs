using System.Numerics;
using Content.Shared.Components;

namespace Content.Shared.Tracks;

public sealed record GroundTruthView
{
    public required int EntityId { get; init; }

    public required string Name { get; init; }

    public required FactionType Side { get; init; }

    public required ContactDomain Domain { get; init; }

    public required Vector2 Position { get; init; }

    public string? TypeName { get; init; }
}
