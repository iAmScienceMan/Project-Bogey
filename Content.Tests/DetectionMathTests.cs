using Content.Shared.Components;
using Content.Sim.Systems;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class DetectionMathTests
{
    [Test]
    public void Probability_IsStrictlyDecreasing_WithDistance_WithinRange()
    {
        Sensor sensor = new() { RangeKm = 100f, MaxDetectProbability = 0.9f, FalloffExponent = 1.5f };
        const float signature = 0.8f;

        float previous = float.MaxValue;
        for (float distance = 0f; distance < sensor.RangeKm; distance += 5f)
        {
            float p = DetectionMath.Probability(distance, sensor, signature);
            Assert.That(p, Is.LessThan(previous), $"probability did not decrease at {distance} km");
            previous = p;
        }
    }

    [Test]
    public void Probability_IsZero_AtAndBeyondRange()
    {
        Sensor sensor = new() { RangeKm = 100f, MaxDetectProbability = 0.9f, FalloffExponent = 1.5f };

        Assert.That(DetectionMath.Probability(100f, sensor, 1.0f), Is.EqualTo(0f));
        Assert.That(DetectionMath.Probability(150f, sensor, 1.0f), Is.EqualTo(0f));
    }

    [Test]
    public void Probability_IsStrictlyIncreasing_WithSignature_AtEqualRange()
    {
        Sensor sensor = new() { RangeKm = 100f, MaxDetectProbability = 1.0f, FalloffExponent = 1.0f };
        const float fixedRange = 20f;

        float previous = -1f;
        for (float signature = 0.0f; signature <= 1.0f + 1e-6f; signature += 0.1f)
        {
            float p = DetectionMath.Probability(fixedRange, sensor, signature);
            Assert.That(p, Is.GreaterThan(previous), $"probability did not increase at signature {signature}");
            previous = p;
        }
    }

    [Test]
    public void Probability_IsZero_ForZeroSignature()
    {
        Sensor sensor = new() { RangeKm = 100f, MaxDetectProbability = 1.0f, FalloffExponent = 1.0f };

        Assert.That(DetectionMath.Probability(10f, sensor, 0.0f), Is.EqualTo(0f));
    }

    [Test]
    public void DetectionRange_FollowsFourthRootOfSignature()
    {
        Sensor sensor = new() { RangeKm = 160f, MaxDetectProbability = 0.9f, FalloffExponent = 1.5f };

        float loud = DetectionMath.DetectionRangeKm(sensor, 1.0f);
        float stealthy = DetectionMath.DetectionRangeKm(sensor, 1f / 16f);

        Assert.That(loud, Is.EqualTo(160f).Within(1e-3f));
        Assert.That(stealthy, Is.EqualTo(80f).Within(1e-3f),
            "a 16x smaller radar cross-section should halve detection range (fourth-root law)");
        Assert.That(DetectionMath.Probability(90f, sensor, 1f / 16f), Is.EqualTo(0f),
            "a stealthy target beyond its shrunken detection bubble is invisible");
    }

    [Test]
    public void Probability_IsHighestAtPointBlank()
    {
        Sensor sensor = new() { RangeKm = 100f, MaxDetectProbability = 0.9f, FalloffExponent = 1.5f };

        
        Assert.That(DetectionMath.Probability(0f, sensor, 1.0f), Is.EqualTo(0.9f).Within(1e-5f));
    }
}
