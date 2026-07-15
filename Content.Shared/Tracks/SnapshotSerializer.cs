using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Content.Shared.Components;

namespace Content.Shared.Tracks;

public static class SnapshotSerializer
{
    public static byte[] Serialize(TrackPictureSnapshot snapshot)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        writer.Write(snapshot.Tick);
        writer.Write(snapshot.Speed);

        writer.Write(snapshot.Tracks.Count);
        foreach (Track track in snapshot.Tracks)
        {
            writer.Write(track.TrackId);
            WriteVector(writer, track.EstimatedPosition);
            WriteVector(writer, track.EstimatedVelocity);
            writer.Write(track.PositionalErrorKm);
            writer.Write(track.Confidence);
            writer.Write((int)track.DomainGuess);
            WriteString(writer, track.TypeGuess);
            writer.Write(track.LastUpdatedTick);
            writer.Write((int)track.State);
            WriteString(writer, track.UnitName);
            WriteString(writer, track.PlayerName);
            writer.Write(track.PlayerColorRgb);
        }

        writer.Write(snapshot.OwnUnits.Count);
        foreach (OwnUnitView unit in snapshot.OwnUnits)
        {
            WriteString(writer, unit.Name);
            WriteVector(writer, unit.Position);
            writer.Write(unit.SensorRangeKm);
            writer.Write((int)unit.Posture);
            writer.Write(unit.HullCurrent);
            writer.Write(unit.HullMax);
            WriteNullableInt(writer, unit.LockedTrackId);
            WriteString(writer, unit.Sprite);
            writer.Write(unit.SpriteScale);
            writer.Write(unit.SpriteVisible);

            writer.Write(unit.Weapons.Count);
            foreach (WeaponStatusView weapon in unit.Weapons)
            {
                WriteString(writer, weapon.Name);
                writer.Write(weapon.Rounds);
                writer.Write(weapon.Ready);
                writer.Write(weapon.PointDefense);
            }
        }

        writer.Write(snapshot.Munitions.Count);
        foreach (MunitionView munition in snapshot.Munitions)
        {
            writer.Write(munition.Id);
            WriteVector(writer, munition.Position);
            writer.Write(munition.HeadingRadians);
            writer.Write((int)munition.Seeker);
            writer.Write(munition.Locked);
            writer.Write(munition.FovDegrees);
            writer.Write(munition.AcquisitionRangeKm);
            WriteVector(writer, munition.Datum);
            writer.Write(munition.DatumPassed);
            writer.Write(munition.Ballistic);
            writer.Write(munition.Finishing);
            WriteNullableVector(writer, munition.TargetPosition);
            WriteString(writer, munition.Sprite);
            writer.Write(munition.SpriteScale);
            writer.Write(munition.SpriteVisible);
        }

        writer.Flush();
        return stream.ToArray();
    }

    public static TrackPictureSnapshot Deserialize(byte[] data)
    {
        using MemoryStream stream = new(data);
        using BinaryReader reader = new(stream);

        int tick = reader.ReadInt32();
        int speed = reader.ReadInt32();

        int trackCount = reader.ReadInt32();
        List<Track> tracks = new(trackCount);
        for (int i = 0; i < trackCount; i++)
        {
            tracks.Add(new Track
            {
                TrackId = reader.ReadInt32(),
                EstimatedPosition = ReadVector(reader),
                EstimatedVelocity = ReadVector(reader),
                PositionalErrorKm = reader.ReadSingle(),
                Confidence = reader.ReadSingle(),
                DomainGuess = (ContactDomain)reader.ReadInt32(),
                TypeGuess = ReadString(reader),
                LastUpdatedTick = reader.ReadInt32(),
                State = (TrackState)reader.ReadInt32(),
                UnitName = ReadString(reader),
                PlayerName = ReadString(reader),
                PlayerColorRgb = reader.ReadUInt32(),
            });
        }

        int unitCount = reader.ReadInt32();
        List<OwnUnitView> units = new(unitCount);
        for (int i = 0; i < unitCount; i++)
        {
            string name = ReadString(reader) ?? string.Empty;
            Vector2 position = ReadVector(reader);
            float sensorRange = reader.ReadSingle();
            WeaponPosture posture = (WeaponPosture)reader.ReadInt32();
            float hullCurrent = reader.ReadSingle();
            float hullMax = reader.ReadSingle();
            int? lockedTrackId = ReadNullableInt(reader);
            string? sprite = ReadString(reader);
            float spriteScale = reader.ReadSingle();
            bool spriteVisible = reader.ReadBoolean();

            int weaponCount = reader.ReadInt32();
            List<WeaponStatusView> weapons = new(weaponCount);
            for (int w = 0; w < weaponCount; w++)
            {
                weapons.Add(new WeaponStatusView
                {
                    Name = ReadString(reader) ?? string.Empty,
                    Rounds = reader.ReadInt32(),
                    Ready = reader.ReadBoolean(),
                    PointDefense = reader.ReadBoolean(),
                });
            }

            units.Add(new OwnUnitView
            {
                Name = name,
                Position = position,
                SensorRangeKm = sensorRange,
                Posture = posture,
                HullCurrent = hullCurrent,
                HullMax = hullMax,
                LockedTrackId = lockedTrackId,
                Sprite = sprite,
                SpriteScale = spriteScale,
                SpriteVisible = spriteVisible,
                Weapons = weapons,
            });
        }

        int munitionCount = reader.ReadInt32();
        List<MunitionView> munitions = new(munitionCount);
        for (int i = 0; i < munitionCount; i++)
        {
            munitions.Add(new MunitionView
            {
                Id = reader.ReadInt32(),
                Position = ReadVector(reader),
                HeadingRadians = reader.ReadSingle(),
                Seeker = (SeekerType)reader.ReadInt32(),
                Locked = reader.ReadBoolean(),
                FovDegrees = reader.ReadSingle(),
                AcquisitionRangeKm = reader.ReadSingle(),
                Datum = ReadVector(reader),
                DatumPassed = reader.ReadBoolean(),
                Ballistic = reader.ReadBoolean(),
                Finishing = reader.ReadBoolean(),
                TargetPosition = ReadNullableVector(reader),
                Sprite = ReadString(reader),
                SpriteScale = reader.ReadSingle(),
                SpriteVisible = reader.ReadBoolean(),
            });
        }

        return new TrackPictureSnapshot
        {
            Tick = tick,
            Speed = speed,
            Tracks = tracks,
            OwnUnits = units,
            Munitions = munitions,
        };
    }

    private static void WriteVector(BinaryWriter writer, Vector2 vector)
    {
        writer.Write(vector.X);
        writer.Write(vector.Y);
    }

    private static Vector2 ReadVector(BinaryReader reader)
        => new(reader.ReadSingle(), reader.ReadSingle());

    private static void WriteNullableVector(BinaryWriter writer, Vector2? value)
    {
        writer.Write(value.HasValue);
        if (value.HasValue)
        {
            WriteVector(writer, value.Value);
        }
    }

    private static Vector2? ReadNullableVector(BinaryReader reader)
        => reader.ReadBoolean() ? ReadVector(reader) : null;

    private static void WriteString(BinaryWriter writer, string? value)
    {
        writer.Write(value is not null);
        if (value is not null)
        {
            writer.Write(value);
        }
    }

    private static string? ReadString(BinaryReader reader)
        => reader.ReadBoolean() ? reader.ReadString() : null;

    private static void WriteNullableInt(BinaryWriter writer, int? value)
    {
        writer.Write(value.HasValue);
        if (value.HasValue)
        {
            writer.Write(value.Value);
        }
    }

    private static int? ReadNullableInt(BinaryReader reader)
        => reader.ReadBoolean() ? reader.ReadInt32() : null;
}
