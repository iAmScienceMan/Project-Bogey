using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Threading;
using Content.Shared.Commands;
using Content.Shared.Configuration;
using Content.Shared.Net;
using Content.Shared.Prototypes;
using Content.Sim;
using Content.Sim.Content;
using Content.Sim.Systems;
using Content.Shared.Tracks;
using Lattice.Logging;
using Lattice.Network;
using Lattice.Shared.Configuration;
using Lattice.Shared.Console;
using Lattice.Sim.Engine;

namespace Content.Server;

public sealed class GameTicker
{
    private const int MaxUsernameLength = 24;
    private const double LobbyBroadcastInterval = 1.0;
    private const int MaxSpeed = 100;

    private enum RunLevel
    {
        Lobby,
        InRound,
    }

    private sealed class PlayerRecord
    {
        public required string Username { get; init; }

        public uint ColorRgb { get; set; }

        public bool Ready { get; set; }

        public bool InGame { get; set; }

        public bool WantsGroundTruth { get; set; }

        public long? ConnectionId { get; set; }

        public bool Connected => ConnectionId is not null;
    }

    private readonly NetworkServer _server;
    private readonly PrototypeManager _prototypes;
    private readonly ScenarioDefinition _scenario;
    private readonly ServerConfig _config;
    private readonly IConfigurationManager _cfg;
    private readonly ILogManager _logManager;
    private readonly ILogbook _log;

    private readonly Dictionary<string, PlayerRecord> _players = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _joinOrder = new();
    private readonly Dictionary<long, string> _usernamesByConnection = new();
    private readonly Dictionary<long, string> _pendingApprovals = new();
    private readonly HashSet<string> _admins = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<string> _consoleCommands = new();

    private readonly ConsoleCommandRegistry _serverConsole;
    private readonly StdoutConsoleShell _stdoutShell = new();

    private RunLevel _level = RunLevel.Lobby;
    private SimRuntime? _sim;
    private double _countdownRemaining;
    private bool _timerPaused;
    private int _speed = 1;
    private bool _lobbyDirty = true;
    private bool _snapshotDirty;
    private double _sinceLobbyBroadcast;
    private bool _stopping;

    public GameTicker(
        NetworkServer server,
        PrototypeManager prototypes,
        ScenarioDefinition scenario,
        ServerConfig config,
        IConfigurationManager cfg,
        ILogManager logManager)
    {
        _server = server;
        _prototypes = prototypes;
        _scenario = scenario;
        _config = config;
        _cfg = cfg;
        _logManager = logManager;
        _log = logManager.GetLogbook("ticker");
        _countdownRemaining = cfg.GetCVar(CCVars.GameLobbyDuration);

        foreach (string admin in config.Admins)
        {
            _admins.Add(admin.Trim());
        }

        _serverConsole = new ConsoleCommandRegistry(
            logManager.GetLogbook("server.console"),
            new object[] { this, cfg });

        _server.ApprovalHandler = HandleApproval;
        _server.PeerConnected += HandlePeerConnected;
        _server.PeerDisconnected += HandlePeerDisconnected;
    }

    public void EnqueueConsoleCommand(string commandLine) => _consoleCommands.Enqueue(commandLine);

    public void Run()
    {
        double secondsPerTick = 1.0 / _config.TickRate;
        Stopwatch clock = Stopwatch.StartNew();
        double last = clock.Elapsed.TotalSeconds;
        double accumulator = 0;

        while (!_stopping)
        {
            foreach ((long connectionId, byte[] payload) in _server.Poll())
            {
                try
                {
                    HandleMessage(connectionId, payload);
                }
                catch (Exception ex)
                {
                    _log.Error($"Dropped malformed message from connection {connectionId}: {ex.Message}");
                }
            }

            while (_consoleCommands.TryDequeue(out string? commandLine))
            {
                _serverConsole.Execute(commandLine, _stdoutShell);
            }

            double now = clock.Elapsed.TotalSeconds;
            double dt = now - last;
            last = now;

            if (_server.ConnectionCount == 0)
            {
                accumulator = 0;
                Thread.Sleep(50);
                continue;
            }

            if (_level == RunLevel.Lobby)
            {
                AdvanceCountdown(dt);
            }
            else
            {
                accumulator += dt * _speed;
                int steps = 0;
                while (accumulator >= secondsPerTick && steps < 100)
                {
                    _sim!.Step();
                    accumulator -= secondsPerTick;
                    steps++;
                }

                if (accumulator >= secondsPerTick)
                {
                    accumulator = 0;
                }

                if (steps > 0 || _snapshotDirty)
                {
                    SendSnapshots();
                    _snapshotDirty = false;
                }
            }

            _sinceLobbyBroadcast += dt;
            if (_lobbyDirty || _sinceLobbyBroadcast >= LobbyBroadcastInterval)
            {
                BroadcastLobbyStatus();
                _lobbyDirty = false;
                _sinceLobbyBroadcast = 0;
            }

            Thread.Sleep(5);
        }
    }

    private void AdvanceCountdown(double dt)
    {
        if (_timerPaused)
        {
            return;
        }

        _countdownRemaining -= dt;
        if (_countdownRemaining > 0)
        {
            return;
        }

        bool anyoneReady = false;
        foreach (PlayerRecord player in _players.Values)
        {
            if (player.Connected && player.Ready)
            {
                anyoneReady = true;
                break;
            }
        }

        if (anyoneReady)
        {
            StartRound();
        }
        else
        {
            _countdownRemaining = _cfg.GetCVar(CCVars.GameLobbyDuration);
            _lobbyDirty = true;
        }
    }

    private void StartRound()
    {
        _sim = new SimRuntime(_scenario, _prototypes, _config.Seed, _config.Sim ?? SimConfigFromCVars(), _logManager);
        _level = RunLevel.InRound;
        _timerPaused = false;
        _log.Info($"Round started: scenario '{_scenario.Id}', seed {_config.Seed}.");

        foreach (string username in _joinOrder)
        {
            PlayerRecord player = _players[username];
            if (!player.Connected || !player.Ready)
            {
                continue;
            }

            SpawnPlayerUnits(player);
            player.InGame = true;
            _server.Send(player.ConnectionId!.Value, ServerMessages.JoinGame());
        }

        _lobbyDirty = true;
    }

    private void SpawnPlayerUnits(PlayerRecord player)
    {
        if (_sim is null || _sim.FactionHasUnits(player.Username))
        {
            return;
        }

        PlayerSpawnDefinition spawn = _scenario.PlayerSpawn;
        Vector2 basePosition = spawn.Position.Count >= 2
            ? new Vector2(spawn.Position[0], spawn.Position[1])
            : Vector2.Zero;
        int index = _joinOrder.IndexOf(player.Username);
        Vector2 position = basePosition + new Vector2(spawn.SpacingKm * Math.Max(0, index), 0f);

        _sim.SpawnPlayerUnit(player.Username, spawn.Proto, spawn.Name, position);
    }

    private void SendSnapshots()
    {
        if (_sim is null)
        {
            return;
        }

        NameVisibility visibility = NameVisibilityParser.Parse(_cfg.GetCVar(CCVars.GameNameVisibility));
        Dictionary<string, uint> colors = new(StringComparer.Ordinal);
        foreach (PlayerRecord player in _players.Values)
        {
            colors[player.Username] = player.ColorRgb;
        }

        List<GroundTruthView>? groundTruth = null;

        foreach (PlayerRecord player in _players.Values)
        {
            if (player.ConnectionId is not { } connectionId || !player.InGame)
            {
                continue;
            }

            TrackPictureSnapshot snapshot = _sim.PublishSnapshot(player.Username, _speed, visibility, colors);
            _server.Send(connectionId, ServerMessages.Snapshot(snapshot));

            if (player.WantsGroundTruth && IsAdmin(player.Username))
            {
                groundTruth ??= CollectGroundTruth();
                _server.Send(connectionId, ServerMessages.GroundTruth(groundTruth));
            }
        }
    }

    private List<GroundTruthView> CollectGroundTruth()
    {
        List<GroundTruthView> entries = new();
        foreach (GroundTruthEntry entry in _sim!.DumpGroundTruth())
        {
            entries.Add(new GroundTruthView
            {
                EntityId = entry.EntityId,
                Name = entry.Name,
                Side = entry.Faction,
                Domain = entry.Domain,
                Position = entry.Position,
                TypeName = entry.TypeName,
            });
        }

        return entries;
    }

    private void BroadcastLobbyStatus()
    {
        LobbyStatus status = BuildLobbyStatus();
        byte[] payload = ServerMessages.Lobby(status);

        foreach (PlayerRecord player in _players.Values)
        {
            if (player.ConnectionId is { } connectionId)
            {
                _server.Send(connectionId, payload);
            }
        }
    }

    private LobbyStatus BuildLobbyStatus()
    {
        List<LobbyPlayer> players = new();
        foreach (string username in _joinOrder)
        {
            PlayerRecord player = _players[username];
            if (!player.Connected)
            {
                continue;
            }

            players.Add(new LobbyPlayer
            {
                Username = player.Username,
                ColorRgb = player.ColorRgb,
                Ready = player.Ready,
                InGame = player.InGame,
                IsAdmin = IsAdmin(player.Username),
            });
        }

        return new LobbyStatus
        {
            ServerName = _config.Name ?? _config.Id,
            ScenarioName = _scenario.Name,
            Phase = _level == RunLevel.Lobby ? RoundPhase.Lobby : RoundPhase.InRound,
            RoundStartSeconds = _level == RunLevel.Lobby ? (float)Math.Max(0, _countdownRemaining) : 0f,
            CountdownPaused = _timerPaused,
            RoundTick = _sim?.CurrentTick ?? 0,
            Players = players,
        };
    }

    private string? HandleApproval(long connectionId, byte[] hail)
    {
        ClientHail parsed;
        try
        {
            parsed = HailSerializer.Deserialize(hail);
        }
        catch (Exception)
        {
            return "Malformed connection request.";
        }

        if (parsed.ProtocolVersion != NetworkProtocol.Version)
        {
            return $"Protocol mismatch: server runs version {NetworkProtocol.Version}, your client uses {parsed.ProtocolVersion}.";
        }

        string username = parsed.Username.Trim();
        if (username.Length == 0)
        {
            return "Username must not be empty.";
        }

        if (username.Length > MaxUsernameLength)
        {
            return $"Username must be at most {MaxUsernameLength} characters.";
        }

        if (_players.TryGetValue(username, out PlayerRecord? existing)
            && existing.Connected
            && existing.ConnectionId != connectionId)
        {
            return $"The username '{existing.Username}' is already present on server!";
        }

        foreach ((long pendingConnection, string pending) in _pendingApprovals)
        {
            if (pendingConnection != connectionId
                && string.Equals(pending, username, StringComparison.OrdinalIgnoreCase))
            {
                return $"The username '{pending}' is already present on server!";
            }
        }

        _pendingApprovals[connectionId] = username;
        return null;
    }

    private void HandlePeerConnected(long connectionId, byte[] hail)
    {
        _pendingApprovals.Remove(connectionId);

        ClientHail parsed;
        try
        {
            parsed = HailSerializer.Deserialize(hail);
        }
        catch (Exception)
        {
            _server.Disconnect(connectionId, "Malformed connection request.");
            return;
        }

        string username = parsed.Username.Trim();
        if (!_players.TryGetValue(username, out PlayerRecord? player))
        {
            player = new PlayerRecord { Username = username };
            _players[username] = player;
            _joinOrder.Add(username);
        }

        player.ConnectionId = connectionId;
        player.ColorRgb = parsed.ColorRgb;
        player.Ready = false;
        player.InGame = false;
        _usernamesByConnection[connectionId] = player.Username;
        _lobbyDirty = true;

        _log.Info($"Player '{player.Username}' connected.");
    }

    private void HandlePeerDisconnected(long connectionId)
    {
        _pendingApprovals.Remove(connectionId);

        if (!_usernamesByConnection.Remove(connectionId, out string? username))
        {
            return;
        }

        PlayerRecord player = _players[username];
        player.ConnectionId = null;
        player.Ready = false;
        player.InGame = false;
        _lobbyDirty = true;

        _log.Info($"Player '{player.Username}' disconnected.");
    }

    private void HandleMessage(long connectionId, byte[] payload)
    {
        if (payload.Length == 0 || !_usernamesByConnection.TryGetValue(connectionId, out string? username))
        {
            return;
        }

        PlayerRecord player = _players[username];

        switch (ClientMessages.Kind(payload))
        {
            case ClientMessages.KindCommand when _level == RunLevel.InRound && player.InGame && _sim is not null:
                SimCommand command = ClientMessages.ReadCommand(payload);
                if (RequiresAdmin(command) && !RequireAdmin(player, connectionId))
                {
                    break;
                }

                SimCommands.Apply(_sim, command, player.Username);
                break;

            case ClientMessages.KindSetColor:
                player.ColorRgb = ClientMessages.ReadSetColor(payload);
                _lobbyDirty = true;
                break;

            case ClientMessages.KindSetReady when _level == RunLevel.Lobby:
                player.Ready = ClientMessages.ReadSetReady(payload);
                _lobbyDirty = true;
                break;

            case ClientMessages.KindJoinGame when _level == RunLevel.InRound && !player.InGame:
                SpawnPlayerUnits(player);
                player.InGame = true;
                _server.Send(connectionId, ServerMessages.JoinGame());
                _lobbyDirty = true;
                _snapshotDirty = true;
                break;

            case ClientMessages.KindSetSpeed when _level == RunLevel.InRound:
                if (!RequireAdmin(player, connectionId))
                {
                    break;
                }

                SetSpeed(ClientMessages.ReadSetSpeed(payload));
                _log.Info($"Admin '{player.Username}' set speed to {_speed}.");
                break;

            case ClientMessages.KindGoLobby when _level == RunLevel.InRound:
                if (RequireAdmin(player, connectionId))
                {
                    GoLobby($"admin '{player.Username}'");
                }

                break;

            case ClientMessages.KindKick:
                if (RequireAdmin(player, connectionId)
                    && !Kick(ClientMessages.ReadKick(payload), player.Username))
                {
                    _server.Send(connectionId, ServerMessages.Notice("No connected player with that username."));
                }

                break;

            case ClientMessages.KindLobbyTime when _level == RunLevel.Lobby:
                if (RequireAdmin(player, connectionId))
                {
                    (bool isDelta, float seconds) = ClientMessages.ReadLobbyTime(payload);
                    AdjustLobbyTime(isDelta, seconds);
                }

                break;

            case ClientMessages.KindPauseTimer when _level == RunLevel.Lobby:
                if (RequireAdmin(player, connectionId))
                {
                    SetTimerPaused(!_timerPaused);
                }

                break;

            case ClientMessages.KindStartRound when _level == RunLevel.Lobby:
                if (RequireAdmin(player, connectionId))
                {
                    ForceStartRound();
                }

                break;

            case ClientMessages.KindSetGroundTruth:
                if (RequireAdmin(player, connectionId))
                {
                    player.WantsGroundTruth = ClientMessages.ReadSetGroundTruth(payload);
                    _snapshotDirty = true;
                }

                break;
        }
    }

    private bool RequireAdmin(PlayerRecord player, long connectionId)
    {
        if (IsAdmin(player.Username))
        {
            return true;
        }

        _server.Send(connectionId, ServerMessages.Notice("You are not an admin on this server."));
        _log.Warning($"Player '{player.Username}' tried an admin action without permission.");
        return false;
    }

    private bool IsAdmin(string username) => _admins.Contains(username);

    private static bool RequiresAdmin(SimCommand command)
        => command is SpawnCommand or TeleportCommand or AiCommand;

    public string StatusLine()
        => $"level={_level} tick={_sim?.CurrentTick ?? 0} speed={_speed} "
           + $"connections={_server.ConnectionCount} known-players={_players.Count} "
           + $"protocol={NetworkProtocol.Version} scenario='{_scenario.Id}'"
           + (_level == RunLevel.Lobby
               ? string.Create(CultureInfo.InvariantCulture,
                   $" countdown={Math.Max(0, _countdownRemaining):0.0}s{(_timerPaused ? " (paused)" : string.Empty)}")
               : string.Empty);

    public IReadOnlyList<string> PlayerLines()
    {
        List<string> lines = new();
        foreach (string username in _joinOrder)
        {
            PlayerRecord player = _players[username];
            string state = player.Connected ? player.InGame ? "in game" : "in lobby" : "offline";
            string admin = IsAdmin(username) ? " [admin]" : string.Empty;
            lines.Add($"{player.Username}{admin} - {state}");
        }

        return lines;
    }

    public void GrantAdmin(string username)
    {
        _admins.Add(username.Trim());
        _lobbyDirty = true;
    }

    public bool RevokeAdmin(string username)
    {
        bool removed = _admins.Remove(username.Trim());
        _lobbyDirty = true;
        return removed;
    }

    public bool Kick(string target, string kickedBy)
    {
        if (!_players.TryGetValue(target.Trim(), out PlayerRecord? player) || player.ConnectionId is not { } connectionId)
        {
            return false;
        }

        _server.Disconnect(connectionId, $"Kicked by {kickedBy}.");
        _log.Info($"Player '{player.Username}' was kicked by {kickedBy}.");
        return true;
    }

    public bool RoundRunning => _level == RunLevel.InRound;

    public bool TimerPaused => _timerPaused;

    public double CountdownRemaining => Math.Max(0, _countdownRemaining);

    public void GoLobby(string initiator)
    {
        _sim = null;
        _level = RunLevel.Lobby;
        _speed = 1;
        _timerPaused = false;
        _countdownRemaining = _cfg.GetCVar(CCVars.GameLobbyDuration);

        foreach (PlayerRecord player in _players.Values)
        {
            player.Ready = false;
            player.InGame = false;
        }

        _lobbyDirty = true;
        _log.Info($"Round ended by {initiator}; returning everyone to the lobby.");
    }

    public void SetSpeed(int speed)
    {
        _speed = Math.Clamp(speed, 0, MaxSpeed);
        _snapshotDirty = true;
    }

    public int Speed => _speed;

    public bool SetAi(bool enabled)
    {
        if (_sim is null)
        {
            return false;
        }

        _sim.SetAiEnabled(enabled);
        return true;
    }

    public void AdjustLobbyTime(bool isDelta, float seconds)
    {
        _countdownRemaining = isDelta ? Math.Max(0, _countdownRemaining + seconds) : Math.Max(0, seconds);
        _lobbyDirty = true;
        _log.Info(string.Create(CultureInfo.InvariantCulture,
            $"Lobby countdown {(isDelta ? "adjusted" : "set")} to {Math.Max(0, _countdownRemaining):0.0}s."));
    }

    public void SetTimerPaused(bool paused)
    {
        _timerPaused = paused;
        _lobbyDirty = true;
        _log.Info(paused ? "Lobby countdown paused." : "Lobby countdown resumed.");
    }

    public bool ForceStartRound()
    {
        if (_level != RunLevel.Lobby)
        {
            return false;
        }

        StartRound();
        return true;
    }

    public void Stop()
    {
        _stopping = true;
        _server.Shutdown();
    }

    private SimConfig SimConfigFromCVars() => new()
    {
        InitialConfidence = _cfg.GetCVar(CCVars.SimInitialConfidence),
        ConfidenceGainPerHit = _cfg.GetCVar(CCVars.SimConfidenceGain),
        ClassifyThreshold = _cfg.GetCVar(CCVars.SimClassifyThreshold),
        IdentifyThreshold = _cfg.GetCVar(CCVars.SimIdentifyThreshold),
        MunitionClassifyThreshold = _cfg.GetCVar(CCVars.SimMunitionClassifyThreshold),
        MunitionIdentifyThreshold = _cfg.GetCVar(CCVars.SimMunitionIdentifyThreshold),
        MaxAutoCommitPerTarget = _cfg.GetCVar(CCVars.SimMaxAutoCommit),
        BasePositionalErrorKm = _cfg.GetCVar(CCVars.SimPositionalErrorBase),
        ObservationNoiseKm = _cfg.GetCVar(CCVars.SimObservationNoise),
        DecayConfidenceFactor = _cfg.GetCVar(CCVars.SimDecayFactor),
        PositionalErrorGrowthKmPerTick = _cfg.GetCVar(CCVars.SimPositionalErrorGrowth),
        StaleAfterIdleTicks = _cfg.GetCVar(CCVars.SimStaleTicks),
        DropAfterIdleTicks = _cfg.GetCVar(CCVars.SimDropTicks),
    };
}
