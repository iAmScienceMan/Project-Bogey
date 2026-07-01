using System;
using System.Collections.Generic;
using System.Numerics;
using Bogey.Shared.Components;
using Bogey.Shared.Events;
using Bogey.Shared.Prototypes;
using Bogey.Shared.Tracks;
using Bogey.Sim.Content;
using Bogey.Sim.Engine;
using Bogey.Sim.Systems;

namespace Bogey.Sim;

public sealed class SimRuntime
{
    private readonly EntityManager _entities = new();
    private readonly EventBus _bus = new();
    private readonly SimClock _clock = new();
    private readonly SystemManager _systems = new();
    private readonly TrackingSystem _tracking = new();

    public SimRuntime(IEnumerable<PrototypeDefinition> prototypes, int seed, SimConfig? config = null)
    {
        Random rng = new(seed);
        SimConfig effectiveConfig = config ?? new SimConfig();

        _systems
            .AddService(_entities)
            .AddService(_bus)
            .AddService(_clock)
            .AddService(rng)
            .AddService(effectiveConfig)
            .AddSystem(_tracking)
            .AddSystem(new OrderingSystem())
            .AddSystem(new MovementSystem())
            .AddSystem(new DetectionSystem())
            .AddSystem(new ClassificationSystem())
            .AddSystem(new TrackDecaySystem());

        _systems.Build();

        foreach (PrototypeDefinition prototype in prototypes)
        {
            PrototypeFactory.Spawn(_entities, prototype);
        }
    }

    public int CurrentTick => _clock.CurrentTick;

    public void Step()
    {
        _clock.Advance();
        _systems.Update();
    }

    public bool IssueMoveOrder(string unitName, Vector2 destination)
    {
        foreach (int entity in _entities.Query<Identity>())
        {
            if (_entities.GetComponent<Faction>(entity).Side != FactionType.Friendly)
            {
                continue;
            }

            if (!string.Equals(_entities.GetComponent<Identity>(entity).Name, unitName, StringComparison.Ordinal))
            {
                continue;
            }

            if (!_entities.HasComponent<Propulsion>(entity))
            {
                return false; 
            }

            _bus.PublishDirected(entity, new MoveOrderEvent
            {
                Destination = destination,
            });
            return true;
        }

        return false;
    }

    
    public TrackPictureSnapshot PublishSnapshot()
    {
        return new TrackPictureSnapshot
        {
            Tick = _clock.CurrentTick,
            Tracks = _tracking.CurrentTracks,
            OwnUnits = CollectOwnUnits(),
        };
    }

    
    public IReadOnlyList<GroundTruthEntry> DumpGroundTruth()
    {
        List<GroundTruthEntry> entries = new();

        foreach (int entity in _entities.Query<Transform>())
        {
            Transform transform = _entities.GetComponent<Transform>(entity);
            Faction faction = _entities.GetComponent<Faction>(entity);
            string name = NameOf(entity);

            ContactDomain domain = ContactDomain.Unknown;
            string? typeName = null;
            if (_entities.TryGetComponent(entity, out ClassificationProfile profile))
            {
                domain = profile.Domain;
                typeName = profile.TypeName;
            }

            entries.Add(new GroundTruthEntry
            {
                EntityId = entity,
                Name = name,
                Faction = faction.Side,
                Position = transform.Position,
                Domain = domain,
                TypeName = typeName,
            });
        }

        return entries;
    }

    public bool DebugSetPosition(int entityId, Vector2 position)
    {
        if (!_entities.HasComponent<Transform>(entityId))
        {
            return false;
        }

        _entities.GetComponent<Transform>(entityId).Position = position;
        return true;
    }

    private IReadOnlyList<OwnUnitView> CollectOwnUnits()
    {
        List<OwnUnitView> own = new();

        foreach (int entity in _entities.Query<Transform>())
        {
            if (_entities.GetComponent<Faction>(entity).Side != FactionType.Friendly)
            {
                continue;
            }

            string name = NameOf(entity);
            float range = _entities.TryGetComponent(entity, out Sensor sensor) ? sensor.RangeKm : 0f;

            own.Add(new OwnUnitView
            {
                Name = name,
                Position = _entities.GetComponent<Transform>(entity).Position,
                SensorRangeKm = range,
            });
        }

        return own;
    }

    private string NameOf(int entity)
        => _entities.TryGetComponent(entity, out Identity identity) ? identity.Name : $"#{entity}";
}
