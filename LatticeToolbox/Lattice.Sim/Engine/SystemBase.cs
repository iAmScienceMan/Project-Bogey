using Lattice.Logging;

namespace Lattice.Sim.Engine;

public abstract class SystemBase
{
    [Dependency]
    private readonly ILogManager _logManager = null!;

    private ILogbook? _log;

    protected ILogbook Log => _log ??= _logManager.GetLogbook("sim." + GetType().Name);

    public virtual void Initialize()
    {
    }

    public virtual void Update()
    {
    }
}
