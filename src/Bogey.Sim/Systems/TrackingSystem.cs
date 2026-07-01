using System;
using System.Collections.Generic;
using Bogey.Shared.Events;
using Bogey.Shared.Tracks;
using Bogey.Sim.Engine;

namespace Bogey.Sim.Systems;

public sealed class TrackingSystem : SystemBase
{
    [Dependency]
    private readonly EventBus _bus = null!;

    [Dependency]
    private readonly SimConfig _config = null!;

    private readonly Dictionary<int, Track> _tracksByTruthEntity = new();
    private int _nextTrackId;

    public override void Initialize()
    {
        _bus.SubscribeDirected<ContactDetectedEvent>(OnContactDetected);
        _bus.Subscribe<TrackDroppedEvent>(OnTrackDropped);
    }

    
    public IReadOnlyDictionary<int, Track> Entries => _tracksByTruthEntity;

    
    public void Set(int truthEntityId, Track track) => _tracksByTruthEntity[truthEntityId] = track;

    
    public IReadOnlyList<Track> CurrentTracks => new List<Track>(_tracksByTruthEntity.Values);

    private void OnContactDetected(int truthEntityId, ContactDetectedEvent evt)
    {
        float gain = _config.ConfidenceGainPerHit * (0.5f + 0.5f * evt.DetectionStrength);

        if (_tracksByTruthEntity.TryGetValue(truthEntityId, out Track? existing))
        {
            _tracksByTruthEntity[truthEntityId] = existing with
            {
                EstimatedPosition = evt.ObservedPosition,
                PositionalErrorKm = _config.BasePositionalErrorKm,
                Confidence = Math.Clamp(existing.Confidence + gain, 0f, 1f),
                LastUpdatedTick = evt.Tick,
                
                State = existing.State is TrackState.Stale or TrackState.Dropped
                    ? TrackState.Detected
                    : existing.State,
            };
            return;
        }

        _tracksByTruthEntity[truthEntityId] = new Track
        {
            TrackId = ++_nextTrackId,
            EstimatedPosition = evt.ObservedPosition,
            PositionalErrorKm = _config.BasePositionalErrorKm,
            Confidence = _config.InitialConfidence,
            DomainGuess = Shared.Components.ContactDomain.Unknown,
            TypeGuess = null,
            LastUpdatedTick = evt.Tick,
            State = TrackState.Detected,
        };
    }

    private void OnTrackDropped(TrackDroppedEvent evt) => _tracksByTruthEntity.Remove(evt.TruthEntityId);
}
