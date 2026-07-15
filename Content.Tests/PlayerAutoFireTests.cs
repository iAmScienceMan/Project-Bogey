using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.Components;
using Content.Shared.Prototypes;
using Content.Shared.Tracks;
using Content.Sim;
using Lattice.Sim.Engine;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class PlayerAutoFireTests
{
    private static PrototypeManager BuildLibrary()
    {
        PrototypeManager prototypes = new(new ComponentFactory(new[] { typeof(Sensor).Assembly }));

        prototypes.Register("ssm", "ssm", () => new List<IComponent>
        {
            new MetaData { EntityName = "ssm" },
            new Faction { Side = FactionType.Neutral },
            new Transform(),
            new Health { Max = 1f },
            new Signature { Value = 0.2f },
            new ClassificationProfile { Domain = ContactDomain.Munition },
            new Propulsion { MaxSpeedKmPerTick = 5f },
            new Projectile
            {
                Damage = 100f,
                DetonationRangeKm = 2f,
                Pk = 1f,
                RangeKm = 300f,
                TargetDomains = new List<ContactDomain> { ContactDomain.Surface },
                MotorBurnTicksRemaining = 60,
                DragPerTick = 0.01f,
            },
            new Seeker { Kind = SeekerType.ActiveRadar, AcquisitionRangeKm = 20f, FovDegrees = 60f, Datalink = true },
        }.AsReadOnly());

        prototypes.Register("warship", "warship", () => new List<IComponent>
        {
            new MetaData { EntityName = "warship" },
            new Faction { Side = FactionType.Friendly },
            new Transform(),
            new Signature { Value = 1f },
            new ClassificationProfile { Domain = ContactDomain.Surface, TypeName = "Warship" },
            new Sensor { RangeKm = 300f, MaxDetectProbability = 0.99f, FalloffExponent = 1f },
            new Health { Max = 400f },
            new Propulsion { MaxSpeedKmPerTick = 1f },
            new Loadout
            {
                Mounts = new List<WeaponMount>
                {
                    new() { ProjectilePrototype = "ssm", CooldownTicks = 4, MagazineCapacity = 20 },
                },
            },
            new WeaponControl { Posture = WeaponPosture.Free },
        }.AsReadOnly());

        prototypes.Register("raider", "raider", () => new List<IComponent>
        {
            new MetaData { EntityName = "raider" },
            new Faction { Side = FactionType.Hostile },
            new Transform(),
            new Signature { Value = 1f },
            new ClassificationProfile { Domain = ContactDomain.Surface, TypeName = "Raider" },
            new Health { Max = 400f },
        }.AsReadOnly());

        return prototypes;
    }

    private static int CountFires(SimRuntime sim, int ticks)
    {
        int fires = 0;
        sim.WeaponFired += _ => fires++;
        for (int i = 0; i < ticks; i++)
        {
            sim.Step();
        }

        return fires;
    }

    [Test]
    public void FreePlayer_DoesNotAutoFire_AtAnotherPlayer()
    {
        SimRuntime sim = new(new ScenarioDefinition { Id = "mp" }, BuildLibrary(), seed: 7);
        sim.SpawnPlayerUnit("alice", "warship", "AliceShip", Vector2.Zero);
        sim.SpawnPlayerUnit("bob", "warship", "BobShip", new Vector2(30f, 0f));

        bool classified = false;
        int fires = 0;
        sim.WeaponFired += _ => fires++;
        for (int i = 0; i < 120; i++)
        {
            sim.Step();
            classified |= sim.PublishSnapshot("alice").Tracks.Any(t => t.DomainGuess == ContactDomain.Surface);
        }

        Assert.That(classified, Is.True, "alice never even classified bob, so the test proves nothing");
        Assert.That(fires, Is.Zero, "a Free player auto-fired at another player");
    }

    [Test]
    public void FreePlayer_StillAutoFires_AtHostileAi()
    {
        SimRuntime sim = new(new ScenarioDefinition { Id = "mp" }, BuildLibrary(), seed: 7);
        sim.SpawnPlayerUnit("alice", "warship", "AliceShip", Vector2.Zero);
        sim.SpawnFromPrototype("raider", new Vector2(30f, 0f), Vector2.Zero);

        Assert.That(CountFires(sim, 120), Is.GreaterThan(0), "a Free player should still auto-engage hostile AI");
    }

    [Test]
    public void Player_CanStillManuallyEngage_AnotherPlayer()
    {
        SimRuntime sim = new(new ScenarioDefinition { Id = "mp" }, BuildLibrary(), seed: 7);
        sim.SpawnPlayerUnit("alice", "warship", "AliceShip", Vector2.Zero);
        sim.SpawnPlayerUnit("bob", "warship", "BobShip", new Vector2(30f, 0f));

        int trackId = 0;
        for (int i = 0; i < 120 && trackId == 0; i++)
        {
            sim.Step();
            Track? track = sim.PublishSnapshot("alice").Tracks.FirstOrDefault();
            if (track is not null)
            {
                trackId = track.TrackId;
            }
        }

        Assert.That(trackId, Is.Not.Zero, "alice never got a track on bob");
        Assert.That(sim.IssueEngagement("AliceShip", trackId, "ssm", 2, "alice"), Is.True);

        Assert.That(CountFires(sim, 30), Is.GreaterThan(0), "a manual engagement order against a player should still fire");
    }
}
