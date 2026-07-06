using System.Collections.Generic;
using Content.Renderer.RealTime;
using Lattice.Shared.Configuration;

namespace Content.Renderer.App;

public readonly record struct SimBoot(ISimSession Session, IDebugOverlay? Overlay, IReadOnlyList<string> Prototypes);

public readonly record struct ScenarioInfo(string Id, string Name);

public delegate SimBoot SimBootFactory(IConfigurationManager configuration);
