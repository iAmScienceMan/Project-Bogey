using System;
using Content.Shared.Components;

namespace Content.Sim.Systems;

public static class DetectionMath
{
    public static float Probability(float distanceKm, Sensor sensor, float signature)
    {
        if (distanceKm < 0f)
        {
            distanceKm = 0f;
        }

        if (sensor.RangeKm <= 0f || signature <= 0f)
        {
            return 0f;
        }

        float detectRangeKm = sensor.RangeKm * MathF.Pow(MathF.Min(signature, 1f), 0.25f);
        if (distanceKm >= detectRangeKm)
        {
            return 0f;
        }

        float normalized = distanceKm / detectRangeKm;
        float falloff = MathF.Pow(1f - normalized, sensor.FalloffExponent);

        return Math.Clamp(sensor.MaxDetectProbability * falloff, 0f, 1f);
    }

    public static float DetectionRangeKm(Sensor sensor, float signature)
        => sensor.RangeKm <= 0f || signature <= 0f
            ? 0f
            : sensor.RangeKm * MathF.Pow(MathF.Min(signature, 1f), 0.25f);
}
