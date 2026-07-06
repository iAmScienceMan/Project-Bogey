using System.Numerics;
using Content.Shared.Tracks;

namespace Content.Renderer.Map;

public sealed class TrackVisual
{
    
    public Vector2 Position { get; set; }

    public float Fade { get; set; }

    
    public Track? Latest { get; set; }

    
    public bool SeenThisFrame { get; set; }
}
