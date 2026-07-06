using System.Numerics;
using Content.Shared.Components;
using Content.Shared.Prototypes;
using Content.Sim;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class OrderingTests
{
    private const float Tolerance = 1e-3f;

    [Test]
    public void OrderedUnit_SteersTowardWaypoint()
    {
        SimRuntime sim = TestScenarios.Build(seed: 1, config: null, TestScenarios.FriendlyMover(0f, 0f, 1f));

        Assert.That(sim.IssueMoveOrder("Mover", new Vector2(3f, 4f)), Is.True);
        sim.Step();

        
        Vector2 position = sim.PublishSnapshot().OwnUnits[0].Position;
        Assert.That(position.X, Is.EqualTo(0.6f).Within(Tolerance));
        Assert.That(position.Y, Is.EqualTo(0.8f).Within(Tolerance));
    }

    [Test]
    public void OrderedUnit_StopsNearWaypointAndHolds()
    {
        Vector2 waypoint = new(5f, 0f);
        SimRuntime sim = TestScenarios.Build(seed: 1, config: null, TestScenarios.FriendlyMover(0f, 0f, 1f));
        sim.IssueMoveOrder("Mover", waypoint);

        for (int i = 0; i < 10; i++)
        {
            sim.Step();
        }

        Vector2 arrived = sim.PublishSnapshot().OwnUnits[0].Position;
        
        Assert.That((arrived - waypoint).Length(), Is.LessThanOrEqualTo(1f));

        
        sim.Step();
        Vector2 afterHold = sim.PublishSnapshot().OwnUnits[0].Position;
        Assert.That(afterHold, Is.EqualTo(arrived));
    }

    [Test]
    public void FastUnit_ReachesWaypointCloserThanOneTickOfTravel()
    {
        Vector2 waypoint = new(4f, 0f);
        SimRuntime sim = TestScenarios.Build(seed: 1, config: null, TestScenarios.FriendlyMover(0f, 0f, 10f));
        sim.IssueMoveOrder("Mover", waypoint);

        for (int i = 0; i < 5; i++)
        {
            sim.Step();
        }

        Vector2 arrived = sim.PublishSnapshot().OwnUnits[0].Position;
        Assert.That((arrived - waypoint).Length(), Is.LessThanOrEqualTo(Tolerance),
            "a fast unit must still be able to travel less than one tick's worth of distance");

        sim.Step();
        Assert.That(sim.PublishSnapshot().OwnUnits[0].Position, Is.EqualTo(arrived), "the unit must hold at the waypoint");
    }

    [Test]
    public void UnorderedMover_StaysPut()
    {
        SimRuntime sim = TestScenarios.Build(seed: 1, config: null, TestScenarios.FriendlyMover(10f, -10f, 1f));

        for (int i = 0; i < 5; i++)
        {
            sim.Step();
        }

        Assert.That(sim.PublishSnapshot().OwnUnits[0].Position, Is.EqualTo(new Vector2(10f, -10f)));
    }

    [Test]
    public void IssueMoveOrder_RejectsUnknownAndUnmovableUnits()
    {
        SpawnSpec stationary = new(
            new PrototypeDefinition { Name = "Tower", Faction = FactionType.Friendly },
            Vector2.Zero,
            Vector2.Zero);
        SimRuntime sim = TestScenarios.Build(seed: 1, config: null, stationary, TestScenarios.FriendlyMover(0f, 0f, 1f));

        Assert.Multiple(() =>
        {
            Assert.That(sim.IssueMoveOrder("Ghost", new Vector2(1f, 1f)), Is.False, "unknown unit");
            Assert.That(sim.IssueMoveOrder("Tower", new Vector2(1f, 1f)), Is.False, "no propulsion");
            Assert.That(sim.IssueMoveOrder("Mover", new Vector2(1f, 1f)), Is.True, "movable friendly");
        });
    }

    [Test]
    public void IssueMoveOrder_DoesNotCommandHostiles()
    {
        SpawnSpec hostileMover = new(
            new PrototypeDefinition
            {
                Name = "Bandit",
                Faction = FactionType.Hostile,
                Propulsion = new PropulsionDef { MaxSpeedKmPerTick = 1f },
            },
            Vector2.Zero,
            Vector2.Zero);
        SimRuntime sim = TestScenarios.Build(seed: 1, config: null, hostileMover);

        Assert.That(sim.IssueMoveOrder("Bandit", new Vector2(5f, 5f)), Is.False,
            "the commander can only order their own side.");
    }
}
