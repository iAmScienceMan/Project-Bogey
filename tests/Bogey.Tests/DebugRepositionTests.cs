using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Bogey.Shared.Components;
using Bogey.Shared.Prototypes;
using Bogey.Sim;
using NUnit.Framework;

namespace Bogey.Tests;

[TestFixture]
public sealed class DebugRepositionTests
{
    private static List<SpawnSpec> Scenario() => new()
    {
        TestScenarios.FriendlyMover(0f, 0f, maxSpeedKmPerTick: 0.4f),
        TestScenarios.Hostile(40f, 25f, 1.8f, 1.1f, 0.55f, ContactDomain.Air, "Wraith-class interceptor"),
    };

    [Test]
    public void DebugSetPosition_MovesTheAddressedEntity_IncludingHostiles()
    {
        SimRuntime sim = TestScenarios.Build(Scenario(), seed: 1337);

        
        GroundTruthEntry hostile = sim.DumpGroundTruth().First(e => e.Faction == FactionType.Hostile);
        Vector2 target = new(-999f, 42f);

        bool moved = sim.DebugSetPosition(hostile.EntityId, target);

        GroundTruthEntry after = sim.DumpGroundTruth().Single(e => e.EntityId == hostile.EntityId);
        Assert.Multiple(() =>
        {
            Assert.That(moved, Is.True);
            Assert.That(after.Position.X, Is.EqualTo(target.X).Within(1e-3));
            Assert.That(after.Position.Y, Is.EqualTo(target.Y).Within(1e-3));
        });
    }

    [Test]
    public void DebugSetPosition_ReturnsFalse_ForUnknownEntity()
    {
        SimRuntime sim = TestScenarios.Build(Scenario(), seed: 1337);
        int unknown = sim.DumpGroundTruth().Max(e => e.EntityId) + 1000;

        Assert.That(sim.DebugSetPosition(unknown, Vector2.Zero), Is.False);
    }
}
