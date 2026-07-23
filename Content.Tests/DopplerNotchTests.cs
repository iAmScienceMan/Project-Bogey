using System.Collections.Generic;
using System.Linq;
using Content.Shared.Components;
using Content.Shared.Tracks;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class DopplerNotchTests
{
    private static float CrossingConfidence(float vx, float vy)
    {
        List<SpawnSpec> scenario = new()
        {
            TestScenarios.FriendlySensorAtOrigin(rangeKm: 220f, maxDetect: 1.0f, falloff: 1.0f),
            TestScenarios.Hostile(
                x: 150f, y: 0f, vx: vx, vy: vy,
                signature: 0.9f, domain: ContactDomain.Air, typeName: "Bandit"),
        };

        List<TrackPictureSnapshot> history = TestScenarios.Run(scenario, seed: 3, ticks: 20);
        Track? track = history[^1].Tracks.SingleOrDefault();
        return track?.Confidence ?? 0f;
    }

    [Test]
    public void AirTargetInTheNotch_IsTrackedFarWorse_ThanARadialTarget()
    {
        float radial = CrossingConfidence(vx: -0.6f, vy: 0f);
        float notching = CrossingConfidence(vx: 0f, vy: 0.6f);

        Assert.That(radial, Is.GreaterThan(0.3f), "a radial (closing) air target should track normally");
        Assert.That(notching, Is.LessThan(radial * 0.5f),
            "an air target crossing perpendicular sits in the doppler notch and should be far harder to track");
    }
}
