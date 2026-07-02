using Bogey.Renderer.RealTime;
using Bogey.Shared.Configuration;

namespace Bogey.Renderer.App;

public readonly record struct SimBoot(ISimSession Session, IDebugOverlay? Overlay);

public delegate SimBoot SimBootFactory(IConfigurationManager configuration);
