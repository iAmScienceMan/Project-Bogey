using System;
using System.IO;
using System.Numerics;
using Content.Shared.Components;

namespace Content.Shared.Commands;

public static class CommandSerializer
{
    private enum Kind : byte
    {
        Engage = 1,
        Lock = 2,
        Posture = 3,
        Move = 4,
        Spawn = 5,
        Teleport = 6,
        Ai = 7,
    }

    public static byte[] Serialize(SimCommand command)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        switch (command)
        {
            case EngageCommand engage:
                writer.Write((byte)Kind.Engage);
                writer.Write(engage.UnitName);
                writer.Write(engage.TrackId);
                writer.Write(engage.Weapon);
                writer.Write(engage.Count);
                break;
            case LockCommand lockCommand:
                writer.Write((byte)Kind.Lock);
                writer.Write(lockCommand.UnitName);
                writer.Write(lockCommand.TrackId.HasValue);
                if (lockCommand.TrackId.HasValue)
                {
                    writer.Write(lockCommand.TrackId.Value);
                }

                break;
            case PostureCommand posture:
                writer.Write((byte)Kind.Posture);
                writer.Write(posture.UnitName);
                writer.Write((int)posture.Posture);
                break;
            case MoveCommand move:
                writer.Write((byte)Kind.Move);
                writer.Write(move.UnitName);
                WriteVector(writer, move.Destination);
                break;
            case SpawnCommand spawn:
                writer.Write((byte)Kind.Spawn);
                writer.Write(spawn.PrototypeId);
                WriteVector(writer, spawn.Position);
                WriteVector(writer, spawn.Velocity);
                break;
            case TeleportCommand teleport:
                writer.Write((byte)Kind.Teleport);
                writer.Write(teleport.EntityId);
                WriteVector(writer, teleport.Position);
                break;
            case AiCommand ai:
                writer.Write((byte)Kind.Ai);
                writer.Write(ai.Enabled);
                break;
            default:
                throw new InvalidOperationException($"No wire format for command '{command.GetType().Name}'.");
        }

        writer.Flush();
        return stream.ToArray();
    }

    public static SimCommand Deserialize(byte[] data)
    {
        using MemoryStream stream = new(data);
        using BinaryReader reader = new(stream);

        Kind kind = (Kind)reader.ReadByte();
        return kind switch
        {
            Kind.Engage => new EngageCommand
            {
                UnitName = reader.ReadString(),
                TrackId = reader.ReadInt32(),
                Weapon = reader.ReadString(),
                Count = reader.ReadInt32(),
            },
            Kind.Lock => new LockCommand
            {
                UnitName = reader.ReadString(),
                TrackId = reader.ReadBoolean() ? reader.ReadInt32() : null,
            },
            Kind.Posture => new PostureCommand
            {
                UnitName = reader.ReadString(),
                Posture = (WeaponPosture)reader.ReadInt32(),
            },
            Kind.Move => new MoveCommand
            {
                UnitName = reader.ReadString(),
                Destination = ReadVector(reader),
            },
            Kind.Spawn => new SpawnCommand
            {
                PrototypeId = reader.ReadString(),
                Position = ReadVector(reader),
                Velocity = ReadVector(reader),
            },
            Kind.Teleport => new TeleportCommand
            {
                EntityId = reader.ReadInt32(),
                Position = ReadVector(reader),
            },
            Kind.Ai => new AiCommand
            {
                Enabled = reader.ReadBoolean(),
            },
            _ => throw new InvalidOperationException($"Unknown command kind {(byte)kind}."),
        };
    }

    private static void WriteVector(BinaryWriter writer, Vector2 vector)
    {
        writer.Write(vector.X);
        writer.Write(vector.Y);
    }

    private static Vector2 ReadVector(BinaryReader reader)
        => new(reader.ReadSingle(), reader.ReadSingle());
}
