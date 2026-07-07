using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Content.Sim.Content;

public sealed class ServerConfigLoader
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public ServerConfig LoadFromYaml(string yaml)
        => _deserializer.Deserialize<ServerConfig>(yaml)
           ?? throw new InvalidOperationException("Server config document was empty.");

    public IReadOnlyDictionary<string, ServerConfig> LoadConfigs(string directory)
    {
        Dictionary<string, ServerConfig> configs = new(StringComparer.Ordinal);
        if (!Directory.Exists(directory))
        {
            return configs;
        }

        foreach (string path in Directory.GetFiles(directory, "*.yaml")
                     .OrderBy(static p => Path.GetFileName(p), StringComparer.Ordinal))
        {
            ServerConfig config = LoadFromYaml(File.ReadAllText(path));
            if (string.IsNullOrWhiteSpace(config.Id))
            {
                throw new InvalidOperationException($"Server config '{Path.GetFileName(path)}' is missing an id.");
            }

            if (!configs.TryAdd(config.Id, config))
            {
                throw new InvalidOperationException($"Duplicate server config id '{config.Id}' in '{Path.GetFileName(path)}'.");
            }
        }

        return configs;
    }
}
