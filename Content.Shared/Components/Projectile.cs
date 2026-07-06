using System.Collections.Generic;
using System.Numerics;

using Lattice.Sim.Engine;

namespace Content.Shared.Components;

[RegisterComponent]
public sealed class Projectile : Component
{
    public int OwnerEntity { get; set; }

    public int TargetEntity { get; set; }

    public FactionType ObserverFaction { get; set; }

    [DataField]
    public float Damage { get; set; }

    [DataField]
    public float DetonationRangeKm { get; set; }

    [DataField]
    public float Pk { get; set; } = 1f;

    [DataField]
    public float RangeKm { get; set; }

    [DataField]
    public List<ContactDomain> TargetDomains { get; set; } = new();

    [DataField]
    public int MotorBurnTicksRemaining { get; set; }

    [DataField]
    public float DragPerTick { get; set; }

    public float SpeedKmPerTick { get; set; }

    public float BurnoutSpeedKmPerTick { get; set; }

    public Vector2 Datum { get; set; }

    public bool DatumPassed { get; set; }
}
