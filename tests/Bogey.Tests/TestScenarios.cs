using System.Collections.Generic;
using Bogey.Shared.Components;
using Bogey.Shared.Prototypes;
using Bogey.Shared.Tracks;
using Bogey.Sim;
using Bogey.Sim.Systems;

namespace Bogey.Tests;


internal static class TestScenarios
{
    public static PrototypeDefinition FriendlySensorAtOrigin(float rangeKm, float maxDetect, float falloff = 1.0f)
        => new()
        {
            Name = "Sensor",
            Faction = FactionType.Friendly,
            Transform = new TransformDef { Position = new() { 0f, 0f }, Velocity = new() { 0f, 0f } },
            Sensor = new SensorDef { RangeKm = rangeKm, MaxDetectProbability = maxDetect, FalloffExponent = falloff },
        };

    public static PrototypeDefinition FriendlyMover(float x, float y, float maxSpeedKmPerTick, string name = "Mover")
        => new()
        {
            Name = name,
            Faction = FactionType.Friendly,
            Transform = new TransformDef { Position = new() { x, y }, Velocity = new() { 0f, 0f } },
            Propulsion = new PropulsionDef { MaxSpeedKmPerTick = maxSpeedKmPerTick },
        };

    public static PrototypeDefinition Hostile(
        float x, float y, float vx, float vy, float signature, ContactDomain domain, string typeName)
        => new()
        {
            Name = typeName,
            Faction = FactionType.Hostile,
            Transform = new TransformDef { Position = new() { x, y }, Velocity = new() { vx, vy } },
            Signature = signature,
            Classification = new ClassificationDef { Domain = domain, TypeName = typeName },
        };

    
    public static List<TrackPictureSnapshot> Run(
        IEnumerable<PrototypeDefinition> prototypes, int seed, int ticks, SimConfig? config = null)
    {
        SimRuntime sim = new(prototypes, seed, config);
        List<TrackPictureSnapshot> history = new(ticks);

        for (int i = 0; i < ticks; i++)
        {
            sim.Step();
            history.Add(sim.PublishSnapshot());
        }

        return history;
    }
}
