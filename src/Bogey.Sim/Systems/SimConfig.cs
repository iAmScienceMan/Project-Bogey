namespace Bogey.Sim.Systems;

public sealed class SimConfig
{
    public float InitialConfidence { get; init; } = 0.12f;

    public float ConfidenceGainPerHit { get; init; } = 0.14f;

    public float ClassifyThreshold { get; init; } = 0.40f;

    public float IdentifyThreshold { get; init; } = 0.75f;

    public float BasePositionalErrorKm { get; init; } = 3.0f;

    public float ObservationNoiseKm { get; init; } = 4.0f;

    public float DecayConfidenceFactor { get; init; } = 0.90f;

    public float PositionalErrorGrowthKmPerTick { get; init; } = 2.5f;

    public int StaleAfterIdleTicks { get; init; } = 8;

    public int DropAfterIdleTicks { get; init; } = 20;
}
