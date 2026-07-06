using Content.Shared.Components;
using Lattice.Sim.Engine;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class PrototypeManagerTests
{
    private PrototypeManager _prototypes = null!;

    [SetUp]
    public void SetUp()
        => _prototypes = new PrototypeManager(new ComponentFactory(new[] { typeof(Sensor).Assembly }));

    [Test]
    public void LoadYaml_And_Spawn_PopulatesComponentsFromData()
    {
        const string yaml = """
            - id: flagship
              name: BVS Meridian (flagship)
              components:
              - type: Faction
                side: Friendly
              - type: Signature
                value: 0.7
              - type: Sensor
                rangeKm: 150
                maxDetectProbability: 0.95
                falloffExponent: 1.5
              - type: ClassificationProfile
                domain: Surface
                typeName: Meridian-class command vessel
            """;

        _prototypes.LoadYaml(yaml);

        EntityManager entities = new();
        int entity = _prototypes.SpawnEntity(entities, "flagship");

        Assert.Multiple(() =>
        {
            Assert.That(entities.GetComponent<Faction>(entity).Side, Is.EqualTo(FactionType.Friendly));
            Assert.That(entities.GetComponent<Signature>(entity).Value, Is.EqualTo(0.7f));
            Assert.That(entities.GetComponent<Sensor>(entity).RangeKm, Is.EqualTo(150f));
            Assert.That(entities.GetComponent<Sensor>(entity).MaxDetectProbability, Is.EqualTo(0.95f));
            Assert.That(entities.GetComponent<Sensor>(entity).FalloffExponent, Is.EqualTo(1.5f));
            Assert.That(entities.GetComponent<ClassificationProfile>(entity).Domain, Is.EqualTo(ContactDomain.Surface));
            Assert.That(entities.HasComponent<Health>(entity), Is.False, "no Health component was listed");
        });
    }

    [Test]
    public void Spawn_OnlyAddsListedComponents()
    {
        _prototypes.LoadYaml("""
            - id: interceptor
              components:
              - type: Faction
                side: Hostile
              - type: Signature
                value: 0.55
            """);

        EntityManager entities = new();
        int entity = _prototypes.SpawnEntity(entities, "interceptor");

        Assert.Multiple(() =>
        {
            Assert.That(entities.HasComponent<Faction>(entity), Is.True);
            Assert.That(entities.HasComponent<Signature>(entity), Is.True);
            Assert.That(entities.HasComponent<Sensor>(entity), Is.False);
            Assert.That(entities.GetComponent<Signature>(entity).Value, Is.EqualTo(0.55f));
        });
    }

    [Test]
    public void LoadYaml_Throws_OnUnknownComponent()
        => Assert.That(
            () => _prototypes.LoadYaml("- id: x\n  components:\n  - type: NotARealComponent\n"),
            Throws.InvalidOperationException);

    [Test]
    public void SpawnEntity_ProducesFreshComponentInstances()
    {
        _prototypes.LoadYaml("""
            - id: dot
              components:
              - type: Signature
                value: 0.4
            """);

        EntityManager entities = new();
        int a = _prototypes.SpawnEntity(entities, "dot");
        int b = _prototypes.SpawnEntity(entities, "dot");

        entities.GetComponent<Signature>(a).Value = 9f;
        Assert.That(
            entities.GetComponent<Signature>(b).Value,
            Is.EqualTo(0.4f),
            "each spawn must get its own component instance");
    }
}
