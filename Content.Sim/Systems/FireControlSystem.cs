using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Components;
using Content.Shared.Events;
using Content.Shared.Tracks;
using Lattice.Sim.Engine;

namespace Content.Sim.Systems;

public sealed class FireControlSystem : EntitySystem
{
    [Dependency]
    private readonly EntityManager _entities = null!;

    [Dependency]
    private readonly EventBus _bus = null!;

    [Dependency]
    private readonly TrackingSystem _tracking = null!;

    [Dependency]
    private readonly Random _rng = null!;

    [Dependency]
    private readonly SimConfig _config = null!;

    [Dependency]
    private readonly PrototypeManager _prototypes = null!;

    private readonly List<PendingOrder> _orders = new();

    public override void Initialize()
    {
        _bus.Subscribe<EngagementOrderEvent>(order =>
            _orders.Add(new PendingOrder(order.Shooter, order.TrackId, order.Weapon, Math.Max(1, order.Count))));

        SubscribeLocalEvent<Loadout, ComponentInit>((_, loadout, _) =>
        {
            foreach (WeaponMount mount in loadout.Mounts)
            {
                mount.RoundsRemaining = mount.MagazineCapacity;
            }
        });
    }

    public override void Update()
    {
        TickCooldowns();
        ReleaseStaleLocks();

        Dictionary<int, int> committedByTarget = CountCommittedMunitions();
        ProcessManualOrders(committedByTarget);
        ProcessPosture(committedByTarget);
    }

    private void TickCooldowns()
    {
        foreach (int entity in _entities.Query<Loadout>())
        {
            foreach (WeaponMount mount in _entities.GetComponent<Loadout>(entity).Mounts)
            {
                if (mount.TicksUntilReady > 0)
                {
                    mount.TicksUntilReady--;
                }
            }
        }
    }

    private void ProcessManualOrders(Dictionary<int, int> committedByTarget)
    {
        if (_orders.Count == 0)
        {
            return;
        }

        List<PendingOrder> surviving = new();
        foreach (PendingOrder order in _orders)
        {
            if (TryAdvanceOrder(order, committedByTarget, out PendingOrder remainder))
            {
                surviving.Add(remainder);
            }
        }

        _orders.Clear();
        _orders.AddRange(surviving);
    }

    private bool TryAdvanceOrder(PendingOrder order, Dictionary<int, int> committedByTarget, out PendingOrder remainder)
    {
        remainder = order;

        int shooter = order.Shooter;
        if (!_entities.HasComponent<Loadout>(shooter) || !_entities.HasComponent<Faction>(shooter))
        {
            return false;
        }

        Faction side = _entities.GetComponent<Faction>(shooter);
        IReadOnlyDictionary<int, Track> picture = _tracking.EntriesFor(side.EffectiveId);

        if (!TryResolveTrack(picture, order.TrackId, out int target, out Track track))
        {
            return false;
        }

        if (!_entities.TryGetComponent(target, out Faction targetFaction) || !Factions.AreHostile(side, targetFaction))
        {
            return false;
        }

        WeaponMount? mount = FindOffensiveMount(shooter, order.Weapon);
        if (mount is null || ProjectileFor(mount) is not { } projectile)
        {
            return false;
        }

        Vector2 origin = _entities.GetComponent<Transform>(shooter).Position;
        bool inRange = Vector2.Distance(origin, track.EstimatedPosition) <= projectile.RangeKm;

        if (mount.TicksUntilReady > 0 || !HasAmmo(mount) || !inRange)
        {
            return true;
        }

        if (!Fire(shooter, side, origin, target, mount, track))
        {
            return false;
        }

        AutoLock(shooter, target);
        ConsumeRound(mount);
        committedByTarget[target] = committedByTarget.GetValueOrDefault(target) + 1;

        int remaining = order.Remaining - 1;
        mount.TicksUntilReady = remaining > 0 ? 1 : mount.CooldownTicks;
        remainder = order with { Remaining = remaining };
        return remaining > 0;
    }

    private void ReleaseStaleLocks()
    {
        foreach (int entity in _entities.Query<WeaponControl>())
        {
            WeaponControl control = _entities.GetComponent<WeaponControl>(entity);
            if (control.LockedTarget >= 0 && !_entities.HasComponent<Transform>(control.LockedTarget))
            {
                control.LockedTarget = -1;
            }
        }
    }

    private void AutoLock(int shooter, int target)
    {
        if (_entities.TryGetComponent(shooter, out WeaponControl control) && control.LockedTarget < 0)
        {
            control.LockedTarget = target;
            Log.Debug($"Entity {shooter} radar locked entity {target}.");
        }
    }

    private void ProcessPosture(Dictionary<int, int> committedByTarget)
    {
        foreach (int shooter in new List<int>(_entities.Query<Loadout, WeaponControl>()))
        {
            if (!_config.AiEnabled && _entities.HasComponent<Ai>(shooter))
            {
                continue;
            }

            WeaponPosture posture = _entities.GetComponent<WeaponControl>(shooter).Posture;
            if (posture == WeaponPosture.Hold)
            {
                continue;
            }

            Faction side = _entities.GetComponent<Faction>(shooter);
            Vector2 origin = _entities.GetComponent<Transform>(shooter).Position;
            IReadOnlyDictionary<int, Track> picture = _tracking.EntriesFor(side.EffectiveId);

            foreach (WeaponMount mount in _entities.GetComponent<Loadout>(shooter).Mounts)
            {
                if (mount.TicksUntilReady > 0 || !HasAmmo(mount))
                {
                    continue;
                }

                if (mount.PointDefense)
                {
                    ServicePointDefense(shooter, side, origin, mount, picture);
                    continue;
                }

                if (posture != WeaponPosture.Free)
                {
                    continue;
                }

                if (ProjectileFor(mount) is not { } projectile)
                {
                    continue;
                }

                int target = SelectOffensiveTarget(side, origin, projectile, picture, committedByTarget);
                if (target < 0)
                {
                    continue;
                }

                if (Fire(shooter, side, origin, target, mount, picture[target]))
                {
                    AutoLock(shooter, target);
                    ConsumeRound(mount);
                    mount.TicksUntilReady = mount.CooldownTicks;
                    committedByTarget[target] = committedByTarget.GetValueOrDefault(target) + 1;
                }
            }
        }
    }

    private Dictionary<int, int> CountCommittedMunitions()
    {
        Dictionary<int, int> committed = new();
        foreach (int munition in _entities.Query<Projectile>())
        {
            int target = _entities.GetComponent<Projectile>(munition).TargetEntity;
            committed[target] = committed.GetValueOrDefault(target) + 1;
        }

        return committed;
    }

    private int SelectOffensiveTarget(
        Faction side,
        Vector2 origin,
        Projectile projectile,
        IReadOnlyDictionary<int, Track> picture,
        IReadOnlyDictionary<int, int> committedByTarget)
    {
        int best = -1;
        float bestDistance = float.MaxValue;

        foreach (KeyValuePair<int, Track> entry in picture)
        {
            int truthEntity = entry.Key;
            Track track = entry.Value;

            if (!projectile.TargetDomains.Contains(track.DomainGuess))
            {
                continue;
            }

            if (!_entities.TryGetComponent(truthEntity, out Faction faction) || !Factions.AreHostile(side, faction))
            {
                continue;
            }

            if (committedByTarget.GetValueOrDefault(truthEntity) >= _config.MaxAutoCommitPerTarget)
            {
                continue;
            }

            float distance = Vector2.Distance(origin, track.EstimatedPosition);
            if (distance > projectile.RangeKm)
            {
                continue;
            }

            if (distance < bestDistance || (distance == bestDistance && truthEntity < best))
            {
                best = truthEntity;
                bestDistance = distance;
            }
        }

        return best;
    }

    private void ServicePointDefense(
        int shooter,
        Faction side,
        Vector2 origin,
        WeaponMount mount,
        IReadOnlyDictionary<int, Track> picture)
    {
        int threat = -1;
        float bestDistance = float.MaxValue;

        foreach (KeyValuePair<int, Track> entry in picture)
        {
            int truthEntity = entry.Key;
            if (!_entities.HasComponent<Projectile>(truthEntity))
            {
                continue;
            }

            if (!_entities.TryGetComponent(truthEntity, out Faction faction) || !Factions.AreHostile(side, faction))
            {
                continue;
            }

            float distance = Vector2.Distance(origin, entry.Value.EstimatedPosition);
            if (distance > mount.PointDefenseRangeKm)
            {
                continue;
            }

            if (distance < bestDistance || (distance == bestDistance && truthEntity < threat))
            {
                threat = truthEntity;
                bestDistance = distance;
            }
        }

        if (threat < 0)
        {
            return;
        }

        ConsumeRound(mount);
        mount.TicksUntilReady = mount.CooldownTicks;

        if (_rng.NextDouble() < mount.PointDefensePk)
        {
            _bus.PublishDirected(threat, new DamageEvent
            {
                Amount = float.MaxValue,
                SourceEntity = shooter,
            });
            Log.Debug($"Point defense on entity {shooter} destroyed inbound munition {threat}.");
        }
    }

    private bool Fire(int shooter, Faction side, Vector2 origin, int target, WeaponMount mount, Track track)
    {
        if (!_prototypes.Has(mount.ProjectilePrototype))
        {
            Log.Error($"Entity {shooter} weapon references unknown projectile prototype '{mount.ProjectilePrototype}'.");
            return false;
        }

        int munitionEntity = _prototypes.SpawnEntity(_entities, mount.ProjectilePrototype);
        _bus.PublishDirected(munitionEntity, new ComponentInit());

        if (!_entities.TryGetComponent(munitionEntity, out Projectile munition))
        {
            Log.Error($"Projectile prototype '{mount.ProjectilePrototype}' has no projectile component.");
            _entities.DestroyEntity(munitionEntity);
            return false;
        }

        if (_entities.TryGetComponent(munitionEntity, out Transform transform))
        {
            transform.Position = origin;
        }

        munition.OwnerEntity = shooter;
        munition.TargetEntity = target;
        munition.ObserverFaction = side.EffectiveId;
        munition.Datum = GuidanceSystem.InterceptPoint(
            origin, munition.SpeedKmPerTick, track.EstimatedPosition, track.EstimatedVelocity);
        Faction munitionFaction = _entities.GetComponent<Faction>(munitionEntity);
        munitionFaction.Side = side.Side;
        munitionFaction.Id = side.EffectiveId;

        _bus.Publish(new WeaponFiredEvent
        {
            Shooter = shooter,
            Target = target,
            Weapon = mount.ProjectilePrototype,
        });
        Log.Debug($"Entity {shooter} fired '{mount.ProjectilePrototype}' ({munitionEntity}) at entity {target}.");
        return true;
    }

    private Projectile? ProjectileFor(WeaponMount mount)
        => _prototypes.Has(mount.ProjectilePrototype)
           && _prototypes.Get(mount.ProjectilePrototype).TryGetComponent(out Projectile projectile)
            ? projectile
            : null;

    private WeaponMount? FindOffensiveMount(int shooter, string weapon)
    {
        foreach (WeaponMount mount in _entities.GetComponent<Loadout>(shooter).Mounts)
        {
            if (!mount.PointDefense
                && string.Equals(mount.ProjectilePrototype, weapon, StringComparison.OrdinalIgnoreCase))
            {
                return mount;
            }
        }

        return null;
    }

    private static bool TryResolveTrack(IReadOnlyDictionary<int, Track> picture, int trackId, out int truthEntity, out Track track)
    {
        foreach (KeyValuePair<int, Track> entry in picture)
        {
            if (entry.Value.TrackId == trackId)
            {
                truthEntity = entry.Key;
                track = entry.Value;
                return true;
            }
        }

        truthEntity = -1;
        track = null!;
        return false;
    }

    private static bool HasAmmo(WeaponMount mount) => mount.MagazineCapacity <= 0 || mount.RoundsRemaining > 0;

    private static void ConsumeRound(WeaponMount mount)
    {
        if (mount.MagazineCapacity > 0)
        {
            mount.RoundsRemaining--;
        }
    }

    private readonly record struct PendingOrder(int Shooter, int TrackId, string Weapon, int Remaining);
}
