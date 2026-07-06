using System;
using System.IO;

namespace Lattice.Shared.Configuration;

public static class CVars
{
    public static readonly CVarDef<float> UiScale =
        CVarDef.Create("ui.scale", 1f, CVarFlags.Archive, "Interface scale multiplier.");

    public static readonly CVarDef<float> RenderZoom =
        CVarDef.Create("render.zoom", 4f, CVarFlags.Archive, "Initial tactical zoom in pixels per km.");

    public static readonly CVarDef<int> RenderWidth =
        CVarDef.Create("render.width", 1280, CVarFlags.Archive, "Window width in pixels.");

    public static readonly CVarDef<int> RenderHeight =
        CVarDef.Create("render.height", 800, CVarFlags.Archive, "Window height in pixels.");

    public static readonly CVarDef<bool> RenderVsync =
        CVarDef.Create("render.vsync", true, CVarFlags.Archive, "Synchronize frames to the display refresh.");

    public static readonly CVarDef<string> RenderFontPath =
        CVarDef.Create("render.font_path", DefaultFontPath(), CVarFlags.Archive | CVarFlags.RequiresRestart,
            "TrueType font used for all text.");

    public static readonly CVarDef<int> ChangelogLastReadId =
        CVarDef.Create("changelog.last_read_id", 0, CVarFlags.Archive, "Highest changelog entry id the player has seen.");

    private static string DefaultFontPath()
        => Path.Combine(AppContext.BaseDirectory, "Resources", "Fonts", "IosevkaTerm.ttf");
}
