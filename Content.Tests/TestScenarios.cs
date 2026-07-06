using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Content.Shared.Components;
using Content.Shared.Prototypes;
using Content.Shared.Tracks;
using Content.Sim;
using Content.Sim.Systems;

namespace Content.Tests;

internal sealed record SpawnSpec(PrototypeDefinition Proto, Vector2 Position, Vector2 Velocity);

internal static class TestScenarios
{
    public static SpawnSpec FriendlySensorAtOrigin(float rangeKm, float maxDetect, float falloff = 1.0f)
        => new(
            new PrototypeDefinition
            {
                Name = "Sensor",
                Faction = FactionType.Friendly,
                Sensor = new SensorDef { RangeKm = rangeKm, MaxDetectProbability = maxDetect, FalloffExponent = falloff },
            },
            Vector2.Zero,
            Vector2.Zero);

    public static SpawnSpec FriendlyMover(float x, float y, float maxSpeedKmPerTick, string name = "Mover")
        => new(
            new PrototypeDefinition
            {
                Name = name,
                Faction = FactionType.Friendly,
                Propulsion = new PropulsionDef { MaxSpeedKmPerTick = maxSpeedKmPerTick },
            },
            new Vector2(x, y),
            Vector2.Zero);

    public static SpawnSpec Hostile(
        float x, float y, float vx, float vy, float signature, ContactDomain domain, string typeName)
        => new(
            new PrototypeDefinition
            {
                Name = typeName,
                Faction = FactionType.Hostile,
                Signature = signature,
                Classification = new ClassificationDef { Domain = domain, TypeName = typeName },
            },
            new Vector2(x, y),
            new Vector2(vx, vy));

    public static PrototypeDefinition MissileProto(
        string id,
        float damage,
        float pk,
        float speed = 3f,
        float seekerRangeKm = 12f,
        float detonationRangeKm = 2f,
        float rangeKm = 200f,
        int motorBurnTicks = 10,
        float dragPerTick = 0.05f,
        float signature = 0.15f,
        SeekerType seeker = SeekerType.ActiveRadar,
        float fovDegrees = 360f,
        bool datalink = true,
        bool targetsMunitions = false,
        params ContactDomain[] targetDomains)
        => new()
        {
            Id = id,
            Name = id,
            Faction = FactionType.Neutral,
            Signature = signature,
            Classification = new ClassificationDef { Domain = ContactDomain.Munition, TypeName = "Missile" },
            Health = new HealthDef { Max = 1f },
            Propulsion = new PropulsionDef { MaxSpeedKmPerTick = speed },
            Projectile = new ProjectileDef
            {
                Damage = damage,
                DetonationRangeKm = detonationRangeKm,
                Pk = pk,
                RangeKm = rangeKm,
                TargetDomains = new List<ContactDomain>(targetDomains),
                MotorBurnTicks = motorBurnTicks,
                DragPerTick = dragPerTick,
                Seeker = new SeekerDef
                {
                    Type = seeker,
                    AcquisitionRangeKm = seekerRangeKm,
                    FovDegrees = fovDegrees,
                    Datalink = datalink,
                    TargetsMunitions = targetsMunitions,
                },
            },
        };

    public static WeaponMountDef Mount(string projectile, int cooldownTicks, int magazine)
        => new()
        {
            ProjectilePrototype = projectile,
            CooldownTicks = cooldownTicks,
            Magazine = magazine,
        };

    public static WeaponMountDef PointDefenseMount(float rangeKm, int cooldownTicks, int magazine, float pk)
        => new()
        {
            RangeKm = rangeKm,
            CooldownTicks = cooldownTicks,
            Magazine = magazine,
            PointDefense = true,
            PointDefensePk = pk,
        };

    public static SpawnSpec CombatUnit(
        float x, float y,
        FactionType side,
        ContactDomain domain,
        string typeName,
        float health,
        float signature = 0.6f,
        float sensorRangeKm = 0f,
        float speed = 0f,
        List<WeaponMountDef>? weapons = null,
        float vx = 0f, float vy = 0f,
        WeaponPosture posture = WeaponPosture.Free,
        AiBehavior? ai = null)
    {
        PrototypeDefinition proto = new()
        {
            Name = typeName,
            Faction = side,
            Signature = signature,
            Classification = new ClassificationDef { Domain = domain, TypeName = typeName },
            Health = new HealthDef { Max = health },
            Weapons = weapons,
            Posture = posture,
            Ai = ai is { } behavior ? new AiDef { Behavior = behavior } : null,
        };

        if (sensorRangeKm > 0f)
        {
            proto.Sensor = new SensorDef { RangeKm = sensorRangeKm, MaxDetectProbability = 0.99f, FalloffExponent = 1f };
        }

        if (speed > 0f)
        {
            proto.Propulsion = new PropulsionDef { MaxSpeedKmPerTick = speed };
        }

        return new SpawnSpec(proto, new Vector2(x, y), new Vector2(vx, vy));
    }

    public static SimRuntime BuildCombat(
        IEnumerable<PrototypeDefinition> library, int seed, SimConfig? config, params SpawnSpec[] specs)
    {
        Dictionary<string, PrototypeDefinition> prototypes = new(StringComparer.Ordinal);
        foreach (PrototypeDefinition lib in library)
        {
            prototypes[lib.Id] = lib;
        }

        ScenarioDefinition scenario = new() { Id = "combat" };

        int index = 0;
        foreach (SpawnSpec spec in specs)
        {
            string id = "u" + index.ToString(CultureInfo.InvariantCulture);
            index++;

            spec.Proto.Id = id;
            prototypes[id] = spec.Proto;
            scenario.Spawns.Add(new ScenarioSpawn
            {
                Proto = id,
                Position = new List<float> { spec.Position.X, spec.Position.Y },
                Velocity = new List<float> { spec.Velocity.X, spec.Velocity.Y },
            });
        }

        return new SimRuntime(scenario, prototypes, seed, config);
    }

    public static SimRuntime Build(int seed, SimConfig? config, params SpawnSpec[] specs)
        => Build(specs, seed, config);

    public static SimRuntime Build(IEnumerable<SpawnSpec> specs, int seed, SimConfig? config = null)
    {
        Dictionary<string, PrototypeDefinition> prototypes = new(StringComparer.Ordinal);
        ScenarioDefinition scenario = new() { Id = "test" };

        int index = 0;
        foreach (SpawnSpec spec in specs)
        {
            string id = "e" + index.ToString(CultureInfo.InvariantCulture);
            index++;

            spec.Proto.Id = id;
            prototypes[id] = spec.Proto;
            scenario.Spawns.Add(new ScenarioSpawn
            {
                Proto = id,
                Position = new List<float> { spec.Position.X, spec.Position.Y },
                Velocity = new List<float> { spec.Velocity.X, spec.Velocity.Y },
            });
        }

        return new SimRuntime(scenario, prototypes, seed, config);
    }

    public static List<TrackPictureSnapshot> Run(
        IEnumerable<SpawnSpec> specs, int seed, int ticks, SimConfig? config = null)
    {
        SimRuntime sim = Build(specs, seed, config);
        List<TrackPictureSnapshot> history = new(ticks);

        for (int i = 0; i < ticks; i++)
        {
            sim.Step();
            history.Add(sim.PublishSnapshot());
        }

        return history;
    }
}
