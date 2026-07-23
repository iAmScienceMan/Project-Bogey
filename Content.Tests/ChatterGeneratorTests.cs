using System;
using Content.Shared.Chatter;
using Content.Sim.Chatter;
using NUnit.Framework;

namespace Content.Tests;

[TestFixture]
public sealed class ChatterGeneratorTests
{
    [Test]
    public void Generate_IsDeterministic_ForSameSeedAndRequest()
    {
        ChatterRequest request = new(48, ChatterTone.Urgent);

        ChatterLine first = ChatterGenerator.Generate(new Random(1234), request);
        ChatterLine second = ChatterGenerator.Generate(new Random(1234), request);

        Assert.That(second.Text, Is.EqualTo(first.Text));
        Assert.That(second.VoiceSeed, Is.EqualTo(first.VoiceSeed));
    }

    [Test]
    public void Generate_ProducesNonEmptyText_WithinLengthBudget()
    {
        ChatterRequest request = new(32, ChatterTone.Routine);

        ChatterLine line = ChatterGenerator.Generate(new Random(7), request);

        Assert.That(line.Text, Is.Not.Empty);
        Assert.That(line.Text.Length, Is.LessThanOrEqualTo(request.LengthSymbols));
    }

    [Test]
    public void FromText_IsStableAndDistinct()
    {
        ChatterLine a = ChatterGenerator.FromText("BRAVO SIX GOING DARK");
        ChatterLine b = ChatterGenerator.FromText("BRAVO SIX GOING DARK");
        ChatterLine c = ChatterGenerator.FromText("BRAVO SIX GOING LOUD");

        Assert.That(a.Text, Is.EqualTo("BRAVO SIX GOING DARK"));
        Assert.That(b.VoiceSeed, Is.EqualTo(a.VoiceSeed));
        Assert.That(c.VoiceSeed, Is.Not.EqualTo(a.VoiceSeed));
    }

    [Test]
    public void Generate_VariesWithSeed()
    {
        ChatterRequest request = new(48, ChatterTone.Routine);

        ChatterLine a = ChatterGenerator.Generate(new Random(1), request);
        ChatterLine b = ChatterGenerator.Generate(new Random(2), request);

        Assert.That(a.Text, Is.Not.EqualTo(b.Text));
    }
}
