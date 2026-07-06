using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Lattice.Sim.Engine;

public sealed class SystemManager
{
    private readonly List<SystemBase> _systems = new();
    private readonly List<object> _injectables = new();
    private bool _built;

    public SystemManager AddService(object service)
    {
        _injectables.Add(service);
        return this;
    }

    
    public SystemManager AddSystem(SystemBase system)
    {
        _systems.Add(system);
        _injectables.Add(system);
        return this;
    }

    public void Build()
    {
        if (_built)
        {
            throw new InvalidOperationException("SystemManager has already been built.");
        }

        foreach (SystemBase system in _systems)
        {
            InjectDependencies(system);
        }

        foreach (SystemBase system in _systems)
        {
            system.Initialize();
        }

        _built = true;
    }

    public void Update()
    {
        if (!_built)
        {
            throw new InvalidOperationException("Call Build() before Update().");
        }

        foreach (SystemBase system in _systems)
        {
            system.Update();
        }
    }

    private void InjectDependencies(SystemBase system)
    {
        for (Type? type = system.GetType(); type is not null && type != typeof(object); type = type.BaseType)
        {
            FieldInfo[] fields = type.GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);

            foreach (FieldInfo field in fields)
            {
                if (field.GetCustomAttribute<DependencyAttribute>() is null)
                {
                    continue;
                }

                object resolved = Resolve(field.FieldType, system);
                field.SetValue(system, resolved);
            }
        }
    }

    private object Resolve(Type fieldType, SystemBase requestingSystem)
    {
        List<object> matches = _injectables
            .Where(candidate => fieldType.IsInstanceOfType(candidate))
            .ToList();

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException(
                $"{requestingSystem.GetType().Name} depends on {fieldType.Name}, but nothing of that type is registered."),
            _ => throw new InvalidOperationException(
                $"{requestingSystem.GetType().Name} depends on {fieldType.Name}, but {matches.Count} candidates are registered (ambiguous)."),
        };
    }
}
