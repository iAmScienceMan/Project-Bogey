using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Content.Renderer.App;
using Content.Shared;
using Content.Shared.Components;
using Content.Shared.Configuration;
using Content.Shared.Prototypes;
using Lattice.Logging;
using Lattice.Shared.Changelog;
using Lattice.Shared.Configuration;
using Lattice.Sim.Engine;

namespace Content.Client;

public sealed class Program
{
    public static int Main(string[] args)
    {
        ILogManager logManager = Logger.InitializeDefault();
        ILogbook log = logManager.GetLogbook("client");

        try
        {
            return Run(args, logManager, log);
        }
        catch (Exception ex)
        {
            log.Fatal($"Unhandled exception - the client crashed.\n{ex}");
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

        ConfigurationManager cfg = new(logManager.GetLogbook("config"));
        cfg.RegisterCVars(typeof(CVars));
        cfg.RegisterCVars(typeof(CCVars));

        string settingsPath = SettingsPath();
        cfg.LoadArchive(settingsPath);
        options.ApplyTo(cfg);
        cfg.EnablePersistence(settingsPath);

        ComponentFactory componentFactory = new(new[] { typeof(Sensor).Assembly });
        PrototypeManager prototypes = new(componentFactory);
        try
        {
            prototypes.LoadDirectory(options.PrototypesPath);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            log.Error($"Failed to load content: {ex.Message}");
            return 1;
        }

        List<string> prototypeIds = prototypes.Prototypes.Keys.ToList();

        GameSessionFactory sessionFactory = (host, port) => new NetworkGameSession(
            host,
            port,
            cfg.GetCVar(CCVars.PlayerUsername),
            ColorRgbUtil.ParseHex(cfg.GetCVar(CCVars.PlayerColor), 0x4DC3FF));

        ChangelogManager changelog = new(cfg, logManager.GetLogbook("changelog"));
        changelog.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "Resources", "Changelog"));

        RendererOptions rendererOptions = new() { Title = "PROJECT BOGEY" };
        using TacticalWindow window = new(rendererOptions, cfg, changelog, sessionFactory, prototypeIds);
        window.Run();
        return 0;
    }

    private static string SettingsPath()
        => Path.Combine(Directory.GetCurrentDirectory(), "bogey-client.cfg");

    private sealed class Options
    {
        public const string Usage =
            "usage: Content.Client [--ui-scale F] [--prototypes PATH]";

        public float? UiScale { get; private set; }
        public string PrototypesPath { get; private set; } =
            Path.Combine(AppContext.BaseDirectory, "Resources", "Prototypes");

        public void ApplyTo(IConfigurationManager cfg)
        {
            if (UiScale is { } uiScale)
            {
                cfg.SetCVar(CVars.UiScale, uiScale);
            }
        }

        public static Options Parse(string[] args)
        {
            Options options = new();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
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
