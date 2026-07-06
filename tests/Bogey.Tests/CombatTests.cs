using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Bogey.Shared.Components;
using Bogey.Shared.Events;
using Bogey.Shared.Prototypes;
using Bogey.Sim;
using Bogey.Sim.Content;
using NUnit.Framework;

namespace Bogey.Tests;

[TestFixture]
public sealed class CombatTests
{
    [Test]
    public void Shooter_HoldsFire_UntilTargetIsClassified()
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[] { TestScenarios.MissileProto("missile", damage: 60f, pk: 1f, targetDomains: ContactDomain.Air) },
            seed: 11,
            config: null,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Shooter",
                health: 300f, sensorRangeKm: 200f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("missile", cooldownTicks: 6, magazine: 10),
                }),
            TestScenarios.CombatUnit(60f, 0f, FactionType.Hostile, ContactDomain.Air, "Bandit", health: 100f, signature: 0.9f));

        CombatLog log = new();
        log.Attach(sim);

        int firstClassifiedTick = -1;
        int firstFiredTick = -1;

        for (int tick = 1; tick <= 60; tick++)
        {
            sim.Step();

            if (firstClassifiedTick < 0 && sim.PublishSnapshot().Tracks.Any(t => t.DomainGuess == ContactDomain.Air))
            {
                firstClassifiedTick = tick;
            }

            if (firstFiredTick < 0 && log.Fired.Count > 0)
            {
                firstFiredTick = tick;
            }
        }

        Assert.That(firstClassifiedTick, Is.GreaterThan(0), "target was never classified");
        Assert.That(firstFiredTick, Is.GreaterThan(0), "shooter never fired");
        Assert.That(firstFiredTick, Is.GreaterThanOrEqualTo(firstClassifiedTick),
            "shooter fired before it had classified the contact's domain");
    }

    [Test]
    public void Weapon_DoesNotEngage_IncompatibleDomain()
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[] { TestScenarios.MissileProto("sam", damage: 60f, pk: 1f, targetDomains: ContactDomain.Air) },
            seed: 3,
            config: null,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "SamShip",
                health: 300f, sensorRangeKm: 200f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("sam", cooldownTicks: 4, magazine: 10),
                }),
            TestScenarios.CombatUnit(30f, 0f, FactionType.Hostile, ContactDomain.Subsurface, "Sub", health: 100f, signature: 0.9f));

        CombatLog log = new();
        log.Attach(sim);
        Run(sim, 90);

        Assert.That(sim.PublishSnapshot().Tracks.Any(t => t.DomainGuess == ContactDomain.Subsurface), Is.True,
            "the sub should have been detected and classified");
        Assert.That(log.Fired, Is.Empty, "an Air-only SAM must not engage a subsurface contact");
        Assert.That(Alive(sim, "Sub"), Is.True, "the sub was destroyed despite being outside the weapon's domain");
    }

    [Test]
    public void Weapon_Engages_CompatibleDomain()
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[] { TestScenarios.MissileProto("ssm", damage: 200f, pk: 1f, targetDomains: ContactDomain.Surface) },
            seed: 3,
            config: null,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Hunter",
                health: 300f, sensorRangeKm: 200f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("ssm", cooldownTicks: 4, magazine: 10),
                }),
            TestScenarios.CombatUnit(30f, 0f, FactionType.Hostile, ContactDomain.Surface, "Raider", health: 100f, signature: 0.9f));

        CombatLog log = new();
        log.Attach(sim);
        Run(sim, 120);

        Assert.That(log.Fired, Is.Not.Empty, "a surface-capable weapon should engage the raider");
        Assert.That(Alive(sim, "Raider"), Is.False, "the raider should have been destroyed");
    }

    [Test]
    public void Shooter_StopsFiring_WhenMagazineEmpty()
    {
        const int magazine = 3;

        SimRuntime sim = TestScenarios.BuildCombat(
            new[] { TestScenarios.MissileProto("dud", damage: 60f, pk: 0f, targetDomains: ContactDomain.Air) },
            seed: 5,
            config: null,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Shooter",
                health: 300f, sensorRangeKm: 200f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("dud", cooldownTicks: 2, magazine: magazine),
                }),
            TestScenarios.CombatUnit(60f, 0f, FactionType.Hostile, ContactDomain.Air, "Bandit", health: 100f, signature: 0.9f));

        CombatLog log = new();
        log.Attach(sim);
        Run(sim, 150);

        Assert.That(log.Fired.Count, Is.EqualTo(magazine),
            "a weapon should fire exactly its magazine and then run dry");
        Assert.That(Alive(sim, "Bandit"), Is.True, "Pk=0 rounds should never kill the target");
    }

    [Test]
    public void AutoFire_Doctrine_NeverOvercommitsToOneTarget()
    {
        const int commitLimit = 2;

        SimRuntime sim = TestScenarios.BuildCombat(
            new[] { TestScenarios.MissileProto("missile", damage: 60f, pk: 1f, targetDomains: ContactDomain.Air) },
            seed: 9,
            config: new Bogey.Sim.Systems.SimConfig { MaxAutoCommitPerTarget = commitLimit },
            TestScenarios.CombatUnit(
                0f, -40f, FactionType.Friendly, ContactDomain.Surface, "ShooterA",
                health: 300f, sensorRangeKm: 300f,
                weapons: new List<WeaponMountDef> { Sam("missile") }),
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "ShooterB",
                health: 300f, sensorRangeKm: 300f,
                weapons: new List<WeaponMountDef> { Sam("missile") }),
            TestScenarios.CombatUnit(
                0f, 40f, FactionType.Friendly, ContactDomain.Surface, "ShooterC",
                health: 300f, sensorRangeKm: 300f,
                weapons: new List<WeaponMountDef> { Sam("missile") }),
            TestScenarios.CombatUnit(80f, 0f, FactionType.Hostile, ContactDomain.Air, "Bandit", health: 100000f, signature: 0.9f));

        int maxInFlight = 0;
        for (int tick = 1; tick <= 80; tick++)
        {
            sim.Step();
            maxInFlight = System.Math.Max(maxInFlight, MunitionsInFlight(sim));
        }

        Assert.That(maxInFlight, Is.GreaterThan(0), "the shooters never engaged");
        Assert.That(maxInFlight, Is.LessThanOrEqualTo(commitLimit),
            "three shooters piled more than the commit limit onto a single target");
    }

    [Test]
    public void HighPk_DestroysTarget()
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[] { TestScenarios.MissileProto("missile", damage: 60f, pk: 1f, targetDomains: ContactDomain.Air) },
            seed: 7,
            config: null,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Shooter",
                health: 300f, sensorRangeKm: 200f,
                weapons: new List<WeaponMountDef> { Sam("missile") }),
            TestScenarios.CombatUnit(60f, 0f, FactionType.Hostile, ContactDomain.Air, "Bandit", health: 100f, signature: 0.9f));

        CombatLog log = new();
        log.Attach(sim);
        Run(sim, 90);

        Assert.That(Alive(sim, "Bandit"), Is.False, "a Pk=1 salvo should destroy the bandit");
        Assert.That(log.Destroyed.Any(), Is.True, "no destruction event was raised");
    }

    [Test]
    public void ImperfectPk_ProducesBothHitsAndMisses()
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[] { TestScenarios.MissileProto("missile", damage: 60f, pk: 0.5f, targetDomains: ContactDomain.Air) },
            seed: 20260703,
            config: null,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Shooter",
                health: 300f, sensorRangeKm: 200f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("missile", cooldownTicks: 2, magazine: 40),
                }),
            TestScenarios.CombatUnit(50f, 0f, FactionType.Hostile, ContactDomain.Air, "Bandit", health: 100000f, signature: 0.9f));

        CombatLog log = new();
        log.Attach(sim);
        Run(sim, 150);

        Assert.That(log.Hits, Is.GreaterThan(0), "a Pk=0.5 engagement produced no hits");
        Assert.That(log.Misses, Is.GreaterThan(0), "a Pk=0.5 engagement produced no misses");
    }

    [Test]
    public void PointDefense_Intercepts_LoneInbound()
    {
        Assert.That(Alive(RunPointDefenseDuel(withCiws: true, shooters: 1), "Defender"), Is.True,
            "CIWS should have intercepted the single inbound missile");

        Assert.That(Alive(RunPointDefenseDuel(withCiws: false, shooters: 1), "Defender"), Is.False,
            "control: without CIWS the lethal missile should destroy the defender");
    }

    [Test]
    public void PointDefense_Saturated_LetsLeakerThrough()
    {
        SimRuntime sim = RunPointDefenseDuel(withCiws: true, shooters: 10);

        Assert.That(Alive(sim, "Defender"), Is.False,
            "a single CIWS should be saturated by a coordinated 10-missile salvo");
    }

    [Test]
    public void FireAndForget_WithoutAcquisition_MissesMovingTarget()
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[]
            {
                TestScenarios.MissileProto(
                    "blind", damage: 100f, pk: 1f, seekerRangeKm: 0.05f,
                    seeker: SeekerType.Ir, datalink: false, targetDomains: ContactDomain.Air),
            },
            seed: 4,
            config: null,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Shooter",
                health: 300f, sensorRangeKm: 200f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("blind", cooldownTicks: 4, magazine: 6),
                }),
            TestScenarios.CombatUnit(
                50f, 0f, FactionType.Hostile, ContactDomain.Air, "Bandit",
                health: 100f, signature: 0.9f, speed: 0.7f, vx: 0.7f));

        CombatLog log = new();
        log.Attach(sim);
        Run(sim, 120);

        Assert.That(log.Fired, Is.Not.Empty, "the shooter should have engaged");
        Assert.That(log.Misses, Is.GreaterThan(0), "a fire-and-forget missile that never acquires must miss a mover");
        Assert.That(log.Hits, Is.EqualTo(0), "a missile flying to a stale datum should never score a hit");
        Assert.That(Alive(sim, "Bandit"), Is.True, "the bandit should out-run an un-acquiring engagement");
    }

    [Test]
    public void SameSeed_CombatRuns_AreIdentical()
    {
        List<string> a = RunReferenceEngagement(seed: 4242);
        List<string> b = RunReferenceEngagement(seed: 4242);

        Assert.That(a, Is.EqualTo(b), "identical seed + scenario diverged in combat");
    }

    [Test]
    public void DifferentSeeds_CombatRuns_Diverge()
    {
        List<string> a = RunReferenceEngagement(seed: 1);
        List<string> b = RunReferenceEngagement(seed: 2);

        Assert.That(a, Is.Not.EqualTo(b), "different seeds should diverge (RNG is in play)");
    }

    [Test]
    public void RealContent_CombatTestScenario_ProducesEngagement()
    {
        DirectoryInfo root = new(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "Resources", "Prototypes")))
        {
            root = root.Parent!;
        }

        Assert.That(root, Is.Not.Null, "could not locate the repository Resources directory");

        PrototypeLoader loader = new();
        IReadOnlyDictionary<string, PrototypeDefinition> prototypes =
            loader.LoadPrototypes(Path.Combine(root!.FullName, "Resources", "Prototypes"));
        ScenarioDefinition scenario = loader.LoadScenarioFromYaml(
            File.ReadAllText(Path.Combine(root.FullName, "Resources", "Scenarios", "combat-test.yaml")));

        SimRuntime sim = new(scenario, prototypes, seed: 12345);
        sim.SetPosture("Legion", WeaponPosture.Free);

        CombatLog log = new();
        log.Attach(sim);

        bool sawMunition = false;
        for (int tick = 0; tick < 250; tick++)
        {
            sim.Step();
            sawMunition |= sim.PublishSnapshot().Munitions.Count > 0;
        }

        Assert.That(log.Fired, Is.Not.Empty, "no weapon was fired with the real combat-test content");
        Assert.That(log.Destroyed, Is.Not.Empty, "the engagement produced no kills");
        Assert.That(sawMunition, Is.True, "friendly munitions never appeared in the snapshot");
    }

    [Test]
    public void RealContent_DefaultScenario_RunsAndBomberShootsBack()
    {
        DirectoryInfo root = new(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "Resources", "Prototypes")))
        {
            root = root.Parent!;
        }

        Assert.That(root, Is.Not.Null, "could not locate the repository Resources directory");

        PrototypeLoader loader = new();
        IReadOnlyDictionary<string, PrototypeDefinition> prototypes =
            loader.LoadPrototypes(Path.Combine(root!.FullName, "Resources", "Prototypes"));
        ScenarioDefinition scenario = loader.LoadScenarioFromYaml(
            File.ReadAllText(Path.Combine(root.FullName, "Resources", "Scenarios", "default.yaml")));

        SimRuntime sim = new(scenario, prototypes, seed: 777);

        CombatLog log = new();
        log.Attach(sim);
        Run(sim, 300);

        Assert.That(log.Fired, Is.Not.Empty, "the default scenario should produce weapon fire (the bomber carries AGM-89s)");
    }

    [Test]
    public void Seeker_LosesLock_WhenTargetTeleportsOutOfCone()
    {
        Assert.That(Alive(RunTeleportEngagement(teleport: false), "Bandit"), Is.False,
            "control: a locked IR missile should kill a stationary bandit");
        Assert.That(Alive(RunTeleportEngagement(teleport: true), "Bandit"), Is.True,
            "teleporting the bandit out of the seeker cone should break the lock and miss");
    }

    private static SimRuntime RunTeleportEngagement(bool teleport)
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[]
            {
                TestScenarios.MissileProto(
                    "ir", damage: 300f, pk: 1f, speed: 3f, seekerRangeKm: 14f,
                    seeker: SeekerType.Ir, fovDegrees: 40f, datalink: false, targetDomains: ContactDomain.Air),
            },
            seed: 5,
            config: null,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Shooter",
                health: 300f, sensorRangeKm: 200f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("ir", cooldownTicks: 4, magazine: 1),
                }),
            TestScenarios.CombatUnit(40f, 0f, FactionType.Hostile, ContactDomain.Air, "Bandit", health: 200f, signature: 0.9f));

        int bandit = EntityId(sim, "Bandit");
        bool teleported = false;

        for (int tick = 1; tick <= 90; tick++)
        {
            sim.Step();
            if (teleport && !teleported && sim.DumpMunitions().Any(m => m.Locked))
            {
                sim.DebugSetPosition(bandit, new Vector2(40f, 90f));
                teleported = true;
            }
        }

        return sim;
    }

    [Test]
    public void Datalink_Missile_TracksAndKillsMovingTarget()
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[]
            {
                TestScenarios.MissileProto(
                    "arh", damage: 200f, pk: 1f, speed: 5f, seekerRangeKm: 20f, rangeKm: 250f,
                    seeker: SeekerType.ActiveRadar, fovDegrees: 60f, datalink: true, targetDomains: ContactDomain.Air),
            },
            seed: 8,
            config: null,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Shooter",
                health: 300f, sensorRangeKm: 250f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("arh", cooldownTicks: 4, magazine: 3),
                }),
            TestScenarios.CombatUnit(
                60f, 0f, FactionType.Hostile, ContactDomain.Air, "Runner",
                health: 150f, signature: 0.9f, speed: 1.0f, vy: 1.0f));

        Run(sim, 120);

        Assert.That(Alive(sim, "Runner"), Is.False,
            "an active-radar datalink missile should track and kill a moving target");
    }

    [Test]
    public void AntiRadiation_HomesOnEmitter()
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[]
            {
                TestScenarios.MissileProto(
                    "harm", damage: 200f, pk: 1f, speed: 4f, seekerRangeKm: 30f,
                    seeker: SeekerType.AntiRadiation, fovDegrees: 70f, datalink: false, targetDomains: ContactDomain.Surface),
            },
            seed: 6,
            config: null,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Shooter",
                health: 300f, sensorRangeKm: 200f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("harm", cooldownTicks: 6, magazine: 3),
                }),
            TestScenarios.CombatUnit(
                50f, 0f, FactionType.Hostile, ContactDomain.Surface, "Emitter",
                health: 150f, signature: 0.9f, sensorRangeKm: 100f));

        Run(sim, 120);

        Assert.That(Alive(sim, "Emitter"), Is.False,
            "an anti-radiation missile should home on and destroy the emitter");
    }

    [Test]
    public void Posture_Hold_SuppressesAutoFire_ButManualEngageFires()
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[] { TestScenarios.MissileProto("aa", damage: 200f, pk: 1f, targetDomains: ContactDomain.Air) },
            seed: 3,
            config: null,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Shooter",
                health: 300f, sensorRangeKm: 200f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("aa", cooldownTicks: 4, magazine: 6),
                },
                posture: WeaponPosture.Hold),
            TestScenarios.CombatUnit(50f, 0f, FactionType.Hostile, ContactDomain.Air, "Bandit", health: 100f, signature: 0.9f));

        CombatLog log = new();
        log.Attach(sim);
        Run(sim, 40);

        Assert.That(log.Fired, Is.Empty, "a Hold unit must not auto-fire");

        int trackId = FirstTrackId(sim);
        Assert.That(trackId, Is.GreaterThan(0), "the bandit should be tracked");
        Assert.That(sim.IssueEngagement("Shooter", trackId, "aa", 1), Is.True);

        Run(sim, 60);

        Assert.That(log.Fired, Is.Not.Empty, "a manual engage order must fire even under Hold");
        Assert.That(Alive(sim, "Bandit"), Is.False, "the manually-ordered shot should kill the bandit");
    }

    [Test]
    public void ManualEngage_FiresAtUnclassifiedTrack()
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[] { TestScenarios.MissileProto("aa", damage: 200f, pk: 1f, targetDomains: ContactDomain.Air) },
            seed: 3,
            config: new Bogey.Sim.Systems.SimConfig { ClassifyThreshold = 2f, IdentifyThreshold = 2f },
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Shooter",
                health: 300f, sensorRangeKm: 200f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("aa", cooldownTicks: 4, magazine: 6),
                },
                posture: WeaponPosture.Hold),
            TestScenarios.CombatUnit(50f, 0f, FactionType.Hostile, ContactDomain.Air, "Bandit", health: 100f, signature: 0.9f));

        CombatLog log = new();
        log.Attach(sim);
        Run(sim, 20);

        Assert.That(sim.PublishSnapshot().Tracks.All(t => t.DomainGuess == ContactDomain.Unknown), Is.True,
            "precondition: the track must still be unclassified");

        int trackId = FirstTrackId(sim);
        Assert.That(trackId, Is.GreaterThan(0), "the bandit should be tracked");
        Assert.That(sim.IssueEngagement("Shooter", trackId, "aa", 1), Is.True);

        Run(sim, 60);

        Assert.That(log.Fired, Is.Not.Empty, "a manual order must fire at an unclassified track");
        Assert.That(Alive(sim, "Bandit"), Is.False, "the on-domain shot should still kill the bandit");
    }

    [Test]
    public void ManualSalvo_FiresExactlyTheOrderedCount()
    {
        const int salvo = 3;

        SimRuntime sim = TestScenarios.BuildCombat(
            new[] { TestScenarios.MissileProto("aa", damage: 1f, pk: 0f, targetDomains: ContactDomain.Air) },
            seed: 4,
            config: null,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Shooter",
                health: 300f, sensorRangeKm: 200f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("aa", cooldownTicks: 10, magazine: 20),
                },
                posture: WeaponPosture.Hold),
            TestScenarios.CombatUnit(50f, 0f, FactionType.Hostile, ContactDomain.Air, "Bandit", health: 100000f, signature: 0.9f));

        CombatLog log = new();
        log.Attach(sim);
        Run(sim, 30);

        int trackId = FirstTrackId(sim);
        Assert.That(trackId, Is.GreaterThan(0), "the bandit should be tracked");
        Assert.That(sim.IssueEngagement("Shooter", trackId, "aa", salvo), Is.True);

        Run(sim, 30);

        Assert.That(log.Fired.Count, Is.EqualTo(salvo),
            "a salvo order must launch exactly one distinct missile per round ordered");
    }

    [Test]
    public void MissedMissile_FliesPastAndExpires_InsteadOfCircling()
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[]
            {
                TestScenarios.MissileProto(
                    "blind", damage: 100f, pk: 1f, speed: 6f, seekerRangeKm: 0.05f,
                    seeker: SeekerType.Ir, datalink: false, targetDomains: ContactDomain.Air),
            },
            seed: 12,
            config: null,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Shooter",
                health: 300f, sensorRangeKm: 200f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("blind", cooldownTicks: 100, magazine: 1),
                }),
            TestScenarios.CombatUnit(31f, 0f, FactionType.Hostile, ContactDomain.Air, "Bandit", health: 100000f, signature: 0.9f));

        float maxOvershoot = 0f;
        bool sawMunition = false;

        for (int tick = 1; tick <= 160; tick++)
        {
            sim.Step();
            foreach (MunitionDebug munition in sim.DumpMunitions())
            {
                sawMunition = true;
                maxOvershoot = System.Math.Max(maxOvershoot, munition.Position.X - munition.Datum.X);
            }
        }

        Assert.That(sawMunition, Is.True, "the shooter never fired");
        Assert.That(sim.DumpMunitions(), Is.Empty, "the missile must eventually run out of energy and expire");
        Assert.That(maxOvershoot, Is.GreaterThan(10f),
            "an unlocked missile that reaches its datum should keep flying straight past it, not orbit it");
    }

    [Test]
    public void Posture_Defensive_HoldsOffensiveFire()
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[] { TestScenarios.MissileProto("aa", damage: 200f, pk: 1f, targetDomains: ContactDomain.Air) },
            seed: 3,
            config: null,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Defender",
                health: 300f, sensorRangeKm: 200f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("aa", cooldownTicks: 4, magazine: 6),
                },
                posture: WeaponPosture.Defensive),
            TestScenarios.CombatUnit(50f, 0f, FactionType.Hostile, ContactDomain.Air, "Bandit", health: 100f, signature: 0.9f));

        CombatLog log = new();
        log.Attach(sim);
        Run(sim, 60);

        Assert.That(log.Fired, Is.Empty, "Defensive posture must hold offensive weapons");
        Assert.That(Alive(sim, "Bandit"), Is.True, "the bandit should be unharmed");
    }

    [Test]
    public void Posture_Defensive_StillRunsPointDefense()
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[]
            {
                TestScenarios.MissileProto(
                    "asm", damage: 400f, pk: 1f, seekerRangeKm: 18f, signature: 0.9f, targetDomains: ContactDomain.Surface),
            },
            seed: 77,
            config: null,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Defender",
                health: 300f, sensorRangeKm: 300f,
                weapons: new List<WeaponMountDef> { TestScenarios.PointDefenseMount(rangeKm: 20f, cooldownTicks: 1, magazine: 50, pk: 1f) },
                posture: WeaponPosture.Defensive),
            TestScenarios.CombatUnit(
                60f, 0f, FactionType.Hostile, ContactDomain.Air, "Archer",
                health: 100f, signature: 0.9f, sensorRangeKm: 300f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("asm", cooldownTicks: 40, magazine: 1),
                }));

        Run(sim, 140);

        Assert.That(Alive(sim, "Defender"), Is.True, "Defensive posture must still auto-run point defense");
    }

    [Test]
    public void Ai_Aggressive_ClosesOnAndEngagesEnemy()
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[] { TestScenarios.MissileProto("asm", damage: 60f, pk: 1f, rangeKm: 60f, targetDomains: ContactDomain.Surface) },
            seed: 9,
            config: null,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Prey",
                health: 100000f, sensorRangeKm: 200f, posture: WeaponPosture.Hold),
            TestScenarios.CombatUnit(
                120f, 0f, FactionType.Hostile, ContactDomain.Air, "Hunter",
                health: 100f, signature: 0.9f, sensorRangeKm: 200f, speed: 1.0f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("asm", cooldownTicks: 10, magazine: 5),
                },
                posture: WeaponPosture.Free, ai: AiBehavior.Aggressive));

        CombatLog log = new();
        log.Attach(sim);

        float startDistance = DistanceFromOrigin(sim, "Hunter");
        Run(sim, 80);
        float endDistance = DistanceFromOrigin(sim, "Hunter");

        Assert.That(endDistance, Is.LessThan(startDistance - 20f), "an aggressive AI unit should close on its enemy");
        Assert.That(log.Fired, Is.Not.Empty, "the AI should open fire once in range");
    }

    [Test]
    public void FriendlyMunitions_AppearInSnapshot()
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[] { TestScenarios.MissileProto("aa", damage: 60f, pk: 1f, targetDomains: ContactDomain.Air) },
            seed: 2,
            config: null,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Shooter",
                health: 300f, sensorRangeKm: 200f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("aa", cooldownTicks: 4, magazine: 6),
                }),
            TestScenarios.CombatUnit(50f, 0f, FactionType.Hostile, ContactDomain.Air, "Bandit", health: 100000f, signature: 0.9f));

        bool saw = false;
        for (int i = 0; i < 60 && !saw; i++)
        {
            sim.Step();
            saw = sim.PublishSnapshot().Munitions.Count > 0;
        }

        Assert.That(saw, Is.True, "friendly munitions should be published in the snapshot for rendering");
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

    private static int FirstTrackId(SimRuntime sim)
    {
        foreach (Bogey.Shared.Tracks.Track track in sim.PublishSnapshot().Tracks)
        {
            return track.TrackId;
        }

        return -1;
    }

    private static float DistanceFromOrigin(SimRuntime sim, string name)
    {
        foreach (GroundTruthEntry entry in sim.DumpGroundTruth())
        {
            if (entry.Name == name)
            {
                return entry.Position.Length();
            }
        }

        return -1f;
    }

    private static WeaponMountDef Sam(string projectile)
        => TestScenarios.Mount(projectile, cooldownTicks: 3, magazine: 20);

    private static SimRuntime RunPointDefenseDuel(bool withCiws, int shooters)
    {
        List<SpawnSpec> specs = new()
        {
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Defender",
                health: 300f, sensorRangeKm: 300f,
                weapons: withCiws
                    ? new List<WeaponMountDef> { TestScenarios.PointDefenseMount(rangeKm: 20f, cooldownTicks: 1, magazine: 4, pk: 1f) }
                    : null),
        };

        for (int i = 0; i < shooters; i++)
        {
            specs.Add(TestScenarios.CombatUnit(
                60f, 0f, FactionType.Hostile, ContactDomain.Air, "Archer" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                health: 100f, signature: 0.9f, sensorRangeKm: 300f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("asm", cooldownTicks: 40, magazine: 1),
                }));
        }

        SimRuntime sim = TestScenarios.BuildCombat(
            new[]
            {
                TestScenarios.MissileProto(
                    "asm", damage: 400f, pk: 1f, seekerRangeKm: 18f, signature: 0.9f, targetDomains: ContactDomain.Surface),
            },
            seed: 77,
            config: null,
            specs.ToArray());

        Run(sim, 140);
        return sim;
    }

    private static List<string> RunReferenceEngagement(int seed)
    {
        SimRuntime sim = TestScenarios.BuildCombat(
            new[]
            {
                TestScenarios.MissileProto("sam", damage: 60f, pk: 0.85f, targetDomains: ContactDomain.Air),
                TestScenarios.MissileProto("asm", damage: 80f, pk: 0.8f, signature: 0.5f, rangeKm: 120f, targetDomains: ContactDomain.Surface),
            },
            seed,
            config: null,
            TestScenarios.CombatUnit(
                0f, 0f, FactionType.Friendly, ContactDomain.Surface, "Cruiser",
                health: 300f, sensorRangeKm: 220f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("sam", cooldownTicks: 5, magazine: 16),
                    TestScenarios.PointDefenseMount(rangeKm: 18f, cooldownTicks: 1, magazine: 300, pk: 0.7f),
                }),
            TestScenarios.CombatUnit(
                90f, 20f, FactionType.Hostile, ContactDomain.Air, "Raider1",
                health: 120f, signature: 0.8f, speed: 0.6f, sensorRangeKm: 160f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("asm", cooldownTicks: 25, magazine: 3),
                }, vx: -0.5f, ai: AiBehavior.Aggressive),
            TestScenarios.CombatUnit(
                100f, -30f, FactionType.Hostile, ContactDomain.Air, "Raider2",
                health: 120f, signature: 0.7f, speed: 0.6f, sensorRangeKm: 160f,
                weapons: new List<WeaponMountDef>
                {
                    TestScenarios.Mount("asm", cooldownTicks: 25, magazine: 3),
                }, vx: -0.4f, vy: 0.2f, ai: AiBehavior.Aggressive));

        List<string> trace = new();
        for (int tick = 0; tick < 120; tick++)
        {
            sim.Step();
            foreach (GroundTruthEntry entry in sim.DumpGroundTruth())
            {
                trace.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"{tick}:{entry.EntityId}:{entry.Position.X:0.###},{entry.Position.Y:0.###}"));
            }
        }

        return trace;
    }

    private static void Run(SimRuntime sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Step();
        }
    }

    private static bool Alive(SimRuntime sim, string name)
        => sim.DumpGroundTruth().Any(e => e.Name == name);

    private static int MunitionsInFlight(SimRuntime sim)
        => sim.DumpGroundTruth().Count(e => e.Domain == ContactDomain.Munition);

    private sealed class CombatLog
    {
        public List<WeaponFiredEvent> Fired { get; } = new();

        public List<MunitionResolvedEvent> Resolved { get; } = new();

        public List<EntityDestroyedEvent> Destroyed { get; } = new();

        public int Hits => Resolved.Count(r => r.Hit);

        public int Misses => Resolved.Count(r => !r.Hit);

        public void Attach(SimRuntime sim)
        {
            sim.WeaponFired += Fired.Add;
            sim.MunitionResolved += Resolved.Add;
            sim.EntityDestroyed += Destroyed.Add;
        }
    }
}
