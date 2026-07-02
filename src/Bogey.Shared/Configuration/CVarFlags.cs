using System;

namespace Bogey.Shared.Configuration;

[Flags]
public enum CVarFlags
{
    None = 0,
    Archive = 1 << 0,
    RequiresRestart = 1 << 1,
    Cheat = 1 << 2,
}
