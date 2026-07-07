using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.Components;
using Content.Shared.Prototypes;
using Content.Shared.Tracks;
using Content.Sim;
using Lattice.Sim.Engine;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class PlayerFactionTests
{
    private static readonly Dictionary<string, uint> Colors = new()
    {
        ["alice"] = 0xFF0000,
        ["bob"] = 0x00FF00,
    };

    private static SimRuntime BuildTwoPlayerSim()
    {
        PrototypeManager prototypes = new(new ComponentFactory(new[] { typeof(Sensor).Assembly }));
        prototypes.Register("player-ship", "player-ship", () => new List<IComponent>
        {
            new MetaData { EntityName = "player-ship" },
            new Faction { Side = FactionType.Friendly },
            new Transform(),
            new Signature { Value = 1f },
            new ClassificationProfile { Domain = ContactDomain.Surface, TypeName = "Test Ship" },
            new Sensor { RangeKm = 200f, MaxDetectProbability = 0.99f, FalloffExponent = 1f },
            new Health { Max = 100f },
            new Propulsion { MaxSpeedKmPerTick = 5f },
        }.AsReadOnly());

        SimRuntime sim = new(new ScenarioDefinition { Id = "mp" }, prototypes, seed: 7);
        Assert.That(sim.SpawnPlayerUnit("alice", "player-ship", "Legion", Vector2.Zero), Is.True);
        Assert.That(sim.SpawnPlayerUnit("bob", "player-ship", "Legion", new Vector2(40f, 0f)), Is.True);
        return sim;
    }

    private static Track StepUntilTrack(SimRuntime sim, string factionId, NameVisibility visibility)
    {
        for (int i = 0; i < 60; i++)
        {
            sim.Step();
            TrackPictureSnapshot snapshot = sim.PublishSnapshot(factionId, 1, visibility, Colors);
            if (snapshot.Tracks.Count > 0)
            {
                return snapshot.Tracks[0];
            }
        }

        Assert.Fail("expected a track within 60 ticks");
        return null!;
    }

    [Test]
    public void OwnUnits_AreScopedToPlayerFaction()
    {
        SimRuntime sim = BuildTwoPlayerSim();

        TrackPictureSnapshot alice = sim.PublishSnapshot("alice");
        TrackPictureSnapshot bob = sim.PublishSnapshot("bob");

        Assert.Multiple(() =>
        {
            Assert.That(alice.OwnUnits, Has.Count.EqualTo(1));
            Assert.That(bob.OwnUnits, Has.Count.EqualTo(1));
            Assert.That(alice.OwnUnits[0].Name, Is.EqualTo("Legion"));
            Assert.That(alice.OwnUnits[0].Position, Is.EqualTo(Vector2.Zero));
            Assert.That(bob.OwnUnits[0].Position, Is.EqualTo(new Vector2(40f, 0f)));
        });
    }

    [Test]
    public void DetectedPolicy_RevealsOwnerOnDetection()
    {
        SimRuntime sim = BuildTwoPlayerSim();

        Track track = StepUntilTrack(sim, "alice", NameVisibility.Detected);

        Assert.Multiple(() =>
        {
            Assert.That(track.UnitName, Is.EqualTo("Legion"));
            Assert.That(track.PlayerName, Is.EqualTo("bob"));
            Assert.That(track.PlayerColorRgb, Is.EqualTo(0x00FF00));
        });
    }

    [Test]
    public void IdentifiedPolicy_HidesOwnerUntilIdentified()
    {
        SimRuntime sim = BuildTwoPlayerSim();

        Track track = StepUntilTrack(sim, "alice", NameVisibility.Identified);

        Assert.Multiple(() =>
        {
            Assert.That(track.State, Is.Not.EqualTo(TrackState.Identified));
            Assert.That(track.PlayerName, Is.Null);
            Assert.That(track.UnitName, Is.Null);
        });

        for (int i = 0; i < 200; i++)
        {
            sim.Step();
            TrackPictureSnapshot snapshot = sim.PublishSnapshot("alice", 1, NameVisibility.Identified, Colors);
            Track? identified = snapshot.Tracks.FirstOrDefault(t => t.State == TrackState.Identified);
            if (identified is not null)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(identified.PlayerName, Is.EqualTo("bob"));
                    Assert.That(identified.UnitName, Is.EqualTo("Legion"));
                });
                return;
            }
        }

        Assert.Fail("track never reached identified state");
    }

    [Test]
    public void AlwaysPolicy_InjectsSyntheticTracksWithoutDetection()
    {
        SimRuntime sim = BuildTwoPlayerSim();

        TrackPictureSnapshot snapshot = sim.PublishSnapshot("alice", 1, NameVisibility.Always, Colors);

        Assert.That(snapshot.Tracks, Has.Count.EqualTo(1));
        Track track = snapshot.Tracks[0];
        Assert.Multiple(() =>
        {
            Assert.That(track.TrackId, Is.LessThan(0));
            Assert.That(track.State, Is.EqualTo(TrackState.Identified));
            Assert.That(track.EstimatedPosition, Is.EqualTo(new Vector2(40f, 0f)));
            Assert.That(track.UnitName, Is.EqualTo("Legion"));
            Assert.That(track.PlayerName, Is.EqualTo("bob"));
        });
    }

    [Test]
    public void Orders_AreScopedToOwningFaction()
    {
        SimRuntime sim = BuildTwoPlayerSim();

        Assert.Multiple(() =>
        {
            Assert.That(sim.IssueMoveOrder("Legion", new Vector2(10f, 10f), "alice"), Is.True);
            Assert.That(sim.IssueMoveOrder("Legion", new Vector2(10f, 10f), "charlie"), Is.False);
        });
    }

    [Test]
    public void FactionHasUnits_TracksOwnership()
    {
        SimRuntime sim = BuildTwoPlayerSim();

        Assert.Multiple(() =>
        {
            Assert.That(sim.FactionHasUnits("alice"), Is.True);
            Assert.That(sim.FactionHasUnits("bob"), Is.True);
            Assert.That(sim.FactionHasUnits("charlie"), Is.False);
        });
    }
}
