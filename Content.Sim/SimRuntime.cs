using System;
using System.Collections.Generic;
using System.Numerics;
using Lattice.Logging;
using Content.Shared.Components;
using Content.Shared.Events;
using Content.Shared.Prototypes;
using Content.Shared.Tracks;
using Content.Sim.Systems;
using Lattice.Sim.Engine;

namespace Content.Sim;

public sealed class SimRuntime
{
    public const string DefaultFaction = "friendly";

    private readonly EntityManager _entities = new();
    private readonly EventBus _bus = new();
    private readonly SimClock _clock;
    private readonly SystemManager _systems = new();
    private readonly TrackingSystem _tracking = new();
    private readonly ILogbook _log;
    private readonly PrototypeManager _prototypes;
    private readonly SimConfig _config;

    public event Action<WeaponFiredEvent>? WeaponFired;

    public event Action<MunitionResolvedEvent>? MunitionResolved;

    public event Action<EntityDestroyedEvent>? EntityDestroyed;

    public SimRuntime(
        ScenarioDefinition scenario,
        PrototypeManager prototypes,
        int seed,
        SimConfig? config = null,
        ILogManager? logManager = null,
        double dt = 1.0)
    {
        _prototypes = prototypes;
        _clock = new SimClock(dt);
        _entities.Bus = _bus;
        Random rng = new(seed);
        SimConfig effectiveConfig = config ?? new SimConfig();
        _config = effectiveConfig;
        ILogManager log = logManager ?? Logger.LogManager;
        _log = log.GetLogbook("sim.runtime");

        _systems
            .AddService(_entities)
            .AddService(_bus)
            .AddService(_clock)
            .AddService(rng)
            .AddService(effectiveConfig)
            .AddService(log)
            .AddService(_prototypes)
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
            if (!_prototypes.Has(spawn.Proto))
            {
                _log.Error($"Scenario '{scenario.Id}' references unknown prototype '{spawn.Proto}'; entry skipped.");
                continue;
            }

            Vector2 velocity = spawn.Velocity.Count == 0
                ? Vector2.Zero
                : ToVector2(spawn.Velocity, spawn.Proto, nameof(spawn.Velocity));
            SpawnEntity(spawn.Proto, ToVector2(spawn.Position, spawn.Proto, nameof(spawn.Position)), velocity, spawn.Name);
            spawned++;
        }

        _log.Info($"Sim initialized: seed={seed}, scenario='{scenario.Id}', {spawned} entities spawned.");
    }

    public int CurrentTick => _clock.CurrentTick;

    public IEnumerable<string> PrototypeIds => _prototypes.Prototypes.Keys;

    public bool SpawnFromPrototype(string prototypeId, Vector2 position, Vector2 velocity)
    {
        if (!_prototypes.Has(prototypeId))
        {
            return false;
        }

        EntityUid entity = SpawnEntity(prototypeId, position, velocity, null);
        _log.Info($"Spawned entity #{entity} from prototype '{prototypeId}' at ({position.X:0.##}, {position.Y:0.##}).");
        return true;
    }

    public bool SpawnPlayerUnit(string username, string prototypeId, string unitName, Vector2 position)
    {
        if (!_prototypes.Has(prototypeId))
        {
            _log.Error($"Player unit prototype '{prototypeId}' does not exist; '{username}' gets nothing.");
            return false;
        }

        EntityUid entity = SpawnEntity(prototypeId, position, Vector2.Zero, unitName);
        Faction faction = _entities.GetComponent<Faction>(entity);
        faction.Side = FactionType.Friendly;
        faction.Id = username;
        if (!_entities.HasComponent<PlayerControlled>(entity))
        {
            _entities.AddComponent(entity, new PlayerControlled());
        }
        _log.Info($"Spawned '{unitName}' #{entity} for player '{username}' at ({position.X:0.##}, {position.Y:0.##}).");
        return true;
    }

    public bool FactionHasUnits(string factionId)
    {
        foreach (EntityUid entity in _entities.Query<Faction>())
        {
            if (_entities.HasComponent<Projectile>(entity))
            {
                continue;
            }

            if (string.Equals(_entities.GetComponent<Faction>(entity).EffectiveId, factionId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private EntityUid SpawnEntity(string prototypeId, Vector2 position, Vector2 velocity, string? name)
    {
        EntityUid entity = _prototypes.SpawnEntity(_entities, prototypeId);

        if (_entities.TryGetComponent(entity, out Transform transform))
        {
            transform.Position = position;
            transform.Velocity = velocity;
        }

        if (name is not null)
        {
            _entities.GetComponent<MetaData>(entity).EntityName = name;
        }

        _bus.PublishDirected(entity, new ComponentInit());
        return entity;
    }

    public void Step()
    {
        _clock.Advance();
        _systems.Update();
        _entities.FlushDeletions();
    }

    public bool IssueMoveOrder(string unitName, Vector2 destination, string factionId = DefaultFaction)
    {
        foreach (EntityUid entity in _entities.Query<MetaData>())
        {
            if (!_entities.TryGetComponent(entity, out Faction faction)
                || !string.Equals(faction.EffectiveId, factionId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(_entities.GetComponent<MetaData>(entity).Name, unitName, StringComparison.Ordinal))
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

    public bool IssueEngagement(string unitName, int trackId, string weapon, int count, string factionId = DefaultFaction)
    {
        EntityUid shooter = FindArmedUnit(unitName, factionId);
        if (!shooter.Valid)
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
            Shooter = shooter,
            TrackId = trackId,
            Weapon = weapon,
            Count = Math.Max(1, count),
        });
        return true;
    }

    public bool SetLock(string unitName, int? trackId, string factionId = DefaultFaction)
    {
        EntityUid unit = FindArmedUnit(unitName, factionId);
        if (!unit.Valid)
        {
            return false;
        }

        WeaponControl control = _entities.GetComponent<WeaponControl>(unit);
        if (trackId is not { } id)
        {
            control.LockedTarget = EntityUid.Invalid;
            _log.Info($"Unit '{unitName}' released its radar lock.");
            return true;
        }

        foreach (KeyValuePair<EntityUid, Track> entry in _tracking.EntriesFor(factionId))
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

    public bool SetPosture(string unitName, WeaponPosture posture, string factionId = DefaultFaction)
    {
        EntityUid unit = FindArmedUnit(unitName, factionId);
        if (!unit.Valid)
        {
            return false;
        }

        _entities.GetComponent<WeaponControl>(unit).Posture = posture;
        _log.Info($"Unit '{unitName}' weapons posture set to {posture}.");
        return true;
    }


    public TrackPictureSnapshot PublishSnapshot(
        string factionId = DefaultFaction,
        int speed = 1,
        NameVisibility nameVisibility = NameVisibility.Detected,
        IReadOnlyDictionary<string, uint>? playerColors = null)
    {
        return new TrackPictureSnapshot
        {
            Tick = _clock.CurrentTick,
            Speed = speed,
            Tracks = CollectTracks(factionId, nameVisibility, playerColors),
            OwnUnits = CollectOwnUnits(factionId),
            Munitions = CollectMunitions(factionId),
        };
    }


    public IReadOnlyList<GroundTruthEntry> DumpGroundTruth()
    {
        List<GroundTruthEntry> entries = new();

        foreach (EntityUid entity in _entities.Query<Transform>())
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
                EntityId = entity.Id,
                Name = name,
                Faction = faction.Side,
                Position = transform.Position,
                Domain = domain,
                TypeName = typeName,
            });
        }

        return entries;
    }

    public void SetAiEnabled(bool enabled)
    {
        _config.AiEnabled = enabled;
        _log.Info($"AI {(enabled ? "enabled" : "disabled")}.");
    }

    public bool DebugSetPosition(int entityId, Vector2 position)
    {
        EntityUid entity = new(entityId);
        if (!_entities.HasComponent<Transform>(entity))
        {
            return false;
        }

        _entities.GetComponent<Transform>(entity).Position = position;
        return true;
    }

    private IReadOnlyList<Track> CollectTracks(
        string factionId,
        NameVisibility nameVisibility,
        IReadOnlyDictionary<string, uint>? playerColors)
    {
        List<Track> tracks = new();
        HashSet<EntityUid> tracked = new();

        foreach (KeyValuePair<EntityUid, Track> entry in _tracking.EntriesFor(factionId))
        {
            tracked.Add(entry.Key);
            tracks.Add(RevealOwner(entry.Key, entry.Value, nameVisibility, playerColors));
        }

        if (nameVisibility == NameVisibility.Always && playerColors is not null)
        {
            foreach (EntityUid entity in _entities.Query<Transform, MetaData>())
            {
                if (tracked.Contains(entity) || _entities.HasComponent<Projectile>(entity))
                {
                    continue;
                }

                if (!_entities.TryGetComponent(entity, out Faction faction))
                {
                    continue;
                }

                string owner = faction.EffectiveId;
                if (string.Equals(owner, factionId, StringComparison.Ordinal)
                    || !playerColors.TryGetValue(owner, out uint color))
                {
                    continue;
                }

                Transform transform = _entities.GetComponent<Transform>(entity);
                ClassificationProfile? profile =
                    _entities.TryGetComponent(entity, out ClassificationProfile found) ? found : null;

                tracks.Add(new Track
                {
                    TrackId = SyntheticTrackId(entity),
                    EstimatedPosition = transform.Position,
                    EstimatedVelocity = transform.Velocity,
                    PositionalErrorKm = 0f,
                    Confidence = 1f,
                    DomainGuess = profile?.Domain ?? ContactDomain.Unknown,
                    TypeGuess = profile?.TypeName,
                    LastUpdatedTick = _clock.CurrentTick,
                    State = TrackState.Identified,
                    UnitName = NameOf(entity),
                    PlayerName = owner,
                    PlayerColorRgb = color,
                });
            }
        }

        return tracks;
    }

    private Track RevealOwner(
        EntityUid truthEntity,
        Track track,
        NameVisibility nameVisibility,
        IReadOnlyDictionary<string, uint>? playerColors)
    {
        if (playerColors is null
            || _entities.HasComponent<Projectile>(truthEntity)
            || !_entities.TryGetComponent(truthEntity, out Faction faction)
            || !playerColors.TryGetValue(faction.EffectiveId, out uint color))
        {
            return track;
        }

        bool revealed = nameVisibility switch
        {
            NameVisibility.Identified => track.State == TrackState.Identified,
            _ => true,
        };

        if (!revealed)
        {
            return track;
        }

        return track with
        {
            UnitName = NameOf(truthEntity),
            PlayerName = faction.EffectiveId,
            PlayerColorRgb = color,
        };
    }

    private IReadOnlyList<OwnUnitView> CollectOwnUnits(string factionId)
    {
        List<OwnUnitView> own = new();
        IReadOnlyDictionary<EntityUid, Track> picture = _tracking.EntriesFor(factionId);

        foreach (EntityUid entity in _entities.Query<Transform>())
        {
            if (!_entities.TryGetComponent(entity, out Faction faction)
                || !string.Equals(faction.EffectiveId, factionId, StringComparison.Ordinal))
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
            if (control is not null && control.LockedTarget.Valid && picture.TryGetValue(control.LockedTarget, out Track? lockedTrack))
            {
                lockedTrackId = lockedTrack.TrackId;
            }

            Sprite? sprite = _entities.TryGetComponent(entity, out Sprite found2) ? found2 : null;

            own.Add(new OwnUnitView
            {
                Name = name,
                Position = _entities.GetComponent<Transform>(entity).Position,
                SensorRangeKm = range,
                Posture = control?.Posture ?? WeaponPosture.Hold,
                HullCurrent = health?.Current ?? 0f,
                HullMax = health?.Max ?? 0f,
                LockedTrackId = lockedTrackId,
                Sprite = sprite?.Texture,
                SpriteScale = sprite?.Scale ?? 1f,
                SpriteVisible = sprite?.Visible ?? true,
                Weapons = CollectWeapons(entity),
            });
        }

        return own;
    }

    private IReadOnlyList<WeaponStatusView> CollectWeapons(EntityUid entity)
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

    private IReadOnlyList<MunitionView> CollectMunitions(string factionId)
    {
        List<MunitionView> munitions = new();

        foreach (EntityUid entity in _entities.Query<Projectile, Transform>())
        {
            if (!_entities.TryGetComponent(entity, out Faction faction)
                || !string.Equals(faction.EffectiveId, factionId, StringComparison.Ordinal))
            {
                continue;
            }

            Transform transform = _entities.GetComponent<Transform>(entity);
            Projectile projectile = _entities.GetComponent<Projectile>(entity);
            Seeker? seeker = _entities.TryGetComponent(entity, out Seeker found) ? found : null;
            Sprite? sprite = _entities.TryGetComponent(entity, out Sprite found2) ? found2 : null;

            EntityUid aimEntity = seeker is { Locked: true } ? seeker.LockedEntity : projectile.TargetEntity;
            Vector2? targetPosition = _entities.TryGetComponent(aimEntity, out Transform aimTransform)
                ? aimTransform.Position
                : null;

            munitions.Add(new MunitionView
            {
                Id = entity.Id,
                Position = transform.Position,
                HeadingRadians = HeadingOf(transform.Velocity),
                Seeker = seeker?.Kind ?? SeekerType.Gps,
                Locked = seeker?.Locked ?? false,
                FovDegrees = seeker?.FovDegrees ?? 360f,
                AcquisitionRangeKm = seeker?.AcquisitionRangeKm ?? 0f,
                Datum = projectile.Datum,
                DatumPassed = projectile.DatumPassed,
                Ballistic = projectile.Ballistic,
                Finishing = projectile.Finishing,
                TargetPosition = targetPosition,
                Sprite = sprite?.Texture,
                SpriteScale = sprite?.Scale ?? 1f,
                SpriteVisible = sprite?.Visible ?? true,
            });
        }

        return munitions;
    }

    public IReadOnlyList<MunitionDebug> DumpMunitions()
    {
        List<MunitionDebug> munitions = new();

        foreach (EntityUid entity in _entities.Query<Projectile, Transform>())
        {
            Projectile projectile = _entities.GetComponent<Projectile>(entity);
            Transform transform = _entities.GetComponent<Transform>(entity);
            Seeker? seeker = _entities.TryGetComponent(entity, out Seeker found) ? found : null;

            EntityUid aimEntity = seeker is { Locked: true } ? seeker.LockedEntity : projectile.TargetEntity;
            Vector2? targetPosition = _entities.TryGetComponent(aimEntity, out Transform target)
                ? target.Position
                : null;

            munitions.Add(new MunitionDebug
            {
                Id = entity.Id,
                Faction = _entities.GetComponent<Faction>(entity).Side,
                Position = transform.Position,
                HeadingRadians = HeadingOf(transform.Velocity),
                Seeker = seeker?.Kind ?? SeekerType.Gps,
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

    private static int SyntheticTrackId(EntityUid entity) => -entity.Id;

    private EntityUid FindArmedUnit(string unitName, string factionId)
    {
        foreach (EntityUid entity in _entities.Query<Loadout, MetaData>())
        {
            if (!_entities.TryGetComponent(entity, out Faction faction)
                || !string.Equals(faction.EffectiveId, factionId, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(_entities.GetComponent<MetaData>(entity).Name, unitName, StringComparison.Ordinal))
            {
                return entity;
            }
        }

        return EntityUid.Invalid;
    }

    private string NameOf(EntityUid entity)
    {
        string name = _entities.GetComponent<MetaData>(entity).Name;
        return string.IsNullOrEmpty(name) ? $"#{entity}" : name;
    }

    private static Vector2 ToVector2(IReadOnlyList<float> values, string prototypeName, string field)
    {
        if (values.Count < 2)
        {
            throw new InvalidOperationException(
                $"Scenario spawn of '{prototypeName}' field '{field}' must list two numbers [x, y].");
        }

        return new Vector2(values[0], values[1]);
    }
}
