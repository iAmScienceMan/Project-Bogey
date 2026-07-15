using System.Linq;
using System.Numerics;
using Content.Shared.Components;
using Content.Sim;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class AiStandoffTests
{
    [Test]
    public void AggressiveAi_ClosesToStandoff_WithoutRammingTarget()
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            System.Array.Empty<ProtoSpec>(),
            seed: 4,
            config: null,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Hunter",
                health: 300f, sensorRangeKm: 400f, speed: 5f, ai: AiBehavior.Aggressive),
            TestScenarios.CombatUnit(
                200f, 0f, FactionType.Hostile, ContactDomain.Surface, "Prey",
                health: 300f, signature: 0.9f));

        for (int tick = 0; tick < 200; tick++)
        {
            sim.Step();
        }

        Vector2 hunter = sim.PublishSnapshot().OwnUnits.Single(u => u.Name == "Hunter").Position;
        float distance = Vector2.Distance(hunter, new Vector2(200f, 0f));

        Assert.That(distance, Is.GreaterThan(40f), "the AI drove far inside its standoff range and rammed the target");
        Assert.That(distance, Is.LessThan(90f), "the AI never closed to its standoff range");
    }
}
