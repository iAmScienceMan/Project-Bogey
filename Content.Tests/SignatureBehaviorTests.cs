using System.Collections.Generic;
using System.Linq;
using Content.Shared.Components;
using Content.Shared.Prototypes;
using Content.Shared.Tracks;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class SignatureBehaviorTests
{
    [Test]
    public void QuieterContact_AccumulatesLessConfidence_ThanLouderContact_AtEqualRange()
    {
        const int seed = 42;
        const int ticks = 40;

        float loudConfidence = FinalConfidence(signature: 0.9f, seed, ticks);
        float quietConfidence = FinalConfidence(signature: 0.2f, seed, ticks);

        Assert.That(quietConfidence, Is.LessThan(loudConfidence),
            "a lower-signature contact should be tracked with less confidence at the same range");
    }

    private static float FinalConfidence(float signature, int seed, int ticks)
    {
        List<SpawnSpec> scenario = new()
        {
            TestScenarios.FriendlySensorAtOrigin(rangeKm: 200f, maxDetect: 1.0f, falloff: 1.0f),
            TestScenarios.Hostile(
                x: 20f, y: 0f, vx: 0f, vy: 0f,
                signature: signature, domain: ContactDomain.Subsurface, typeName: "Test contact"),
        };

        List<TrackPictureSnapshot> history = TestScenarios.Run(scenario, seed, ticks);

        
        Track? finalTrack = history[^1].Tracks.SingleOrDefault();
        return finalTrack?.Confidence ?? 0f;
    }
}
