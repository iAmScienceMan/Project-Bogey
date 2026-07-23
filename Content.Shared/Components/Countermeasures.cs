using Lattice.Sim.Engine;

namespace Content.Shared.Components;

public enum DecoyKind
{
    Flare,
    Chaff,
}

[RegisterComponent]
public sealed class Countermeasures : Component
{
    [DataField]
    public int Flares { get; set; }

    [DataField]
    public int Chaff { get; set; }

    [DataField]
    public string FlarePrototype { get; set; } = "flare";

    [DataField]
    public string ChaffPrototype { get; set; } = "chaff";

    [DataField]
    public int SalvoSize { get; set; } = 3;

    [DataField]
    public int CooldownTicks { get; set; } = 4;

    [DataField]
    public float FlareTriggerRangeKm { get; set; } = 12f;

    public int TicksUntilReady { get; set; }
}

[RegisterComponent]
public sealed class Decoy : Component
{
    [DataField]
    public DecoyKind Kind { get; set; }

    [DataField]
    public int LifetimeTicks { get; set; } = 8;

    public int TicksRemaining { get; set; } = -1;
}
