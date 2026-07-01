using System.Numerics;
using Bogey.Renderer.Camera;
using NUnit.Framework;

namespace Bogey.Tests;

[TestFixture]
public sealed class Camera2DTests
{
    private static readonly Vector2 Viewport = new(800f, 600f);

    [Test]
    public void Center_MapsToViewportMiddle()
    {
        Camera2D camera = new(Viewport, new Vector2(10f, -5f), zoom: 4f);

        Vector2 screen = camera.WorldToScreen(new Vector2(10f, -5f));

        Assert.That(screen.X, Is.EqualTo(400f).Within(1e-3));
        Assert.That(screen.Y, Is.EqualTo(300f).Within(1e-3));
    }

    [Test]
    public void North_IsUp_OnScreen()
    {
        Camera2D camera = new(Viewport, Vector2.Zero, zoom: 4f);

        Vector2 atCenter = camera.WorldToScreen(Vector2.Zero);
        Vector2 toNorth = camera.WorldToScreen(new Vector2(0f, 10f)); 

        Assert.That(toNorth.Y, Is.LessThan(atCenter.Y), "a point to the north should sit higher (smaller pixel-Y).");
        Assert.That(toNorth.X, Is.EqualTo(atCenter.X).Within(1e-3), "due north should not shift horizontally.");
    }

    [Test]
    public void ScreenToWorld_IsInverseOfWorldToScreen()
    {
        Camera2D camera = new(Viewport, new Vector2(3f, 7f), zoom: 6.5f);
        Vector2 world = new(42f, -18f);

        Vector2 roundTrip = camera.ScreenToWorld(camera.WorldToScreen(world));

        Assert.That(roundTrip.X, Is.EqualTo(world.X).Within(1e-2));
        Assert.That(roundTrip.Y, Is.EqualTo(world.Y).Within(1e-2));
    }

    [Test]
    public void ZoomAt_KeepsTheAnchoredWorldPointFixed()
    {
        Camera2D camera = new(Viewport, Vector2.Zero, zoom: 4f);
        Vector2 anchor = new(250f, 180f);
        Vector2 worldUnderAnchor = camera.ScreenToWorld(anchor);

        camera.ZoomAt(2f, anchor);

        Vector2 worldStill = camera.ScreenToWorld(anchor);
        Assert.That(camera.Zoom, Is.EqualTo(8f).Within(1e-3));
        Assert.That(worldStill.X, Is.EqualTo(worldUnderAnchor.X).Within(1e-2));
        Assert.That(worldStill.Y, Is.EqualTo(worldUnderAnchor.Y).Within(1e-2));
    }
}
