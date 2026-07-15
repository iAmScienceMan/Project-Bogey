using Content.Shared.Components;
using Lattice.Sim.Engine;

namespace Content.Sim.Systems;

public sealed class MovementSystem : EntitySystem
{
    [Dependency]
    private readonly EntityManager _entities = null!;

    [Dependency]
    private readonly SimConfig _config = null!;

    [Dependency]
    private readonly SimClock _clock = null!;

    public override void Update()
    {
        float dt = (float)_clock.Dt;
        EntityQueryEnumerator<Transform> query = _entities.AllEntityQuery<Transform>();
        while (query.MoveNext(out EntityUid entity, out Transform transform))
        {
            if (!_config.AiEnabled && _entities.HasComponent<Ai>(entity))
            {
                continue;
            }

            transform.Position += transform.Velocity * dt;
        }
    }
}
