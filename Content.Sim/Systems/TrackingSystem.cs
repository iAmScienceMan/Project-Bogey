using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Components;
using Content.Shared.Events;
using Content.Shared.Tracks;
using Lattice.Sim.Engine;

namespace Content.Sim.Systems;

public sealed class TrackingSystem : EntitySystem
{
    [Dependency]
    private readonly EventBus _bus = null!;

    [Dependency]
    private readonly SimConfig _config = null!;

    private readonly Dictionary<string, Dictionary<int, Track>> _picturesByFaction = new(StringComparer.Ordinal);
    private int _nextTrackId;

    public override void Initialize()
    {
        _bus.SubscribeDirected<ContactDetectedEvent>(OnContactDetected);
        _bus.Subscribe<TrackDroppedEvent>(OnTrackDropped);
    }

    public IEnumerable<string> KnownFactions => _picturesByFaction.Keys;

    public IReadOnlyDictionary<int, Track> EntriesFor(string faction) => Picture(faction);

    public void Set(string faction, int truthEntityId, Track track) => Picture(faction)[truthEntityId] = track;

    public IReadOnlyList<Track> TracksFor(string faction) => new List<Track>(Picture(faction).Values);

    private void OnContactDetected(int truthEntityId, ContactDetectedEvent evt)
    {
        Dictionary<int, Track> picture = Picture(evt.ObserverFaction);
        float gain = _config.ConfidenceGainPerHit * (0.5f + 0.5f * evt.DetectionStrength);

        if (picture.TryGetValue(truthEntityId, out Track? existing))
        {
            int elapsedTicks = Math.Max(1, evt.Tick - existing.LastUpdatedTick);
            Vector2 measuredVelocity = (evt.ObservedPosition - existing.EstimatedPosition) / elapsedTicks;

            picture[truthEntityId] = existing with
            {
                EstimatedPosition = evt.ObservedPosition,
                EstimatedVelocity = Vector2.Lerp(existing.EstimatedVelocity, measuredVelocity, 0.4f),
                PositionalErrorKm = _config.BasePositionalErrorKm,
                Confidence = Math.Clamp(existing.Confidence + gain, 0f, 1f),
                LastUpdatedTick = evt.Tick,

                State = existing.State is TrackState.Stale or TrackState.Dropped
                    ? TrackState.Detected
                    : existing.State,
            };
            return;
        }

        picture[truthEntityId] = new Track
        {
            TrackId = ++_nextTrackId,
            EstimatedPosition = evt.ObservedPosition,
            PositionalErrorKm = _config.BasePositionalErrorKm,
            Confidence = _config.InitialConfidence,
            DomainGuess = ContactDomain.Unknown,
            TypeGuess = null,
            LastUpdatedTick = evt.Tick,
            State = TrackState.Detected,
        };
    }

    private void OnTrackDropped(TrackDroppedEvent evt) => Picture(evt.ObserverFaction).Remove(evt.TruthEntityId);

    private Dictionary<int, Track> Picture(string faction)
    {
        if (!_picturesByFaction.TryGetValue(faction, out Dictionary<int, Track>? picture))
        {
            picture = new Dictionary<int, Track>();
            _picturesByFaction[faction] = picture;
        }

        return picture;
    }
}
