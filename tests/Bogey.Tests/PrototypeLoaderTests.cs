using System.Numerics;
using Bogey.Shared.Components;
using Bogey.Shared.Prototypes;
using Bogey.Sim.Content;
using Bogey.Sim.Engine;
using NUnit.Framework;

namespace Bogey.Tests;


[TestFixture]
public sealed class PrototypeLoaderTests
{
    private PrototypeLoader _loader = null!;

    [SetUp]
    public void SetUp() => _loader = new PrototypeLoader();

    [Test]
    public void LoadFromYaml_ReadsAllComponents_OfAFullPrototype()
    {
        const string yaml = """
            id: flagship
            name: BVS Meridian (flagship)
            faction: Friendly
            signature: 0.7
            sensor:
              rangeKm: 150
              maxDetectProbability: 0.95
              falloffExponent: 1.5
            classification:
              domain: Surface
              typeName: Meridian-class command vessel
            """;

        PrototypeDefinition def = _loader.LoadFromYaml(yaml);

        Assert.That(def.Id, Is.EqualTo("flagship"));
        Assert.That(def.Name, Is.EqualTo("BVS Meridian (flagship)"));
        Assert.That(def.Faction, Is.EqualTo(FactionType.Friendly));

        Assert.That(def.Signature, Is.EqualTo(0.7f));

        Assert.That(def.Sensor, Is.Not.Null);
        Assert.That(def.Sensor!.RangeKm, Is.EqualTo(150f));
        Assert.That(def.Sensor.MaxDetectProbability, Is.EqualTo(0.95f));
        Assert.That(def.Sensor.FalloffExponent, Is.EqualTo(1.5f));

        Assert.That(def.Classification, Is.Not.Null);
        Assert.That(def.Classification!.Domain, Is.EqualTo(ContactDomain.Surface));
        Assert.That(def.Classification.TypeName, Is.EqualTo("Meridian-class command vessel"));
    }

    [Test]
    public void LoadFromYaml_LeavesAbsentComponentsNull()
    {
        const string yaml = """
            id: submersible
            name: Silt-class submersible
            faction: Hostile
            signature: 0.18
            classification:
              domain: Subsurface
              typeName: Silt-class submersible
            """;

        PrototypeDefinition def = _loader.LoadFromYaml(yaml);

        Assert.That(def.Faction, Is.EqualTo(FactionType.Hostile));
        Assert.That(def.Sensor, Is.Null, "an omitted sensor: block must yield a null Sensor def");
        Assert.That(def.Signature, Is.EqualTo(0.18f));
        Assert.That(def.Classification!.Domain, Is.EqualTo(ContactDomain.Subsurface));
    }

    [Test]
    public void LoadFromYaml_HandlesMinimalPrototype()
    {
        PrototypeDefinition def = _loader.LoadFromYaml("id: bare\nname: Bare\nfaction: Neutral\n");

        Assert.That(def.Id, Is.EqualTo("bare"));
        Assert.That(def.Name, Is.EqualTo("Bare"));
        Assert.That(def.Faction, Is.EqualTo(FactionType.Neutral));
        Assert.That(def.Signature, Is.Null);
        Assert.That(def.Sensor, Is.Null);
        Assert.That(def.Classification, Is.Null);
    }

    [Test]
    public void LoadedPrototype_SpawnsIntoExpectedComponents()
    {
        
        const string yaml = """
            id: interceptor
            name: Wraith-class interceptor
            faction: Hostile
            signature: 0.55
            classification:
              domain: Air
              typeName: Wraith-class interceptor
            """;

        PrototypeDefinition def = _loader.LoadFromYaml(yaml);

        EntityManager entities = new();
        int entity = PrototypeFactory.Spawn(entities, def, new Placement(new Vector2(40f, 25f), new Vector2(1.8f, 1.1f)));

        Assert.That(entities.HasComponent<Identity>(entity), Is.True);
        Assert.That(entities.HasComponent<Faction>(entity), Is.True);
        Assert.That(entities.HasComponent<Transform>(entity), Is.True);
        Assert.That(entities.HasComponent<Signature>(entity), Is.True);
        Assert.That(entities.HasComponent<ClassificationProfile>(entity), Is.True);
        Assert.That(entities.HasComponent<Sensor>(entity), Is.False, "interceptor has no sensor block");

        Assert.That(entities.GetComponent<Transform>(entity).Position, Is.EqualTo(new Vector2(40f, 25f)));
        Assert.That(entities.GetComponent<Signature>(entity).Value, Is.EqualTo(0.55f));
        Assert.That(entities.GetComponent<ClassificationProfile>(entity).Domain, Is.EqualTo(ContactDomain.Air));
    }

    [Test]
    public void LoadFromYaml_Throws_OnEmptyDocument()
    {
        Assert.That(() => _loader.LoadFromYaml(""), Throws.InvalidOperationException);
    }
}
