using System;

namespace Content.Shared.Tracks;

public enum NameVisibility
{
    Always,
    Detected,
    Identified,
}

public static class NameVisibilityParser
{
    public static NameVisibility Parse(string value) => value.Trim().ToLowerInvariant() switch
    {
        "always" => NameVisibility.Always,
        "identified" => NameVisibility.Identified,
        _ => NameVisibility.Detected,
    };
}
