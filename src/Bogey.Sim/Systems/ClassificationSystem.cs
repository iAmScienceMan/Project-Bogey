using System.Collections.Generic;
using Bogey.Shared.Components;
using Bogey.Shared.Tracks;
using Bogey.Sim.Engine;

namespace Bogey.Sim.Systems;

public sealed class ClassificationSystem : SystemBase
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
        
        List<KeyValuePair<int, Track>> entries = new(_tracking.Entries);

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

            Track resolved = Resolve(track, profile);
            _tracking.Set(truthEntity, resolved);
        }
    }

    private Track Resolve(Track track, ClassificationProfile profile)
    {
        if (track.Confidence >= _config.IdentifyThreshold)
        {
            return track with
            {
                DomainGuess = profile.Domain,
                TypeGuess = profile.TypeName,
                State = TrackState.Identified,
            };
        }

        if (track.Confidence >= _config.ClassifyThreshold)
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
