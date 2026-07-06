using System;
using System.Linq;
using System.Reflection;
using Content.Shared.Presentation;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class ViewBoundaryTests
{
    [Test]
    public void SharedAssembly_DoesNotReferenceSimAssembly()
    {
        Assembly shared = typeof(TrackPresentation).Assembly;

        bool referencesSim = shared.GetReferencedAssemblies()
            .Any(name => string.Equals(name.Name, "Content.Sim", StringComparison.Ordinal));

        Assert.That(referencesSim, Is.False,
            "Content.Shared holds the data model and must NOT reference Content.Sim.");
    }

    [Test]
    public void SharedAssembly_DoesNotReferenceRenderer()
    {
        Assembly shared = typeof(TrackPresentation).Assembly;

        string[] rendererRefs = shared.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .Where(name => name.EndsWith(".Renderer", StringComparison.Ordinal))
            .ToArray();

        Assert.That(rendererRefs, Is.Empty,
            "Content.Shared must stay independent of any renderer assembly.");
    }
}
