using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Components;
using Content.Shared.Tracks;
using Lattice.Sim.Engine;

namespace Content.Sim.Systems;

public sealed class AiSystem : EntitySystem
{
    [Dependency]
    private readonly EntityManager _entities = null!;

    [Dependency]
    private readonly TrackingSystem _tracking = null!;

    [Dependency]
    private readonly SimConfig _config = null!;

    public override void Update()
    {
        if (!_config.AiEnabled)
        {
            return;
        }

        EntityQueryEnumerator<Ai, Transform> query = _entities.AllEntityQuery<Ai, Transform>();
        while (query.MoveNext(out EntityUid entity, out Ai ai, out Transform transform))
        {
            if (ai.Behavior != AiBehavior.Aggressive)
            {
                continue;
            }

            if (!_entities.TryGetComponent(entity, out Propulsion propulsion))
            {
                continue;
            }

            Faction observer = _entities.GetComponent<Faction>(entity);
            Vector2 position = transform.Position;

            if (TryNearestContact(observer, position, out Vector2 contact))
            {
                propulsion.Waypoint = StandoffPoint(position, contact, ai.StandoffKm);
            }
        }
    }

    private static Vector2 StandoffPoint(Vector2 self, Vector2 contact, float standoffKm)
    {
        Vector2 away = self - contact;
        float distance = away.Length();
        Vector2 direction = distance > 1e-3f ? away / distance : new Vector2(1f, 0f);
        return contact + (direction * standoffKm);
    }

    private bool TryNearestContact(Faction observer, Vector2 from, out Vector2 destination)
    {
        destination = Vector2.Zero;
        EntityUid best = EntityUid.Invalid;
        float bestDistance = float.MaxValue;

        foreach (KeyValuePair<EntityUid, Track> entry in _tracking.EntriesFor(observer.EffectiveId))
        {
            if (!_entities.TryGetComponent(entry.Key, out Faction faction) || !Factions.AreHostile(observer, faction))
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

        return best.Valid;
    }
}
