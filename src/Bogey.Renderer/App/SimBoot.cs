using System.Collections.Generic;
using Bogey.Renderer.RealTime;
using Bogey.Shared.Configuration;

namespace Bogey.Renderer.App;

public readonly record struct SimBoot(ISimSession Session, IDebugOverlay? Overlay, IReadOnlyList<string> Prototypes);

public readonly record struct ScenarioInfo(string Id, string Name);

public delegate SimBoot SimBootFactory(IConfigurationManager configuration);
