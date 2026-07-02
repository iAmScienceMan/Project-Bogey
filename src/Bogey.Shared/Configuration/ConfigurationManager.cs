using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Bogey.Logging;

namespace Bogey.Shared.Configuration;

public sealed class ConfigurationManager : IConfigurationManager
{
    private readonly Dictionary<string, CVarDef> _defs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Action<object>>> _handlers = new(StringComparer.Ordinal);
    private readonly ILogbook? _log;

    public ConfigurationManager(ILogbook? log = null) => _log = log;

    public IReadOnlyCollection<CVarDef> Definitions => _defs.Values;

    public void RegisterCVars(Type holder)
    {
        foreach (FieldInfo field in holder.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is CVarDef def)
            {
                Register(def);
            }
        }
    }

    public void Register(CVarDef cVar)
    {
        if (_defs.ContainsKey(cVar.Name))
        {
            _log?.Warning($"Duplicate CVar '{cVar.Name}' ignored.");
            return;
        }

        _defs[cVar.Name] = cVar;
        _values[cVar.Name] = cVar.DefaultBoxed;
    }

    public T GetCVar<T>(CVarDef<T> cVar)
        where T : notnull
    {
        EnsureRegistered(cVar);
        return (T)_values[cVar.Name];
    }

    public void SetCVar<T>(CVarDef<T> cVar, T value)
        where T : notnull
    {
        EnsureRegistered(cVar);
        Store(cVar.Name, value);
    }

    public bool TrySetCVar(string name, string value, out string? error)
    {
        if (!_defs.TryGetValue(name, out CVarDef? def))
        {
            error = $"Unknown CVar '{name}'.";
            return false;
        }

        if (!TryParse(value, def.Type, out object? parsed, out error))
        {
            return false;
        }

        Store(name, parsed!);
        error = null;
        return true;
    }

    public string? GetCVarString(string name)
        => _values.TryGetValue(name, out object? value) ? Format(value) : null;

    public bool IsRegistered(string name) => _defs.ContainsKey(name);

    public void OnValueChanged<T>(CVarDef<T> cVar, Action<T> handler, bool invokeImmediately = false)
        where T : notnull
    {
        EnsureRegistered(cVar);
        if (!_handlers.TryGetValue(cVar.Name, out List<Action<object>>? list))
        {
            list = new List<Action<object>>();
            _handlers[cVar.Name] = list;
        }

        list.Add(boxed => handler((T)boxed));

        if (invokeImmediately)
        {
            handler(GetCVar(cVar));
        }
    }

    public void ResetToDefault(CVarDef cVar)
    {
        EnsureRegistered(cVar);
        Store(cVar.Name, cVar.DefaultBoxed);
    }

    private void Store(string name, object value)
    {
        _values[name] = value;
        if (!_handlers.TryGetValue(name, out List<Action<object>>? list))
        {
            return;
        }

        foreach (Action<object> handler in list)
        {
            handler(value);
        }
    }

    private void EnsureRegistered(CVarDef cVar)
    {
        if (!_defs.ContainsKey(cVar.Name))
        {
            Register(cVar);
        }
    }

    private static bool TryParse(string raw, Type type, out object? value, out string? error)
    {
        error = null;
        value = null;

        try
        {
            if (type == typeof(string))
            {
                value = raw;
            }
            else if (type == typeof(bool))
            {
                value = ParseBool(raw);
            }
            else if (type == typeof(int))
            {
                value = int.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(long))
            {
                value = long.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(float))
            {
                value = float.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            else if (type == typeof(double))
            {
                value = double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            else if (type.IsEnum)
            {
                value = Enum.Parse(type, raw, ignoreCase: true);
            }
            else
            {
                error = $"CVar type {type.Name} is not supported.";
                return false;
            }
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        {
            error = $"'{raw}' is not a valid {type.Name}.";
            return false;
        }

        return true;
    }

    private static bool ParseBool(string raw) => raw.Trim().ToLowerInvariant() switch
    {
        "true" or "1" or "on" or "yes" => true,
        "false" or "0" or "off" or "no" => false,
        _ => throw new FormatException($"'{raw}' is not a boolean."),
    };

    private static string Format(object value)
        => value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString() ?? string.Empty;
}
