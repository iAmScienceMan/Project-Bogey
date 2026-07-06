using System.Numerics;
using Content.Shared.Components;
using Content.Shared.Events;
using Lattice.Sim.Engine;

namespace Content.Sim.Systems;

public sealed class OrderingSystem : SystemBase
{
    [Dependency]
    private readonly EntityManager _entities = null!;

    [Dependency]
    private readonly EventBus _bus = null!;

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
        foreach (int entity in _entities.Query<Propulsion, Transform>())
        {
            Propulsion propulsion = _entities.GetComponent<Propulsion>(entity);
            if (propulsion.Waypoint is not { } waypoint)
            {
                continue; 
            }

            Transform transform = _entities.GetComponent<Transform>(entity);
            Vector2 toGo = waypoint - transform.Position;
            float distance = toGo.Length();
            float stepKm = propulsion.MaxSpeedKmPerTick * (float)SimClock.SecondsPerTick;

            if (distance < 1e-3f)
            {
                transform.Velocity = Vector2.Zero;
                propulsion.Waypoint = null;
                continue;
            }

            if (distance <= stepKm)
            {
                transform.Velocity = toGo / (float)SimClock.SecondsPerTick;
                continue;
            }

            transform.Velocity = toGo / distance * propulsion.MaxSpeedKmPerTick;
        }
    }
}
