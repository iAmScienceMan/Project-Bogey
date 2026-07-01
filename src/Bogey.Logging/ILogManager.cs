namespace Bogey.Logging;

public interface ILogManager
{
    ISawmill RootSawmill { get; }

    ISawmill GetSawmill(string name);

    void AddHandler(ILogHandler handler);
}
