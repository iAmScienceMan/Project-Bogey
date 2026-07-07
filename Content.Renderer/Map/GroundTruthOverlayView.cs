using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Components;
using Content.Shared.Tracks;
using Lattice.Renderer.Camera;
using Lattice.Renderer.Gl;
using Lattice.Renderer.Text;

namespace Content.Renderer.Map;

public sealed class GroundTruthOverlayView
{
    private const float PickRadiusPx = 18f;
    private const float MarkerPx = 5f;

    private static readonly Rgba FriendlyColor = new(0.35f, 0.85f, 1.0f, 0.9f);
    private static readonly Rgba HostileColor = new(1.0f, 0.42f, 0.35f, 0.9f);
    private static readonly Rgba NeutralColor = new(0.7f, 0.7f, 0.75f, 0.9f);
    private static readonly Rgba PickColor = new(1.0f, 0.95f, 0.35f);

    private int _pickedEntity = -1;

    public void Draw(PrimitiveBatch prims, TextBatch text, Camera2D camera, IReadOnlyList<GroundTruthView> entries)
    {
        foreach (GroundTruthView entry in entries)
        {
            Vector2 screen = camera.WorldToScreen(entry.Position);
            Rgba color = ColorFor(entry.Side);

            prims.Line(screen - new Vector2(MarkerPx, 0f), screen + new Vector2(MarkerPx, 0f), color);
            prims.Line(screen - new Vector2(0f, MarkerPx), screen + new Vector2(0f, MarkerPx), color);
            text.Text(screen + new Vector2(MarkerPx + 3f, -12f), 10f, color,
                $"#{entry.EntityId} {entry.Name}");

            if (entry.EntityId == _pickedEntity)
            {
                prims.Ring(screen, PickRadiusPx * 0.7f, PickColor, 24);
            }
        }
    }

    public (int EntityId, Vector2 World)? PickOrPlace(
        Vector2 px, Camera2D camera, IReadOnlyList<GroundTruthView> entries)
    {
        if (_pickedEntity >= 0)
        {
            bool stillExists = false;
            foreach (GroundTruthView entry in entries)
            {
                if (entry.EntityId == _pickedEntity)
                {
                    stillExists = true;
                    break;
                }
            }

            int picked = _pickedEntity;
            _pickedEntity = -1;
            if (stillExists)
            {
                return (picked, camera.ScreenToWorld(px));
            }
        }

        int best = -1;
        float bestDistance = PickRadiusPx;
        foreach (GroundTruthView entry in entries)
        {
            float distance = Vector2.Distance(camera.WorldToScreen(entry.Position), px);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = entry.EntityId;
            }
        }

        _pickedEntity = best;
        return null;
    }

    public void Clear() => _pickedEntity = -1;

    private static Rgba ColorFor(FactionType side) => side switch
    {
        FactionType.Friendly => FriendlyColor,
        FactionType.Hostile => HostileColor,
        _ => NeutralColor,
    };
}
