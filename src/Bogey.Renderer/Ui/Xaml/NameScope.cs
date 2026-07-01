using System;
using System.Collections.Generic;
using Bogey.Renderer.Ui.Controls;

namespace Bogey.Renderer.Ui.Xaml;

public sealed class NameScope
{
    private readonly Dictionary<string, Control> _byName = new(StringComparer.Ordinal);

    public void Register(string name, Control control) => _byName[name] = control;

    public Control Lookup(string name)
    {
        if (_byName.TryGetValue(name, out Control? control))
        {
            return control;
        }

        throw new InvalidOperationException($"No control named '{name}' was found in the loaded XAML.");
    }

    public bool TryLookup(string name, out Control control)
    {
        if (_byName.TryGetValue(name, out Control? found))
        {
            control = found;
            return true;
        }

        control = null!;
        return false;
    }
}
