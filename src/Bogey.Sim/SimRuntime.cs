using System;
using System.Collections.Generic;
using System.Numerics;
using Bogey.Logging;
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
    private readonly ILogbook _log;
    private readonly IReadOnlyDictionary<string, PrototypeDefinition> _prototypes;

    public event Action<WeaponFiredEvent>? WeaponFired;

    public event Action<MunitionResolvedEvent>? MunitionResolved;

    public event Action<EntityDestroyedEvent>? EntityDestroyed;

    public SimRuntime(
        ScenarioDefinition scenario,
        IReadOnlyDictionary<string, PrototypeDefinition> prototypes,
        int seed,
        SimConfig? config = null,
        ILogManager? logManager = null)
    {
        _prototypes = prototypes;
        Random rng = new(seed);
        SimConfig effectiveConfig = config ?? new SimConfig();
        ILogManager log = logManager ?? Logger.LogManager;
        _log = log.GetLogbook("sim.runtime");

        _systems
            .AddService(_entities)
            .AddService(_bus)
            .AddService(_clock)
            .AddService(rng)
            .AddService(effectiveConfig)
            .AddService(log)
            .AddService(prototypes)
            .AddSystem(_tracking)
            .AddSystem(new AiSystem())
            .AddSystem(new OrderingSystem())
            .AddSystem(new FireControlSystem())
            .AddSystem(new GuidanceSystem())
            .AddSystem(new MovementSystem())
            .AddSystem(new DetectionSystem())
            .AddSystem(new ClassificationSystem())
            .AddSystem(new TrackDecaySystem())
            .AddSystem(new DamageSystem());

        _systems.Build();

        _bus.Subscribe<WeaponFiredEvent>(evt => WeaponFired?.Invoke(evt));
        _bus.Subscribe<MunitionResolvedEvent>(evt => MunitionResolved?.Invoke(evt));
        _bus.Subscribe<EntityDestroyedEvent>(evt => EntityDestroyed?.Invoke(evt));

        int spawned = 0;
        foreach (ScenarioSpawn spawn in scenario.Spawns)
        {
            if (!_prototypes.TryGetValue(spawn.Proto, out PrototypeDefinition? prototype))
            {
                _log.Error($"Scenario '{scenario.Id}' references unknown prototype '{spawn.Proto}'; entry skipped.");
                continue;
            }

            PrototypeFactory.Spawn(_entities, prototype, PrototypeFactory.PlacementFor(spawn));
            spawned++;
        }

        _log.Info($"Sim initialized: seed={seed}, scenario='{scenario.Id}', {spawned} entities spawned.");
    }

    public int CurrentTick => _clock.CurrentTick;

    public IEnumerable<string> PrototypeIds => _prototypes.Keys;

    public bool SpawnFromPrototype(string prototypeId, Vector2 position, Vector2 velocity)
    {
        if (!_prototypes.TryGetValue(prototypeId, out PrototypeDefinition? prototype))
        {
            return false;
        }

        int entity = PrototypeFactory.Spawn(_entities, prototype, new Placement(position, velocity));
        _log.Info($"Spawned entity #{entity} from prototype '{prototypeId}' at ({position.X:0.##}, {position.Y:0.##}).");
        return true;
    }

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

    public bool IssueEngagement(string unitName, int trackId, string weapon, int count)
    {
        int shooter = FindArmedUnit(unitName);
        if (shooter < 0)
        {
            return false;
        }

        bool hasWeapon = false;
        foreach (WeaponMount mount in _entities.GetComponent<Loadout>(shooter).Mounts)
        {
            if (!mount.PointDefense && string.Equals(mount.ProjectilePrototype, weapon, StringComparison.OrdinalIgnoreCase))
            {
                hasWeapon = true;
                break;
            }
        }

        if (!hasWeapon)
        {
            return false;
        }

        _bus.Publish(new EngagementOrderEvent
        {
            Shooter = _entities.GetComponent<Identity>(shooter).Name,
            TrackId = trackId,
            Weapon = weapon,
            Count = Math.Max(1, count),
        });
        return true;
    }

    public bool SetLock(string unitName, int? trackId)
    {
        int unit = FindArmedUnit(unitName);
        if (unit < 0)
        {
            return false;
        }

        WeaponControl control = _entities.GetComponent<WeaponControl>(unit);
        if (trackId is not { } id)
        {
            control.LockedTarget = -1;
            _log.Info($"Unit '{unitName}' released its radar lock.");
            return true;
        }

        foreach (KeyValuePair<int, Track> entry in _tracking.EntriesFor(FactionType.Friendly))
        {
            if (entry.Value.TrackId == id)
            {
                control.LockedTarget = entry.Key;
                _log.Info($"Unit '{unitName}' locked track {id}.");
                return true;
            }
        }

        return false;
    }

    public bool SetPosture(string unitName, WeaponPosture posture)
    {
        int unit = FindArmedUnit(unitName);
        if (unit < 0)
        {
            return false;
        }

        _entities.GetComponent<WeaponControl>(unit).Posture = posture;
        _log.Info($"Unit '{unitName}' weapons posture set to {posture}.");
        return true;
    }


    public TrackPictureSnapshot PublishSnapshot()
    {
        return new TrackPictureSnapshot
        {
            Tick = _clock.CurrentTick,
            Tracks = _tracking.TracksFor(FactionType.Friendly),
            OwnUnits = CollectOwnUnits(),
            Munitions = CollectMunitions(),
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
        IReadOnlyDictionary<int, Track> picture = _tracking.EntriesFor(FactionType.Friendly);

        foreach (int entity in _entities.Query<Transform>())
        {
            if (_entities.GetComponent<Faction>(entity).Side != FactionType.Friendly)
            {
                continue;
            }

            if (_entities.HasComponent<Projectile>(entity))
            {
                continue;
            }

            string name = NameOf(entity);
            float range = _entities.TryGetComponent(entity, out Sensor sensor) ? sensor.RangeKm : 0f;
            WeaponControl? control = _entities.TryGetComponent(entity, out WeaponControl found) ? found : null;
            Health? health = _entities.TryGetComponent(entity, out Health hull) ? hull : null;

            int? lockedTrackId = null;
            if (control is { LockedTarget: >= 0 } && picture.TryGetValue(control.LockedTarget, out Track? lockedTrack))
            {
                lockedTrackId = lockedTrack.TrackId;
            }

            own.Add(new OwnUnitView
            {
                Name = name,
                Position = _entities.GetComponent<Transform>(entity).Position,
                SensorRangeKm = range,
                Posture = control?.Posture ?? WeaponPosture.Hold,
                HullCurrent = health?.Current ?? 0f,
                HullMax = health?.Max ?? 0f,
                LockedTrackId = lockedTrackId,
                Weapons = CollectWeapons(entity),
            });
        }

        return own;
    }

    private IReadOnlyList<WeaponStatusView> CollectWeapons(int entity)
    {
        if (!_entities.TryGetComponent(entity, out Loadout loadout))
        {
            return Array.Empty<WeaponStatusView>();
        }

        List<WeaponStatusView> weapons = new();
        foreach (WeaponMount mount in loadout.Mounts)
        {
            weapons.Add(new WeaponStatusView
            {
                Name = mount.PointDefense ? "CIWS" : mount.ProjectilePrototype,
                Rounds = mount.MagazineCapacity <= 0 ? -1 : mount.RoundsRemaining,
                Ready = mount.TicksUntilReady <= 0,
                PointDefense = mount.PointDefense,
            });
        }

        return weapons;
    }

    private IReadOnlyList<MunitionView> CollectMunitions()
    {
        List<MunitionView> munitions = new();

        foreach (int entity in _entities.Query<Projectile, Transform>())
        {
            if (_entities.GetComponent<Faction>(entity).Side != FactionType.Friendly)
            {
                continue;
            }

            Transform transform = _entities.GetComponent<Transform>(entity);
            Seeker? seeker = _entities.TryGetComponent(entity, out Seeker found) ? found : null;

            munitions.Add(new MunitionView
            {
                Id = entity,
                Position = transform.Position,
                HeadingRadians = HeadingOf(transform.Velocity),
                Faction = FactionType.Friendly,
                Seeker = seeker?.Type ?? SeekerType.Gps,
                Locked = seeker?.Locked ?? false,
            });
        }

        return munitions;
    }

    public IReadOnlyList<MunitionDebug> DumpMunitions()
    {
        List<MunitionDebug> munitions = new();

        foreach (int entity in _entities.Query<Projectile, Transform>())
        {
            Projectile projectile = _entities.GetComponent<Projectile>(entity);
            Transform transform = _entities.GetComponent<Transform>(entity);
            Seeker? seeker = _entities.TryGetComponent(entity, out Seeker found) ? found : null;

            int aimEntity = seeker is { LockedEntity: >= 0 } ? seeker.LockedEntity : projectile.TargetEntity;
            Vector2? targetPosition = _entities.TryGetComponent(aimEntity, out Transform target)
                ? target.Position
                : null;

            munitions.Add(new MunitionDebug
            {
                Id = entity,
                Faction = _entities.GetComponent<Faction>(entity).Side,
                Position = transform.Position,
                HeadingRadians = HeadingOf(transform.Velocity),
                Seeker = seeker?.Type ?? SeekerType.Gps,
                FovDegrees = seeker?.FovDegrees ?? 360f,
                AcquisitionRangeKm = seeker?.AcquisitionRangeKm ?? 0f,
                Locked = seeker?.Locked ?? false,
                Datum = projectile.Datum,
                DatumPassed = projectile.DatumPassed,
                TargetPosition = targetPosition,
            });
        }

        return munitions;
    }

    private static float HeadingOf(Vector2 velocity)
        => velocity.LengthSquared() > 1e-6f ? MathF.Atan2(velocity.Y, velocity.X) : 0f;

    private int FindArmedUnit(string unitName)
    {
        foreach (int entity in _entities.Query<Loadout, Identity>())
        {
            if (string.Equals(_entities.GetComponent<Identity>(entity).Name, unitName, StringComparison.Ordinal))
            {
                return entity;
            }
        }

        return -1;
    }

    private string NameOf(int entity)
        => _entities.TryGetComponent(entity, out Identity identity) ? identity.Name : $"#{entity}";
}
