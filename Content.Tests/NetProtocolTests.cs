using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Content.Shared.Commands;
using Content.Shared.Net;
using Lattice.Network;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class NetProtocolTests
{
    [Test]
    public void Hail_RoundTrips()
    {
        ClientHail wire = HailSerializer.Deserialize(HailSerializer.Serialize(new ClientHail
        {
            Username = "science",
            ColorRgb = 0xABCDEF,
        }));

        Assert.Multiple(() =>
        {
            Assert.That(wire.Username, Is.EqualTo("science"));
            Assert.That(wire.ColorRgb, Is.EqualTo(0xABCDEF));
        });
    }

    [Test]
    public void LobbyStatus_RoundTrips()
    {
        LobbyStatus original = new()
        {
            ServerName = "Test Server",
            ScenarioName = "Meridian Patrol",
            Phase = RoundPhase.InRound,
            RoundStartSeconds = 12.5f,
            CountdownPaused = false,
            RoundTick = 420,
            Players = new List<LobbyPlayer>
            {
                new() { Username = "alice", ColorRgb = 0xFF0000, Ready = true, InGame = true },
                new() { Username = "bob", ColorRgb = 0x00FF00, Ready = false, InGame = false },
            },
        };

        byte[] payload = ServerMessages.Lobby(original);
        Assert.That(ServerMessages.Kind(payload), Is.EqualTo(ServerMessages.KindLobby));

        LobbyStatus wire = ServerMessages.ReadLobby(payload);

        Assert.Multiple(() =>
        {
            Assert.That(wire.ServerName, Is.EqualTo("Test Server"));
            Assert.That(wire.ScenarioName, Is.EqualTo("Meridian Patrol"));
            Assert.That(wire.Phase, Is.EqualTo(RoundPhase.InRound));
            Assert.That(wire.RoundStartSeconds, Is.EqualTo(12.5f));
            Assert.That(wire.RoundTick, Is.EqualTo(420));
            Assert.That(wire.Players, Has.Count.EqualTo(2));
            Assert.That(wire.Players[0].Username, Is.EqualTo("alice"));
            Assert.That(wire.Players[0].InGame, Is.True);
            Assert.That(wire.Players[1].ColorRgb, Is.EqualTo(0x00FF00));
        });
    }

    [Test]
    public void Connect_ToGarbageAddress_FailsWithoutThrowing()
    {
        NetworkClient client = new();
        client.Connect("definitely-not-a-real-host-xyz)", 8712);

        Assert.Multiple(() =>
        {
            Assert.That(client.State, Is.EqualTo(ClientConnectionState.Disconnected));
            Assert.That(client.DisconnectReason, Is.Not.Null.And.Not.Empty);
        });
    }

    [Test]
    public void AdminCommands_RoundTrip()
    {
        Content.Shared.Commands.TeleportCommand teleport =
            (Content.Shared.Commands.TeleportCommand)ClientMessages.ReadCommand(
                ClientMessages.Command(new Content.Shared.Commands.TeleportCommand
                {
                    EntityId = 12,
                    Position = new System.Numerics.Vector2(3f, 4f),
                }));

        Content.Shared.Commands.AiCommand ai =
            (Content.Shared.Commands.AiCommand)ClientMessages.ReadCommand(
                ClientMessages.Command(new Content.Shared.Commands.AiCommand { Enabled = false }));

        Assert.Multiple(() =>
        {
            Assert.That(teleport.EntityId, Is.EqualTo(12));
            Assert.That(teleport.Position, Is.EqualTo(new System.Numerics.Vector2(3f, 4f)));
            Assert.That(ai.Enabled, Is.False);
            Assert.That(ClientMessages.Kind(ClientMessages.GoLobby()), Is.EqualTo(ClientMessages.KindGoLobby));
            Assert.That(ClientMessages.ReadKick(ClientMessages.Kick("science")), Is.EqualTo("science"));
        });
    }

    [Test]
    public void ClientMessages_RoundTrip()
    {
        byte[] command = ClientMessages.Command(new EngageCommand
        {
            UnitName = "Legion",
            TrackId = 4,
            Weapon = "AIM-120D",
            Count = 2,
        });

        Assert.That(ClientMessages.Kind(command), Is.EqualTo(ClientMessages.KindCommand));
        EngageCommand engage = (EngageCommand)ClientMessages.ReadCommand(command);

        Assert.Multiple(() =>
        {
            Assert.That(engage.UnitName, Is.EqualTo("Legion"));
            Assert.That(engage.TrackId, Is.EqualTo(4));
            Assert.That(ClientMessages.ReadSetColor(ClientMessages.SetColor(0x123456)), Is.EqualTo(0x123456));
            Assert.That(ClientMessages.ReadSetReady(ClientMessages.SetReady(true)), Is.True);
            Assert.That(ClientMessages.ReadSetReady(ClientMessages.SetReady(false)), Is.False);
            Assert.That(ClientMessages.ReadSetSpeed(ClientMessages.SetSpeed(10)), Is.EqualTo(10));
            Assert.That(ClientMessages.Kind(ClientMessages.JoinGame()), Is.EqualTo(ClientMessages.KindJoinGame));
        });
    }

    [Test]
    public void Approval_DeniedClientReceivesReason()
    {
        int port = 47600 + (Environment.ProcessId % 300);
        NetworkServer server = new(port);
        HashSet<string> usernames = new(StringComparer.OrdinalIgnoreCase);

        server.ApprovalHandler = (_, hail) =>
        {
            string username = HailSerializer.Deserialize(hail).Username;
            return usernames.Add(username)
                ? null
                : $"The username '{username}' is already present on server!";
        };
        server.Start();

        NetworkClient first = new();
        NetworkClient second = new();

        try
        {
            first.Connect("127.0.0.1", port, HailSerializer.Serialize(new ClientHail { Username = "science" }));
            Assert.That(
                SpinUntil(() =>
                {
                    server.Poll();
                    first.Poll();
                    return first.IsConnected;
                }),
                Is.True,
                "first client should connect");

            second.Connect("127.0.0.1", port, HailSerializer.Serialize(new ClientHail { Username = "SCIENCE" }));
            Assert.That(
                SpinUntil(() =>
                {
                    server.Poll();
                    second.Poll();
                    return second.State == ClientConnectionState.Disconnected;
                }),
                Is.True,
                "second client should be denied");

            Assert.That(second.DisconnectReason, Is.EqualTo("The username 'SCIENCE' is already present on server!"));
        }
        finally
        {
            first.Shutdown();
            second.Shutdown();
            server.Shutdown();
        }
    }

    [Test]
    public void Server_SendsToSingleConnection()
    {
        int port = 47900 + (Environment.ProcessId % 300);
        NetworkServer server = new(port);
        List<(long ConnectionId, string Username)> connected = new();
        server.PeerConnected += (id, hail) => connected.Add((id, HailSerializer.Deserialize(hail).Username));
        server.Start();

        NetworkClient alice = new();
        NetworkClient bob = new();

        try
        {
            alice.Connect("127.0.0.1", port, HailSerializer.Serialize(new ClientHail { Username = "alice" }));
            bob.Connect("127.0.0.1", port, HailSerializer.Serialize(new ClientHail { Username = "bob" }));

            Assert.That(
                SpinUntil(() =>
                {
                    server.Poll();
                    alice.Poll();
                    bob.Poll();
                    return connected.Count == 2 && alice.IsConnected && bob.IsConnected;
                }),
                Is.True,
                "both clients should connect and hails should arrive");

            long aliceId = connected.Find(c => c.Username == "alice").ConnectionId;
            server.Send(aliceId, new byte[] { 42 });

            byte[]? aliceGot = null;
            bool bobGot = false;
            Assert.That(
                SpinUntil(() =>
                {
                    server.Poll();
                    foreach (byte[] payload in alice.Poll())
                    {
                        aliceGot = payload;
                    }

                    foreach (byte[] _ in bob.Poll())
                    {
                        bobGot = true;
                    }

                    return aliceGot is not null;
                }),
                Is.True,
                "alice should receive the direct message");

            Assert.Multiple(() =>
            {
                Assert.That(aliceGot, Is.EqualTo(new byte[] { 42 }));
                Assert.That(bobGot, Is.False);
            });
        }
        finally
        {
            alice.Shutdown();
            bob.Shutdown();
            server.Shutdown();
        }
    }

    private static bool SpinUntil(Func<bool> condition)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < 5000)
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
