using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Bogey.Logging;
using Bogey.Renderer.App;
using Bogey.Shared.Changelog;
using Bogey.Shared.Configuration;
using Bogey.Shared.Prototypes;
using Bogey.Sim;
using Bogey.Sim.Content;
using Bogey.Sim.Systems;

namespace Bogey.Host;

public sealed class Program
{
    public static int Main(string[] args)
    {
        ILogManager logManager = Logger.InitializeDefault();
        ILogbook log = logManager.GetLogbook("host");

        try
        {
            return Run(args, logManager, log);
        }
        catch (Exception ex)
        {
            log.Fatal($"Unhandled exception - the game crashed.\n{ex}");
            return 1;
        }
    }

    private static int Run(string[] args, ILogManager logManager, ILogbook log)
    {
        Options options;
        try
        {
            options = Options.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(Options.Usage);
            return 1;
        }

        if (options.Debug)
        {
            logManager.RootLogbook.Level = LogLevel.Verbose;
        }

        IConfigurationManager cfg = new ConfigurationManager(logManager.GetLogbook("config"));
        cfg.RegisterCVars(typeof(CVars));
        options.ApplyTo(cfg);

        IReadOnlyList<PrototypeDefinition> prototypes;
        try
        {
            prototypes = new PrototypeLoader().LoadDirectory(options.PrototypesPath);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            log.Error($"Failed to load prototypes from '{options.PrototypesPath}': {ex.Message}");
            return 1;
        }

        return RunRealtime(cfg, prototypes, logManager);
    }

    private static int RunRealtime(IConfigurationManager cfg, IReadOnlyList<PrototypeDefinition> prototypes, ILogManager logManager)
    {
        bool debug = cfg.GetCVar(CVars.DebugOverlay);
        RendererOptions rendererOptions = new()
        {
            Title = $"PROJECT BOGEY - tactical (seed {cfg.GetCVar(CVars.GameSeed)})" + (debug ? " [DEBUG]" : string.Empty),
        };

        SimBootFactory factory = configuration =>
        {
            SimConfig simConfig = BuildSimConfig(configuration);
            SimRuntime runtime = new(prototypes, configuration.GetCVar(CVars.GameSeed), simConfig, logManager);
            SimSession session = new(
                runtime,
                configuration.GetCVar(CVars.SimNormalTps),
                configuration.GetCVar(CVars.SimFastTps));
            IDebugOverlay? overlay = configuration.GetCVar(CVars.DebugOverlay)
                ? new GroundTruthOverlay(runtime)
                : null;
            return new SimBoot(session, overlay);
        };

        ChangelogManager changelog = new(cfg, logManager.GetLogbook("changelog"));
        changelog.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "Resources", "Changelog"));

        Console.WriteLine("PROJECT BOGEY - live tactical view");
        Console.WriteLine("  main menu: set a callsign and seed, then DEPLOY. OPTIONS for settings, CHANGELOG for news.");
        Console.WriteLine("  tactical view: click a friendly unit to select, click the map to order a move.");
        Console.WriteLine("  SPACE pause/resume   1 normal   2 fast   drag pan   scroll zoom   ESC quit   ` console");

        using TacticalWindow window = new(rendererOptions, cfg, changelog, factory);
        window.Run();
        return 0;
    }

    private static SimConfig BuildSimConfig(IConfigurationManager cfg) => new()
    {
        InitialConfidence = cfg.GetCVar(CVars.SimInitialConfidence),
        ConfidenceGainPerHit = cfg.GetCVar(CVars.SimConfidenceGain),
        ClassifyThreshold = cfg.GetCVar(CVars.SimClassifyThreshold),
        IdentifyThreshold = cfg.GetCVar(CVars.SimIdentifyThreshold),
        BasePositionalErrorKm = cfg.GetCVar(CVars.SimPositionalErrorBase),
        ObservationNoiseKm = cfg.GetCVar(CVars.SimObservationNoise),
        DecayConfidenceFactor = cfg.GetCVar(CVars.SimDecayFactor),
        PositionalErrorGrowthKmPerTick = cfg.GetCVar(CVars.SimPositionalErrorGrowth),
        StaleAfterIdleTicks = cfg.GetCVar(CVars.SimStaleTicks),
        DropAfterIdleTicks = cfg.GetCVar(CVars.SimDropTicks),
    };

    private sealed class Options
    {
        public const string Usage =
            "usage: Bogey.Host [--seed N] [--debug] [--ui-scale F] [--prototypes PATH]";

        public int Seed { get; private set; } = 1337;
        public bool Debug { get; private set; }
        public float UiScale { get; private set; } = 1f;
        public string PrototypesPath { get; private set; } =
            Path.Combine(AppContext.BaseDirectory, "Resources", "Prototypes");

        public void ApplyTo(IConfigurationManager cfg)
        {
            cfg.SetCVar(CVars.GameSeed, Seed);
            cfg.SetCVar(CVars.UiScale, UiScale);
            cfg.SetCVar(CVars.DebugOverlay, Debug);
        }

        public static Options Parse(string[] args)
        {
            Options options = new();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--seed":
                        options.Seed = NextInt(args, ref i, "--seed");
                        break;
                    case "--debug":
                        options.Debug = true;
                        break;
                    case "--ui-scale":
                        options.UiScale = NextFloat(args, ref i, "--ui-scale");
                        break;
                    case "--prototypes":
                        options.PrototypesPath = NextValue(args, ref i, "--prototypes");
                        break;
                    default:
                        throw new ArgumentException($"Unknown argument: {args[i]}");
                }
            }

            return options;
        }

        private static string NextValue(string[] args, ref int i, string flag)
        {
            if (i + 1 >= args.Length)
            {
                throw new ArgumentException($"Missing value for {flag}.");
            }

            return args[++i];
        }

        private static int NextInt(string[] args, ref int i, string flag)
        {
            string raw = NextValue(args, ref i, flag);
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                throw new ArgumentException($"Value for {flag} must be an integer, got '{raw}'.");
            }

            return value;
        }

        private static float NextFloat(string[] args, ref int i, string flag)
        {
            string raw = NextValue(args, ref i, flag);
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || value <= 0f)
            {
                throw new ArgumentException($"Value for {flag} must be a positive number, got '{raw}'.");
            }

            return value;
        }
    }
}
