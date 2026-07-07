using System.Numerics;
using Content.Shared.Components;
using Lattice.Sim.Engine;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class GameStateReplicationTests
{
    private ComponentFactory _factory = null!;
    private GameStateManager _manager = null!;
    private EntityManager _server = null!;
    private ClientState _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new ComponentFactory(new[] { typeof(Sensor).Assembly });
        _manager = new GameStateManager(_factory);
        _server = new EntityManager { CurrentTick = 1 };
        _client = new ClientState(new EntityManager(), _factory);
    }

    private int SpawnShip(string name, Vector2 position, float hull)
    {
        int entity = _server.CreateEntity();
        _server.AddComponent(entity, new MetaData { EntityName = name, Prototype = "ship" });
        _server.AddComponent(entity, new Transform { Position = position });
        _server.AddComponent(entity, new Health { Max = hull });
        return entity;
    }

    [Test]
    public void FullState_ReplicatesNetworkedComponents()
    {
        int server = SpawnShip("Alpha", new Vector2(3f, 4f), 100f);

        _client.Apply(_manager.BuildState(_server, fromTick: 0, static _ => true));

        Assert.That(_client.TryResolve(new NetEntity(server), out int local), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(_client.Entities.GetComponent<MetaData>(local).Name, Is.EqualTo("Alpha"));
            Assert.That(_client.Entities.GetComponent<MetaData>(local).NetEntity, Is.EqualTo(new NetEntity(server)));
            Assert.That(_client.Entities.GetComponent<Transform>(local).Position, Is.EqualTo(new Vector2(3f, 4f)));
            Assert.That(_client.Entities.GetComponent<Health>(local).Max, Is.EqualTo(100f));
        });
    }

    [Test]
    public void DeltaState_OnlyCarriesChangedComponents()
    {
        int server = SpawnShip("Alpha", new Vector2(0f, 0f), 100f);
        _client.Apply(_manager.BuildState(_server, fromTick: 0, static _ => true));

        _server.CurrentTick = 2;
        Transform transform = _server.GetComponent<Transform>(server);
        transform.Position = new Vector2(9f, 9f);
        _server.Dirty(transform);

        GameState delta = _manager.BuildState(_server, fromTick: 1, static _ => true);

        Assert.That(delta.Entities, Has.Count.EqualTo(1));
        Assert.That(delta.Entities[0].Components, Has.Count.EqualTo(1), "only the dirtied Transform is sent");

        _client.Apply(delta);
        _client.TryResolve(new NetEntity(server), out int local);
        Assert.Multiple(() =>
        {
            Assert.That(_client.Entities.GetComponent<Transform>(local).Position, Is.EqualTo(new Vector2(9f, 9f)));
            Assert.That(_client.Entities.GetComponent<Health>(local).Max, Is.EqualTo(100f), "unchanged component survives");
        });
    }

    [Test]
    public void Visibility_FiltersEntitiesOutOfView()
    {
        int visible = SpawnShip("Seen", Vector2.Zero, 100f);
        int hidden = SpawnShip("Hidden", Vector2.One, 100f);

        GameState state = _manager.BuildState(_server, fromTick: 0, uid => uid == visible);
        _client.Apply(state);

        Assert.Multiple(() =>
        {
            Assert.That(_client.TryResolve(new NetEntity(visible), out _), Is.True);
            Assert.That(_client.TryResolve(new NetEntity(hidden), out _), Is.False);
        });
    }

    [Test]
    public void Deletion_RemovesEntityOnClient()
    {
        int server = SpawnShip("Doomed", Vector2.Zero, 100f);
        _client.Apply(_manager.BuildState(_server, fromTick: 0, static _ => true));

        _server.CurrentTick = 3;
        _server.DestroyEntity(server);

        GameState delta = _manager.BuildState(_server, fromTick: 2, static _ => true);
        Assert.That(delta.Deletions, Does.Contain(new NetEntity(server)));

        _client.Apply(delta);
        Assert.That(_client.TryResolve(new NetEntity(server), out _), Is.False);
    }

    [Test]
    public void WireFormat_RoundTripsEveryNetworkedType()
    {
        int server = _server.CreateEntity();
        _server.AddComponent(server, new MetaData { EntityName = "Wire", Prototype = "p" });
        _server.AddComponent(server, new Transform { Position = new Vector2(1f, 2f), Velocity = new Vector2(3f, 4f) });
        _server.AddComponent(server, new Health { Max = 200f });
        _server.AddComponent(server, new Sprite { Texture = "own.png", Scale = 1.5f, Visible = false });
        _server.AddComponent(server, new Faction { Side = FactionType.Hostile });
        _server.AddComponent(server, new ClassificationProfile { Domain = ContactDomain.Air, TypeName = "Fighter" });
        _server.AddComponent(server, new Seeker { Kind = SeekerType.Ir, AcquisitionRangeKm = 12f, Datalink = true, LockedEntity = 7 });
        _server.AddComponent(server, new Propulsion { MaxSpeedKmPerTick = 3f, Waypoint = new Vector2(5f, 6f) });

        GameState state = _manager.BuildState(_server, fromTick: 0, static _ => true);
        byte[] bytes = GameStateSerializer.Serialize(state);
        GameState wire = GameStateSerializer.Deserialize(bytes, _factory);
        _client.Apply(wire);

        _client.TryResolve(new NetEntity(server), out int local);
        EntityManager entities = _client.Entities;
        Assert.Multiple(() =>
        {
            Assert.That(entities.GetComponent<MetaData>(local).Name, Is.EqualTo("Wire"));
            Assert.That(entities.GetComponent<Transform>(local).Position, Is.EqualTo(new Vector2(1f, 2f)));
            Assert.That(entities.GetComponent<Transform>(local).Velocity, Is.EqualTo(new Vector2(3f, 4f)));
            Assert.That(entities.GetComponent<Health>(local).Max, Is.EqualTo(200f));
            Assert.That(entities.GetComponent<Sprite>(local).Texture, Is.EqualTo("own.png"));
            Assert.That(entities.GetComponent<Sprite>(local).Scale, Is.EqualTo(1.5f));
            Assert.That(entities.GetComponent<Sprite>(local).Visible, Is.False);
            Assert.That(entities.GetComponent<Faction>(local).Side, Is.EqualTo(FactionType.Hostile));
            Assert.That(entities.GetComponent<ClassificationProfile>(local).Domain, Is.EqualTo(ContactDomain.Air));
            Assert.That(entities.GetComponent<ClassificationProfile>(local).TypeName, Is.EqualTo("Fighter"));
            Assert.That(entities.GetComponent<Seeker>(local).Kind, Is.EqualTo(SeekerType.Ir));
            Assert.That(entities.GetComponent<Seeker>(local).Datalink, Is.True);
            Assert.That(entities.GetComponent<Seeker>(local).LockedEntity, Is.EqualTo(7));
            Assert.That(entities.GetComponent<Propulsion>(local).Waypoint, Is.EqualTo(new Vector2(5f, 6f)));
        });
    }

    [Test]
    public void AppliedState_IsDecoupledFromServerComponents()
    {
        int server = SpawnShip("Alpha", new Vector2(1f, 1f), 100f);
        _client.Apply(_manager.BuildState(_server, fromTick: 0, static _ => true));
        _client.TryResolve(new NetEntity(server), out int local);

        _server.GetComponent<Transform>(server).Position = new Vector2(50f, 50f);

        Assert.That(
            _client.Entities.GetComponent<Transform>(local).Position,
            Is.EqualTo(new Vector2(1f, 1f)),
            "client holds its own copy, not a reference to the server component");
    }
}
