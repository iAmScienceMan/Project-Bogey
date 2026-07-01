using System;
using System.Linq;
using System.Reflection;
using Bogey.View;
using NUnit.Framework;

namespace Bogey.Tests;

[TestFixture]
public sealed class ViewBoundaryTests
{
    [Test]
    public void ViewAssembly_DoesNotReferenceSimAssembly()
    {
        Assembly view = typeof(TrackPictureRenderer).Assembly;

        bool referencesSim = view.GetReferencedAssemblies()
            .Any(name => string.Equals(name.Name, "Bogey.Sim", StringComparison.Ordinal));

        Assert.That(referencesSim, Is.False,
            "Bogey.View must NOT reference Bogey.Sim, the View may only see Shared track snapshots.");
    }

    [Test]
    public void ViewAssembly_ReferencesSharedAssembly()
    {
        Assembly view = typeof(TrackPictureRenderer).Assembly;

        bool referencesShared = view.GetReferencedAssemblies()
            .Any(name => string.Equals(name.Name, "Bogey.Shared", StringComparison.Ordinal));

        Assert.That(referencesShared, Is.True, "Bogey.View should consume the Shared track contracts.");
    }
}
