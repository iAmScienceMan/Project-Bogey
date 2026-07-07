using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Net;
using Content.Shared.Tracks;
using Lattice.Network;

namespace Content.Renderer.RealTime;

public enum GamePhase
{
    Connecting,
    Lobby,
    InGame,
    Disconnected,
}

public interface IGameSession : ISimSession
{
    GamePhase Phase { get; }

    string? DisconnectReason { get; }

    LobbyStatus? Lobby { get; }

    string Username { get; }

    NetworkStats Stats { get; }

    GroundTruthUpdate? GroundTruth { get; }

    bool TryDequeueNotice([NotNullWhen(true)] out string? notice);

    void SetReady(bool ready);

    void JoinGame();

    void SetColor(uint rgb);

    void GoLobby();

    void Kick(string username);

    void AdjustLobbyTime(bool isDelta, float seconds);

    void PauseTimer();

    void StartRound();

    void SetGroundTruth(bool enabled);

    void Disconnect();
}
