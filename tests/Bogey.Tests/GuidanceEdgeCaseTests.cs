using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Bogey.Shared.Components;
using Bogey.Shared.Prototypes;
using Bogey.Shared.Tracks;
using Bogey.Sim;
using Bogey.Sim.Content;
using NUnit.Framework;

namespace Bogey.Tests;

[TestFixture]
public sealed class GuidanceEdgeCaseTests
{
    [Test]
    public void Burst_DestroyingAnotherInFlightMunition_DoesNotThrow()
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[]
            {
                TestScenarios.MissileProto(
                    "flak", damage: 30f, pk: 1f, speed: 5f, detonationRangeKm: 12f,
                    seeker: SeekerType.ActiveRadar, seekerRangeKm: 30f, datalink: true,
                    targetsMunitions: true, targetDomains: ContactDomain.Air),
            },
            seed: 3,
            config: null,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Shooter",
                health: 300f, sensorRangeKm: 300f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("flak", cooldownTicks: 1, magazine: 40),
                }),
            TestScenarios.CombatUnit(60f, 0f, FactionType.Hostile, ContactDomain.Air, "Bandit", health: 100000f, signature: 0.9f));

        Assert.DoesNotThrow(() =>
        {
            for (int tick = 0; tick < 200; tick++)
            {
                sim.Step();
            }
        }, "one munition's burst destroying another mid-tick must not crash guidance");
    }

    [Test]
    public void DatalinkSalvo_TargetDiesMidFlight_TrailingMissileFliesPastInsteadOfCircling()
    {
        // A salvo of datalink missiles: the first kills the low-health bandit while the trailing
        // rounds are still en route. The shooter's lock lingers on the (now destroyed) entity and
        // its track keeps decaying on the plot, but the trailing missiles must go inertial and fly
        // straight past their last datum instead of orbiting the ghost track.
        SimRuntime sim = TestScenarios.BuildCombat(
            new[]
            {
                TestScenarios.MissileProto(
                    "arh", damage: 500f, pk: 1f, speed: 4f, seekerRangeKm: 3f, rangeKm: 250f,
                    detonationRangeKm: 2f, motorBurnTicks: 200, dragPerTick: 0f,
                    seeker: SeekerType.ActiveRadar, fovDegrees: 50f, datalink: true,
                    targetDomains: ContactDomain.Air),
            },
            seed: 8,
            config: null,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Shooter",
                health: 300f, sensorRangeKm: 250f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("arh", cooldownTicks: 3, magazine: 2),
                }),
            TestScenarios.CombatUnit(80f, 0f, FactionType.Hostile, ContactDomain.Air, "Bandit", health: 100f, signature: 0.9f));

        int bandit = EntityId(sim, "Bandit");
        bool sawMunition = false;
        bool banditDied = false;
        float maxOvershoot = 0f;

        for (int tick = 0; tick < 200; tick++)
        {
            sim.Step();

            banditDied |= sim.DumpGroundTruth().All(e => e.EntityId != bandit);

            foreach (MunitionDebug munition in sim.DumpMunitions())
            {
                sawMunition = true;
                if (banditDied)
                {
                    maxOvershoot = MathF.Max(maxOvershoot, munition.Position.X - munition.Datum.X);
                }
            }
        }

        Assert.That(sawMunition, Is.True, "the shooter never launched");
        Assert.That(banditDied, Is.True, "the bandit was never killed, so the scenario never armed");
        Assert.That(maxOvershoot, Is.GreaterThan(10f),
            "a datalink missile whose target dies should fly straight past its last datum, not orbit it");
    }

    private static int EntityId(SimRuntime sim, string name)
    {
        foreach (GroundTruthEntry entry in sim.DumpGroundTruth())
        {
            if (entry.Name == name)
            {
                return entry.EntityId;
            }
        }

        return -1;
    }
}
