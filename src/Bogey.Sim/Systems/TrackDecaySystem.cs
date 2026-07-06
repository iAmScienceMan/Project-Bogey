using System.Collections.Generic;
using Bogey.Shared.Components;
using Bogey.Shared.Events;
using Bogey.Shared.Tracks;
using Bogey.Sim.Engine;

namespace Bogey.Sim.Systems;

public sealed class TrackDecaySystem : SystemBase
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
        foreach (FactionType faction in Factions.InOrder)
        {
            List<KeyValuePair<int, Track>> entries = new(_tracking.EntriesFor(faction));

            foreach (KeyValuePair<int, Track> entry in entries)
            {
                int truthEntity = entry.Key;
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
