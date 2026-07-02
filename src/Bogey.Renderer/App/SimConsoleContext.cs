using Bogey.Renderer.RealTime;

namespace Bogey.Renderer.App;

public sealed class SimConsoleContext
{
    public ISimSession? Session { get; set; }

    public IDebugOverlay? Overlay { get; set; }
}
