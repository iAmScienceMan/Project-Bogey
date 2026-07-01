namespace Bogey.Renderer.Gl;

public readonly record struct Rgba(float R, float G, float B, float A)
{
    public Rgba(float r, float g, float b)
        : this(r, g, b, 1f)
    {
    }

    
    public Rgba WithAlpha(float alpha) => new(R, G, B, alpha);

    
    public Rgba FadeBy(float factor) => new(R, G, B, A * factor);
}
