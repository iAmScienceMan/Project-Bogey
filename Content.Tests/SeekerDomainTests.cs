using System.Collections.Generic;
using System.Linq;
using Content.Shared.Components;
using Content.Shared.Tracks;
using Content.Sim;
using Content.Sim.Systems;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class SeekerDomainTests
{
    private static bool ShipKilled(ContactDomain missileDomain)
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[]
            {
                TestScenarios.MissileProto(
                    "m", damage: 200f, pk: 1f, speed: 5f, seekerRangeKm: 20f, rangeKm: 200f,
                    detonationRangeKm: 2f, motorBurnTicks: 200, dragPerTick: 0f,
                    seeker: SeekerType.ActiveRadar, fovDegrees: 60f, datalink: true,
                    targetDomains: missileDomain),
            },
            seed: 4,
            config: new SimConfig { ObservationNoiseKm = 0f },
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Shooter",
                health: 300f, sensorRangeKm: 250f,
                posture: WeaponPosture.Hold,
                weapons: new List<WeaponMount>
                {
                    TestScenarios.Mount("m", cooldownTicks: 3, magazine: 4),
                }),
            TestScenarios.CombatUnit(40f, 0f, FactionType.Hostile, ContactDomain.Surface, "Ship", health: 200f, signature: 0.9f));

        int trackId = 0;
        for (int i = 0; i < 60 && trackId == 0; i++)
        {
            sim.Step();
            Track? track = sim.PublishSnapshot("friendly").Tracks.FirstOrDefault();
            if (track is not null)
            {
                trackId = track.TrackId;
            }
        }

        sim.IssueEngagement("Shooter", trackId, "m", 4, "friendly");

        for (int i = 0; i < 120; i++)
        {
            sim.Step();
        }

        return sim.DumpGroundTruth().All(e => e.Name != "Ship");
    }

    [Test]
    public void AntiShipSeeker_KillsShip_ButAirToAirSeeker_CannotLockIt()
    {
        Assert.That(ShipKilled(ContactDomain.Surface), Is.True,
            "an anti-surface missile should lock and destroy the ship");
        Assert.That(ShipKilled(ContactDomain.Air), Is.False,
            "an air-to-air seeker must not lock a ground target, so those missiles fly past and the ship survives");
    }

    [Test]
    public void IrSeeker_IsFactionBlind_AndLocksAWingmanInItsPath()
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[]
            {
                TestScenarios.MissileProto(
                    "irm", damage: 200f, pk: 1f, speed: 5f, seekerRangeKm: 50f, rangeKm: 200f,
                    detonationRangeKm: 2f, motorBurnTicks: 200, dragPerTick: 0f,
                    seeker: SeekerType.Ir, fovDegrees: 60f, datalink: false,
                    targetDomains: ContactDomain.Air),
            },
            seed: 4,
            config: new SimConfig { ObservationNoiseKm = 0f },
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Shooter",
                health: 300f, sensorRangeKm: 250f,
                weapons: new List<WeaponMount>
                {
                    TestScenarios.Mount("irm", cooldownTicks: 1000, magazine: 1),
                }),
            TestScenarios.CombatUnit(25f, 0f, FactionType.Friendly, ContactDomain.Air, "Wingman", health: 100f, signature: 0.9f),
            TestScenarios.CombatUnit(55f, 0f, FactionType.Hostile, ContactDomain.Air, "Bandit", health: 100000f, signature: 0.9f));

        for (int i = 0; i < 120; i++)
        {
            sim.Step();
        }

        Assert.That(sim.DumpGroundTruth().All(e => e.Name != "Wingman"), Is.True,
            "a faction-blind IR seeker should lock the nearer friendly wingman in its path and hit it (fratricide)");
    }
}
