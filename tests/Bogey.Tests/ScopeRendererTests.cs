using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Bogey.Shared.Components;
using Bogey.Shared.Tracks;
using Bogey.View;
using NUnit.Framework;

namespace Bogey.Tests;

[TestFixture]
public sealed class ScopeRendererTests
{
    private static TrackPictureSnapshot Snapshot(IReadOnlyList<Track> tracks, IReadOnlyList<OwnUnitView> ownUnits) =>
        new() { Tick = 1, Tracks = tracks, OwnUnits = ownUnits };

    private static OwnUnitView Own(string name, Vector2 pos) =>
        new() { Name = name, Position = pos, SensorRangeKm = 50f };

    private static Track Track(Vector2 pos, TrackState state, ContactDomain domain = ContactDomain.Unknown) =>
        new()
        {
            TrackId = 1,
            EstimatedPosition = pos,
            PositionalErrorKm = 1f,
            Confidence = 0.5f,
            DomainGuess = domain,
            LastUpdatedTick = 1,
            State = state,
        };

    [Test]
    public void ReferenceUnit_IsPlottedAtGridCentre()
    {
        string[] grid = GridLines(Snapshot(
            Array.Empty<Track>(),
            new[] { Own("flagship", new Vector2(100f, 100f)) }));

        int row = IndexOf(grid, '@');
        Assert.That(row, Is.EqualTo(grid.Length / 2), "the reference unit should sit on the centre row.");
        Assert.That(grid[row].IndexOf('@'), Is.EqualTo(grid[row].Length / 2), "and on the centre column.");
    }

    [Test]
    public void ContactToTheNorth_PlotsAboveTheReference()
    {
        Vector2 origin = new(0f, 0f);
        string[] grid = GridLines(Snapshot(
            new[] { Track(new Vector2(0f, 20f), TrackState.Detected) }, 
            new[] { Own("flagship", origin) }));

        Assert.That(IndexOf(grid, '?'), Is.LessThan(IndexOf(grid, '@')),
            "a contact due north should render on a row above the reference unit.");
    }

    [Test]
    public void Glyph_ReflectsClassificationProgress_NotGroundTruth()
    {
        Vector2 origin = new(0f, 0f);

        Assert.Multiple(() =>
        {
            Assert.That(GlyphFor(Track(new Vector2(10f, 0f), TrackState.Detected)), Is.EqualTo('?'));
            Assert.That(GlyphFor(Track(new Vector2(10f, 0f), TrackState.Classifying, ContactDomain.Air)), Is.EqualTo('a'));
            Assert.That(GlyphFor(Track(new Vector2(10f, 0f), TrackState.Identified, ContactDomain.Surface)), Is.EqualTo('S'));
            Assert.That(GlyphFor(Track(new Vector2(10f, 0f), TrackState.Stale)), Is.EqualTo('~'));
        });

        char GlyphFor(Track track)
        {
            string[] grid = GridLines(Snapshot(new[] { track }, new[] { Own("flagship", origin) }));
            return grid.SelectMany(static line => line)
                .First(ch => ch is '?' or 'a' or 'S' or '~');
        }
    }

    
    private static string[] GridLines(TrackPictureSnapshot snapshot)
    {
        string[] lines = new ScopeRenderer().Render(snapshot)
            .Split('\n')
            .Select(static line => line.TrimEnd('\r'))
            .ToArray();

        int top = Array.FindIndex(lines, static line => line.StartsWith("+-", StringComparison.Ordinal));
        int bottom = Array.FindLastIndex(lines, static line => line.StartsWith("+-", StringComparison.Ordinal));
        return lines[(top + 1)..bottom];
    }

    private static int IndexOf(string[] grid, char glyph) =>
        Array.FindIndex(grid, line => line.IndexOf(glyph) >= 0);
}
