namespace Lattice.Logging;

public interface ILogHandler
{
    void Log(in LogMessage message);
}
