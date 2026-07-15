using System.Numerics;
using Content.Shared.Components;
using Content.Shared.Events;
using Lattice.Sim.Engine;

namespace Content.Sim.Systems;

public sealed class OrderingSystem : EntitySystem
{
    [Dependency]
    private readonly EntityManager _entities = null!;

    [Dependency]
    private readonly EventBus _bus = null!;

    [Dependency]
    private readonly SimClock _clock = null!;

    public override void Initialize()
    {
        _bus.SubscribeDirected<MoveOrderEvent>((entity, order) =>
        {
            if (_entities.TryGetComponent(entity, out Propulsion propulsion))
            {
                propulsion.Waypoint = order.Destination;
                Log.Debug($"Entity {entity} waypoint set to {order.Destination}.");
            }
        });
    }

    public override void Update()
    {
        float dt = (float)_clock.Dt;
        EntityQueryEnumerator<Propulsion, Transform> query = _entities.AllEntityQuery<Propulsion, Transform>();
        while (query.MoveNext(out _, out Propulsion propulsion, out Transform transform))
        {
            if (propulsion.Waypoint is not { } waypoint)
            {
                continue;
            }

            Vector2 toGo = waypoint - transform.Position;
            float distance = toGo.Length();
            float stepKm = propulsion.MaxSpeedKmPerTick * dt;

            if (distance < 1e-3f)
            {
                transform.Velocity = Vector2.Zero;
                propulsion.Waypoint = null;
                continue;
            }

            if (distance <= stepKm)
            {
                transform.Velocity = toGo / dt;
                continue;
            }

            transform.Velocity = toGo / distance * propulsion.MaxSpeedKmPerTick;
        }
    }
}
