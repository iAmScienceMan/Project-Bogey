using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Content.Server;
using Content.Shared.Components;
using Content.Shared.Configuration;
using Content.Shared.Net;
using Content.Shared.Prototypes;
using Content.Shared.Tracks;
using Content.Sim.Content;
using Lattice.Logging;
using Lattice.Network;
using Lattice.Shared.Configuration;
using Lattice.Sim.Engine;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class GameTickerTests
{
    [Test]
    public void FullFlow_LobbyReadyRoundStartSnapshot()
    {
        int port = 48300 + (Environment.ProcessId % 300);

        PrototypeManager prototypes = new(new ComponentFactory(new[] { typeof(Sensor).Assembly }));
        prototypes.Register("player-ship", "player-ship", () => new List<IComponent>
        {
            new MetaData { EntityName = "player-ship" },
            new Faction { Side = FactionType.Friendly },
            new Transform(),
            new Signature { Value = 1f },
            new Health { Max = 100f },
        }.AsReadOnly());

        ScenarioDefinition scenario = new()
        {
            Id = "mp-test",
            Name = "MP Test",
            PlayerSpawn = new PlayerSpawnDefinition
            {
                Proto = "player-ship",
                Name = "Legion",
                Position = new List<float> { 0f, 0f },
            },
        };

        ServerConfig config = new()
        {
            Id = "test",
            Name = "Ticker Test",
            TickRate = 20,
        };

        ConfigurationManager cfg = new();
        cfg.RegisterCVars(typeof(CCVars));
        cfg.SetCVar(CCVars.GameLobbyDuration, 0.3f);

        NetworkServer server = new(port);
        server.Start();
        GameTicker ticker = new(server, prototypes, scenario, config, cfg, Logger.LogManager);
        Thread thread = new(ticker.Run) { IsBackground = true };
        thread.Start();

        NetworkClient client = new();

        try
        {
            client.Connect("127.0.0.1", port, HailSerializer.Serialize(new ClientHail
            {
                Username = "alice",
                ColorRgb = 0xAA0000,
                ProtocolVersion = NetworkProtocol.Version,
            }));

            LobbyStatus? lobby = null;
            TrackPictureSnapshot? snapshot = null;
            bool joined = false;
            bool readySent = false;
            int lobbyCount = 0;

            Assert.That(
                SpinUntil(() =>
                {
                    foreach (byte[] payload in client.Poll())
                    {
                        switch (ServerMessages.Kind(payload))
                        {
                            case ServerMessages.KindLobby:
                                lobby = ServerMessages.ReadLobby(payload);
                                lobbyCount++;
                                break;
                            case ServerMessages.KindJoinGame:
                                joined = true;
                                break;
                            case ServerMessages.KindSnapshot:
                                snapshot = ServerMessages.ReadSnapshot(payload);
                                break;
                        }
                    }

                    if (!readySent && client.IsConnected && lobby is not null)
                    {
                        client.Send(ClientMessages.SetReady(true));
                        readySent = true;
                    }

                    return joined && snapshot is not null;
                }),
                Is.True,
                $"expected lobby status, round start, and a snapshot; state={client.State} reason='{client.DisconnectReason}' lobbies={lobbyCount} readySent={readySent} joined={joined} snapshot={snapshot is not null} phase={lobby?.Phase}");

            Assert.Multiple(() =>
            {
                Assert.That(lobby!.ServerName, Is.EqualTo("Ticker Test"));
                Assert.That(lobby.Players, Has.Count.EqualTo(1));
                Assert.That(lobby.Players[0].Username, Is.EqualTo("alice"));
                Assert.That(lobby.Players[0].ColorRgb, Is.EqualTo(0xAA0000));
                Assert.That(snapshot!.OwnUnits, Has.Count.EqualTo(1));
                Assert.That(snapshot.OwnUnits[0].Name, Is.EqualTo("Legion"));
            });
        }
        finally
        {
            client.Shutdown();
            server.Shutdown();
        }
    }

    [Test]
    public void AdminFlow_SpeedGatingGoLobbyAndKick()
    {
        int port = 48700 + (Environment.ProcessId % 300);

        PrototypeManager prototypes = new(new ComponentFactory(new[] { typeof(Sensor).Assembly }));
        prototypes.Register("player-ship", "player-ship", () => new List<IComponent>
        {
            new MetaData { EntityName = "player-ship" },
            new Faction { Side = FactionType.Friendly },
            new Transform(),
            new Signature { Value = 1f },
            new Health { Max = 100f },
        }.AsReadOnly());

        ScenarioDefinition scenario = new()
        {
            Id = "mp-admin",
            Name = "Admin Test",
            PlayerSpawn = new PlayerSpawnDefinition
            {
                Proto = "player-ship",
                Name = "Legion",
                Position = new List<float> { 0f, 0f },
            },
        };

        ServerConfig config = new() { Id = "test", Name = "Admin Test", TickRate = 20 };
        ConfigurationManager cfg = new();
        cfg.RegisterCVars(typeof(CCVars));
        cfg.SetCVar(CCVars.GameLobbyDuration, 0.3f);

        NetworkServer server = new(port);
        server.Start();
        GameTicker ticker = new(server, prototypes, scenario, config, cfg, Logger.LogManager);
        Thread thread = new(ticker.Run) { IsBackground = true };
        thread.Start();

        NetworkClient client = new();

        try
        {
            client.Connect("127.0.0.1", port, HailSerializer.Serialize(new ClientHail
            {
                Username = "alice",
                ProtocolVersion = NetworkProtocol.Version,
            }));

            LobbyStatus? lobby = null;
            TrackPictureSnapshot? snapshot = null;
            List<string> notices = new();
            bool readySent = false;

            void Pump()
            {
                foreach (byte[] payload in client.Poll())
                {
                    switch (ServerMessages.Kind(payload))
                    {
                        case ServerMessages.KindLobby:
                            lobby = ServerMessages.ReadLobby(payload);
                            break;
                        case ServerMessages.KindSnapshot:
                            snapshot = ServerMessages.ReadSnapshot(payload);
                            break;
                        case ServerMessages.KindNotice:
                            notices.Add(ServerMessages.ReadNotice(payload));
                            break;
                    }
                }
            }

            Assert.That(
                SpinUntil(() =>
                {
                    Pump();
                    if (!readySent && client.IsConnected && lobby is not null)
                    {
                        client.Send(ClientMessages.SetReady(true));
                        readySent = true;
                    }

                    return snapshot is not null;
                }),
                Is.True,
                "round should start");

            client.Send(ClientMessages.SetSpeed(7));
            Assert.That(
                SpinUntil(() =>
                {
                    Pump();
                    return notices.Count > 0;
                }),
                Is.True,
                "non-admin speed change should produce a notice");
            Assert.That(notices[0], Is.EqualTo("You are not an admin on this server."));

            snapshot = null;
            Assert.That(
                SpinUntil(() =>
                {
                    Pump();
                    return snapshot is not null;
                }),
                Is.True);
            Assert.That(snapshot!.Speed, Is.EqualTo(1), "non-admin speed change must be ignored");

            ticker.EnqueueConsoleCommand("speed 5");
            Assert.That(
                SpinUntil(() =>
                {
                    Pump();
                    return snapshot is not null && snapshot.Speed == 5;
                }),
                Is.True,
                "server console speed change should apply mid-round");

            ticker.EnqueueConsoleCommand("admin alice");
            Assert.That(
                SpinUntil(() =>
                {
                    Pump();
                    return lobby is not null && lobby.Players.Count == 1 && lobby.Players[0].IsAdmin;
                }),
                Is.True,
                "lobby status should show alice as admin");

            client.Send(ClientMessages.SetSpeed(7));
            Assert.That(
                SpinUntil(() =>
                {
                    Pump();
                    return snapshot is not null && snapshot.Speed == 7;
                }),
                Is.True,
                "admin speed change should apply");

            GroundTruthUpdate? truth = null;
            client.Send(ClientMessages.SetGroundTruth(true));
            Assert.That(
                SpinUntil(() =>
                {
                    foreach (byte[] payload in client.Poll())
                    {
                        switch (ServerMessages.Kind(payload))
                        {
                            case ServerMessages.KindLobby:
                                lobby = ServerMessages.ReadLobby(payload);
                                break;
                            case ServerMessages.KindSnapshot:
                                snapshot = ServerMessages.ReadSnapshot(payload);
                                break;
                            case ServerMessages.KindGroundTruth:
                                truth = ServerMessages.ReadGroundTruth(payload);
                                break;
                        }
                    }

                    return truth is not null;
                }),
                Is.True,
                "admin should receive ground truth after requesting it");
            Assert.That(truth!.Entities, Has.Some.Matches<GroundTruthView>(e => e.Name == "Legion"));

            client.Send(ClientMessages.GoLobby());
            Assert.That(
                SpinUntil(() =>
                {
                    Pump();
                    return lobby is not null && lobby.Phase == RoundPhase.Lobby;
                }),
                Is.True,
                "golobby should return the server to the lobby");

            ticker.EnqueueConsoleCommand("pausetimer");
            Assert.That(
                SpinUntil(() =>
                {
                    Pump();
                    return lobby is not null && lobby.CountdownPaused;
                }),
                Is.True,
                "pausetimer should pause the countdown");

            ticker.EnqueueConsoleCommand("lobbytime 120");
            Assert.That(
                SpinUntil(() =>
                {
                    Pump();
                    return lobby is not null && lobby.RoundStartSeconds > 60f;
                }),
                Is.True,
                "lobbytime should set the countdown");

            ticker.EnqueueConsoleCommand("startround");
            Assert.That(
                SpinUntil(() =>
                {
                    Pump();
                    return lobby is not null && lobby.Phase == RoundPhase.InRound;
                }),
                Is.True,
                "startround should start the round immediately");

            ticker.EnqueueConsoleCommand("kick alice");
            Assert.That(
                SpinUntil(() =>
                {
                    Pump();
                    return client.State == ClientConnectionState.Disconnected;
                }),
                Is.True,
                "kicked client should disconnect");
            Assert.That(client.DisconnectReason, Is.EqualTo("Kicked by the server console."));
        }
        finally
        {
            client.Shutdown();
            server.Shutdown();
        }
    }

    private static bool SpinUntil(Func<bool> condition)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < 8000)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(15);
        }

        return false;
    }
}
