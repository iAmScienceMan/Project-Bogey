using Bogey.Logging;

namespace Bogey.Sim.Engine;

public abstract class SystemBase
{
    [Dependency]
    private readonly ILogManager _logManager = null!;

    private ISawmill? _log;

    protected ISawmill Log => _log ??= _logManager.GetSawmill("sim." + GetType().Name);

    public virtual void Initialize()
    {
    }

    public virtual void Update()
    {
    }
}
