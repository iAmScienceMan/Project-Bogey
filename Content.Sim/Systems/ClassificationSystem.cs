using System.Collections.Generic;
using Content.Shared.Components;
using Content.Shared.Tracks;
using Lattice.Sim.Engine;

namespace Content.Sim.Systems;

public sealed class ClassificationSystem : EntitySystem
{
    [Dependency]
    private readonly EntityManager _entities = null!;

    [Dependency]
    private readonly TrackingSystem _tracking = null!;

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

                if (track.LastUpdatedTick != _clock.CurrentTick)
                {
                    continue;
                }

                if (!_entities.TryGetComponent(truthEntity, out ClassificationProfile profile))
                {
                    continue;
                }

                _tracking.Set(faction, truthEntity, Resolve(track, profile));
            }
        }
    }

    private Track Resolve(Track track, ClassificationProfile profile)
    {
        bool munition = profile.Domain == ContactDomain.Munition;
        float identifyThreshold = munition ? _config.MunitionIdentifyThreshold : _config.IdentifyThreshold;
        float classifyThreshold = munition ? _config.MunitionClassifyThreshold : _config.ClassifyThreshold;

        if (track.Confidence >= identifyThreshold)
        {
            return track with
            {
                DomainGuess = profile.Domain,
                TypeGuess = profile.TypeName,
                State = TrackState.Identified,
            };
        }

        if (track.Confidence >= classifyThreshold)
        {
            return track with
            {
                DomainGuess = profile.Domain,
                TypeGuess = null,
                State = TrackState.Classifying,
            };
        }

        return track with
        {
            DomainGuess = ContactDomain.Unknown,
            TypeGuess = null,
            State = TrackState.Detected,
        };
    }
}
