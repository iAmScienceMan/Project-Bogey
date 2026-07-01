using System.Numerics;
using Bogey.Shared.Tracks;

namespace Bogey.Renderer.Map;

public sealed class TrackVisual
{
    
    public Vector2 Position { get; set; }

    public float Fade { get; set; }

    
    public Track? Latest { get; set; }

    
    public bool SeenThisFrame { get; set; }
}
