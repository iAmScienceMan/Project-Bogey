namespace Content.Shared.Components;

public sealed class Health
{
    public float Max { get; set; }

    public float Current { get; set; }

    public bool IsAlive => Current > 0f;
}
