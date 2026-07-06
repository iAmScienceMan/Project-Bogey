using Lattice.Shared.Configuration;

namespace Content.Shared.Configuration;

public static class CCVars
{
    public static readonly CVarDef<string> PlayerCallsign =
        CVarDef.Create("player.callsign", "MAVERICK", CVarFlags.Archive, "Callsign shown for your task force.");

    public static readonly CVarDef<int> GameSeed =
        CVarDef.Create("game.seed", 1337, CVarFlags.None, "Deterministic scenario seed applied on deploy.");

    public static readonly CVarDef<string> GameScenario =
        CVarDef.Create("game.scenario", "default", CVarFlags.Archive, "Scenario id spawned on deploy.");

    public static readonly CVarDef<int> GameDefaultSpeed =
        CVarDef.Create("game.default_speed", 1, CVarFlags.Archive, "Sim speed on deploy in ticks per second (0-100).");

    public static readonly CVarDef<bool> GameStartPaused =
        CVarDef.Create("game.start_paused", false, CVarFlags.Archive, "Deploy with the simulation paused.");

    public static readonly CVarDef<bool> DebugOverlay =
        CVarDef.Create("debug.overlay", false, CVarFlags.Cheat, "Draw the ground-truth debug overlay.");

    public static readonly CVarDef<float> SimInitialConfidence =
        CVarDef.Create("sim.initial_confidence", 0.12f, CVarFlags.Archive, "Track confidence on first detection.");

    public static readonly CVarDef<float> SimConfidenceGain =
        CVarDef.Create("sim.confidence_gain", 0.14f, CVarFlags.Archive, "Confidence gained per sensor hit.");

    public static readonly CVarDef<float> SimClassifyThreshold =
        CVarDef.Create("sim.classify_threshold", 0.40f, CVarFlags.Archive, "Confidence needed to classify a contact.");

    public static readonly CVarDef<float> SimIdentifyThreshold =
        CVarDef.Create("sim.identify_threshold", 0.75f, CVarFlags.Archive, "Confidence needed to identify a contact.");

    public static readonly CVarDef<float> SimPositionalErrorBase =
        CVarDef.Create("sim.pos_error_base", 3.0f, CVarFlags.Archive, "Base positional error in km.");

    public static readonly CVarDef<float> SimObservationNoise =
        CVarDef.Create("sim.obs_noise", 4.0f, CVarFlags.Archive, "Per-observation position noise in km.");

    public static readonly CVarDef<float> SimDecayFactor =
        CVarDef.Create("sim.decay_factor", 0.90f, CVarFlags.Archive, "Confidence retained per decay step.");

    public static readonly CVarDef<float> SimPositionalErrorGrowth =
        CVarDef.Create("sim.pos_error_growth", 2.5f, CVarFlags.Archive, "Positional error growth in km per idle tick.");

    public static readonly CVarDef<int> SimStaleTicks =
        CVarDef.Create("sim.stale_ticks", 8, CVarFlags.Archive, "Idle ticks before a track is marked stale.");

    public static readonly CVarDef<int> SimDropTicks =
        CVarDef.Create("sim.drop_ticks", 20, CVarFlags.Archive, "Idle ticks before a track is dropped.");
}
