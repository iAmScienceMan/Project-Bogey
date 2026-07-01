using System.Numerics;
using Bogey.Shared.Components;
using Bogey.Shared.Prototypes;
using Bogey.Sim;
using NUnit.Framework;

namespace Bogey.Tests;

[TestFixture]
public sealed class OrderingTests
{
    private const float Tolerance = 1e-3f;

    [Test]
    public void OrderedUnit_SteersTowardWaypoint()
    {
        SimRuntime sim = new(new[] { TestScenarios.FriendlyMover(0f, 0f, 1f) }, seed: 1);

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
        SimRuntime sim = new(new[] { TestScenarios.FriendlyMover(0f, 0f, 1f) }, seed: 1);
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
    public void UnorderedMover_StaysPut()
    {
        SimRuntime sim = new(new[] { TestScenarios.FriendlyMover(10f, -10f, 1f) }, seed: 1);

        for (int i = 0; i < 5; i++)
        {
            sim.Step();
        }

        Assert.That(sim.PublishSnapshot().OwnUnits[0].Position, Is.EqualTo(new Vector2(10f, -10f)));
    }

    [Test]
    public void IssueMoveOrder_RejectsUnknownAndUnmovableUnits()
    {
        PrototypeDefinition stationary = new()
        {
            Name = "Tower",
            Faction = FactionType.Friendly,
            Transform = new TransformDef { Position = new() { 0f, 0f }, Velocity = new() { 0f, 0f } },
        };
        SimRuntime sim = new(new[] { stationary, TestScenarios.FriendlyMover(0f, 0f, 1f) }, seed: 1);

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
        PrototypeDefinition hostileMover = new()
        {
            Name = "Bandit",
            Faction = FactionType.Hostile,
            Transform = new TransformDef { Position = new() { 0f, 0f }, Velocity = new() { 0f, 0f } },
            Propulsion = new PropulsionDef { MaxSpeedKmPerTick = 1f },
        };
        SimRuntime sim = new(new[] { hostileMover }, seed: 1);

        Assert.That(sim.IssueMoveOrder("Bandit", new Vector2(5f, 5f)), Is.False,
            "the commander can only order their own side.");
    }
}
