using System;
using System.Collections.Generic;
using Content.Renderer.Map;
using Content.Renderer.RealTime;

namespace Content.Renderer.App;

public sealed class SimConsoleContext
{
    public ISimSession? Session { get; set; }

    public GroundTruthOverlayView? Overlay { get; set; }

    public IReadOnlyList<string> Prototypes { get; set; } = Array.Empty<string>();
}
