using System.Collections.Generic;
using Bogey.Shared.Components;

namespace Bogey.Shared.Prototypes;

public sealed class PrototypeDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public FactionType Faction { get; set; } = FactionType.Neutral;

    public float? Signature { get; set; }

    public SensorDef? Sensor { get; set; }

    public ClassificationDef? Classification { get; set; }

    public PropulsionDef? Propulsion { get; set; }

    public HealthDef? Health { get; set; }

    public List<WeaponMountDef>? Weapons { get; set; }

    public ProjectileDef? Projectile { get; set; }

    public AiDef? Ai { get; set; }

    public WeaponPosture? Posture { get; set; }
}

public sealed class AiDef
{
    public AiBehavior Behavior { get; set; } = AiBehavior.Aggressive;
}

public sealed class HealthDef
{
    public float Max { get; set; }
}

public sealed class WeaponMountDef
{
    public string ProjectilePrototype { get; set; } = string.Empty;
    public int CooldownTicks { get; set; }
    public int Magazine { get; set; }
    public bool PointDefense { get; set; }
    public float PointDefensePk { get; set; }
    public float RangeKm { get; set; }
}

public sealed class ProjectileDef
{
    public float Damage { get; set; }
    public float DetonationRangeKm { get; set; }
    public float Pk { get; set; } = 1f;
    public float RangeKm { get; set; }
    public List<ContactDomain> TargetDomains { get; set; } = new();
    public int MotorBurnTicks { get; set; }
    public float DragPerTick { get; set; } = 0.04f;
    public SeekerDef? Seeker { get; set; }
}

public sealed class SeekerDef
{
    public SeekerType Type { get; set; } = SeekerType.ActiveRadar;
    public float AcquisitionRangeKm { get; set; }
    public float FovDegrees { get; set; } = 360f;
    public bool Datalink { get; set; }
    public bool TargetsMunitions { get; set; }
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
