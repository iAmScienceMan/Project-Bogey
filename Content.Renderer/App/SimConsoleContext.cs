using System;
using System.Collections.Generic;
using Content.Renderer.RealTime;

namespace Content.Renderer.App;

public sealed class SimConsoleContext
{
    public ISimSession? Session { get; set; }

    public IDebugOverlay? Overlay { get; set; }

    public IReadOnlyList<string> Prototypes { get; set; } = Array.Empty<string>();
}
