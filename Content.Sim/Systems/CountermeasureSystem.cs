using System;
using System.Numerics;
using Content.Shared.Components;
using Lattice.Sim.Engine;

namespace Content.Sim.Systems;

public sealed class CountermeasureSystem : EntitySystem
{
    [Dependency]
    private readonly EntityManager _entities = null!;

    [Dependency]
    private readonly EventBus _bus = null!;

    [Dependency]
    private readonly PrototypeManager _prototypes = null!;

    [Dependency]
    private readonly Random _rng = null!;

    public override void Update()
    {
        AgeDecoys();
        ServiceDispensers();
    }

    private void AgeDecoys()
    {
        EntityQueryEnumerator<Decoy> query = _entities.AllEntityQuery<Decoy>();
        while (query.MoveNext(out EntityUid entity, out Decoy decoy))
        {
            if (--decoy.TicksRemaining <= 0)
            {
                _entities.DestroyEntity(entity);
            }
        }
    }

    private void ServiceDispensers()
    {
        EntityQueryEnumerator<Countermeasures, Transform> query = _entities.AllEntityQuery<Countermeasures, Transform>();
        while (query.MoveNext(out EntityUid unit, out Countermeasures cms, out Transform transform))
        {
            if (cms.TicksUntilReady > 0)
            {
                cms.TicksUntilReady--;
                continue;
            }

            if (cms.Chaff > 0 && RadarMissileLockedOn(unit))
            {
                Dispense(unit, cms, transform, DecoyKind.Chaff, cms.ChaffPrototype);
                cms.Chaff--;
                cms.TicksUntilReady = cms.CooldownTicks;
                continue;
            }

            if (cms.Flares > 0 && HeatMissileNear(transform.Position, cms.FlareTriggerRangeKm))
            {
                Dispense(unit, cms, transform, DecoyKind.Flare, cms.FlarePrototype);
                cms.Flares--;
                cms.TicksUntilReady = cms.CooldownTicks;
            }
        }
    }

    private bool RadarMissileLockedOn(EntityUid unit)
    {
        EntityQueryEnumerator<Projectile, Seeker> query = _entities.AllEntityQuery<Projectile, Seeker>();
        while (query.MoveNext(out _, out _, out Seeker seeker))
        {
            if (seeker.Locked
                && seeker.LockedEntity == unit
                && seeker.Kind is SeekerType.ActiveRadar or SeekerType.SemiActiveRadar)
            {
                return true;
            }
        }

        return false;
    }

    private bool HeatMissileNear(Vector2 position, float rangeKm)
    {
        EntityQueryEnumerator<Seeker, Transform> query = _entities.AllEntityQuery<Seeker, Transform>();
        while (query.MoveNext(out EntityUid missile, out Seeker seeker, out Transform transform))
        {
            if (seeker.Kind is SeekerType.Ir or SeekerType.Optical
                && _entities.HasComponent<Projectile>(missile)
                && Vector2.Distance(position, transform.Position) <= rangeKm)
            {
                return true;
            }
        }

        return false;
    }

    private void Dispense(EntityUid unit, Countermeasures cms, Transform transform, DecoyKind kind, string prototype)
    {
        if (!_prototypes.Has(prototype))
        {
            Log.Error($"Countermeasures on entity {unit} reference unknown decoy prototype '{prototype}'.");
            return;
        }

        Faction? faction = _entities.TryGetComponent(unit, out Faction found) ? found : null;

        for (int i = 0; i < cms.SalvoSize; i++)
        {
            EntityUid decoyEntity = _prototypes.SpawnEntity(_entities, prototype);
            _bus.PublishDirected(decoyEntity, new ComponentInit());

            if (_entities.TryGetComponent(decoyEntity, out Decoy decoy))
            {
                decoy.Kind = kind;
                decoy.TicksRemaining = decoy.LifetimeTicks;
            }

            if (_entities.TryGetComponent(decoyEntity, out Transform decoyTransform))
            {
                decoyTransform.Position = transform.Position;
                decoyTransform.Velocity = (transform.Velocity * 0.25f) + Spread();
            }

            if (faction is not null && _entities.TryGetComponent(decoyEntity, out Faction decoyFaction))
            {
                decoyFaction.Side = faction.Side;
                decoyFaction.Id = faction.Id;
            }
        }
    }

    private Vector2 Spread()
    {
        float dx = (float)(_rng.NextDouble() * 2.0 - 1.0) * 0.05f;
        float dy = (float)(_rng.NextDouble() * 2.0 - 1.0) * 0.05f;
        return new Vector2(dx, dy);
    }
}
