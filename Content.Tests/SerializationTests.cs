using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Commands;
using Content.Shared.Components;
using Content.Shared.Tracks;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class SerializationTests
{
    [Test]
    public void Snapshot_RoundTripsThroughTheWire()
    {
        TrackPictureSnapshot original = new()
        {
            Tick = 42,
            Tracks = new List<Track>
            {
                new()
                {
                    TrackId = 3,
                    EstimatedPosition = new Vector2(10f, 20f),
                    EstimatedVelocity = new Vector2(-1f, 2f),
                    PositionalErrorKm = 4.5f,
                    Confidence = 0.75f,
                    DomainGuess = ContactDomain.Air,
                    TypeGuess = "Fighter",
                    LastUpdatedTick = 40,
                    State = TrackState.Identified,
                },
            },
            OwnUnits = new List<OwnUnitView>
            {
                new()
                {
                    Name = "Flagship",
                    Position = new Vector2(0f, 5f),
                    SensorRangeKm = 150f,
                    Posture = WeaponPosture.Defensive,
                    HullCurrent = 320f,
                    HullMax = 400f,
                    LockedTrackId = 3,
                    Sprite = "own.png",
                    SpriteScale = 1.25f,
                    SpriteVisible = true,
                    Weapons = new List<WeaponStatusView>
                    {
                        new() { Name = "AIM-120D", Rounds = 16, Ready = true, PointDefense = false },
                        new() { Name = "CIWS", Rounds = -1, Ready = false, PointDefense = true },
                    },
                },
            },
            Munitions = new List<MunitionView>
            {
                new()
                {
                    Id = 99,
                    Position = new Vector2(7f, 8f),
                    HeadingRadians = 1.2f,
                    Seeker = SeekerType.ActiveRadar,
                    Locked = true,
                    Sprite = "missile.png",
                    SpriteScale = 1f,
                    SpriteVisible = true,
                },
            },
        };

        TrackPictureSnapshot wire = SnapshotSerializer.Deserialize(SnapshotSerializer.Serialize(original));

        Assert.Multiple(() =>
        {
            Assert.That(wire.Tick, Is.EqualTo(42));
            Assert.That(wire.Tracks[0].TypeGuess, Is.EqualTo("Fighter"));
            Assert.That(wire.Tracks[0].DomainGuess, Is.EqualTo(ContactDomain.Air));
            Assert.That(wire.Tracks[0].EstimatedPosition, Is.EqualTo(new Vector2(10f, 20f)));
            Assert.That(wire.OwnUnits[0].Name, Is.EqualTo("Flagship"));
            Assert.That(wire.OwnUnits[0].LockedTrackId, Is.EqualTo(3));
            Assert.That(wire.OwnUnits[0].Sprite, Is.EqualTo("own.png"));
            Assert.That(wire.OwnUnits[0].Weapons, Has.Count.EqualTo(2));
            Assert.That(wire.OwnUnits[0].Weapons[1].PointDefense, Is.True);
            Assert.That(wire.Munitions[0].Seeker, Is.EqualTo(SeekerType.ActiveRadar));
            Assert.That(wire.Munitions[0].Locked, Is.True);
        });
    }

    [Test]
    public void Snapshot_RoundTripsNullOptionals()
    {
        TrackPictureSnapshot original = new()
        {
            Tick = 1,
            Tracks = new List<Track>(),
            OwnUnits = new List<OwnUnitView>
            {
                new()
                {
                    Name = "Blip",
                    Position = Vector2.Zero,
                    SensorRangeKm = 0f,
                    LockedTrackId = null,
                    Sprite = null,
                },
            },
            Munitions = new List<MunitionView>(),
        };

        TrackPictureSnapshot wire = SnapshotSerializer.Deserialize(SnapshotSerializer.Serialize(original));

        Assert.Multiple(() =>
        {
            Assert.That(wire.OwnUnits[0].LockedTrackId, Is.Null);
            Assert.That(wire.OwnUnits[0].Sprite, Is.Null);
        });
    }

    [Test]
    public void Commands_RoundTripThroughTheWire()
    {
        SimCommand[] commands =
        {
            new EngageCommand { UnitName = "Flagship", TrackId = 2, Weapon = "AIM-120D", Count = 3 },
            new LockCommand { UnitName = "Flagship", TrackId = 5 },
            new LockCommand { UnitName = "Flagship", TrackId = null },
            new PostureCommand { UnitName = "Picket", Posture = WeaponPosture.Free },
            new MoveCommand { UnitName = "Picket", Destination = new Vector2(12f, -3f) },
            new SpawnCommand { PrototypeId = "AIM-120D", Position = new Vector2(1f, 2f), Velocity = new Vector2(3f, 4f) },
        };

        foreach (SimCommand command in commands)
        {
            SimCommand wire = CommandSerializer.Deserialize(CommandSerializer.Serialize(command));
            Assert.That(wire, Is.EqualTo(command), $"{command.GetType().Name} should survive the wire");
        }
    }
}
