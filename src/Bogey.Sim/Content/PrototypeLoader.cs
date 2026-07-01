using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bogey.Shared.Prototypes;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Bogey.Sim.Content;

public sealed class PrototypeLoader
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public PrototypeDefinition LoadFromYaml(string yaml)
        => _deserializer.Deserialize<PrototypeDefinition>(yaml)
           ?? throw new InvalidOperationException("YAML document was empty.");

    public IReadOnlyList<PrototypeDefinition> LoadDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Prototype directory not found: {directory}");
        }

        return Directory.GetFiles(directory, "*.yaml")
            .OrderBy(static path => Path.GetFileName(path), StringComparer.Ordinal)
            .Select(path => LoadFromYaml(File.ReadAllText(path)))
            .ToList();
    }
}
