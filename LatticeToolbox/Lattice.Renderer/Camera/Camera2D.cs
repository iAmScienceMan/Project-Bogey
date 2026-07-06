using System;
using System.Numerics;

namespace Lattice.Renderer.Camera;

public sealed class Camera2D
{
    private const float MinZoom = 0.05f;  
    private const float MaxZoom = 200f;

    private Vector2 _viewport;
    private Vector2 _center;
    private float _zoom;

    public Camera2D(Vector2 viewport, Vector2 center, float zoom)
    {
        _viewport = viewport;
        _center = center;
        _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
    }

    public Vector2 Center => _center;
    public float Zoom => _zoom;
    public Vector2 Viewport => _viewport;

    public void ResizeViewport(Vector2 viewport) => _viewport = viewport;

    public void SetCenter(Vector2 center) => _center = center;

    
    public Vector2 WorldToScreen(Vector2 world)
    {
        Vector2 delta = world - _center;
        return new Vector2(
            (_viewport.X * 0.5f) + (delta.X * _zoom),
            (_viewport.Y * 0.5f) - (delta.Y * _zoom));
    }

    
    public Vector2 ScreenToWorld(Vector2 screen)
    {
        float dx = (screen.X - (_viewport.X * 0.5f)) / _zoom;
        float dy = -(screen.Y - (_viewport.Y * 0.5f)) / _zoom;
        return _center + new Vector2(dx, dy);
    }

    
    public float KmToPixels(float km) => km * _zoom;

    
    public void Pan(Vector2 screenDelta)
    {
        _center.X -= screenDelta.X / _zoom;
        _center.Y += screenDelta.Y / _zoom; 
    }

    
    public void ZoomAt(float factor, Vector2 anchorPx)
    {
        Vector2 worldBefore = ScreenToWorld(anchorPx);
        _zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);

        float dx = (anchorPx.X - (_viewport.X * 0.5f)) / _zoom;
        float dy = -(anchorPx.Y - (_viewport.Y * 0.5f)) / _zoom;
        _center = worldBefore - new Vector2(dx, dy);
    }
}
