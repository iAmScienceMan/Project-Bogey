using System.Numerics;

namespace Bogey.Shared.Commands;

public abstract record SimCommand;

public sealed record MoveCommand : SimCommand
{
    public required string UnitName { get; init; }

    public required Vector2 Destination { get; init; }
}
