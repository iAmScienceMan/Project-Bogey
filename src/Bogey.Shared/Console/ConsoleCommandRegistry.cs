using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bogey.Logging;

namespace Bogey.Shared.Console;

public sealed class ConsoleCommandRegistry
{
    private const string AssemblyPrefix = "Bogey.";

    private readonly Dictionary<string, IConsoleCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogbook _log;
    private readonly IReadOnlyList<object> _services;

    public ConsoleCommandRegistry(ILogbook log, IReadOnlyList<object>? services = null)
    {
        _log = log;
        _services = services ?? Array.Empty<object>();
        Discover();
    }

    public IReadOnlyDictionary<string, IConsoleCommand> Commands => _commands;

    public void Execute(string input, IConsoleShell shell)
    {
        string trimmed = input.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        int space = trimmed.IndexOf(' ');
        string name = space < 0 ? trimmed : trimmed[..space];
        string argStr = space < 0 ? string.Empty : trimmed[(space + 1)..].Trim();
        string[] args = argStr.Length == 0
            ? Array.Empty<string>()
            : argStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (!_commands.TryGetValue(name, out IConsoleCommand? command))
        {
            shell.WriteError($"Unknown command: {name}");
            return;
        }

        try
        {
            command.Execute(shell, argStr, args);
        }
        catch (Exception ex)
        {
            shell.WriteError($"Command '{name}' failed: {ex.Message}");
            _log.Error(ex, $"Console command '{name}' threw.");
        }
    }

    private void Discover()
    {
        foreach (IConsoleCommand command in InstantiateCommands())
        {
            if (_commands.ContainsKey(command.Command))
            {
                _log.Warning($"Duplicate console command '{command.Command}' ignored ({command.GetType().FullName}).");
                continue;
            }

            _commands[command.Command] = command;
        }

        _log.Debug($"Registered {_commands.Count} console command(s).");
    }

    private IEnumerable<IConsoleCommand> InstantiateCommands()
    {
        foreach (Type type in CommandTypes())
        {
            if (type.GetConstructor(Type.EmptyTypes) is null)
            {
                _log.Warning(
                    $"Console command {type.FullName} has no parameterless constructor and was skipped. " +
                    "Declare dependencies as [Dependency] fields instead.");
                continue;
            }

            IConsoleCommand? command;
            try
            {
                command = (IConsoleCommand?)Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to instantiate console command {type.FullName}: {ex.Message}");
                continue;
            }

            if (command is not null && Inject(command))
            {
                yield return command;
            }
        }
    }

    private bool Inject(IConsoleCommand command)
    {
        for (Type? type = command.GetType(); type is not null && type != typeof(object); type = type.BaseType)
        {
            FieldInfo[] fields = type.GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);

            foreach (FieldInfo field in fields)
            {
                if (field.GetCustomAttribute<DependencyAttribute>() is null)
                {
                    continue;
                }

                object? service = _services.FirstOrDefault(candidate => field.FieldType.IsInstanceOfType(candidate));
                if (service is null)
                {
                    _log.Error(
                        $"Console command {command.GetType().Name} depends on {field.FieldType.Name}, " +
                        "but no such service is registered; command skipped.");
                    return false;
                }

                field.SetValue(command, service);
            }
        }

        return true;
    }

    private IEnumerable<Type> CommandTypes()
    {
        foreach (Assembly assembly in BogeyAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).ToArray()!;
            }

            foreach (Type type in types)
            {
                if (type is { IsAbstract: false, IsInterface: false }
                    && typeof(IConsoleCommand).IsAssignableFrom(type))
                {
                    yield return type;
                }
            }
        }
    }

    private static IEnumerable<Assembly> BogeyAssemblies()
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        Queue<Assembly> pending = new();

        foreach (Assembly loaded in AppDomain.CurrentDomain.GetAssemblies())
        {
            pending.Enqueue(loaded);
        }

        while (pending.Count > 0)
        {
            Assembly assembly = pending.Dequeue();
            string name = assembly.GetName().Name ?? string.Empty;
            if (!name.StartsWith(AssemblyPrefix, StringComparison.Ordinal) || !seen.Add(name))
            {
                continue;
            }

            yield return assembly;

            foreach (AssemblyName referenced in assembly.GetReferencedAssemblies())
            {
                if (referenced.Name is { } refName
                    && refName.StartsWith(AssemblyPrefix, StringComparison.Ordinal)
                    && !seen.Contains(refName))
                {
                    Assembly? resolved = TryLoad(referenced);
                    if (resolved is not null)
                    {
                        pending.Enqueue(resolved);
                    }
                }
            }
        }
    }

    private static Assembly? TryLoad(AssemblyName name)
    {
        try
        {
            return Assembly.Load(name);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
