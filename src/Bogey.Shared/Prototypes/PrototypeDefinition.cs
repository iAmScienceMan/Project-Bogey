using System.Collections.Generic;
using Bogey.Shared.Components;

namespace Bogey.Shared.Prototypes;

public sealed class PrototypeDefinition
{
    public string Name { get; set; } = string.Empty;

    public FactionType Faction { get; set; } = FactionType.Neutral;

    public TransformDef? Transform { get; set; }

    
    public float? Signature { get; set; }

    public SensorDef? Sensor { get; set; }

    public ClassificationDef? Classification { get; set; }

    public PropulsionDef? Propulsion { get; set; }
}


public sealed class TransformDef
{
    public List<float> Position { get; set; } = new();
    public List<float> Velocity { get; set; } = new();
}

public sealed class SensorDef
{
    public float RangeKm { get; set; }
    public float MaxDetectProbability { get; set; }
    public float FalloffExponent { get; set; } = 1.0f;
}

public sealed class ClassificationDef
{
    public ContactDomain Domain { get; set; } = ContactDomain.Unknown;
    public string TypeName { get; set; } = string.Empty;
}

public sealed class PropulsionDef
{
    
    public float MaxSpeedKmPerTick { get; set; }
}
