using System.Collections.Generic;

namespace Content.Shared.Prototypes;

public sealed class ScenarioDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public List<ScenarioSpawn> Spawns { get; set; } = new();

    public PlayerSpawnDefinition PlayerSpawn { get; set; } = new();
}

public sealed class PlayerSpawnDefinition
{
    public string Proto { get; set; } = "test-doom";

    public string Name { get; set; } = "Legion";

    public List<float> Position { get; set; } = new();

    public float SpacingKm { get; set; } = 30f;
}

public sealed class ScenarioSpawn
{
    public string Proto { get; set; } = string.Empty;

    public List<float> Position { get; set; } = new();

    public List<float> Velocity { get; set; } = new();

    public string? Name { get; set; }
}
