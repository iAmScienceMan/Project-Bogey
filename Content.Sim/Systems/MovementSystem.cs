using Content.Shared.Components;
using Lattice.Sim.Engine;

namespace Content.Sim.Systems;

public sealed class MovementSystem : EntitySystem
{
    [Dependency]
    private readonly EntityManager _entities = null!;

    [Dependency]
    private readonly SimConfig _config = null!;

    public override void Update()
    {
        foreach (int entity in _entities.Query<Transform>())
        {
            if (!_config.AiEnabled && _entities.HasComponent<Ai>(entity))
            {
                continue;
            }

            Transform transform = _entities.GetComponent<Transform>(entity);
            transform.Position += transform.Velocity * (float)SimClock.SecondsPerTick;
        }
    }
}
