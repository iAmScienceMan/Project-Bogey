using System;
using System.Linq;
using System.Reflection;
using Bogey.Renderer.Camera;
using NUnit.Framework;

namespace Bogey.Tests;

[TestFixture]
public sealed class RendererBoundaryTests
{
    [Test]
    public void RendererAssembly_DoesNotReferenceSimAssembly()
    {
        Assembly renderer = typeof(Camera2D).Assembly;

        bool referencesSim = renderer.GetReferencedAssemblies()
            .Any(name => string.Equals(name.Name, "Bogey.Sim", StringComparison.Ordinal));

        Assert.That(referencesSim, Is.False,
            "Bogey.Renderer must NOT reference Bogey.Sim, it may only see Shared track snapshots.");
    }

    [Test]
    public void RendererAssembly_ReferencesViewAndShared()
    {
        Assembly renderer = typeof(Camera2D).Assembly;
        string[] referenced = renderer.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(referenced, Does.Contain("Bogey.View"),
                "Bogey.Renderer should reuse Bogey.View's shared presentation logic.");
            Assert.That(referenced, Does.Contain("Bogey.Shared"),
                "Bogey.Renderer should consume the Shared track + command contracts.");
        });
    }
}
