using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Shared.Prototypes;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Content.Sim.Content;

public sealed class ScenarioLoader
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public ScenarioDefinition LoadFromYaml(string yaml)
        => _deserializer.Deserialize<ScenarioDefinition>(yaml)
           ?? throw new InvalidOperationException("YAML document was empty.");

    public IReadOnlyDictionary<string, ScenarioDefinition> LoadScenarios(string directory)
    {
        Dictionary<string, ScenarioDefinition> scenarios = new(StringComparer.Ordinal);

        foreach (string path in YamlFiles(directory))
        {
            ScenarioDefinition scenario = LoadFromYaml(File.ReadAllText(path));
            if (string.IsNullOrWhiteSpace(scenario.Id))
            {
                throw new InvalidOperationException($"Scenario '{Path.GetFileName(path)}' is missing an id.");
            }

            if (!scenarios.TryAdd(scenario.Id, scenario))
            {
                throw new InvalidOperationException($"Duplicate scenario id '{scenario.Id}' in '{Path.GetFileName(path)}'.");
            }
        }

        return scenarios;
    }

    private static IEnumerable<string> YamlFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Content directory not found: {directory}");
        }

        return Directory.GetFiles(directory, "*.yaml")
            .OrderBy(static path => Path.GetFileName(path), StringComparer.Ordinal);
    }
}
