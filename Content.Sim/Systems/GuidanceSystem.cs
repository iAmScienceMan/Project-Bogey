using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Components;
using Content.Shared.Events;
using Content.Shared.Tracks;
using Lattice.Sim.Engine;

namespace Content.Sim.Systems;

public sealed class GuidanceSystem : EntitySystem
{
    private const float OffDomainPkFactor = 0.15f;
    private const float MaxLeadTicks = 120f;

    [Dependency]
    private readonly EntityManager _entities = null!;

    [Dependency]
    private readonly EventBus _bus = null!;

    [Dependency]
    private readonly TrackingSystem _tracking = null!;

    [Dependency]
    private readonly Random _rng = null!;

    public override void Initialize()
    {
        SubscribeLocalEvent<Projectile, ComponentInit>((entity, projectile, _) =>
        {
            float launchSpeed = TryComp<Propulsion>(entity, out Propulsion propulsion)
                ? propulsion.MaxSpeedKmPerTick
                : 0f;
            projectile.SpeedKmPerTick = launchSpeed;
            projectile.BurnoutSpeedKmPerTick = launchSpeed * 0.25f;
        });
    }

    public override void Update()
    {
        foreach (int entity in new List<int>(_entities.Query<Projectile, Transform>()))
        {
            if (!_entities.TryGetComponent(entity, out Projectile projectile)
                || !_entities.TryGetComponent(entity, out Transform transform))
            {
                continue;
            }

            if (!UpdateEnergy(projectile))
            {
                Resolve(entity, projectile, hit: false);
                continue;
            }

            Vector2 heading = transform.Velocity.LengthSquared() > 1e-6f
                ? Vector2.Normalize(transform.Velocity)
                : SafeDirection(projectile.Datum - transform.Position);

            Seeker? seeker = _entities.TryGetComponent(entity, out Seeker found) ? found : null;
            SeekerType type = seeker?.Kind ?? SeekerType.Gps;

            if (seeker is not null && type != SeekerType.Gps)
            {
                UpdateSeekerLock(entity, projectile, seeker, transform.Position, heading);
            }

            UpdateDatum(projectile, type, seeker, transform.Position);

            float speed = projectile.SpeedKmPerTick;
            Vector2 aimpoint = SelectAimpoint(projectile, seeker, type, transform.Position, heading, speed);

            Vector2 direction = SafeDirection(aimpoint - transform.Position);
            if (direction == Vector2.Zero)
            {
                direction = heading == Vector2.Zero ? new Vector2(1f, 0f) : heading;
            }

            Vector2 step = direction * speed;

            if (TryFuse(entity, projectile, seeker, type, transform.Position, step))
            {
                continue;
            }

            if (seeker is not { Locked: true }
                && type != SeekerType.Gps
                && !projectile.DatumPassed
                && Vector2.Distance(transform.Position, projectile.Datum) <= speed)
            {
                projectile.DatumPassed = true;
            }

            transform.Velocity = step;
        }
    }

    private static bool UpdateEnergy(Projectile projectile)
    {
        if (projectile.MotorBurnTicksRemaining > 0)
        {
            projectile.MotorBurnTicksRemaining--;
            return true;
        }

        projectile.SpeedKmPerTick *= 1f - projectile.DragPerTick;
        return projectile.SpeedKmPerTick > projectile.BurnoutSpeedKmPerTick;
    }

    private void UpdateSeekerLock(int munition, Projectile projectile, Seeker seeker, Vector2 position, Vector2 heading)
    {
        if (seeker.LockedEntity >= 0
            && IsLockable(munition, projectile, seeker, seeker.LockedEntity)
            && WithinSeeker(position, heading, _entities.GetComponent<Transform>(seeker.LockedEntity).Position, seeker))
        {
            return;
        }

        seeker.LockedEntity = -1;

        if (IsLockable(munition, projectile, seeker, projectile.TargetEntity)
            && WithinSeeker(position, heading, _entities.GetComponent<Transform>(projectile.TargetEntity).Position, seeker))
        {
            seeker.LockedEntity = projectile.TargetEntity;
            return;
        }

        int best = -1;
        float bestDistance = float.MaxValue;
        foreach (int candidate in _entities.Query<Transform, Health>())
        {
            if (!IsLockable(munition, projectile, seeker, candidate))
            {
                continue;
            }

            Vector2 candidatePosition = _entities.GetComponent<Transform>(candidate).Position;
            if (!WithinSeeker(position, heading, candidatePosition, seeker)
                || !WithinAcquisitionBasket(projectile, seeker, candidatePosition))
            {
                continue;
            }

            float distance = Vector2.Distance(position, candidatePosition);
            if (distance < bestDistance || (distance == bestDistance && candidate < best))
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        seeker.LockedEntity = best;
    }

    private static bool WithinAcquisitionBasket(Projectile projectile, Seeker seeker, Vector2 candidatePosition)
        => projectile.DatumPassed
           || seeker.Kind == SeekerType.AntiRadiation
           || Vector2.Distance(candidatePosition, projectile.Datum) <= seeker.AcquisitionRangeKm;

    private bool IsLockable(int munition, Projectile projectile, Seeker seeker, int candidate)
    {
        if (candidate < 0 || candidate == munition || candidate == projectile.OwnerEntity)
        {
            return false;
        }

        if (!_entities.HasComponent<Transform>(candidate)
            || !_entities.TryGetComponent(candidate, out Health health)
            || !health.IsAlive)
        {
            return false;
        }

        if (_entities.HasComponent<Projectile>(candidate) && !seeker.TargetsMunitions)
        {
            return false;
        }

        if (seeker.Kind == SeekerType.AntiRadiation)
        {
            return _entities.HasComponent<Sensor>(candidate)
                && _entities.TryGetComponent(candidate, out Faction emitter)
                && Factions.AreHostile(projectile.ObserverFaction, emitter.Side);
        }

        if (seeker.Kind == SeekerType.SemiActiveRadar && !IsIlluminatedByShooter(projectile.OwnerEntity, candidate))
        {
            return false;
        }

        return _entities.TryGetComponent(candidate, out ClassificationProfile profile)
            && profile.Domain is ContactDomain.Air or ContactDomain.Surface or ContactDomain.Munition;
    }

    private bool IsIlluminatedByShooter(int shooter, int candidate)
        => _entities.HasComponent<Transform>(shooter)
           && _entities.TryGetComponent(shooter, out WeaponControl control)
           && control.LockedTarget == candidate;

    private void UpdateDatum(Projectile projectile, SeekerType type, Seeker? seeker, Vector2 missilePosition)
    {
        bool guided = type switch
        {
            SeekerType.ActiveRadar when seeker is { Datalink: true } => true,
            SeekerType.SemiActiveRadar => true,
            _ => false,
        };

        if (!guided || !ShooterMaintainsLock(projectile))
        {
            return;
        }

        if (_tracking.EntriesFor(projectile.ObserverFaction).TryGetValue(projectile.TargetEntity, out Track? track))
        {
            projectile.Datum = InterceptPoint(
                missilePosition, projectile.SpeedKmPerTick, track.EstimatedPosition, track.EstimatedVelocity);
            projectile.DatumPassed = false;
        }
    }

    private bool ShooterMaintainsLock(Projectile projectile)
        => _entities.HasComponent<Transform>(projectile.OwnerEntity)
           && _entities.TryGetComponent(projectile.OwnerEntity, out WeaponControl control)
           && control.LockedTarget == projectile.TargetEntity;

    private Vector2 SelectAimpoint(
        Projectile projectile, Seeker? seeker, SeekerType type, Vector2 position, Vector2 heading, float speed)
    {
        if (seeker is { LockedEntity: >= 0 } && _entities.TryGetComponent(seeker.LockedEntity, out Transform locked))
        {
            return InterceptPoint(position, speed, locked.Position, locked.Velocity);
        }

        if (type == SeekerType.Gps || !projectile.DatumPassed)
        {
            return projectile.Datum;
        }

        return position + (heading * MathF.Max(speed, 1f));
    }

    private bool TryFuse(int munition, Projectile projectile, Seeker? seeker, SeekerType type, Vector2 position, Vector2 step)
    {
        if (seeker is { LockedEntity: >= 0 } && _entities.TryGetComponent(seeker.LockedEntity, out Transform locked))
        {
            Vector2 closest = ClosestPointOnSegment(position, position + step, locked.Position);
            if (Vector2.Distance(closest, locked.Position) <= projectile.DetonationRangeKm)
            {
                Burst(munition, projectile, closest);
                return true;
            }

            return false;
        }

        if (type == SeekerType.Gps)
        {
            Vector2 closest = ClosestPointOnSegment(position, position + step, projectile.Datum);
            if (Vector2.Distance(closest, projectile.Datum) <= projectile.DetonationRangeKm)
            {
                Burst(munition, projectile, closest);
                return true;
            }
        }

        return false;
    }

    private void Burst(int munition, Projectile projectile, Vector2 burstPoint)
    {
        bool hit = false;

        foreach (int victim in new List<int>(_entities.Query<Transform, Health>()))
        {
            if (victim == munition || victim == projectile.OwnerEntity)
            {
                continue;
            }

            if (!_entities.GetComponent<Health>(victim).IsAlive)
            {
                continue;
            }

            if (Vector2.Distance(_entities.GetComponent<Transform>(victim).Position, burstPoint) > projectile.DetonationRangeKm)
            {
                continue;
            }

            if (_rng.NextDouble() >= EffectivePk(projectile, victim))
            {
                continue;
            }

            hit = true;
            _bus.PublishDirected(victim, new DamageEvent
            {
                Amount = projectile.Damage,
                SourceEntity = projectile.OwnerEntity,
            });
        }

        Resolve(munition, projectile, hit);
    }

    private float EffectivePk(Projectile projectile, int victim)
    {
        if (projectile.TargetDomains.Count == 0)
        {
            return projectile.Pk;
        }

        ContactDomain domain = _entities.TryGetComponent(victim, out ClassificationProfile profile)
            ? profile.Domain
            : ContactDomain.Unknown;

        return projectile.TargetDomains.Contains(domain)
            ? projectile.Pk
            : projectile.Pk * OffDomainPkFactor;
    }

    public static Vector2 InterceptPoint(Vector2 from, float speed, Vector2 targetPosition, Vector2 targetVelocity)
    {
        Vector2 relative = targetPosition - from;
        if (speed <= 1e-4f || targetVelocity.LengthSquared() < 1e-8f)
        {
            return targetPosition;
        }

        float a = targetVelocity.LengthSquared() - (speed * speed);
        float b = 2f * Vector2.Dot(relative, targetVelocity);
        float c = relative.LengthSquared();

        float time;
        if (MathF.Abs(a) < 1e-6f)
        {
            time = b < -1e-6f ? -c / b : 0f;
        }
        else
        {
            float discriminant = (b * b) - (4f * a * c);
            if (discriminant < 0f)
            {
                return targetPosition;
            }

            float root = MathF.Sqrt(discriminant);
            float t1 = (-b - root) / (2f * a);
            float t2 = (-b + root) / (2f * a);
            time = MathF.Min(t1, t2) > 0f ? MathF.Min(t1, t2) : MathF.Max(t1, t2);
        }

        time = Math.Clamp(time, 0f, MaxLeadTicks);
        return targetPosition + (targetVelocity * time);
    }

    private static Vector2 ClosestPointOnSegment(Vector2 start, Vector2 end, Vector2 point)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared < 1e-8f)
        {
            return start;
        }

        float t = Math.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0f, 1f);
        return start + (segment * t);
    }

    private static bool WithinSeeker(Vector2 position, Vector2 heading, Vector2 targetPosition, Seeker seeker)
    {
        Vector2 toTarget = targetPosition - position;
        float distance = toTarget.Length();
        if (distance > seeker.AcquisitionRangeKm)
        {
            return false;
        }

        if (seeker.FovDegrees >= 360f || distance < 1e-4f)
        {
            return true;
        }

        if (heading == Vector2.Zero)
        {
            return true;
        }

        float cosHalfFov = MathF.Cos(seeker.FovDegrees * 0.5f * (MathF.PI / 180f));
        return Vector2.Dot(heading, toTarget / distance) >= cosHalfFov;
    }

    private static Vector2 SafeDirection(Vector2 delta)
    {
        float length = delta.Length();
        return length > 1e-6f ? delta / length : Vector2.Zero;
    }

    private void Resolve(int munition, Projectile projectile, bool hit)
    {
        _bus.Publish(new MunitionResolvedEvent
        {
            Munition = munition,
            Target = projectile.TargetEntity,
            Hit = hit,
        });
        _entities.DestroyEntity(munition);
    }
}
