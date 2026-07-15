using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Content.Shared.Components;
using Content.Shared.Prototypes;
using Content.Shared.Tracks;
using Content.Sim;
using Content.Sim.Content;
using Content.Sim.Systems;
using NUnit.Framework;

namespace Content.Tests;

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
                weapons: new List<WeaponMount>
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
    public void DatalinkSalvo_TargetDiesMidFlight_TrailingMissileFinishesAtLastDatum()
    {
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
            config: new SimConfig { ObservationNoiseKm = 0f },
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Shooter",
                health: 300f, sensorRangeKm: 250f,
                weapons: new List<WeaponMount>
                {
                    TestScenarios.Mount("arh", cooldownTicks: 3, magazine: 2),
                }),
            TestScenarios.CombatUnit(80f, 0f, FactionType.Hostile, ContactDomain.Air, "Bandit", health: 100f, signature: 0.9f));

        int bandit = EntityId(sim, "Bandit");
        bool sawMunition = false;
        bool banditDied = false;
        bool sawMunitionAfterDeath = false;
        float maxOvershoot = 0f;
        int finalMunitionCount = 0;
        string lastState = "none";

        for (int tick = 0; tick < 200; tick++)
        {
            sim.Step();

            banditDied |= sim.DumpGroundTruth().All(e => e.EntityId != bandit);

            finalMunitionCount = 0;
            foreach (MunitionDebug munition in sim.DumpMunitions())
            {
                sawMunition = true;
                finalMunitionCount++;
                lastState = $"pos=({munition.Position.X:0.0},{munition.Position.Y:0.0}) datum=({munition.Datum.X:0.0},{munition.Datum.Y:0.0}) passed={munition.DatumPassed} locked={munition.Locked}";
                if (banditDied)
                {
                    sawMunitionAfterDeath = true;
                    maxOvershoot = MathF.Max(maxOvershoot, munition.Position.X - munition.Datum.X);
                }
            }
        }

        Assert.That(sawMunition, Is.True, "the shooter never launched");
        Assert.That(banditDied, Is.True, "the bandit was never killed, so the scenario never armed");
        Assert.That(sawMunitionAfterDeath, Is.True, "no trailing missile was still in flight when the bandit died");
        Assert.That(maxOvershoot, Is.LessThan(5f),
            "a datalink missile whose target dies mid-flight should finish at its last datum, not sail past it");
        Assert.That(finalMunitionCount, Is.Zero, "the trailing missile should detonate and resolve, not orbit the ghost track forever. leftover: " + lastState);
    }

    [Test]
    public void MissileHeading_SlewsWithinTurnRate_AgainstNoisyCrossingTarget()
    {
        const float turnRate = 20f;
        float maxDeltaRad = (turnRate * (MathF.PI / 180f)) + 0.02f;

        SimRuntime sim = TestScenarios.BuildCombat(
            new[]
            {
                TestScenarios.MissileProto(
                    "arh", damage: 60f, pk: 1f, speed: 3f, seekerRangeKm: 4f, rangeKm: 250f,
                    detonationRangeKm: 2f, motorBurnTicks: 200, dragPerTick: 0f,
                    seeker: SeekerType.ActiveRadar, fovDegrees: 50f, datalink: true,
                    turnRateDegPerSecond: turnRate, targetDomains: ContactDomain.Air),
            },
            seed: 5,
            config: null,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Shooter",
                health: 300f, sensorRangeKm: 250f,
                weapons: new List<WeaponMount>
                {
                    TestScenarios.Mount("arh", cooldownTicks: 3, magazine: 1),
                }),
            TestScenarios.CombatUnit(
                60f, 40f, FactionType.Hostile, ContactDomain.Air, "Bandit",
                health: 100000f, signature: 0.9f, speed: 0.8f, vx: -0.6f, vy: -0.5f));

        var headings = new Dictionary<int, float>();
        float worstDelta = 0f;

        for (int tick = 0; tick < 120; tick++)
        {
            sim.Step();
            foreach (MunitionDebug munition in sim.DumpMunitions())
            {
                if (headings.TryGetValue(munition.Id, out float previous))
                {
                    float delta = MathF.Abs(WrapPi(munition.HeadingRadians - previous));
                    worstDelta = MathF.Max(worstDelta, delta);
                }

                headings[munition.Id] = munition.HeadingRadians;
            }
        }

        Assert.That(headings, Is.Not.Empty, "no missile was ever observed in flight");
        Assert.That(worstDelta, Is.LessThanOrEqualTo(maxDeltaRad),
            "a turn-rate-limited missile must never snap its heading, even chasing a jittery radar datum");
    }

    private static float WrapPi(float angle)
    {
        while (angle > MathF.PI)
        {
            angle -= 2f * MathF.PI;
        }

        while (angle < -MathF.PI)
        {
            angle += 2f * MathF.PI;
        }

        return angle;
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
