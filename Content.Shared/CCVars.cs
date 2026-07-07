using Lattice.Shared.Configuration;

namespace Content.Shared.Configuration;

public static class CCVars
{
    public static readonly CVarDef<string> PlayerUsername =
        CVarDef.Create("player.username", "Bogeyman", CVarFlags.Archive, "Username sent to servers when connecting.");

    public static readonly CVarDef<string> PlayerColor =
        CVarDef.Create("player.color", "#4DC3FF", CVarFlags.Archive, "Unit color other players see you as, hex #RRGGBB.");

    public static readonly CVarDef<string> ClientAddress =
        CVarDef.Create("client.address", "localhost", CVarFlags.Archive, "Last server address used to connect.");

    public static readonly CVarDef<bool> NetGraph =
        CVarDef.Create("net.graph", false, CVarFlags.None, "Draw the network statistics graph overlay.");

    public static readonly CVarDef<bool> DebugOverlay =
        CVarDef.Create("debug.overlay", false, CVarFlags.Cheat, "Draw the ground-truth debug overlay (admin only).");

    public static readonly CVarDef<string> GameNameVisibility =
        CVarDef.Create(
            "game.name_visibility",
            "detected",
            CVarFlags.Archive,
            "Server rule for when other players' unit names appear: always, detected, or identified.");

    public static readonly CVarDef<float> GameLobbyDuration =
        CVarDef.Create("game.lobby_duration", 30f, CVarFlags.Archive, "Seconds the pre-round lobby countdown runs.");

    public static readonly CVarDef<float> SimInitialConfidence =
        CVarDef.Create("sim.initial_confidence", 0.12f, CVarFlags.Archive, "Track confidence on first detection.");

    public static readonly CVarDef<float> SimConfidenceGain =
        CVarDef.Create("sim.confidence_gain", 0.14f, CVarFlags.Archive, "Confidence gained per sensor hit.");

    public static readonly CVarDef<float> SimClassifyThreshold =
        CVarDef.Create("sim.classify_threshold", 0.40f, CVarFlags.Archive, "Confidence needed to classify a contact.");

    public static readonly CVarDef<float> SimIdentifyThreshold =
        CVarDef.Create("sim.identify_threshold", 0.75f, CVarFlags.Archive, "Confidence needed to identify a contact.");

    public static readonly CVarDef<float> SimMunitionClassifyThreshold =
        CVarDef.Create("sim.munition_classify_threshold", 0.05f, CVarFlags.Archive, "Confidence needed to classify a munition track.");

    public static readonly CVarDef<float> SimMunitionIdentifyThreshold =
        CVarDef.Create("sim.munition_identify_threshold", 0.30f, CVarFlags.Archive, "Confidence needed to identify a munition track.");

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

    public static readonly CVarDef<int> SimMaxAutoCommit =
        CVarDef.Create("sim.max_auto_commit", 2, CVarFlags.Archive, "Maximum munitions auto-fire commits per target.");
}
