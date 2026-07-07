using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Content.Shared.Components;
using Content.Shared.Configuration;
using Content.Shared.Prototypes;
using Content.Sim.Content;
using Lattice.Logging;
using Lattice.Network;
using Lattice.Shared.Configuration;
using Lattice.Sim.Engine;

namespace Content.Server;

public sealed class Program
{
    public static int Main(string[] args)
    {
        ILogManager logManager = Logger.InitializeDefault();
        ILogbook log = logManager.GetLogbook("server");

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

        ComponentFactory componentFactory = new(new[] { typeof(Sensor).Assembly });
        PrototypeManager prototypes = new(componentFactory);
        IReadOnlyDictionary<string, ScenarioDefinition> scenarios;
        IReadOnlyDictionary<string, ServerConfig> configs;
        try
        {
            prototypes.LoadDirectory(options.PrototypesPath);
            scenarios = new ScenarioLoader().LoadScenarios(options.ScenariosPath);
            configs = new ServerConfigLoader().LoadConfigs(options.ServerConfigsPath);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            log.Error($"Failed to load content: {ex.Message}");
            return 1;
        }

        ServerConfig? config = SelectConfig(configs, options.ConfigId, log);
        if (config is null)
        {
            return 1;
        }

        if (!scenarios.TryGetValue(config.Scenario, out ScenarioDefinition? scenario))
        {
            log.Error($"Server config '{config.Id}' references unknown scenario '{config.Scenario}'. Available: {string.Join(", ", scenarios.Keys)}");
            return 1;
        }

        ConfigurationManager cfg = new(logManager.GetLogbook("config"));
        cfg.RegisterCVars(typeof(CCVars));
        string settingsPath = Path.Combine(Directory.GetCurrentDirectory(), "bogey-server.cfg");
        cfg.LoadArchive(settingsPath);
        cfg.EnablePersistence(settingsPath);

        int port = options.Port ?? config.Port;
        NetworkServer server = new(port);
        server.Start();
        log.Info($"Server listening on port {port}: config '{config.Id}', scenario '{scenario.Id}', seed {config.Seed}, {config.TickRate} tick/s.");
        Console.WriteLine($"PROJECT BOGEY server - '{config.Name ?? config.Id}' on port {port}. Type 'help' for commands, 'stop' to shut down.");

        GameTicker ticker = new(server, prototypes, scenario, config, cfg, logManager);

        System.Threading.Thread stdinReader = new(() =>
        {
            while (Console.ReadLine() is { } line)
            {
                ticker.EnqueueConsoleCommand(line);
            }
        })
        {
            IsBackground = true,
        };
        stdinReader.Start();

        ticker.Run();
        return 0;
    }

    private static ServerConfig? SelectConfig(
        IReadOnlyDictionary<string, ServerConfig> configs, string? requested, ILogbook log)
    {
        if (requested is not null)
        {
            if (configs.TryGetValue(requested, out ServerConfig? chosen))
            {
                return chosen;
            }

            log.Error($"Unknown server config '{requested}'. Available: {string.Join(", ", configs.Keys)}");
            return null;
        }

        if (configs.TryGetValue("default", out ServerConfig? fallback))
        {
            return fallback;
        }

        return configs.Count > 0 ? configs.Values.First() : new ServerConfig { Id = "builtin-default" };
    }

    private sealed class Options
    {
        public const string Usage =
            "usage: Content.Server [--config ID] [--port N] [--prototypes PATH] [--scenarios PATH] [--server-configs PATH] [--debug]";

        public string? ConfigId { get; private set; }
        public int? Port { get; private set; }
        public bool Debug { get; private set; }
        public string PrototypesPath { get; private set; } =
            Path.Combine(AppContext.BaseDirectory, "Resources", "Prototypes");
        public string ScenariosPath { get; private set; } =
            Path.Combine(AppContext.BaseDirectory, "Resources", "Scenarios");
        public string ServerConfigsPath { get; private set; } =
            Path.Combine(AppContext.BaseDirectory, "Resources", "ServerConfigs");

        public static Options Parse(string[] args)
        {
            Options options = new();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--config":
                        options.ConfigId = NextValue(args, ref i, "--config");
                        break;
                    case "--port":
                        options.Port = NextInt(args, ref i, "--port");
                        break;
                    case "--prototypes":
                        options.PrototypesPath = NextValue(args, ref i, "--prototypes");
                        break;
                    case "--scenarios":
                        options.ScenariosPath = NextValue(args, ref i, "--scenarios");
                        break;
                    case "--server-configs":
                        options.ServerConfigsPath = NextValue(args, ref i, "--server-configs");
                        break;
                    case "--debug":
                        options.Debug = true;
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
    }
}
