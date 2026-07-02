using System;

namespace Bogey.Shared.Configuration;

public abstract class CVarDef
{
    private protected CVarDef(string name, CVarFlags flags, string description, Type type)
    {
        Name = name;
        Flags = flags;
        Description = description;
        Type = type;
    }

    public string Name { get; }

    public CVarFlags Flags { get; }

    public string Description { get; }

    public Type Type { get; }

    public abstract object DefaultBoxed { get; }

    public static CVarDef<T> Create<T>(string name, T defaultValue, CVarFlags flags = CVarFlags.None, string description = "")
        where T : notnull
        => new(name, defaultValue, flags, description);
}

public sealed class CVarDef<T> : CVarDef
    where T : notnull
{
    internal CVarDef(string name, T defaultValue, CVarFlags flags, string description)
        : base(name, flags, description, typeof(T))
    {
        DefaultValue = defaultValue;
    }

    public T DefaultValue { get; }

    public override object DefaultBoxed => DefaultValue;
}
