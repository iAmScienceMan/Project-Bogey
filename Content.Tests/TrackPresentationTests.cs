using System.Numerics;
using Content.Shared.Components;
using Content.Shared.Tracks;
using Content.Shared.Presentation;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class TrackPresentationTests
{
    private static Track Track(TrackState state, ContactDomain domain = ContactDomain.Unknown, string? typeGuess = null) =>
        new()
        {
            TrackId = 1,
            EstimatedPosition = Vector2.Zero,
            PositionalErrorKm = 1f,
            Confidence = 0.5f,
            DomainGuess = domain,
            TypeGuess = typeGuess,
            LastUpdatedTick = 1,
            State = state,
        };

    [Test]
    public void StyleFor_MapsEachLifecycleState()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TrackPresentation.StyleFor(Track(TrackState.Detected)), Is.EqualTo(MarkerStyle.Unknown));
            Assert.That(TrackPresentation.StyleFor(Track(TrackState.Classifying)), Is.EqualTo(MarkerStyle.Classifying));
            Assert.That(TrackPresentation.StyleFor(Track(TrackState.Identified)), Is.EqualTo(MarkerStyle.Identified));
            Assert.That(TrackPresentation.StyleFor(Track(TrackState.Stale)), Is.EqualTo(MarkerStyle.Stale));
            Assert.That(TrackPresentation.StyleFor(Track(TrackState.Dropped)), Is.EqualTo(MarkerStyle.Dropped));
        });
    }

    [Test]
    public void ScopeGlyph_ReflectsClassificationProgress()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TrackPresentation.ScopeGlyph(Track(TrackState.Detected)), Is.EqualTo('?'));
            Assert.That(TrackPresentation.ScopeGlyph(Track(TrackState.Classifying, ContactDomain.Air)), Is.EqualTo('a'));
            Assert.That(TrackPresentation.ScopeGlyph(Track(TrackState.Identified, ContactDomain.Surface)), Is.EqualTo('S'));
            Assert.That(TrackPresentation.ScopeGlyph(Track(TrackState.Stale)), Is.EqualTo('~'));
            Assert.That(TrackPresentation.ScopeGlyph(Track(TrackState.Dropped)), Is.EqualTo('x'));
        });
    }

    [Test]
    public void DomainText_AndStateLabel_AreStable()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TrackPresentation.DomainText(ContactDomain.Air), Is.EqualTo("AIR"));
            Assert.That(TrackPresentation.DomainText(ContactDomain.Subsurface), Is.EqualTo("SUBSURFACE"));
            Assert.That(TrackPresentation.DomainText(ContactDomain.Unknown), Is.EqualTo("UNKNOWN"));
            Assert.That(TrackPresentation.StateLabel(TrackState.Identified), Is.EqualTo("IDENTIFIED"));
        });
    }

    [Test]
    public void DescribeGuess_PrefersTypeThenDomainThenUnknown()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                TrackPresentation.DescribeGuess(Track(TrackState.Identified, ContactDomain.Air, "Wraith-class interceptor")),
                Is.EqualTo("Wraith-class interceptor"));
            Assert.That(
                TrackPresentation.DescribeGuess(Track(TrackState.Classifying, ContactDomain.Air)),
                Is.EqualTo("AIR (class)"));
            Assert.That(
                TrackPresentation.DescribeGuess(Track(TrackState.Detected)),
                Is.EqualTo("UNKNOWN"));
        });
    }
}
