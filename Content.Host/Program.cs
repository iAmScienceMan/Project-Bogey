using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Lattice.Logging;
using Content.Renderer.App;
using Lattice.Shared.Changelog;
using Lattice.Shared.Configuration;
using Content.Shared.Configuration;
using Content.Shared.Prototypes;
using Content.Sim;
using Content.Sim.Content;
using Content.Sim.Systems;

namespace Content.Host;

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

        ConfigurationManager cfg = new(logManager.GetLogbook("config"));
        cfg.RegisterCVars(typeof(CVars));
        cfg.RegisterCVars(typeof(CCVars));

        string settingsPath = SettingsPath();
        cfg.LoadArchive(settingsPath);
        options.ApplyTo(cfg);
        cfg.EnablePersistence(settingsPath);

        IReadOnlyDictionary<string, PrototypeDefinition> prototypes;
        IReadOnlyDictionary<string, ScenarioDefinition> scenarios;
        try
        {
            PrototypeLoader loader = new();
            prototypes = loader.LoadPrototypes(options.PrototypesPath);
            scenarios = loader.LoadScenarios(options.ScenariosPath);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            log.Error($"Failed to load content: {ex.Message}");
            return 1;
        }

        if (scenarios.Count == 0)
        {
            log.Error("No scenarios found; nothing to deploy.");
            return 1;
        }

        if (!scenarios.ContainsKey(cfg.GetCVar(CCVars.GameScenario)))
        {
            cfg.SetCVar(CCVars.GameScenario, scenarios.ContainsKey("default") ? "default" : scenarios.Keys.First());
        }

        return RunRealtime(cfg, prototypes, scenarios, logManager);
    }

    private static int RunRealtime(
        IConfigurationManager cfg,
        IReadOnlyDictionary<string, PrototypeDefinition> prototypes,
        IReadOnlyDictionary<string, ScenarioDefinition> scenarios,
        ILogManager logManager)
    {
        bool debug = cfg.GetCVar(CCVars.DebugOverlay);
        RendererOptions rendererOptions = new()
        {
            Title = $"PROJECT BOGEY - tactical (seed {cfg.GetCVar(CCVars.GameSeed)})" + (debug ? " [DEBUG]" : string.Empty),
        };

        List<string> prototypeIds = prototypes.Keys.ToList();
        List<ScenarioInfo> scenarioCatalog = scenarios.Values
            .OrderBy(static s => s.Name, StringComparer.Ordinal)
            .Select(static s => new ScenarioInfo(s.Id, s.Name))
            .ToList();

        SimBootFactory factory = configuration =>
        {
            string scenarioId = configuration.GetCVar(CCVars.GameScenario);
            if (!scenarios.TryGetValue(scenarioId, out ScenarioDefinition? scenario))
            {
                scenario = scenarios.TryGetValue("default", out ScenarioDefinition? fallback)
                    ? fallback
                    : scenarios.Values.First();
            }

            SimConfig simConfig = BuildSimConfig(configuration);
            SimRuntime runtime = new(scenario, prototypes, configuration.GetCVar(CCVars.GameSeed), simConfig, logManager);
            SimSession session = new(runtime);
            IDebugOverlay? overlay = configuration.GetCVar(CCVars.DebugOverlay)
                ? new GroundTruthOverlay(runtime)
                : null;
            return new SimBoot(session, overlay, prototypeIds);
        };

        ChangelogManager changelog = new(cfg, logManager.GetLogbook("changelog"));
        changelog.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "Resources", "Changelog"));

        Console.WriteLine("PROJECT BOGEY - live tactical view");
        Console.WriteLine("  main menu: set a callsign and seed, then DEPLOY. OPTIONS for settings, CHANGELOG for news.");
        Console.WriteLine("  tactical view: click a friendly unit to select, click the map to order a move.");
        Console.WriteLine("  SPACE pause/resume   1 normal   2 fast   drag pan   scroll zoom   ESC main menu   ` console");

        using TacticalWindow window = new(rendererOptions, cfg, changelog, factory, scenarioCatalog);
        window.Run();
        return 0;
    }

    private static string SettingsPath()
    {
        const string fileName = "bogey.cfg";
#if BOGEY_PUBLISH
        return Path.Combine(AppContext.BaseDirectory, fileName);
#else
        return Path.Combine(Directory.GetCurrentDirectory(), fileName);
#endif
    }

    private static SimConfig BuildSimConfig(IConfigurationManager cfg) => new()
    {
        InitialConfidence = cfg.GetCVar(CCVars.SimInitialConfidence),
        ConfidenceGainPerHit = cfg.GetCVar(CCVars.SimConfidenceGain),
        ClassifyThreshold = cfg.GetCVar(CCVars.SimClassifyThreshold),
        IdentifyThreshold = cfg.GetCVar(CCVars.SimIdentifyThreshold),
        BasePositionalErrorKm = cfg.GetCVar(CCVars.SimPositionalErrorBase),
        ObservationNoiseKm = cfg.GetCVar(CCVars.SimObservationNoise),
        DecayConfidenceFactor = cfg.GetCVar(CCVars.SimDecayFactor),
        PositionalErrorGrowthKmPerTick = cfg.GetCVar(CCVars.SimPositionalErrorGrowth),
        StaleAfterIdleTicks = cfg.GetCVar(CCVars.SimStaleTicks),
        DropAfterIdleTicks = cfg.GetCVar(CCVars.SimDropTicks),
    };

    private sealed class Options
    {
        public const string Usage =
            "usage: Content.Host [--seed N] [--debug] [--ui-scale F] [--prototypes PATH] [--scenarios PATH] [--scenario ID]";

        public int Seed { get; private set; } = 1337;
        public bool Debug { get; private set; }
        public float? UiScale { get; private set; }
        public string PrototypesPath { get; private set; } =
            Path.Combine(AppContext.BaseDirectory, "Resources", "Prototypes");
        public string ScenariosPath { get; private set; } =
            Path.Combine(AppContext.BaseDirectory, "Resources", "Scenarios");
        public string? Scenario { get; private set; }

        public void ApplyTo(IConfigurationManager cfg)
        {
            cfg.SetCVar(CCVars.GameSeed, Seed);
            cfg.SetCVar(CCVars.DebugOverlay, Debug);
            if (UiScale is { } uiScale)
            {
                cfg.SetCVar(CVars.UiScale, uiScale);
            }

            if (Scenario is { } scenario)
            {
                cfg.SetCVar(CCVars.GameScenario, scenario);
            }
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
                    case "--scenarios":
                        options.ScenariosPath = NextValue(args, ref i, "--scenarios");
                        break;
                    case "--scenario":
                        options.Scenario = NextValue(args, ref i, "--scenario");
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
