using Bogey.Shared.Components;
using Bogey.Sim.Engine;

namespace Bogey.Sim.Systems;

public sealed class MovementSystem : SystemBase
{
    [Dependency]
    private readonly EntityManager _entities = null!;

    public override void Update()
    {
        foreach (int entity in _entities.Query<Transform>())
        {
            Transform transform = _entities.GetComponent<Transform>(entity);
            transform.Position += transform.Velocity * (float)SimClock.SecondsPerTick;
        }
    }
}
