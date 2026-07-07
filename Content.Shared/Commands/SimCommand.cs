using System.Numerics;
using Content.Shared.Components;

namespace Content.Shared.Commands;

public abstract record SimCommand;

public sealed record EngageCommand : SimCommand
{
    public required string UnitName { get; init; }

    public required int TrackId { get; init; }

    public required string Weapon { get; init; }

    public int Count { get; init; } = 1;
}

public sealed record LockCommand : SimCommand
{
    public required string UnitName { get; init; }

    public int? TrackId { get; init; }
}

public sealed record PostureCommand : SimCommand
{
    public required string UnitName { get; init; }

    public required WeaponPosture Posture { get; init; }
}

public sealed record MoveCommand : SimCommand
{
    public required string UnitName { get; init; }

    public required Vector2 Destination { get; init; }
}

public sealed record SpawnCommand : SimCommand
{
    public required string PrototypeId { get; init; }

    public required Vector2 Position { get; init; }

    public Vector2 Velocity { get; init; }
}

public sealed record TeleportCommand : SimCommand
{
    public required int EntityId { get; init; }

    public required Vector2 Position { get; init; }
}

public sealed record AiCommand : SimCommand
{
    public required bool Enabled { get; init; }
}
