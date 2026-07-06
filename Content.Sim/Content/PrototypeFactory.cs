using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Components;
using Content.Shared.Prototypes;
using Lattice.Sim.Engine;

namespace Content.Sim.Content;

public readonly record struct Placement(Vector2 Position, Vector2 Velocity, string? Name = null);

public static class PrototypeFactory
{
    public static int Spawn(EntityManager entities, PrototypeDefinition definition, Placement placement)
    {
        int entity = entities.CreateEntity();

        entities.AddComponent(entity, new Identity { Name = placement.Name ?? definition.Name });
        entities.AddComponent(entity, new Faction { Side = definition.Faction });
        entities.AddComponent(entity, new Transform
        {
            Position = placement.Position,
            Velocity = placement.Velocity,
        });

        if (definition.Signature is { } signature)
        {
            entities.AddComponent(entity, new Signature { Value = signature });
        }

        if (definition.Sensor is { } sensorDef)
        {
            entities.AddComponent(entity, new Sensor
            {
                RangeKm = sensorDef.RangeKm,
                MaxDetectProbability = sensorDef.MaxDetectProbability,
                FalloffExponent = sensorDef.FalloffExponent,
            });
        }

        if (definition.Classification is { } classificationDef)
        {
            entities.AddComponent(entity, new ClassificationProfile
            {
                Domain = classificationDef.Domain,
                TypeName = classificationDef.TypeName,
            });
        }

        if (definition.Propulsion is { } propulsionDef)
        {
            entities.AddComponent(entity, new Propulsion
            {
                MaxSpeedKmPerTick = propulsionDef.MaxSpeedKmPerTick,
            });
        }

        if (definition.Health is { } healthDef)
        {
            entities.AddComponent(entity, new Health
            {
                Max = healthDef.Max,
                Current = healthDef.Max,
            });
        }

        if (definition.Weapons is { Count: > 0 } weaponDefs)
        {
            Loadout loadout = new();
            foreach (WeaponMountDef mountDef in weaponDefs)
            {
                loadout.Mounts.Add(new WeaponMount
                {
                    ProjectilePrototype = mountDef.ProjectilePrototype,
                    CooldownTicks = mountDef.CooldownTicks,
                    MagazineCapacity = mountDef.Magazine,
                    RoundsRemaining = mountDef.Magazine,
                    PointDefense = mountDef.PointDefense,
                    PointDefensePk = mountDef.PointDefensePk,
                    PointDefenseRangeKm = mountDef.RangeKm,
                });
            }

            entities.AddComponent(entity, loadout);
            entities.AddComponent(entity, new WeaponControl
            {
                Posture = definition.Posture ?? DefaultPosture(definition.Faction),
            });
        }

        if (definition.Projectile is { } projectileDef)
        {
            float launchSpeed = definition.Propulsion?.MaxSpeedKmPerTick ?? 0f;
            entities.AddComponent(entity, new Projectile
            {
                Damage = projectileDef.Damage,
                DetonationRangeKm = projectileDef.DetonationRangeKm,
                Pk = projectileDef.Pk,
                RangeKm = projectileDef.RangeKm,
                TargetDomains = new List<ContactDomain>(projectileDef.TargetDomains),
                MotorBurnTicksRemaining = projectileDef.MotorBurnTicks,
                DragPerTick = projectileDef.DragPerTick,
                SpeedKmPerTick = launchSpeed,
                BurnoutSpeedKmPerTick = launchSpeed * 0.25f,
            });

            SeekerDef seekerDef = projectileDef.Seeker ?? new SeekerDef();
            entities.AddComponent(entity, new Seeker
            {
                Type = seekerDef.Type,
                AcquisitionRangeKm = seekerDef.AcquisitionRangeKm,
                FovDegrees = seekerDef.FovDegrees,
                Datalink = seekerDef.Datalink,
                TargetsMunitions = seekerDef.TargetsMunitions,
            });
        }

        if (definition.Ai is { } aiDef)
        {
            entities.AddComponent(entity, new Ai { Behavior = aiDef.Behavior });
        }

        return entity;
    }

    private static WeaponPosture DefaultPosture(FactionType faction) => faction switch
    {
        FactionType.Friendly => WeaponPosture.Defensive,
        FactionType.Hostile => WeaponPosture.Free,
        _ => WeaponPosture.Hold,
    };

    public static Placement PlacementFor(ScenarioSpawn spawn)
        => new(
            ToVector2(spawn.Position, spawn.Proto, nameof(spawn.Position)),
            spawn.Velocity.Count == 0 ? Vector2.Zero : ToVector2(spawn.Velocity, spawn.Proto, nameof(spawn.Velocity)),
            spawn.Name);

    private static Vector2 ToVector2(IReadOnlyList<float> values, string prototypeName, string field)
    {
        if (values.Count < 2)
        {
            throw new InvalidOperationException(
                $"Prototype '{prototypeName}' field '{field}' must list two numbers [x, y].");
        }

        return new Vector2(values[0], values[1]);
    }
}
