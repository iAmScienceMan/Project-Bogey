using System;
using System.Linq;
using System.Reflection;
using Lattice.Renderer.Camera;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class RendererBoundaryTests
{
    [Test]
    public void RendererAssembly_DoesNotReferenceSimAssembly()
    {
        Assembly renderer = typeof(Camera2D).Assembly;

        bool referencesSim = renderer.GetReferencedAssemblies()
            .Any(name => string.Equals(name.Name, "Content.Sim", StringComparison.Ordinal));

        Assert.That(referencesSim, Is.False,
            "Lattice.Renderer is an engine toolkit and must NOT reference Content.Sim.");
    }

    [Test]
    public void RendererAssembly_DoesNotReferenceAnyContentAssembly()
    {
        Assembly renderer = typeof(Camera2D).Assembly;

        string[] contentRefs = renderer.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .Where(name => name.StartsWith("Content.", StringComparison.Ordinal))
            .ToArray();

        Assert.That(contentRefs, Is.Empty,
            "Lattice.Renderer must stay game-agnostic and reference no Content.* assembly.");
    }
}
