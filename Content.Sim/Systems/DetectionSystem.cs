using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Components;
using Content.Shared.Events;
using Lattice.Sim.Engine;

namespace Content.Sim.Systems;

public sealed class DetectionSystem : EntitySystem
{
    [Dependency]
    private readonly EntityManager _entities = null!;

    [Dependency]
    private readonly EventBus _bus = null!;

    [Dependency]
    private readonly Random _rng = null!;

    [Dependency]
    private readonly SimClock _clock = null!;

    [Dependency]
    private readonly SimConfig _config = null!;

    public override void Update()
    {
        List<(EntityUid Uid, string Faction, Vector2 Position, float Signature)> targets = new();
        EntityQueryEnumerator<Signature, Transform> targetQuery = _entities.AllEntityQuery<Signature, Transform>();
        while (targetQuery.MoveNext(out EntityUid targetEntity, out Signature signature, out Transform targetTransform))
        {
            targets.Add((
                targetEntity,
                _entities.GetComponent<Faction>(targetEntity).EffectiveId,
                targetTransform.Position,
                signature.Value));
        }

        EntityQueryEnumerator<Sensor, Transform> sensorQuery = _entities.AllEntityQuery<Sensor, Transform>();
        while (sensorQuery.MoveNext(out EntityUid sensorEntity, out Sensor sensor, out Transform sensorTransform))
        {
            if (!_config.AiEnabled && _entities.HasComponent<Ai>(sensorEntity))
            {
                continue;
            }

            string observerFaction = _entities.GetComponent<Faction>(sensorEntity).EffectiveId;
            Vector2 sensorPos = sensorTransform.Position;

            foreach ((EntityUid targetEntity, string targetFaction, Vector2 targetPos, float signature) in targets)
            {
                if (string.Equals(targetFaction, observerFaction, StringComparison.Ordinal))
                {
                    continue;
                }

                float distance = Vector2.Distance(sensorPos, targetPos);
                float p = DetectionMath.Probability(distance, sensor, signature);

                double roll = _rng.NextDouble();
                if (roll >= p)
                {
                    continue;
                }

                Vector2 observed = targetPos + NoiseOffset(distance, sensor.RangeKm);
                _bus.PublishDirected(targetEntity, new ContactDetectedEvent
                {
                    ObserverFaction = observerFaction,
                    ObservedPosition = observed,
                    DetectionStrength = p,
                    Tick = _clock.CurrentTick,
                });
            }
        }
    }

    private Vector2 NoiseOffset(float distance, float rangeKm)
    {
        float edgeFactor = rangeKm > 0f ? 0.3f + 0.7f * (distance / rangeKm) : 1f;
        float magnitude = _config.ObservationNoiseKm * edgeFactor;

        float dx = (float)(_rng.NextDouble() * 2.0 - 1.0) * magnitude;
        float dy = (float)(_rng.NextDouble() * 2.0 - 1.0) * magnitude;
        return new Vector2(dx, dy);
    }
}
