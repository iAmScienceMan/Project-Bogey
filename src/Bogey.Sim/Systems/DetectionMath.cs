using System;
using Bogey.Shared.Components;

namespace Bogey.Sim.Systems;

public static class DetectionMath
{
    public static float Probability(float distanceKm, Sensor sensor, float signature)
    {
        if (distanceKm < 0f)
        {
            distanceKm = 0f;
        }

        if (distanceKm >= sensor.RangeKm || sensor.RangeKm <= 0f)
        {
            return 0f;
        }

        float normalized = distanceKm / sensor.RangeKm;
        float falloff = MathF.Pow(1f - normalized, sensor.FalloffExponent);
        float p = signature * sensor.MaxDetectProbability * falloff;

        return Math.Clamp(p, 0f, 1f);
    }
}
