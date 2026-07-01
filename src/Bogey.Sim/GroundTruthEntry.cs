using System.Numerics;
using Bogey.Shared.Components;

namespace Bogey.Sim;

public sealed record GroundTruthEntry
{
    public required int EntityId { get; init; }

    public required string Name { get; init; }
    public required FactionType Faction { get; init; }
    public required Vector2 Position { get; init; }
    public ContactDomain Domain { get; init; } = ContactDomain.Unknown;
    public string? TypeName { get; init; }
}
