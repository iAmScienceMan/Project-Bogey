using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Shared.Prototypes;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Content.Sim.Content;

public sealed class PrototypeLoader
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public PrototypeDefinition LoadFromYaml(string yaml)
        => _deserializer.Deserialize<PrototypeDefinition>(yaml)
           ?? throw new InvalidOperationException("YAML document was empty.");

    public ScenarioDefinition LoadScenarioFromYaml(string yaml)
        => _deserializer.Deserialize<ScenarioDefinition>(yaml)
           ?? throw new InvalidOperationException("YAML document was empty.");

    public IReadOnlyDictionary<string, PrototypeDefinition> LoadPrototypes(string directory)
    {
        Dictionary<string, PrototypeDefinition> prototypes = new(StringComparer.Ordinal);

        foreach (string path in YamlFiles(directory))
        {
            PrototypeDefinition prototype = LoadFromYaml(File.ReadAllText(path));
            if (string.IsNullOrWhiteSpace(prototype.Id))
            {
                throw new InvalidOperationException($"Prototype '{Path.GetFileName(path)}' is missing an id.");
            }

            if (!prototypes.TryAdd(prototype.Id, prototype))
            {
                throw new InvalidOperationException($"Duplicate prototype id '{prototype.Id}' in '{Path.GetFileName(path)}'.");
            }
        }

        return prototypes;
    }

    public IReadOnlyDictionary<string, ScenarioDefinition> LoadScenarios(string directory)
    {
        Dictionary<string, ScenarioDefinition> scenarios = new(StringComparer.Ordinal);

        foreach (string path in YamlFiles(directory))
        {
            ScenarioDefinition scenario = LoadScenarioFromYaml(File.ReadAllText(path));
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
