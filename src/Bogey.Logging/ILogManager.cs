namespace Bogey.Logging;

public interface ILogManager
{
    ILogbook RootLogbook { get; }

    ILogbook GetLogbook(string name);

    void AddHandler(ILogHandler handler);
}
