using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Content.Renderer.RealTime;
using Content.Shared.Commands;
using Content.Shared.Net;
using Content.Shared.Tracks;
using Lattice.Network;

namespace Content.Client;

public sealed class NetworkGameSession : IGameSession
{
    private readonly NetworkClient _client;
    private readonly Queue<string> _notices = new();
    private double _sinceSnapshot;
    private double _interval = 1.0;

    public NetworkGameSession(string host, int port, string username, uint colorRgb)
    {
        Username = username;
        _client = new NetworkClient();
        _client.Connect(host, port, HailSerializer.Serialize(new ClientHail
        {
            Username = username,
            ColorRgb = colorRgb,
            ProtocolVersion = NetworkProtocol.Version,
        }));
    }

    public GamePhase Phase { get; private set; } = GamePhase.Connecting;

    public string? DisconnectReason { get; private set; }

    public LobbyStatus? Lobby { get; private set; }

    public string Username { get; }

    public TrackPictureSnapshot? Previous { get; private set; }

    public TrackPictureSnapshot? Current { get; private set; }

    public float Alpha { get; private set; }

    public int Speed => Current?.Speed ?? 1;

    public int Tick => Current?.Tick ?? 0;

    public void Advance(double realDeltaSeconds)
    {
        if (realDeltaSeconds < 0)
        {
            realDeltaSeconds = 0;
        }

        bool received = false;
        foreach (byte[] payload in _client.Poll())
        {
            if (payload.Length == 0)
            {
                continue;
            }

            switch (ServerMessages.Kind(payload))
            {
                case ServerMessages.KindLobby:
                    Lobby = ServerMessages.ReadLobby(payload);
                    if (Phase == GamePhase.Connecting
                        || (Phase == GamePhase.InGame && Lobby.Phase == RoundPhase.Lobby))
                    {
                        Phase = GamePhase.Lobby;
                        Previous = null;
                        Current = null;
                        GroundTruth = null;
                    }

                    break;
                case ServerMessages.KindJoinGame:
                    Phase = GamePhase.InGame;
                    break;
                case ServerMessages.KindSnapshot:
                    Previous = Current;
                    Current = ServerMessages.ReadSnapshot(payload);
                    received = true;
                    break;
                case ServerMessages.KindNotice:
                    _notices.Enqueue(ServerMessages.ReadNotice(payload));
                    break;
                case ServerMessages.KindGroundTruth:
                    GroundTruth = ServerMessages.ReadGroundTruth(payload);
                    break;
            }
        }

        if (_client.State == ClientConnectionState.Disconnected && Phase != GamePhase.Disconnected)
        {
            Phase = GamePhase.Disconnected;
            DisconnectReason = _client.DisconnectReason;
        }

        if (received)
        {
            if (_sinceSnapshot > 1e-3)
            {
                _interval = _sinceSnapshot;
            }

            _sinceSnapshot = 0;
        }

        _sinceSnapshot += realDeltaSeconds;
        Alpha = _interval > 1e-3 ? (float)Math.Clamp(_sinceSnapshot / _interval, 0.0, 1.0) : 0f;
    }

    public void SetSpeed(int speed) => Send(ClientMessages.SetSpeed(speed));

    public void Enqueue(SimCommand command) => Send(ClientMessages.Command(command));

    public NetworkStats Stats => _client.GetStats();

    public GroundTruthUpdate? GroundTruth { get; private set; }

    public bool TryDequeueNotice([NotNullWhen(true)] out string? notice)
        => _notices.TryDequeue(out notice);

    public void SetReady(bool ready) => Send(ClientMessages.SetReady(ready));

    public void JoinGame() => Send(ClientMessages.JoinGame());

    public void SetColor(uint rgb) => Send(ClientMessages.SetColor(rgb));

    public void GoLobby() => Send(ClientMessages.GoLobby());

    public void Kick(string username) => Send(ClientMessages.Kick(username));

    public void AdjustLobbyTime(bool isDelta, float seconds) => Send(ClientMessages.LobbyTime(isDelta, seconds));

    public void PauseTimer() => Send(ClientMessages.PauseTimer());

    public void StartRound() => Send(ClientMessages.StartRound());

    public void SetGroundTruth(bool enabled)
    {
        if (!enabled)
        {
            GroundTruth = null;
        }

        Send(ClientMessages.SetGroundTruth(enabled));
    }

    public void Disconnect() => _client.Shutdown();

    private void Send(byte[] payload)
    {
        if (_client.IsConnected)
        {
            _client.Send(payload);
        }
    }
}
