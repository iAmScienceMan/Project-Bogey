using System;
using System.Collections.Generic;
using Content.Shared.Components;
using Content.Sim;
using Content.Sim.Systems;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class KinematicRangeTests
{
    private static float MaxReach(double dt)
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[]
            {
                TestScenarios.MissileProto(
                    "blind", damage: 100f, pk: 1f, speed: 6f, seekerRangeKm: 0.01f, rangeKm: 100f,
                    seeker: SeekerType.Ir, datalink: false, targetDomains: ContactDomain.Air),
            },
            seed: 12,
            config: new SimConfig { ObservationNoiseKm = 0f },
            dt,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Shooter",
                health: 300f, sensorRangeKm: 200f,
                weapons: new List<WeaponMount>
                {
                    TestScenarios.Mount("blind", cooldownTicks: 1000, magazine: 1),
                }),
            TestScenarios.CombatUnit(20f, 0f, FactionType.Hostile, ContactDomain.Air, "Bandit", health: 100000f, signature: 0.9f));

        float maxX = 0f;
        int steps = (int)Math.Ceiling(400 / dt);
        for (int i = 0; i < steps; i++)
        {
            sim.Step();
            foreach (MunitionDebug munition in sim.DumpMunitions())
            {
                maxX = MathF.Max(maxX, munition.Position.X);
            }
        }

        return maxX;
    }

    [Test]
    public void MissileReach_IsGovernedByRangeKm_AndIndependentOfTickRate()
    {
        float coarse = MaxReach(dt: 1.0);
        float fine = MaxReach(dt: 0.1);

        Assert.That(coarse, Is.EqualTo(130f).Within(8f),
            "a rangeKm=100 missile should coast to roughly rangeKm*margin before expiring");
        Assert.That(fine, Is.EqualTo(coarse).Within(8f),
            "kinematic reach must not shrink when the tick rate rises (the die-mid-way regression)");
    }
}
