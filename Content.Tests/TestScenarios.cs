using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Shared.Components;
using Content.Shared.Prototypes;
using Content.Shared.Tracks;
using Content.Sim;
using Content.Sim.Systems;
using Lattice.Sim.Engine;

namespace Content.Tests;

internal sealed record SpawnSpec(Func<List<IComponent>> Build, Vector2 Position, Vector2 Velocity);

internal sealed record ProtoSpec(string Id, Func<List<IComponent>> Build);

internal static class TestScenarios
{
    private static PrototypeManager NewManager()
        => new(new ComponentFactory(new[] { typeof(Sensor).Assembly }));

    public static SpawnSpec FriendlySensorAtOrigin(float rangeKm, float maxDetect, float falloff = 1.0f)
        => new(
            () => new List<IComponent>
            {
                new MetaData { EntityName = "Sensor" },
                new Faction { Side = FactionType.Friendly },
                new Transform(),
                new Sensor { RangeKm = rangeKm, MaxDetectProbability = maxDetect, FalloffExponent = falloff },
            },
            Vector2.Zero,
            Vector2.Zero);

    public static SpawnSpec FriendlyMover(float x, float y, float maxSpeedKmPerTick, string name = "Mover")
        => new(
            () => new List<IComponent>
            {
                new MetaData { EntityName = name },
                new Faction { Side = FactionType.Friendly },
                new Transform(),
                new Propulsion { MaxSpeedKmPerTick = maxSpeedKmPerTick },
            },
            new Vector2(x, y),
            Vector2.Zero);

    public static SpawnSpec Hostile(
        float x, float y, float vx, float vy, float signature, ContactDomain domain, string typeName)
        => new(
            () => new List<IComponent>
            {
                new MetaData { EntityName = typeName },
                new Faction { Side = FactionType.Hostile },
                new Transform(),
                new Signature { Value = signature },
                new ClassificationProfile { Domain = domain, TypeName = typeName },
            },
            new Vector2(x, y),
            new Vector2(vx, vy));

    public static ProtoSpec MissileProto(
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
        float turnRateDegPerSecond = 90f,
        params ContactDomain[] targetDomains)
        => new(
            id,
            () => new List<IComponent>
            {
                new MetaData { EntityName = id },
                new Faction { Side = FactionType.Neutral },
                new Transform(),
                new Signature { Value = signature },
                new ClassificationProfile { Domain = ContactDomain.Munition, TypeName = "Missile" },
                new Health { Max = 1f },
                new Propulsion { MaxSpeedKmPerTick = speed, MaxTurnRateDegPerSecond = turnRateDegPerSecond },
                new Projectile
                {
                    Damage = damage,
                    DetonationRangeKm = detonationRangeKm,
                    Pk = pk,
                    RangeKm = rangeKm,
                    TargetDomains = new List<ContactDomain>(targetDomains),
                    MotorBurnTicksRemaining = motorBurnTicks,
                    DragPerTick = dragPerTick,
                },
                new Seeker
                {
                    Kind = seeker,
                    AcquisitionRangeKm = seekerRangeKm,
                    FovDegrees = fovDegrees,
                    Datalink = datalink,
                    TargetsMunitions = targetsMunitions,
                },
            });

    public static WeaponMount Mount(string projectile, int cooldownTicks, int magazine)
        => new()
        {
            ProjectilePrototype = projectile,
            CooldownTicks = cooldownTicks,
            MagazineCapacity = magazine,
        };

    public static WeaponMount PointDefenseMount(float rangeKm, int cooldownTicks, int magazine, float pk)
        => new()
        {
            PointDefenseRangeKm = rangeKm,
            CooldownTicks = cooldownTicks,
            MagazineCapacity = magazine,
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
        List<WeaponMount>? weapons = null,
        float vx = 0f, float vy = 0f,
        WeaponPosture posture = WeaponPosture.Free,
        AiBehavior? ai = null)
        => new(
            () =>
            {
                List<IComponent> components = new()
                {
                    new MetaData { EntityName = typeName },
                    new Faction { Side = side },
                    new Transform(),
                    new Signature { Value = signature },
                    new ClassificationProfile { Domain = domain, TypeName = typeName },
                    new Health { Max = health },
                };

                if (sensorRangeKm > 0f)
                {
                    components.Add(new Sensor
                    {
                        RangeKm = sensorRangeKm,
                        MaxDetectProbability = 0.99f,
                        FalloffExponent = 1f,
                    });
                }

                if (speed > 0f)
                {
                    components.Add(new Propulsion { MaxSpeedKmPerTick = speed });
                }

                if (weapons is { Count: > 0 })
                {
                    components.Add(new Loadout { Mounts = weapons.Select(CloneMount).ToList() });
                    components.Add(new WeaponControl { Posture = posture });
                }

                if (ai is { } behavior)
                {
                    components.Add(new Ai { Behavior = behavior });
                }

                return components;
            },
            new Vector2(x, y),
            new Vector2(vx, vy));

    public static SimRuntime BuildCombat(
        IEnumerable<ProtoSpec> library, int seed, SimConfig? config, params SpawnSpec[] specs)
    {
        PrototypeManager prototypes = NewManager();
        foreach (ProtoSpec lib in library)
        {
            prototypes.Register(lib.Id, lib.Id, () => lib.Build().AsReadOnly());
        }

        ScenarioDefinition scenario = new() { Id = "combat" };
        AddSpawns(prototypes, scenario, specs, "u");
        return new SimRuntime(scenario, prototypes, seed, config);
    }

    public static SimRuntime Build(int seed, SimConfig? config, params SpawnSpec[] specs)
        => Build(specs, seed, config);

    public static SimRuntime Build(IEnumerable<SpawnSpec> specs, int seed, SimConfig? config = null)
    {
        PrototypeManager prototypes = NewManager();
        ScenarioDefinition scenario = new() { Id = "test" };
        AddSpawns(prototypes, scenario, specs, "e");
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

    private static void AddSpawns(
        PrototypeManager prototypes, ScenarioDefinition scenario, IEnumerable<SpawnSpec> specs, string prefix)
    {
        int index = 0;
        foreach (SpawnSpec spec in specs)
        {
            string id = prefix + index.ToString(CultureInfo.InvariantCulture);
            index++;

            prototypes.Register(id, id, () => spec.Build().AsReadOnly());
            scenario.Spawns.Add(new ScenarioSpawn
            {
                Proto = id,
                Position = new List<float> { spec.Position.X, spec.Position.Y },
                Velocity = new List<float> { spec.Velocity.X, spec.Velocity.Y },
            });
        }
    }

    private static WeaponMount CloneMount(WeaponMount mount)
        => new()
        {
            ProjectilePrototype = mount.ProjectilePrototype,
            CooldownTicks = mount.CooldownTicks,
            MagazineCapacity = mount.MagazineCapacity,
            PointDefense = mount.PointDefense,
            PointDefensePk = mount.PointDefensePk,
            PointDefenseRangeKm = mount.PointDefenseRangeKm,
        };
}
