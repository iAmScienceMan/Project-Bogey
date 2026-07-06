using Bogey.Shared.Components;
using Bogey.Shared.Events;
using Bogey.Sim.Engine;

namespace Bogey.Sim.Systems;

public sealed class DamageSystem : SystemBase
{
    [Dependency]
    private readonly EntityManager _entities = null!;

    [Dependency]
    private readonly EventBus _bus = null!;

    public override void Initialize()
    {
        _bus.SubscribeDirected<DamageEvent>((entity, damage) =>
        {
            if (!_entities.TryGetComponent(entity, out Health health) || !health.IsAlive)
            {
                return;
            }

            health.Current -= damage.Amount;
            Log.Debug($"Entity {entity} took {damage.Amount} damage ({health.Current}/{health.Max} remaining).");

            if (health.IsAlive)
            {
                return;
            }

            _entities.DestroyEntity(entity);
            _bus.Publish(new EntityDestroyedEvent
            {
                EntityId = entity,
                KillerEntity = damage.SourceEntity,
            });
            Log.Info($"Entity {entity} destroyed by entity {damage.SourceEntity}.");
        });
    }
}
