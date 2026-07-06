using System;
using System.Collections.Generic;
using System.Linq;

namespace Lattice.Sim.Engine;

public sealed class EntityManager
{
    private readonly List<int> _entities = new();
    private readonly Dictionary<Type, object> _stores = new();
    private int _nextId;

    public IReadOnlyList<int> Entities => _entities;

    public int CreateEntity()
    {
        int id = ++_nextId;
        _entities.Add(id);
        return id;
    }

    public void DestroyEntity(int entityId)
    {
        _entities.Remove(entityId);
        foreach (object store in _stores.Values)
        {
            ((System.Collections.IDictionary)store).Remove(entityId);
        }
    }

    public void AddComponent<T>(int entityId, T component)
        where T : class
    {
        Store<T>()[entityId] = component;
    }

    public T GetComponent<T>(int entityId)
        where T : class
    {
        if (!Store<T>().TryGetValue(entityId, out T? component))
        {
            throw new InvalidOperationException(
                $"Entity {entityId} has no component of type {typeof(T).Name}.");
        }

        return component;
    }

    public bool TryGetComponent<T>(int entityId, out T component)
        where T : class
    {
        if (Store<T>().TryGetValue(entityId, out T? found))
        {
            component = found;
            return true;
        }

        component = null!;
        return false;
    }

    public bool HasComponent<T>(int entityId)
        where T : class
        => Store<T>().ContainsKey(entityId);

    public IEnumerable<int> Query<T>()
        where T : class
        => Store<T>().Keys.OrderBy(static id => id);

    public IEnumerable<int> Query<T1, T2>()
        where T1 : class
        where T2 : class
    {
        Dictionary<int, T2> second = Store<T2>();
        return Store<T1>().Keys
            .Where(second.ContainsKey)
            .OrderBy(static id => id);
    }

    private Dictionary<int, T> Store<T>()
        where T : class
    {
        if (!_stores.TryGetValue(typeof(T), out object? store))
        {
            store = new Dictionary<int, T>();
            _stores[typeof(T)] = store;
        }

        return (Dictionary<int, T>)store;
    }
}
