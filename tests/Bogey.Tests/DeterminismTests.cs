using System.Collections.Generic;
using Bogey.Shared.Components;
using Bogey.Shared.Prototypes;
using Bogey.Shared.Tracks;
using NUnit.Framework;

namespace Bogey.Tests;

[TestFixture]
public sealed class DeterminismTests
{
    private static List<SpawnSpec> MixedScenario() => new()
    {
        TestScenarios.FriendlySensorAtOrigin(rangeKm: 150f, maxDetect: 0.95f, falloff: 1.5f),
        TestScenarios.Hostile(40f, 25f, 1.8f, 1.1f, 0.55f, ContactDomain.Air, "Wraith-class interceptor"),
        TestScenarios.Hostile(120f, -40f, -1.2f, 0.3f, 0.9f, ContactDomain.Air, "Goliath-class bomber"),
        TestScenarios.Hostile(30f, 70f, 0.2f, -0.3f, 0.18f, ContactDomain.Subsurface, "Silt-class submersible"),
    };

    [Test]
    public void SameSeedAndScenario_ProduceIdenticalTrackHistory()
    {
        const int seed = 20260630;
        const int ticks = 90;

        List<TrackPictureSnapshot> runA = TestScenarios.Run(MixedScenario(), seed, ticks);
        List<TrackPictureSnapshot> runB = TestScenarios.Run(MixedScenario(), seed, ticks);

        Assert.That(runA, Has.Count.EqualTo(runB.Count));

        for (int tick = 0; tick < runA.Count; tick++)
        {
            
            Assert.That(runA[tick].Tracks, Is.EqualTo(runB[tick].Tracks),
                $"track pictures diverged at tick {tick + 1}");
        }
    }

    [Test]
    public void DifferentSeeds_ProduceDifferentHistory()
    {
        const int ticks = 90;

        List<TrackPictureSnapshot> runA = TestScenarios.Run(MixedScenario(), seed: 1, ticks: ticks);
        List<TrackPictureSnapshot> runB = TestScenarios.Run(MixedScenario(), seed: 2, ticks: ticks);

        bool anyDifference = false;
        for (int tick = 0; tick < ticks && !anyDifference; tick++)
        {
            if (!AreEqual(runA[tick].Tracks, runB[tick].Tracks))
            {
                anyDifference = true;
            }
        }

        Assert.That(anyDifference, Is.True, "different seeds should diverge (RNG is actually in play)");
    }

    private static bool AreEqual(IReadOnlyList<Track> a, IReadOnlyList<Track> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Count; i++)
        {
            if (!a[i].Equals(b[i]))
            {
                return false;
            }
        }

        return true;
    }
}
