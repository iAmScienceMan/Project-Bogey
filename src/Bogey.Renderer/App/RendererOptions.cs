using System;
using System.IO;

namespace Bogey.Renderer.App;

public sealed record RendererOptions
{
    public int Width { get; init; } = 1280;

    public int Height { get; init; } = 800;

    public string Title { get; init; } = "PROJECT BOGEY - tactical";


    public float InitialZoomPxPerKm { get; init; } = 4f;


    public string SpritesPath { get; init; } = Path.Combine(AppContext.BaseDirectory, "Resources");
}
