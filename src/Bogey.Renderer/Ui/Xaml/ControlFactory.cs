using System;
using System.Collections.Generic;
using Bogey.Renderer.Ui.Controls;

namespace Bogey.Renderer.Ui.Xaml;

internal static class ControlFactory
{
    private static readonly Dictionary<string, Func<Control>> Factories = new(StringComparer.Ordinal)
    {
        ["Control"] = () => new Control(),
        ["BoxContainer"] = () => new BoxContainer(),
        ["PanelContainer"] = () => new PanelContainer(),
        ["Button"] = () => new Button(),
        ["Label"] = () => new Label(),
    };

    public static Control Create(string elementName)
    {
        if (Factories.TryGetValue(elementName, out Func<Control>? make))
        {
            return make();
        }

        throw new InvalidOperationException($"Unknown UI element <{elementName}>.");
    }
}
