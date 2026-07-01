namespace Bogey.Sim.Engine;

public sealed class SimClock
{
    public const double SecondsPerTick = 1.0;

    public int CurrentTick { get; private set; }

    public void Advance() => CurrentTick++;
}
