using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Bogey.Renderer.Ui.Controls;

namespace Bogey.Renderer.Ui.Xaml;

public static class BogeyXaml
{
    
    public static void Load(Control self)
    {
        XElement root = XDocument.Parse(ReadEmbeddedMarkup(self.GetType())).Root
            ?? throw new InvalidOperationException("UI markup has no root element.");

        ApplyAttributes(self, root);
        foreach (XElement childElement in root.Elements())
        {
            self.AddChild(BuildElement(childElement));
        }

        ResolveNames(self);
    }

    public static Control Parse(string markup, out NameScope scope)
    {
        XElement root = XDocument.Parse(markup).Root
            ?? throw new InvalidOperationException("UI markup has no root element.");

        Control control = BuildElement(root);
        scope = ResolveNames(control);
        return control;
    }

    private static Control BuildElement(XElement element)
    {
        Control control = ControlFactory.Create(element.Name.LocalName);
        ApplyAttributes(control, element);
        foreach (XElement childElement in element.Elements())
        {
            control.AddChild(BuildElement(childElement));
        }

        return control;
    }

    private static NameScope ResolveNames(Control root)
    {
        NameScope scope = new();
        foreach (Control control in root.SelfAndDescendants())
        {
            if (control.Name is { } name)
            {
                scope.Register(name, control);
            }
        }

        if (root is IXamlNameResolver resolver)
        {
            resolver.Resolve(scope);
        }

        return scope;
    }

    private static void ApplyAttributes(Control control, XElement element)
    {
        foreach (XAttribute attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration)
            {
                continue;
            }

            SetProperty(control, attribute.Name.LocalName, attribute.Value);
        }
    }

    private static void SetProperty(Control control, string name, string value)
    {
        PropertyInfo? property = control.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (property is null || !property.CanWrite)
        {
            throw new InvalidOperationException(
                $"<{control.GetType().Name}> has no settable property '{name}'.");
        }

        property.SetValue(control, Convert(property.PropertyType, value, control.GetType().Name, name));
    }

    private static object Convert(Type target, string value, string element, string attribute)
    {
        Type underlying = Nullable.GetUnderlyingType(target) ?? target;

        if (underlying == typeof(string))
        {
            return value;
        }

        if (underlying == typeof(bool))
        {
            return bool.Parse(value);
        }

        if (underlying == typeof(int))
        {
            return int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        if (underlying == typeof(float))
        {
            return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        if (underlying == typeof(Thickness))
        {
            return Thickness.Parse(value);
        }

        if (underlying.IsEnum)
        {
            return Enum.Parse(underlying, value, ignoreCase: true);
        }

        throw new InvalidOperationException(
            $"Attribute '{attribute}' on <{element}> has unsupported type {underlying.Name}.");
    }

    private static string ReadEmbeddedMarkup(Type type)
    {
        Assembly assembly = type.Assembly;
        string expected = type.FullName + ".xaml";
        Stream? stream = assembly.GetManifestResourceStream(expected);

        if (stream is null)
        {
            
            string suffix = "." + type.Name + ".xaml";
            string? match = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.Ordinal));
            if (match is not null)
            {
                stream = assembly.GetManifestResourceStream(match);
            }
        }

        if (stream is null)
        {
            throw new InvalidOperationException(
                $"No embedded XAML resource found for '{type.FullName}' (expected '{expected}').");
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
