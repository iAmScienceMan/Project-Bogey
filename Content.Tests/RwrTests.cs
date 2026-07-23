using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.Components;
using Content.Shared.Tracks;
using Content.Sim;
using Content.Sim.Systems;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class RwrTests
{
    [Test]
    public void OwnUnit_UnderActiveRadarMissile_ReportsMissileThreat()
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[]
            {
                TestScenarios.MissileProto(
                    "arh", damage: 60f, pk: 1f, speed: 3f, seekerRangeKm: 40f, rangeKm: 250f,
                    detonationRangeKm: 2f, motorBurnTicks: 200, dragPerTick: 0f,
                    seeker: SeekerType.ActiveRadar, fovDegrees: 60f, datalink: true,
                    targetDomains: ContactDomain.Air),
            },
            seed: 6,
            config: new SimConfig { ObservationNoiseKm = 0f },
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Hostile, ContactDomain.Surface, "Shooter",
                health: 300f, sensorRangeKm: 250f,
                weapons: new List<WeaponMount>
                {
                    TestScenarios.Mount("arh", cooldownTicks: 3, magazine: 1),
                }),
            TestScenarios.CombatUnit(
                40f, 0f, FactionType.Friendly, ContactDomain.Air, "Victim",
                health: 100000f, signature: 0.9f));

        RwrThreat worst = RwrThreat.None;
        for (int tick = 0; tick < 60; tick++)
        {
            sim.Step();
            OwnUnitView? victim = sim.PublishSnapshot("friendly").OwnUnits.FirstOrDefault(u => u.Name == "Victim");
            if (victim is not null && victim.Rwr > worst)
            {
                worst = victim.Rwr;
            }
        }

        Assert.That(worst, Is.EqualTo(RwrThreat.MissileActive),
            "a unit with an active-radar missile locked onto it should get a missile warning on its RWR");
    }
}
