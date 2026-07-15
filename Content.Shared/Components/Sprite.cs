using Lattice.Sim.Engine;

namespace Content.Shared.Components;

[RegisterComponent]
public sealed class Sprite : Component
{
    [DataField]
    public string Texture { get; set; } = string.Empty;

    [DataField]
    public float Scale { get; set; } = 1f;

    [DataField]
    public bool Visible { get; set; } = true;
}
