using System.Collections.Generic;

namespace Bogey.Shared.Prototypes;

public sealed class ScenarioDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public List<ScenarioSpawn> Spawns { get; set; } = new();
}

public sealed class ScenarioSpawn
{
    public string Proto { get; set; } = string.Empty;

    public List<float> Position { get; set; } = new();

    public List<float> Velocity { get; set; } = new();

    public string? Name { get; set; }
}
