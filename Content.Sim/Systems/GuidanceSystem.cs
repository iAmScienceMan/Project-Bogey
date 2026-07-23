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
    private const float KinematicMargin = 1.3f;
    private const float PositionCorrectionGain = 0.08f;
    private const float VelocityCorrectionGain = 0.04f;
    private const float TerminalHandoffKm = 2f;

    [Dependency]
    private readonly EntityManager _entities = null!;

    [Dependency]
    private readonly EventBus _bus = null!;

    [Dependency]
    private readonly TrackingSystem _tracking = null!;

    [Dependency]
    private readonly Random _rng = null!;

    [Dependency]
    private readonly SimClock _clock = null!;

    [Dependency]
    private readonly SimConfig _config = null!;

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
        float dt = (float)_clock.Dt;
        float maxTurnScale = dt * (MathF.PI / 180f);
        EntityQueryEnumerator<Projectile, Transform> query = _entities.AllEntityQuery<Projectile, Transform>();
        while (query.MoveNext(out EntityUid entity, out Projectile projectile, out Transform transform))
        {
            if (!UpdateEnergy(projectile, dt))
            {
                Resolve(entity, projectile, hit: false);
                continue;
            }

            Vector2 heading = transform.Velocity.LengthSquared() > 1e-6f
                ? Vector2.Normalize(transform.Velocity)
                : SafeDirection(projectile.Datum - transform.Position);

            Seeker? seeker = _entities.TryGetComponent(entity, out Seeker found) ? found : null;
            SeekerType type = seeker?.Kind ?? SeekerType.Gps;

            bool targetAlive = IsAliveTarget(projectile.TargetEntity);
            if (targetAlive)
            {
                projectile.LastTargetPosition = _entities.GetComponent<Transform>(projectile.TargetEntity).Position;
                projectile.HadLiveTarget = true;
            }

            if (seeker is not null && type != SeekerType.Gps)
            {
                UpdateSeekerLock(entity, projectile, seeker, transform.Position, heading);
                if (seeker.Locked)
                {
                    projectile.Ballistic = false;
                }
            }

            UpdateInertialGuidance(projectile, type, seeker, transform.Position, dt);
            HandleLostTarget(projectile, seeker, type, targetAlive);

            float speed = projectile.SpeedKmPerTick;
            Vector2 aimpoint = SelectAimpoint(projectile, seeker, type, transform.Position, heading, speed);

            Vector2 desired = SafeDirection(aimpoint - transform.Position);
            if (desired == Vector2.Zero)
            {
                desired = heading == Vector2.Zero ? new Vector2(1f, 0f) : heading;
            }

            float maxTurn = TurnRateOf(entity) * maxTurnScale;
            Vector2 newHeading = projectile.Ballistic ? heading : RotateToward(heading, desired, maxTurn);
            if (newHeading == Vector2.Zero)
            {
                newHeading = new Vector2(1f, 0f);
            }

            Vector2 step = newHeading * speed;

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

    private bool IsAliveTarget(EntityUid target)
        => target.Valid
           && _entities.HasComponent<Transform>(target)
           && _entities.TryGetComponent(target, out Health health)
           && health.IsAlive;

    private void HandleLostTarget(Projectile projectile, Seeker? seeker, SeekerType type, bool targetAlive)
    {
        if (targetAlive
            || type == SeekerType.Gps
            || !projectile.HadLiveTarget
            || seeker is { Locked: true }
            || projectile.Ballistic
            || projectile.Finishing)
        {
            return;
        }

        if (!projectile.DatumPassed)
        {
            projectile.Datum = projectile.LastTargetPosition;
            projectile.Finishing = true;
        }
        else
        {
            projectile.Ballistic = true;
        }
    }

    private float TurnRateOf(EntityUid entity)
        => _entities.TryGetComponent(entity, out Propulsion propulsion)
            ? propulsion.MaxTurnRateDegPerSecond
            : 90f;

    private static Vector2 RotateToward(Vector2 from, Vector2 to, float maxRadians)
    {
        if (from == Vector2.Zero || to == Vector2.Zero)
        {
            return to == Vector2.Zero ? from : to;
        }

        float dot = Math.Clamp(Vector2.Dot(from, to), -1f, 1f);
        float angle = MathF.Acos(dot);
        if (angle <= maxRadians)
        {
            return to;
        }

        float cross = (from.X * to.Y) - (from.Y * to.X);
        float step = cross < 0f ? -maxRadians : maxRadians;
        float s = MathF.Sin(step);
        float c = MathF.Cos(step);
        return new Vector2((from.X * c) - (from.Y * s), (from.X * s) + (from.Y * c));
    }

    private static bool UpdateEnergy(Projectile projectile, float dt)
    {
        projectile.DistanceTraveledKm += projectile.SpeedKmPerTick * dt;
        return projectile.DistanceTraveledKm < projectile.RangeKm * KinematicMargin;
    }

    private void UpdateSeekerLock(EntityUid munition, Projectile projectile, Seeker seeker, Vector2 position, Vector2 heading)
    {
        EntityUid best = EntityUid.Invalid;
        float bestDistance = float.MaxValue;

        if (IsLockable(munition, projectile, seeker, projectile.TargetEntity))
        {
            Vector2 designated = _entities.GetComponent<Transform>(projectile.TargetEntity).Position;
            if (WithinSeeker(position, heading, designated, seeker))
            {
                best = projectile.TargetEntity;
                bestDistance = Vector2.Distance(position, designated);
            }
        }

        EntityQueryEnumerator<Transform, Health> query = _entities.AllEntityQuery<Transform, Health>();
        while (query.MoveNext(out EntityUid candidate, out Transform candidateTransform, out Health _))
        {
            if (candidate == projectile.TargetEntity || !IsLockable(munition, projectile, seeker, candidate))
            {
                continue;
            }

            Vector2 candidatePosition = candidateTransform.Position;
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

    private bool IsLockable(EntityUid munition, Projectile projectile, Seeker seeker, EntityUid candidate)
    {
        if (!candidate.Valid || candidate == munition || candidate == projectile.OwnerEntity)
        {
            return false;
        }

        if (!_entities.HasComponent<Transform>(candidate)
            || !_entities.TryGetComponent(candidate, out Health health)
            || !health.IsAlive)
        {
            return false;
        }

        if (_entities.TryGetComponent(candidate, out Decoy decoy))
        {
            return DecoySpoofs(decoy.Kind, seeker.Kind);
        }

        if (_entities.HasComponent<Projectile>(candidate) && !seeker.TargetsMunitions)
        {
            return false;
        }

        if (seeker.Kind == SeekerType.AntiRadiation)
        {
            return _entities.HasComponent<Sensor>(candidate)
                && _entities.TryGetComponent(candidate, out Faction emitter)
                && Factions.IsHostileTo(projectile.ObserverFaction, emitter);
        }

        if (seeker.Kind == SeekerType.SemiActiveRadar && !IsIlluminatedByShooter(projectile.OwnerEntity, candidate))
        {
            return false;
        }

        if (seeker.Kind is SeekerType.ActiveRadar or SeekerType.SemiActiveRadar && IsInSeekerNotch(munition, candidate))
        {
            return false;
        }

        if (!_entities.TryGetComponent(candidate, out ClassificationProfile profile))
        {
            return false;
        }

        if (projectile.TargetDomains.Count == 0)
        {
            return profile.Domain is ContactDomain.Air or ContactDomain.Surface or ContactDomain.Munition;
        }

        return projectile.TargetDomains.Contains(profile.Domain)
            || (profile.Domain == ContactDomain.Munition && seeker.TargetsMunitions);
    }

    private static bool DecoySpoofs(DecoyKind kind, SeekerType seeker)
        => kind switch
        {
            DecoyKind.Flare => seeker == SeekerType.Ir,
            DecoyKind.Chaff => seeker is SeekerType.ActiveRadar or SeekerType.SemiActiveRadar,
            _ => false,
        };

    private bool IsInSeekerNotch(EntityUid munition, EntityUid candidate)
    {
        Vector2 candidateVelocity = _entities.GetComponent<Transform>(candidate).Velocity;
        float gate = _config.NotchGateKmPerSecond;
        if (candidateVelocity.Length() <= gate)
        {
            return false;
        }

        Vector2 lineOfSight = _entities.GetComponent<Transform>(candidate).Position
            - _entities.GetComponent<Transform>(munition).Position;
        float distance = lineOfSight.Length();
        if (distance < 1e-4f)
        {
            return false;
        }

        float radialSpeed = MathF.Abs(Vector2.Dot(candidateVelocity, lineOfSight / distance));
        return radialSpeed < gate;
    }

    private bool IsIlluminatedByShooter(EntityUid shooter, EntityUid candidate)
        => _entities.HasComponent<Transform>(shooter)
           && _entities.TryGetComponent(shooter, out WeaponControl control)
           && control.LockedTarget == candidate;

    private void UpdateInertialGuidance(Projectile projectile, SeekerType type, Seeker? seeker, Vector2 missilePosition, float dt)
    {
        bool guided = type switch
        {
            SeekerType.ActiveRadar when seeker is { Datalink: true } => true,
            SeekerType.SemiActiveRadar => true,
            _ => false,
        };

        if (!guided || projectile.Ballistic)
        {
            return;
        }

        float terminalRange = MathF.Max(seeker?.AcquisitionRangeKm ?? 0f, TerminalHandoffKm);
        if (Vector2.Distance(missilePosition, projectile.EstimatedTargetPosition) <= terminalRange)
        {
            if (seeker is not { Locked: true })
            {
                projectile.Ballistic = true;
            }

            return;
        }

        projectile.EstimatedTargetPosition += projectile.EstimatedTargetVelocity * dt;

        if (ShooterMaintainsLock(projectile)
            && IsAliveTarget(projectile.TargetEntity)
            && _tracking.EntriesFor(projectile.ObserverFaction).TryGetValue(projectile.TargetEntity, out Track? track)
            && track.State is not (TrackState.Stale or TrackState.Dropped))
        {
            projectile.EstimatedTargetPosition =
                Vector2.Lerp(projectile.EstimatedTargetPosition, track.EstimatedPosition, PositionCorrectionGain);
            projectile.EstimatedTargetVelocity =
                Vector2.Lerp(projectile.EstimatedTargetVelocity, track.EstimatedVelocity, VelocityCorrectionGain);
        }

        projectile.Datum = InterceptPoint(
            missilePosition,
            projectile.SpeedKmPerTick,
            projectile.EstimatedTargetPosition,
            projectile.EstimatedTargetVelocity);
        projectile.DatumPassed = false;
    }

    private bool ShooterMaintainsLock(Projectile projectile)
        => _entities.HasComponent<Transform>(projectile.OwnerEntity)
           && _entities.TryGetComponent(projectile.OwnerEntity, out WeaponControl control)
           && control.LockedTarget == projectile.TargetEntity;

    private Vector2 SelectAimpoint(
        Projectile projectile, Seeker? seeker, SeekerType type, Vector2 position, Vector2 heading, float speed)
    {
        if (seeker is { Locked: true } && _entities.TryGetComponent(seeker.LockedEntity, out Transform locked))
        {
            return InterceptPoint(position, speed, locked.Position, locked.Velocity);
        }

        if (projectile.Ballistic)
        {
            return position + (heading * MathF.Max(speed, 1f));
        }

        if (type == SeekerType.Gps || projectile.Finishing || !projectile.DatumPassed)
        {
            return projectile.Datum;
        }

        return position + (heading * MathF.Max(speed, 1f));
    }

    private bool TryFuse(EntityUid munition, Projectile projectile, Seeker? seeker, SeekerType type, Vector2 position, Vector2 step)
    {
        if (seeker is { Locked: true } && _entities.TryGetComponent(seeker.LockedEntity, out Transform locked))
        {
            Vector2 closest = ClosestPointOnSegment(position, position + step, locked.Position);
            if (Vector2.Distance(closest, locked.Position) <= projectile.DetonationRangeKm)
            {
                Burst(munition, projectile, closest);
                return true;
            }

            return false;
        }

        if (type == SeekerType.Gps || projectile.Finishing)
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

    private void Burst(EntityUid munition, Projectile projectile, Vector2 burstPoint)
    {
        bool hit = false;

        EntityQueryEnumerator<Transform, Health> query = _entities.AllEntityQuery<Transform, Health>();
        while (query.MoveNext(out EntityUid victim, out Transform victimTransform, out Health victimHealth))
        {
            if (victim == munition || victim == projectile.OwnerEntity)
            {
                continue;
            }

            if (!victimHealth.IsAlive)
            {
                continue;
            }

            if (Vector2.Distance(victimTransform.Position, burstPoint) > projectile.DetonationRangeKm)
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

    private float EffectivePk(Projectile projectile, EntityUid victim)
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

    private void Resolve(EntityUid munition, Projectile projectile, bool hit)
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
