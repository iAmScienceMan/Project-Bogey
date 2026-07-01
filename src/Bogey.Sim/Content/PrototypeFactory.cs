using System;
using System.Collections.Generic;
using System.Numerics;
using Bogey.Shared.Components;
using Bogey.Shared.Prototypes;
using Bogey.Sim.Engine;

namespace Bogey.Sim.Content;

public static class PrototypeFactory
{
    public static int Spawn(EntityManager entities, PrototypeDefinition definition)
    {
        int entity = entities.CreateEntity();

        entities.AddComponent(entity, new Identity { Name = definition.Name });
        entities.AddComponent(entity, new Faction { Side = definition.Faction });

        if (definition.Transform is { } transformDef)
        {
            entities.AddComponent(entity, new Transform
            {
                Position = ToVector2(transformDef.Position, definition.Name, nameof(transformDef.Position)),
                Velocity = ToVector2(transformDef.Velocity, definition.Name, nameof(transformDef.Velocity)),
            });
        }

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

        return entity;
    }

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
