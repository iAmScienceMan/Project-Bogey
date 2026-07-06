namespace Bogey.Shared.Components;

public enum WeaponPosture
{
    Hold,
    Defensive,
    Free,
}

public sealed class WeaponControl
{
    public WeaponPosture Posture { get; set; } = WeaponPosture.Hold;

    public int LockedTarget { get; set; } = -1;
}
