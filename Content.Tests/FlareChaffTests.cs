using System.Collections.Generic;
using System.Linq;
using Content.Shared.Components;
using Content.Sim;
using Content.Sim.Systems;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class FlareChaffTests
{
    private static bool VictimSurvives(int flares)
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[]
            {
                TestScenarios.MissileProto(
                    "irm", damage: 60f, pk: 1f, speed: 4f, seekerRangeKm: 16f, rangeKm: 120f,
                    detonationRangeKm: 2f, motorBurnTicks: 200, dragPerTick: 0f,
                    seeker: SeekerType.Ir, fovDegrees: 45f, datalink: false,
                    targetDomains: ContactDomain.Air),
                TestScenarios.DecoyProto("flare", DecoyKind.Flare),
                TestScenarios.DecoyProto("chaff", DecoyKind.Chaff),
            },
            seed: 9,
            config: new SimConfig { ObservationNoiseKm = 0f },
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Hostile, ContactDomain.Surface, "Shooter",
                health: 300f, sensorRangeKm: 250f,
                weapons: new List<WeaponMount>
                {
                    TestScenarios.Mount("irm", cooldownTicks: 4, magazine: 4),
                }),
            TestScenarios.CombatUnit(
                30f, 0f, FactionType.Friendly, ContactDomain.Air, "Victim",
                health: 100f, signature: 0.9f, vy: 0.5f,
                posture: WeaponPosture.Hold,
                countermeasures: new Countermeasures
                {
                    Flares = flares,
                    SalvoSize = 4,
                    CooldownTicks = 2,
                    FlareTriggerRangeKm = 16f,
                }));

        for (int tick = 0; tick < 120; tick++)
        {
            sim.Step();
        }

        return sim.DumpGroundTruth().Any(e => e.Name == "Victim");
    }

    [Test]
    public void FlaresSpoofIrMissiles_LettingTheVictimSurvive()
    {
        Assert.That(VictimSurvives(flares: 0), Is.False,
            "with no flares the IR missiles should track the aircraft and kill it (control case)");
        Assert.That(VictimSurvives(flares: 24), Is.True,
            "a jinking aircraft dumping flares should decoy the IR missiles and survive");
    }
}
