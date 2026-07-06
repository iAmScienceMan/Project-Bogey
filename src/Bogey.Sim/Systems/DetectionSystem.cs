using System;
using System.Collections.Generic;
using System.Numerics;
using Bogey.Shared.Components;
using Bogey.Shared.Events;
using Bogey.Sim.Engine;

namespace Bogey.Sim.Systems;

public sealed class DetectionSystem : SystemBase
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
        List<int> sensors = new(_entities.Query<Sensor, Transform>());
        List<int> targets = new(_entities.Query<Signature, Transform>());

        foreach (int sensorEntity in sensors)
        {
            FactionType observerSide = _entities.GetComponent<Faction>(sensorEntity).Side;
            Sensor sensor = _entities.GetComponent<Sensor>(sensorEntity);
            Vector2 sensorPos = _entities.GetComponent<Transform>(sensorEntity).Position;

            foreach (int targetEntity in targets)
            {
                if (_entities.GetComponent<Faction>(targetEntity).Side == observerSide)
                {
                    continue;
                }

                Vector2 targetPos = _entities.GetComponent<Transform>(targetEntity).Position;
                float signature = _entities.GetComponent<Signature>(targetEntity).Value;
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
                    ObserverFaction = observerSide,
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
