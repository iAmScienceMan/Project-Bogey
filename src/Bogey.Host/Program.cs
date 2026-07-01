using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Bogey.Logging;
using Bogey.Renderer.App;
using Bogey.Shared.Prototypes;
using Bogey.Shared.Tracks;
using Bogey.Sim;
using Bogey.Sim.Content;
using Bogey.View;

namespace Bogey.Host;

public sealed class Program
{
    public static int Main(string[] args)
    {
        ILogManager logManager = Logger.InitializeDefault();
        ISawmill log = logManager.GetSawmill("host");

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

    private static int Run(string[] args, ILogManager logManager, ISawmill log)
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
            logManager.RootSawmill.Level = LogLevel.Verbose;
        }

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

        SimRuntime sim = new(prototypes, options.Seed, logManager: logManager);

        if (options.Render)
        {
            return RunRealtime(sim, options.Seed, options.Debug);
        }

        TrackPictureRenderer listRenderer = new();
        ScopeRenderer scopeRenderer = new();

        Console.WriteLine("PROJECT BOGEY - fog-of-war tactical sim");
        Console.WriteLine($"seed={options.Seed}  ticks={options.Ticks}  print every {options.Interval} tick(s)" +
                          (options.Debug ? "  [DEBUG: ground truth shown]" : string.Empty));
        Console.WriteLine();

        for (int i = 0; i < options.Ticks; i++)
        {
            IssueOrdersForTick(sim, options.Orders, sim.CurrentTick + 1, log);

            sim.Step();

            if (sim.CurrentTick % options.Interval != 0 && sim.CurrentTick != options.Ticks)
            {
                continue;
            }

            TrackPictureSnapshot snapshot = sim.PublishSnapshot();
            Console.Write(options.Scope ? scopeRenderer.Render(snapshot) : listRenderer.Render(snapshot));

            if (options.Debug)
            {
                PrintGroundTruth(sim);
            }

            Console.WriteLine();
        }

        return 0;
    }

    private static int RunRealtime(SimRuntime sim, int seed, bool debug)
    {
        SimSession session = new(sim);
        RendererOptions rendererOptions = new()
        {
            Title = $"PROJECT BOGEY - tactical (seed {seed})" + (debug ? " [DEBUG]" : string.Empty),
        };

        GroundTruthOverlay? overlay = debug ? new GroundTruthOverlay(sim) : null;

        Console.WriteLine("PROJECT BOGEY - live tactical view" + (debug ? "  [DEBUG: ground truth overlay]" : string.Empty));
        Console.WriteLine("  click a friendly unit to select, click the map to order a move.");
        Console.WriteLine("  SPACE pause/resume   1 normal   2 fast   drag pan   scroll zoom   ESC quit");
        if (debug)
        {
            Console.WriteLine("  DEBUG: G cycles ground-truth declutter; RIGHT-CLICK selects then repositions ANY entity.");
        }

        using TacticalWindow window = new(rendererOptions, session, overlay);
        window.Run();
        return 0;
    }

    private static void IssueOrdersForTick(SimRuntime sim, IReadOnlyList<MoveOrder> orders, int tick, ISawmill log)
    {
        foreach (MoveOrder order in orders)
        {
            if (order.Tick != tick)
            {
                continue;
            }

            string target = string.Create(CultureInfo.InvariantCulture,
                $"({order.Destination.X:0.0}, {order.Destination.Y:0.0})");

            if (sim.IssueMoveOrder(order.Unit, order.Destination))
            {
                log.Info($"[tick {tick}] order: {order.Unit} -> {target}");
            }
            else
            {
                log.Warning($"[tick {tick}] order ignored: no movable friendly unit named '{order.Unit}'.");
            }
        }
    }

    private readonly record struct MoveOrder(int Tick, string Unit, Vector2 Destination);

    private static void PrintGroundTruth(SimRuntime sim)
    {
        Console.WriteLine("  -- [DEBUG] ground truth --");
        foreach (GroundTruthEntry entry in sim.DumpGroundTruth())
        {
            string pos = string.Create(CultureInfo.InvariantCulture,
                $"({entry.Position.X:0.0}, {entry.Position.Y:0.0})");
            string classification = entry.TypeName ?? entry.Domain.ToString();
            Console.WriteLine($"     {entry.Faction,-8} {entry.Name,-32} {classification,-28} @ {pos}");
        }
    }

    private sealed class Options
    {
        public const string Usage =
            "usage: Bogey.Host [--seed N] [--ticks N] [--interval N] [--scope] [--debug]\n" +
            "                  [--render] [--order TICK:UNIT:X:Y]... [--prototypes PATH]";

        private readonly List<MoveOrder> _orders = new();

        public int Seed { get; private set; } = 1337;
        public int Ticks { get; private set; } = 90;
        public int Interval { get; private set; } = 6;
        public bool Scope { get; private set; }
        public bool Debug { get; private set; }
        public bool Render { get; private set; }
        public string PrototypesPath { get; private set; } =
            Path.Combine(AppContext.BaseDirectory, "Prototypes");
        public IReadOnlyList<MoveOrder> Orders => _orders;

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
                    case "--ticks":
                        options.Ticks = NextInt(args, ref i, "--ticks");
                        break;
                    case "--interval":
                        options.Interval = Math.Max(1, NextInt(args, ref i, "--interval"));
                        break;
                    case "--scope":
                        options.Scope = true;
                        break;
                    case "--debug":
                        options.Debug = true;
                        break;
                    case "--render":
                        options.Render = true;
                        break;
                    case "--order":
                        options._orders.Add(ParseOrder(NextValue(args, ref i, "--order")));
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

        private static MoveOrder ParseOrder(string raw)
        {
            string[] parts = raw.Split(':');
            if (parts.Length != 4)
            {
                throw new ArgumentException(
                    $"--order must be TICK:UNIT:X:Y, got '{raw}'.");
            }

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int tick) || tick < 1)
            {
                throw new ArgumentException($"--order tick must be an integer >= 1, got '{parts[0]}'.");
            }

            string unit = parts[1];
            if (unit.Length == 0)
            {
                throw new ArgumentException($"--order is missing a unit name in '{raw}'.");
            }

            float x = ParseCoordinate(parts[2], raw);
            float y = ParseCoordinate(parts[3], raw);
            return new MoveOrder(tick, unit, new Vector2(x, y));
        }

        private static float ParseCoordinate(string raw, string order)
        {
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                throw new ArgumentException($"--order coordinate must be a number, got '{raw}' in '{order}'.");
            }

            return value;
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
