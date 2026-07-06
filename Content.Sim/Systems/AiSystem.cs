using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Components;
using Content.Shared.Tracks;
using Lattice.Sim.Engine;

namespace Content.Sim.Systems;

public sealed class AiSystem : SystemBase
{
    [Dependency]
    private readonly EntityManager _entities = null!;

    [Dependency]
    private readonly TrackingSystem _tracking = null!;

    public override void Update()
    {
        foreach (int entity in _entities.Query<Ai, Transform>())
        {
            if (_entities.GetComponent<Ai>(entity).Behavior != AiBehavior.Aggressive)
            {
                continue;
            }

            if (!_entities.TryGetComponent(entity, out Propulsion propulsion))
            {
                continue;
            }

            FactionType side = _entities.GetComponent<Faction>(entity).Side;
            Vector2 position = _entities.GetComponent<Transform>(entity).Position;

            if (TryNearestContact(side, position, out Vector2 destination))
            {
                propulsion.Waypoint = destination;
            }
        }
    }

    private bool TryNearestContact(FactionType side, Vector2 from, out Vector2 destination)
    {
        destination = Vector2.Zero;
        int best = -1;
        float bestDistance = float.MaxValue;

        foreach (KeyValuePair<int, Track> entry in _tracking.EntriesFor(side))
        {
            if (!_entities.TryGetComponent(entry.Key, out Faction faction) || !Factions.AreHostile(side, faction.Side))
            {
                continue;
            }

            float distance = Vector2.Distance(from, entry.Value.EstimatedPosition);
            if (distance < bestDistance || (distance == bestDistance && entry.Key < best))
            {
                best = entry.Key;
                bestDistance = distance;
                destination = entry.Value.EstimatedPosition;
            }
        }

        return best >= 0;
    }
}
