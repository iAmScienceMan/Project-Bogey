namespace Bogey.Logging;

public static class Logger
{
    private static readonly object Gate = new();
    private static ILogManager? _manager;

    public static ILogManager LogManager
    {
        get
        {
            lock (Gate)
            {
                return _manager ??= new LogManager();
            }
        }
    }

    public static void Initialize(ILogManager manager)
    {
        lock (Gate)
        {
            _manager = manager;
        }
    }

    public static ILogManager InitializeDefault()
    {
        LogManager manager = new();
        manager.AddHandler(new ConsoleLogHandler());
        Initialize(manager);
        return manager;
    }

    public static ISawmill GetSawmill(string name) => LogManager.GetSawmill(name);

    public static ISawmill Root => LogManager.RootSawmill;
}
