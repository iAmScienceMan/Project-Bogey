using Content.Shared.Components;
using Content.Shared.Events;
using Lattice.Sim.Engine;

namespace Content.Sim.Systems;

public sealed class DamageSystem : EntitySystem
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

        SubscribeLocalEvent<Health, ComponentInit>((_, health, _) => health.Current = health.Max);
    }
}
