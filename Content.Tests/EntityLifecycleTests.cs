using Content.Shared.Components;
using Lattice.Sim.Engine;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class EntityLifecycleTests
{
    [Test]
    public void Query_IsSafeToDestroyDuringIteration()
    {
        EntityManager entities = new();
        for (int i = 0; i < 5; i++)
        {
            EntityUid entity = entities.CreateEntity();
            entities.AddComponent(entity, new Signature { Value = i });
        }

        Assert.DoesNotThrow(() =>
        {
            foreach (EntityUid entity in entities.Query<Signature>())
            {
                entities.DestroyEntity(entity);
            }
        });

        Assert.That(entities.Entities, Is.Empty);
    }

    [Test]
    public void DestroyEntity_FiresTerminating_WithComponentsStillPresent()
    {
        EventBus bus = new();
        EntityManager entities = new() { Bus = bus };
        EntityUid entity = entities.CreateEntity();
        entities.AddComponent(entity, new Signature { Value = 3f });

        bool fired = false;
        float observed = -1f;
        bus.SubscribeDirected<EntityTerminating>((id, _) =>
        {
            fired = true;
            if (entities.TryGetComponent(id, out Signature signature))
            {
                observed = signature.Value;
            }
        });

        Assert.That(entities.DestroyEntity(entity), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(fired, Is.True);
            Assert.That(observed, Is.EqualTo(3f), "components are readable while terminating");
            Assert.That(entities.Deleted(entity), Is.True);
        });
    }

    [Test]
    public void DestroyEntity_IsIdempotent()
    {
        EventBus bus = new();
        EntityManager entities = new() { Bus = bus };
        EntityUid entity = entities.CreateEntity();

        int fired = 0;
        bus.SubscribeDirected<EntityTerminating>((_, _) => fired++);

        Assert.Multiple(() =>
        {
            Assert.That(entities.DestroyEntity(entity), Is.True);
            Assert.That(entities.DestroyEntity(entity), Is.False, "second destroy is a no-op");
            Assert.That(fired, Is.EqualTo(1), "terminating fires exactly once");
        });
    }

    [Test]
    public void QueueDeletion_DefersUntilFlush()
    {
        EntityManager entities = new();
        EntityUid entity = entities.CreateEntity();
        entities.AddComponent(entity, new Signature());

        entities.QueueDeletion(entity);
        Assert.That(entities.IsAlive(entity), Is.True, "queued but not yet flushed");

        entities.FlushDeletions();
        Assert.That(entities.Deleted(entity), Is.True);
    }

    [Test]
    public void QueueDeletion_IsDeduplicated()
    {
        EventBus bus = new();
        EntityManager entities = new() { Bus = bus };
        EntityUid entity = entities.CreateEntity();

        int fired = 0;
        bus.SubscribeDirected<EntityTerminating>((_, _) => fired++);

        entities.QueueDeletion(entity);
        entities.QueueDeletion(entity);
        entities.FlushDeletions();

        Assert.That(fired, Is.EqualTo(1));
    }
}
