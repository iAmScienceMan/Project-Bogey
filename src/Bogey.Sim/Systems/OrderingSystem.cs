using System.Numerics;
using Bogey.Shared.Components;
using Bogey.Shared.Events;
using Bogey.Sim.Engine;

namespace Bogey.Sim.Systems;

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

            
            if (distance <= stepKm)
            {
                transform.Velocity = Vector2.Zero;
                propulsion.Waypoint = null;
                continue;
            }

            transform.Velocity = toGo / distance * propulsion.MaxSpeedKmPerTick;
        }
    }
}
