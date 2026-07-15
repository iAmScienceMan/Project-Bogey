using System.Collections.Generic;
using Content.Shared.Components;
using Content.Shared.Events;
using Content.Shared.Tracks;
using Lattice.Sim.Engine;

namespace Content.Sim.Systems;

public sealed class TrackDecaySystem : EntitySystem
{
    [Dependency]
    private readonly TrackingSystem _tracking = null!;

    [Dependency]
    private readonly EventBus _bus = null!;

    [Dependency]
    private readonly SimClock _clock = null!;

    [Dependency]
    private readonly SimConfig _config = null!;

    public override void Update()
    {
        foreach (string faction in new List<string>(_tracking.KnownFactions))
        {
            List<KeyValuePair<EntityUid, Track>> entries = new(_tracking.EntriesFor(faction));

            foreach (KeyValuePair<EntityUid, Track> entry in entries)
            {
                EntityUid truthEntity = entry.Key;
                Track track = entry.Value;

                int idleTicks = _clock.CurrentTick - track.LastUpdatedTick;
                if (idleTicks <= 0)
                {
                    continue;
                }

                if (idleTicks >= _config.DropAfterIdleTicks)
                {
                    _bus.Publish(new TrackDroppedEvent
                    {
                        ObserverFaction = faction,
                        TruthEntityId = truthEntity,
                    });
                    continue;
                }

                TrackState state = idleTicks >= _config.StaleAfterIdleTicks ? TrackState.Stale : track.State;

                _tracking.Set(faction, truthEntity, track with
                {
                    Confidence = track.Confidence * _config.DecayConfidenceFactor,
                    PositionalErrorKm = track.PositionalErrorKm + _config.PositionalErrorGrowthKmPerTick,
                    State = state,
                });
            }
        }
    }
}
