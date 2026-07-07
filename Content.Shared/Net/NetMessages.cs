using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Content.Shared.Commands;
using Content.Shared.Components;
using Content.Shared.Tracks;

namespace Content.Shared.Net;

public static class NetDefaults
{
    public const int Port = 8712;
}

public enum RoundPhase : byte
{
    Lobby = 0,
    InRound = 1,
}

public sealed record ClientHail
{
    public required string Username { get; init; }

    public uint ColorRgb { get; init; }

    public int ProtocolVersion { get; init; }
}

public sealed record LobbyPlayer
{
    public required string Username { get; init; }

    public uint ColorRgb { get; init; }

    public bool Ready { get; init; }

    public bool InGame { get; init; }

    public bool IsAdmin { get; init; }
}

public sealed record LobbyStatus
{
    public required string ServerName { get; init; }

    public required string ScenarioName { get; init; }

    public required RoundPhase Phase { get; init; }

    public float RoundStartSeconds { get; init; }

    public bool CountdownPaused { get; init; }

    public int RoundTick { get; init; }

    public IReadOnlyList<LobbyPlayer> Players { get; init; } = new List<LobbyPlayer>();
}

public static class HailSerializer
{
    public static byte[] Serialize(ClientHail hail)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(hail.ProtocolVersion);
        writer.Write(hail.Username);
        writer.Write(hail.ColorRgb);
        writer.Flush();
        return stream.ToArray();
    }

    public static ClientHail Deserialize(byte[] data)
    {
        using MemoryStream stream = new(data);
        using BinaryReader reader = new(stream);
        return new ClientHail
        {
            ProtocolVersion = reader.ReadInt32(),
            Username = reader.ReadString(),
            ColorRgb = reader.ReadUInt32(),
        };
    }
}

public static class ServerMessages
{
    public const byte KindLobby = 1;
    public const byte KindSnapshot = 2;
    public const byte KindJoinGame = 3;
    public const byte KindNotice = 4;
    public const byte KindGroundTruth = 5;

    public static byte Kind(byte[] data) => data[0];

    public static byte[] Notice(string message)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(KindNotice);
        writer.Write(message);
        writer.Flush();
        return stream.ToArray();
    }

    public static string ReadNotice(byte[] data)
    {
        using MemoryStream stream = new(data, 1, data.Length - 1);
        using BinaryReader reader = new(stream);
        return reader.ReadString();
    }

    public static byte[] GroundTruth(GroundTruthUpdate update)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(KindGroundTruth);

        writer.Write(update.Entities.Count);
        foreach (GroundTruthView entry in update.Entities)
        {
            writer.Write(entry.EntityId);
            writer.Write(entry.Name);
            writer.Write((int)entry.Side);
            writer.Write((int)entry.Domain);
            writer.Write(entry.Position.X);
            writer.Write(entry.Position.Y);
            writer.Write(entry.TypeName is not null);
            if (entry.TypeName is not null)
            {
                writer.Write(entry.TypeName);
            }
        }

        writer.Write(update.Munitions.Count);
        foreach (MunitionDebugView munition in update.Munitions)
        {
            writer.Write(munition.Id);
            writer.Write((int)munition.Side);
            writer.Write(munition.Position.X);
            writer.Write(munition.Position.Y);
            writer.Write(munition.HeadingRadians);
            writer.Write((int)munition.Seeker);
            writer.Write(munition.FovDegrees);
            writer.Write(munition.AcquisitionRangeKm);
            writer.Write(munition.Locked);
            writer.Write(munition.Datum.X);
            writer.Write(munition.Datum.Y);
            writer.Write(munition.DatumPassed);
            writer.Write(munition.TargetPosition.HasValue);
            if (munition.TargetPosition is { } target)
            {
                writer.Write(target.X);
                writer.Write(target.Y);
            }
        }

        writer.Flush();
        return stream.ToArray();
    }

    public static GroundTruthUpdate ReadGroundTruth(byte[] data)
    {
        using MemoryStream stream = new(data, 1, data.Length - 1);
        using BinaryReader reader = new(stream);

        int entityCount = reader.ReadInt32();
        List<GroundTruthView> entities = new(entityCount);
        for (int i = 0; i < entityCount; i++)
        {
            entities.Add(new GroundTruthView
            {
                EntityId = reader.ReadInt32(),
                Name = reader.ReadString(),
                Side = (FactionType)reader.ReadInt32(),
                Domain = (ContactDomain)reader.ReadInt32(),
                Position = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                TypeName = reader.ReadBoolean() ? reader.ReadString() : null,
            });
        }

        int munitionCount = reader.ReadInt32();
        List<MunitionDebugView> munitions = new(munitionCount);
        for (int i = 0; i < munitionCount; i++)
        {
            munitions.Add(new MunitionDebugView
            {
                Id = reader.ReadInt32(),
                Side = (FactionType)reader.ReadInt32(),
                Position = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                HeadingRadians = reader.ReadSingle(),
                Seeker = (SeekerType)reader.ReadInt32(),
                FovDegrees = reader.ReadSingle(),
                AcquisitionRangeKm = reader.ReadSingle(),
                Locked = reader.ReadBoolean(),
                Datum = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
                DatumPassed = reader.ReadBoolean(),
                TargetPosition = reader.ReadBoolean()
                    ? new Vector2(reader.ReadSingle(), reader.ReadSingle())
                    : null,
            });
        }

        return new GroundTruthUpdate { Entities = entities, Munitions = munitions };
    }

    public static byte[] Lobby(LobbyStatus status)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(KindLobby);
        writer.Write(status.ServerName);
        writer.Write(status.ScenarioName);
        writer.Write((byte)status.Phase);
        writer.Write(status.RoundStartSeconds);
        writer.Write(status.CountdownPaused);
        writer.Write(status.RoundTick);
        writer.Write(status.Players.Count);
        foreach (LobbyPlayer player in status.Players)
        {
            writer.Write(player.Username);
            writer.Write(player.ColorRgb);
            writer.Write(player.Ready);
            writer.Write(player.InGame);
            writer.Write(player.IsAdmin);
        }

        writer.Flush();
        return stream.ToArray();
    }

    public static LobbyStatus ReadLobby(byte[] data)
    {
        using MemoryStream stream = new(data, 1, data.Length - 1);
        using BinaryReader reader = new(stream);

        string serverName = reader.ReadString();
        string scenarioName = reader.ReadString();
        RoundPhase phase = (RoundPhase)reader.ReadByte();
        float roundStartSeconds = reader.ReadSingle();
        bool countdownPaused = reader.ReadBoolean();
        int roundTick = reader.ReadInt32();

        int playerCount = reader.ReadInt32();
        List<LobbyPlayer> players = new(playerCount);
        for (int i = 0; i < playerCount; i++)
        {
            players.Add(new LobbyPlayer
            {
                Username = reader.ReadString(),
                ColorRgb = reader.ReadUInt32(),
                Ready = reader.ReadBoolean(),
                InGame = reader.ReadBoolean(),
                IsAdmin = reader.ReadBoolean(),
            });
        }

        return new LobbyStatus
        {
            ServerName = serverName,
            ScenarioName = scenarioName,
            Phase = phase,
            RoundStartSeconds = roundStartSeconds,
            CountdownPaused = countdownPaused,
            RoundTick = roundTick,
            Players = players,
        };
    }

    public static byte[] Snapshot(TrackPictureSnapshot snapshot)
    {
        byte[] body = SnapshotSerializer.Serialize(snapshot);
        byte[] framed = new byte[body.Length + 1];
        framed[0] = KindSnapshot;
        body.CopyTo(framed, 1);
        return framed;
    }

    public static TrackPictureSnapshot ReadSnapshot(byte[] data)
    {
        byte[] body = new byte[data.Length - 1];
        System.Array.Copy(data, 1, body, 0, body.Length);
        return SnapshotSerializer.Deserialize(body);
    }

    public static byte[] JoinGame() => new[] { KindJoinGame };
}

public static class ClientMessages
{
    public const byte KindCommand = 1;
    public const byte KindSetColor = 2;
    public const byte KindSetReady = 3;
    public const byte KindJoinGame = 4;
    public const byte KindSetSpeed = 5;
    public const byte KindGoLobby = 6;
    public const byte KindKick = 7;
    public const byte KindLobbyTime = 8;
    public const byte KindPauseTimer = 9;
    public const byte KindStartRound = 10;
    public const byte KindSetGroundTruth = 11;

    public static byte Kind(byte[] data) => data[0];

    public static byte[] Command(SimCommand command)
    {
        byte[] body = CommandSerializer.Serialize(command);
        byte[] framed = new byte[body.Length + 1];
        framed[0] = KindCommand;
        body.CopyTo(framed, 1);
        return framed;
    }

    public static SimCommand ReadCommand(byte[] data)
    {
        byte[] body = new byte[data.Length - 1];
        System.Array.Copy(data, 1, body, 0, body.Length);
        return CommandSerializer.Deserialize(body);
    }

    public static byte[] SetColor(uint rgb)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(KindSetColor);
        writer.Write(rgb);
        writer.Flush();
        return stream.ToArray();
    }

    public static uint ReadSetColor(byte[] data)
    {
        using MemoryStream stream = new(data, 1, data.Length - 1);
        using BinaryReader reader = new(stream);
        return reader.ReadUInt32();
    }

    public static byte[] SetReady(bool ready) => new[] { KindSetReady, ready ? (byte)1 : (byte)0 };

    public static bool ReadSetReady(byte[] data) => data[1] != 0;

    public static byte[] JoinGame() => new[] { KindJoinGame };

    public static byte[] SetSpeed(int speed)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(KindSetSpeed);
        writer.Write(speed);
        writer.Flush();
        return stream.ToArray();
    }

    public static int ReadSetSpeed(byte[] data)
    {
        using MemoryStream stream = new(data, 1, data.Length - 1);
        using BinaryReader reader = new(stream);
        return reader.ReadInt32();
    }

    public static byte[] GoLobby() => new[] { KindGoLobby };

    public static byte[] Kick(string username)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(KindKick);
        writer.Write(username);
        writer.Flush();
        return stream.ToArray();
    }

    public static string ReadKick(byte[] data)
    {
        using MemoryStream stream = new(data, 1, data.Length - 1);
        using BinaryReader reader = new(stream);
        return reader.ReadString();
    }

    public static byte[] LobbyTime(bool isDelta, float seconds)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(KindLobbyTime);
        writer.Write(isDelta);
        writer.Write(seconds);
        writer.Flush();
        return stream.ToArray();
    }

    public static (bool IsDelta, float Seconds) ReadLobbyTime(byte[] data)
    {
        using MemoryStream stream = new(data, 1, data.Length - 1);
        using BinaryReader reader = new(stream);
        return (reader.ReadBoolean(), reader.ReadSingle());
    }

    public static byte[] PauseTimer() => new[] { KindPauseTimer };

    public static byte[] StartRound() => new[] { KindStartRound };

    public static byte[] SetGroundTruth(bool enabled) => new[] { KindSetGroundTruth, enabled ? (byte)1 : (byte)0 };

    public static bool ReadSetGroundTruth(byte[] data) => data[1] != 0;
}
