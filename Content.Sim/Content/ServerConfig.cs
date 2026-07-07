using System.Collections.Generic;
using Content.Sim.Systems;

namespace Content.Sim.Content;

public sealed class ServerConfig
{
    public string Id { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string Scenario { get; set; } = "default";

    public int Seed { get; set; } = 1337;

    public double TickRate { get; set; } = 1.0;

    public int Port { get; set; } = 8712;

    public List<string> Admins { get; set; } = new();

    public SimConfig? Sim { get; set; }
}
