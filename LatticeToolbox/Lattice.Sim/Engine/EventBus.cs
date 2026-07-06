using System;
using System.Collections.Generic;

namespace Lattice.Sim.Engine;

public sealed class EventBus
{
    private readonly Dictionary<Type, List<Delegate>> _broadcast = new();
    private readonly Dictionary<Type, List<Delegate>> _directed = new();

    public void Subscribe<TEvent>(Action<TEvent> handler)
        => Handlers(_broadcast, typeof(TEvent)).Add(handler);

    public void SubscribeDirected<TEvent>(Action<int, TEvent> handler)
        => Handlers(_directed, typeof(TEvent)).Add(handler);

    public void Publish<TEvent>(TEvent payload)
    {
        if (!_broadcast.TryGetValue(typeof(TEvent), out List<Delegate>? handlers))
        {
            return;
        }

        
        foreach (Delegate handler in handlers.ToArray())
        {
            ((Action<TEvent>)handler)(payload);
        }
    }

    public void PublishDirected<TEvent>(int entityId, TEvent payload)
    {
        if (!_directed.TryGetValue(typeof(TEvent), out List<Delegate>? handlers))
        {
            return;
        }

        foreach (Delegate handler in handlers.ToArray())
        {
            ((Action<int, TEvent>)handler)(entityId, payload);
        }
    }

    private static List<Delegate> Handlers(Dictionary<Type, List<Delegate>> map, Type eventType)
    {
        if (!map.TryGetValue(eventType, out List<Delegate>? list))
        {
            list = new List<Delegate>();
            map[eventType] = list;
        }

        return list;
    }
}
