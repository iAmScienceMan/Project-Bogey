using System;
using System.IO;

namespace Bogey.Shared.Configuration;

public static class CVars
{
    public static readonly CVarDef<string> PlayerCallsign =
        CVarDef.Create("player.callsign", "MAVERICK", CVarFlags.Archive, "Callsign shown for your task force.");

    public static readonly CVarDef<int> GameSeed =
        CVarDef.Create("game.seed", 1337, CVarFlags.None, "Deterministic scenario seed applied on deploy.");

    public static readonly CVarDef<int> GameDefaultSpeed =
        CVarDef.Create("game.default_speed", 1, CVarFlags.Archive, "Sim speed on deploy: 0 paused, 1 normal, 2 fast.");

    public static readonly CVarDef<bool> GameStartPaused =
        CVarDef.Create("game.start_paused", false, CVarFlags.Archive, "Deploy with the simulation paused.");

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

    public static readonly CVarDef<bool> DebugOverlay =
        CVarDef.Create("debug.overlay", false, CVarFlags.Cheat, "Draw the ground-truth debug overlay.");

    public static readonly CVarDef<int> ChangelogLastReadId =
        CVarDef.Create("changelog.last_read_id", 0, CVarFlags.Archive, "Highest changelog entry id the player has seen.");

    public static readonly CVarDef<float> SimNormalTps =
        CVarDef.Create("sim.normal_tps", 1f, CVarFlags.Archive, "Ticks per second at normal speed.");

    public static readonly CVarDef<float> SimFastTps =
        CVarDef.Create("sim.fast_tps", 10f, CVarFlags.Archive, "Ticks per second at fast speed.");

    public static readonly CVarDef<float> SimInitialConfidence =
        CVarDef.Create("sim.initial_confidence", 0.12f, CVarFlags.None, "Track confidence on first detection.");

    public static readonly CVarDef<float> SimConfidenceGain =
        CVarDef.Create("sim.confidence_gain", 0.14f, CVarFlags.None, "Confidence gained per sensor hit.");

    public static readonly CVarDef<float> SimClassifyThreshold =
        CVarDef.Create("sim.classify_threshold", 0.40f, CVarFlags.None, "Confidence needed to classify a contact.");

    public static readonly CVarDef<float> SimIdentifyThreshold =
        CVarDef.Create("sim.identify_threshold", 0.75f, CVarFlags.None, "Confidence needed to identify a contact.");

    public static readonly CVarDef<float> SimPositionalErrorBase =
        CVarDef.Create("sim.pos_error_base", 3.0f, CVarFlags.None, "Base positional error in km.");

    public static readonly CVarDef<float> SimObservationNoise =
        CVarDef.Create("sim.obs_noise", 4.0f, CVarFlags.None, "Per-observation position noise in km.");

    public static readonly CVarDef<float> SimDecayFactor =
        CVarDef.Create("sim.decay_factor", 0.90f, CVarFlags.None, "Confidence retained per decay step.");

    public static readonly CVarDef<float> SimPositionalErrorGrowth =
        CVarDef.Create("sim.pos_error_growth", 2.5f, CVarFlags.None, "Positional error growth in km per idle tick.");

    public static readonly CVarDef<int> SimStaleTicks =
        CVarDef.Create("sim.stale_ticks", 8, CVarFlags.None, "Idle ticks before a track is marked stale.");

    public static readonly CVarDef<int> SimDropTicks =
        CVarDef.Create("sim.drop_ticks", 20, CVarFlags.None, "Idle ticks before a track is dropped.");

    private static string DefaultFontPath()
        => Path.Combine(AppContext.BaseDirectory, "Resources", "Fonts", "IosevkaTerm.ttf");
}
