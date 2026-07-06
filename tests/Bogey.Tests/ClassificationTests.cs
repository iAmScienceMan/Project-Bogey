using System.Collections.Generic;
using System.Linq;
using Bogey.Shared.Components;
using Bogey.Shared.Prototypes;
using Bogey.Shared.Tracks;
using NUnit.Framework;

namespace Bogey.Tests;

[TestFixture]
public sealed class ClassificationTests
{
    [Test]
    public void PersistentlyDetectedContact_ResolvesFromUnknownToIdentified()
    {
        
        List<SpawnSpec> scenario = new()
        {
            TestScenarios.FriendlySensorAtOrigin(rangeKm: 200f, maxDetect: 1.0f, falloff: 1.0f),
            TestScenarios.Hostile(
                x: 4f, y: 0f, vx: 0f, vy: 0f,
                signature: 1.0f, domain: ContactDomain.Air, typeName: "Wraith-class interceptor"),
        };

        List<TrackPictureSnapshot> history = TestScenarios.Run(scenario, seed: 42, ticks: 60);

        
        TrackPictureSnapshot firstWithContact = history.First(s => s.Tracks.Count > 0);
        Track firstSighting = firstWithContact.Tracks.Single();
        Assert.That(firstSighting.State, Is.EqualTo(TrackState.Detected));
        Assert.That(firstSighting.DomainGuess, Is.EqualTo(ContactDomain.Unknown));
        Assert.That(firstSighting.TypeGuess, Is.Null);

        
        Track finalSighting = history[^1].Tracks.Single();
        Assert.That(finalSighting.State, Is.EqualTo(TrackState.Identified));
        Assert.That(finalSighting.DomainGuess, Is.EqualTo(ContactDomain.Air));
        Assert.That(finalSighting.TypeGuess, Is.EqualTo("Wraith-class interceptor"));

        
        bool wasClassifying = history.Any(s =>
            s.Tracks.Any(t => t.State == TrackState.Classifying));
        Assert.That(wasClassifying, Is.True, "track should pass through Classifying before Identified");
    }
}
