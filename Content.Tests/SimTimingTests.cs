using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Components;
using Content.Shared.Prototypes;
using Content.Sim;
using Lattice.Sim.Engine;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class SimTimingTests
{
    private static Vector2 MoverPositionAfter(double dt, int steps)
    {
        PrototypeManager prototypes = new(new ComponentFactory(new[] { typeof(Sensor).Assembly }));
        prototypes.Register("mover", "mover", () => new List<IComponent>
        {
            new MetaData { EntityName = "mover" },
            new Faction { Side = FactionType.Hostile },
            new Transform(),
        }.AsReadOnly());

        ScenarioDefinition scenario = new() { Id = "t" };
        scenario.Spawns.Add(new ScenarioSpawn
        {
            Proto = "mover",
            Position = new List<float> { 0f, 0f },
            Velocity = new List<float> { 2f, 0f },
        });

        SimRuntime sim = new(scenario, prototypes, seed: 1, config: null, logManager: null, dt: dt);
        for (int i = 0; i < steps; i++)
        {
            sim.Step();
        }

        foreach (GroundTruthEntry entry in sim.DumpGroundTruth())
        {
            if (entry.Name == "mover")
            {
                return entry.Position;
            }
        }

        return Vector2.Zero;
    }

    [Test]
    public void MovementDisplacement_DependsOnSimTime_NotTickRate()
    {
        Vector2 coarse = MoverPositionAfter(dt: 1.0, steps: 10);
        Vector2 fine = MoverPositionAfter(dt: 0.25, steps: 40);

        Assert.That(coarse.X, Is.EqualTo(20f).Within(1e-3f),
            "10 sim-seconds at 2 km/s should travel 20km");
        Assert.That(fine.X, Is.EqualTo(coarse.X).Within(1e-3f),
            "the same sim-time at a finer dt (higher tickRate) must travel the same distance");
    }
}
