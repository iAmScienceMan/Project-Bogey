using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Lattice.Logging;

namespace Lattice.Shared.Configuration;

public sealed class ConfigurationManager : IConfigurationManager
{
    private readonly Dictionary<string, CVarDef> _defs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Action<object>>> _handlers = new(StringComparer.Ordinal);
    private readonly ILogbook? _log;
    private string? _persistPath;

    public ConfigurationManager(ILogbook? log = null) => _log = log;

    public IReadOnlyCollection<CVarDef> Definitions => _defs.Values;

    public void LoadArchive(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (IOException ex)
        {
            _log?.Error($"Failed to read settings '{path}': {ex.Message}");
            return;
        }

        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed[0] == '#')
            {
                continue;
            }

            int separator = trimmed.IndexOf('=');
            if (separator < 0)
            {
                continue;
            }

            string name = trimmed[..separator].Trim();
            string value = trimmed[(separator + 1)..].Trim();

            if (!_defs.TryGetValue(name, out CVarDef? def) || !def.Flags.HasFlag(CVarFlags.Archive))
            {
                continue;
            }

            if (!TrySetCVar(name, value, out string? error))
            {
                _log?.Warning($"Ignoring saved setting '{name}': {error}");
            }
        }
    }

    public void EnablePersistence(string path) => _persistPath = path;

    public void Save()
    {
        if (_persistPath is null)
        {
            return;
        }

        StringBuilder builder = new();
        builder.AppendLine("# Settings saved automatically.");
        foreach (CVarDef def in _defs.Values
                     .Where(d => d.Flags.HasFlag(CVarFlags.Archive) && !_values[d.Name].Equals(d.DefaultBoxed))
                     .OrderBy(static d => d.Name, StringComparer.Ordinal))
        {
            builder.Append(def.Name).Append(" = ").AppendLine(GetCVarString(def.Name));
        }

        try
        {
            File.WriteAllText(_persistPath, builder.ToString());
        }
        catch (IOException ex)
        {
            _log?.Error($"Failed to save settings '{_persistPath}': {ex.Message}");
        }
    }

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

        if (_handlers.TryGetValue(name, out List<Action<object>>? list))
        {
            foreach (Action<object> handler in list)
            {
                handler(value);
            }
        }

        if (_persistPath is not null
            && _defs.TryGetValue(name, out CVarDef? def)
            && def.Flags.HasFlag(CVarFlags.Archive))
        {
            Save();
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
