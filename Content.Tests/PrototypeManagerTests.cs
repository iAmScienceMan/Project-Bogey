using System;
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
        EntityUid entity = _prototypes.SpawnEntity(entities, "flagship");

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
        EntityUid entity = _prototypes.SpawnEntity(entities, "interceptor");

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
    public void Inheritance_MergesComponentsFieldByField()
    {
        _prototypes.LoadYaml("""
            - id: BaseShip
              abstract: true
              components:
              - type: Faction
                side: Friendly
              - type: Sensor
                rangeKm: 100
                maxDetectProbability: 0.95
                falloffExponent: 1.5
            - id: picket
              parent: BaseShip
              components:
              - type: Sensor
                rangeKm: 60
              - type: Signature
                value: 0.5
            """);

        EntityManager entities = new();
        EntityUid entity = _prototypes.SpawnEntity(entities, "picket");

        Assert.Multiple(() =>
        {
            Assert.That(entities.GetComponent<Faction>(entity).Side, Is.EqualTo(FactionType.Friendly));
            Assert.That(entities.GetComponent<Sensor>(entity).RangeKm, Is.EqualTo(60f), "child overrides one field");
            Assert.That(
                entities.GetComponent<Sensor>(entity).MaxDetectProbability,
                Is.EqualTo(0.95f),
                "unspecified fields inherit from the parent component");
            Assert.That(entities.GetComponent<Signature>(entity).Value, Is.EqualTo(0.5f));
        });
    }

    [Test]
    public void Abstract_PrototypesAreNotSpawnable()
    {
        _prototypes.LoadYaml("""
            - id: BaseThing
              abstract: true
              components:
              - type: Faction
                side: Neutral
            """);

        Assert.Multiple(() =>
        {
            Assert.That(_prototypes.Has("BaseThing"), Is.False);
            Assert.That(() => _prototypes.Get("BaseThing"), Throws.InvalidOperationException);
        });
    }

    [Test]
    public void MultipleParents_ComposeAsMixins_LaterParentWins()
    {
        _prototypes.LoadYaml("""
            - id: HasFaction
              abstract: true
              components:
              - type: Faction
                side: Friendly
              - type: Signature
                value: 0.1
            - id: HasSignature
              abstract: true
              components:
              - type: Signature
                value: 0.9
            - id: mixed
              parent: [HasFaction, HasSignature]
              components:
              - type: Transform
            """);

        EntityManager entities = new();
        EntityUid entity = _prototypes.SpawnEntity(entities, "mixed");

        Assert.Multiple(() =>
        {
            Assert.That(entities.HasComponent<Faction>(entity), Is.True);
            Assert.That(entities.HasComponent<Transform>(entity), Is.True);
            Assert.That(
                entities.GetComponent<Signature>(entity).Value,
                Is.EqualTo(0.9f),
                "the later parent overrides the earlier one");
        });
    }

    [Test]
    public void Inheritance_Throws_OnCycle()
        => Assert.That(
            () => _prototypes.LoadYaml("""
                - id: a
                  parent: b
                  components: []
                - id: b
                  parent: a
                  components: []
                """),
            Throws.InvalidOperationException);

    [Test]
    public void Inheritance_Throws_OnUnknownParent()
        => Assert.That(
            () => _prototypes.LoadYaml("- id: orphan\n  parent: ghost\n  components: []\n"),
            Throws.InvalidOperationException);

    [Test]
    public void MetaData_TakesNameFromPrototype_AndRecordsOrigin()
    {
        _prototypes.LoadYaml("""
            - id: flagship
              name: BVS Meridian
              components:
              - type: Faction
                side: Friendly
            """);

        EntityManager entities = new();
        EntityUid entity = _prototypes.SpawnEntity(entities, "flagship");

        MetaData meta = entities.GetComponent<MetaData>(entity);
        Assert.Multiple(() =>
        {
            Assert.That(meta.Name, Is.EqualTo("BVS Meridian"));
            Assert.That(meta.Prototype, Is.EqualTo("flagship"));
        });
    }

    [Test]
    public void MetaData_InheritsNameFromParent_WhenChildOmitsIt()
    {
        _prototypes.LoadYaml("""
            - id: BaseCarp
              abstract: true
              name: space carp
              components:
              - type: Faction
                side: Hostile
            - id: carp
              parent: BaseCarp
              components:
              - type: Signature
                value: 0.2
            """);

        EntityManager entities = new();
        EntityUid entity = _prototypes.SpawnEntity(entities, "carp");

        Assert.That(entities.GetComponent<MetaData>(entity).Name, Is.EqualTo("space carp"));
    }

    [Test]
    public void DataField_RejectsRuntimeOnlyField()
    {
        _prototypes.LoadYaml("""
            - id: hurt
              components:
              - type: Health
                max: 10
                current: 5
            """);

        EntityManager entities = new();
        Exception? error = Assert.Catch(() => _prototypes.SpawnEntity(entities, "hurt"));

        string message = error!.Message + " " + (error.InnerException?.Message ?? string.Empty);
        Assert.That(message, Does.Contain("DataField"));
    }

    [Test]
    public void DataField_RejectsUnknownField()
    {
        _prototypes.LoadYaml("""
            - id: bogus
              components:
              - type: Signature
                banana: 5
            """);

        EntityManager entities = new();
        Assert.That(() => _prototypes.SpawnEntity(entities, "bogus"), Throws.Exception);
    }

    [Test]
    public void DataField_LeavesRuntimeFieldsAtTheirDefault()
    {
        _prototypes.LoadYaml("""
            - id: ship
              components:
              - type: Health
                max: 10
            """);

        EntityManager entities = new();
        EntityUid entity = _prototypes.SpawnEntity(entities, "ship");

        Assert.Multiple(() =>
        {
            Assert.That(entities.GetComponent<Health>(entity).Max, Is.EqualTo(10f));
            Assert.That(entities.GetComponent<Health>(entity).Current, Is.EqualTo(0f), "runtime field untouched by data");
        });
    }

    [Test]
    public void Sprite_BindsFromPrototypeData()
    {
        _prototypes.LoadYaml("""
            - id: ship
              components:
              - type: Sprite
                texture: own.png
                scale: 2
            """);

        EntityManager entities = new();
        EntityUid entity = _prototypes.SpawnEntity(entities, "ship");
        Sprite sprite = entities.GetComponent<Sprite>(entity);

        Assert.Multiple(() =>
        {
            Assert.That(sprite.Texture, Is.EqualTo("own.png"));
            Assert.That(sprite.Scale, Is.EqualTo(2f));
            Assert.That(sprite.Visible, Is.True);
        });
    }

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
        EntityUid a = _prototypes.SpawnEntity(entities, "dot");
        EntityUid b = _prototypes.SpawnEntity(entities, "dot");

        entities.GetComponent<Signature>(a).Value = 9f;
        Assert.That(
            entities.GetComponent<Signature>(b).Value,
            Is.EqualTo(0.4f),
            "each spawn must get its own component instance");
    }
}
