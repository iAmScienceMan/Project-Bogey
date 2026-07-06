using System;
using System.IO;

namespace Content.Renderer.App;

public sealed record RendererOptions
{
    public string Title { get; init; } = "PROJECT BOGEY - tactical";

    public string SpritesPath { get; init; } = Path.Combine(AppContext.BaseDirectory, "Resources");
}
