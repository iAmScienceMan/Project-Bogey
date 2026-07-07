using Content.Sim.Content;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class ServerConfigTests
{
    [Test]
    public void LoadsFieldsAndSimTuning()
    {
        ServerConfig config = new ServerConfigLoader().LoadFromYaml("""
            id: test
            name: Test Match
            scenario: combat-test
            seed: 99
            tickRate: 8
            port: 9000
            sim:
              classifyThreshold: 0.5
              staleAfterIdleTicks: 4
            """);

        Assert.Multiple(() =>
        {
            Assert.That(config.Id, Is.EqualTo("test"));
            Assert.That(config.Name, Is.EqualTo("Test Match"));
            Assert.That(config.Scenario, Is.EqualTo("combat-test"));
            Assert.That(config.Seed, Is.EqualTo(99));
            Assert.That(config.TickRate, Is.EqualTo(8.0));
            Assert.That(config.Port, Is.EqualTo(9000));
            Assert.That(config.Sim, Is.Not.Null);
            Assert.That(config.Sim!.ClassifyThreshold, Is.EqualTo(0.5f), "overridden tuning applies");
            Assert.That(config.Sim.StaleAfterIdleTicks, Is.EqualTo(4));
            Assert.That(config.Sim.IdentifyThreshold, Is.EqualTo(0.75f), "unspecified tuning keeps its default");
        });
    }

    [Test]
    public void OmittedSimBlockLeavesTuningNull()
    {
        ServerConfig config = new ServerConfigLoader().LoadFromYaml("""
            id: bare
            scenario: default
            """);

        Assert.Multiple(() =>
        {
            Assert.That(config.Sim, Is.Null);
            Assert.That(config.Scenario, Is.EqualTo("default"));
            Assert.That(config.Port, Is.EqualTo(8712), "port defaults when omitted");
        });
    }
}
