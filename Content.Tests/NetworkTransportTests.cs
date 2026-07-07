using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using Content.Shared.Components;
using Lattice.Network;
using Lattice.Sim.Engine;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class NetworkTransportTests
{
    [Test]
    public void GameState_TravelsOverLidgrenSocket()
    {
        int port = 47100 + (Environment.ProcessId % 400);
        ComponentFactory factory = new(new[] { typeof(Sensor).Assembly });
        GameStateManager manager = new(factory);

        NetworkServer server = new(port);
        server.Start();
        NetworkClient client = new();
        client.Connect("127.0.0.1", port);

        try
        {
            Assert.That(
                SpinUntil(() =>
                {
                    server.Poll();
                    client.Poll();
                    return client.IsConnected;
                }),
                Is.True,
                "client should connect to the server");

            EntityManager sim = new() { CurrentTick = 1 };
            int entity = sim.CreateEntity();
            sim.AddComponent(entity, new MetaData { EntityName = "NetShip" });
            sim.AddComponent(entity, new Transform { Position = new Vector2(12f, 34f) });

            byte[] payload = GameStateSerializer.Serialize(manager.BuildState(sim, fromTick: 0, static _ => true));
            server.Broadcast(payload);

            byte[]? received = null;
            Assert.That(
                SpinUntil(() =>
                {
                    server.Poll();
                    foreach (byte[] message in client.Poll())
                    {
                        received = message;
                        return true;
                    }

                    return false;
                }),
                Is.True,
                "client should receive the broadcast game state");

            GameState wire = GameStateSerializer.Deserialize(received!, factory);
            ClientState clientState = new(new EntityManager(), factory);
            clientState.Apply(wire);

            Assert.That(clientState.TryResolve(new NetEntity(entity), out int local), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(clientState.Entities.GetComponent<MetaData>(local).Name, Is.EqualTo("NetShip"));
                Assert.That(
                    clientState.Entities.GetComponent<Transform>(local).Position,
                    Is.EqualTo(new Vector2(12f, 34f)));
            });
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
