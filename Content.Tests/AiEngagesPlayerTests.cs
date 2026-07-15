using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Components;
using Content.Shared.Prototypes;
using Content.Shared.Tracks;
using Content.Sim;
using Content.Sim.Systems;
using Lattice.Sim.Engine;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class AiEngagesPlayerTests
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

        prototypes.Register("aiRaider", "aiRaider", () => new List<IComponent>
        {
            new MetaData { EntityName = "aiRaider" },
            new Faction { Side = FactionType.Hostile },
            new Transform(),
            new Signature { Value = 0.9f },
            new ClassificationProfile { Domain = ContactDomain.Surface, TypeName = "Raider" },
            new Sensor { RangeKm = 300f, MaxDetectProbability = 0.99f, FalloffExponent = 1f },
            new Health { Max = 400f },
            new Loadout
            {
                Mounts = new List<WeaponMount>
                {
                    new() { ProjectilePrototype = "ssm", CooldownTicks = 4, MagazineCapacity = 20 },
                },
            },
            new WeaponControl { Posture = WeaponPosture.Free },
            new Ai { Behavior = AiBehavior.Aggressive },
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
        }.AsReadOnly());

        prototypes.Register("banditLike", "banditLike", () => new List<IComponent>
        {
            new MetaData { EntityName = "banditLike" },
            new Faction { Side = FactionType.Hostile },
            new Transform(),
            new Signature { Value = 0.6f },
            new ClassificationProfile { Domain = ContactDomain.Air, TypeName = "Bandit" },
            new Sensor { RangeKm = 130f, MaxDetectProbability = 0.9f, FalloffExponent = 1.5f },
            new Propulsion { MaxSpeedKmPerTick = 0.6f },
            new Health { Max = 100f },
            new Loadout
            {
                Mounts = new List<WeaponMount>
                {
                    new() { ProjectilePrototype = "antiship", CooldownTicks = 25, MagazineCapacity = 3 },
                },
            },
            new WeaponControl { Posture = WeaponPosture.Free },
            new Ai { Behavior = AiBehavior.Aggressive },
        }.AsReadOnly());

        prototypes.Register("antiship", "antiship", () => new List<IComponent>
        {
            new MetaData { EntityName = "antiship" },
            new Faction { Side = FactionType.Neutral },
            new Transform(),
            new Health { Max = 1f },
            new Signature { Value = 0.35f },
            new ClassificationProfile { Domain = ContactDomain.Munition },
            new Propulsion { MaxSpeedKmPerTick = 3.2f },
            new Projectile
            {
                Damage = 130f,
                DetonationRangeKm = 2f,
                Pk = 0.9f,
                RangeKm = 180f,
                TargetDomains = new List<ContactDomain> { ContactDomain.Surface },
                MotorBurnTicksRemaining = 55,
                DragPerTick = 0.04f,
            },
            new Seeker { Kind = SeekerType.ActiveRadar, AcquisitionRangeKm = 20f, FovDegrees = 50f, Datalink = true },
        }.AsReadOnly());

        prototypes.Register("lowSigWarship", "lowSigWarship", () => new List<IComponent>
        {
            new MetaData { EntityName = "lowSigWarship" },
            new Faction { Side = FactionType.Friendly },
            new Transform(),
            new Signature { Value = 0.3f },
            new ClassificationProfile { Domain = ContactDomain.Surface, TypeName = "TestPlatform" },
            new Sensor { RangeKm = 170f, MaxDetectProbability = 0.95f, FalloffExponent = 1.5f },
            new Health { Max = 1000000f },
        }.AsReadOnly());

        return prototypes;
    }

    [Test]
    public void AggressiveAi_AutoFires_AtPlayerHeldUnit()
    {
        ScenarioDefinition scenario = new() { Id = "mp" };
        scenario.Spawns.Add(new ScenarioSpawn
        {
            Proto = "aiRaider",
            Position = new List<float> { 40f, 0f },
            Velocity = new List<float> { 0f, 0f },
        });

        SimRuntime sim = new(scenario, BuildLibrary(), seed: 11);
        sim.SpawnPlayerUnit("alice", "warship", "AliceShip", Vector2.Zero);

        int fires = 0;
        sim.WeaponFired += _ => fires++;
        for (int i = 0; i < 150; i++)
        {
            sim.Step();
        }

        Assert.That(fires, Is.GreaterThan(0), "an aggressive hostile AI should auto-engage a player-held hostile unit");
    }

    [Test]
    public void AggressiveAi_EventuallyEngages_LowSignaturePlayerAtRange()
    {
        ScenarioDefinition scenario = new() { Id = "mp" };
        scenario.Spawns.Add(new ScenarioSpawn
        {
            Proto = "banditLike",
            Position = new List<float> { 90f, 20f },
            Velocity = new List<float> { 0f, 0f },
        });

        SimRuntime sim = new(scenario, BuildLibrary(), seed: 4);
        sim.SpawnPlayerUnit("alice", "lowSigWarship", "Legion", Vector2.Zero);

        int fires = 0;
        sim.WeaponFired += _ => fires++;
        for (int tick = 0; tick < 600; tick++)
        {
            sim.Step();
        }

        Assert.That(fires, Is.GreaterThan(0),
            "a low-signature player at 90km should still eventually be engaged by an aggressive AI");
    }
}
