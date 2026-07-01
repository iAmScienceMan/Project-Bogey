using System.Collections.Generic;
using System.Linq;
using Bogey.Shared.Components;
using Bogey.Shared.Prototypes;
using Bogey.Shared.Tracks;
using NUnit.Framework;

namespace Bogey.Tests;

[TestFixture]
public sealed class TrackDecayTests
{
    [Test]
    public void ContactLeavingSensorRange_GoesStale_ThenDropped()
    {
        
        List<PrototypeDefinition> scenario = new()
        {
            TestScenarios.FriendlySensorAtOrigin(rangeKm: 60f, maxDetect: 1.0f, falloff: 1.0f),
            TestScenarios.Hostile(
                x: 20f, y: 0f, vx: 3f, vy: 0f,
                signature: 1.0f, domain: ContactDomain.Surface, typeName: "Reaver-class raider"),
        };

        List<TrackPictureSnapshot> history = TestScenarios.Run(scenario, seed: 7, ticks: 60);

        
        int firstSeen = history.FindIndex(s => s.Tracks.Count > 0);
        Assert.That(firstSeen, Is.GreaterThanOrEqualTo(0), "contact was never detected");

        
        int firstStale = history.FindIndex(s => s.Tracks.Any(t => t.State == TrackState.Stale));
        Assert.That(firstStale, Is.GreaterThanOrEqualTo(0), "contact never went Stale");

        
        int firstGoneAfterStale = -1;
        for (int i = firstStale; i < history.Count; i++)
        {
            if (history[i].Tracks.Count == 0)
            {
                firstGoneAfterStale = i;
                break;
            }
        }

        Assert.That(firstGoneAfterStale, Is.GreaterThan(firstStale),
            "contact went Stale but was never Dropped from the picture");

        
        Assert.That(firstStale, Is.LessThan(firstGoneAfterStale));

        
        Assert.That(history.Skip(firstGoneAfterStale).All(s => s.Tracks.Count == 0), Is.True);
    }

    [Test]
    public void StaleTrack_HasGrowingPositionalError()
    {
        List<PrototypeDefinition> scenario = new()
        {
            TestScenarios.FriendlySensorAtOrigin(rangeKm: 60f, maxDetect: 1.0f, falloff: 1.0f),
            TestScenarios.Hostile(
                x: 20f, y: 0f, vx: 3f, vy: 0f,
                signature: 1.0f, domain: ContactDomain.Surface, typeName: "Reaver-class raider"),
        };

        List<TrackPictureSnapshot> history = TestScenarios.Run(scenario, seed: 7, ticks: 40);

        List<float> staleErrors = history
            .SelectMany(s => s.Tracks)
            .Where(t => t.State == TrackState.Stale)
            .Select(t => t.PositionalErrorKm)
            .ToList();

        Assert.That(staleErrors, Is.Not.Empty);
        
        Assert.That(staleErrors.Last(), Is.GreaterThan(staleErrors.First()));
    }
}
