using Lattice.Sim.Engine;

namespace Content.Shared.Components;

[RegisterComponent]
public sealed class Health : Component
{
    [DataField]
    public float Max { get; set; }

    public float Current { get; set; }

    public bool IsAlive => Current > 0f;
}
