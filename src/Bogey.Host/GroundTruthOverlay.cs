using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Bogey.Renderer.App;
using Bogey.Renderer.Camera;
using Bogey.Renderer.Gl;
using Bogey.Renderer.Text;
using Bogey.Shared.Components;
using Bogey.Sim;

namespace Bogey.Host;

public sealed class GroundTruthOverlay : IDebugOverlay
{
    private enum DisplayMode
    {
        LabelsAndMarkers,
        MarkersOnly,
        Hidden,
    }

    private static readonly Rgba HostileColor = new(1.0f, 0.35f, 0.35f);
    private static readonly Rgba FriendlyColor = new(0.45f, 1.0f, 1.0f);
    private static readonly Rgba NeutralColor = new(0.80f, 0.80f, 0.85f);
    private static readonly Rgba LabelColor = new(0.95f, 0.95f, 0.98f);
    private static readonly Rgba SelectColor = new(1.0f, 0.95f, 0.35f);

    private const float HalfSizePx = 7f;
    private const float PickRadiusPx = 16f;

    private readonly SimRuntime _sim;

    private DisplayMode _mode = DisplayMode.LabelsAndMarkers;
    private int? _selectedEntity;

    public GroundTruthOverlay(SimRuntime sim) => _sim = sim;

    public void CycleDisplay()
    {
        _mode = _mode switch
        {
            DisplayMode.LabelsAndMarkers => DisplayMode.MarkersOnly,
            DisplayMode.MarkersOnly => DisplayMode.Hidden,
            _ => DisplayMode.LabelsAndMarkers,
        };
    }

    public bool HandleClick(Vector2 screenPx, Camera2D camera)
    {
        if (_mode == DisplayMode.Hidden)
        {
            return false;
        }

        if (TryPick(screenPx, camera, out int entityId))
        {
            _selectedEntity = entityId;
            return true;
        }

        if (_selectedEntity is { } selected)
        {
            _sim.DebugSetPosition(selected, camera.ScreenToWorld(screenPx));
            _selectedEntity = null;
            return true;
        }

        return false;
    }

    public void Draw(PrimitiveBatch prims, TextBatch text, Camera2D camera, Vector2 viewport)
    {
        if (_mode == DisplayMode.Hidden)
        {
            return;
        }

        bool showLabels = _mode == DisplayMode.LabelsAndMarkers;
        IReadOnlyList<GroundTruthEntry> truth = _sim.DumpGroundTruth();
        string? selectedName = null;

        foreach (GroundTruthEntry entry in truth)
        {
            Rgba color = ColorFor(entry.Faction);
            Vector2 screen = camera.WorldToScreen(entry.Position);
            bool selected = _selectedEntity == entry.EntityId;

            DrawSquare(prims, screen, HalfSizePx, color);

            if (selected)
            {
                selectedName = entry.Name;
                prims.Ring(screen, HalfSizePx + 5f, SelectColor, 24);
            }

            if (showLabels || selected)
            {
                text.Text(screen + new Vector2(-HalfSizePx, HalfSizePx + 3f), 11f, LabelColor, LabelFor(entry));
            }
        }

        DrawFooter(text, viewport, selectedName);
    }

    private bool TryPick(Vector2 screenPx, Camera2D camera, out int entityId)
    {
        entityId = 0;
        float best = PickRadiusPx;
        bool found = false;

        foreach (GroundTruthEntry entry in _sim.DumpGroundTruth())
        {
            Vector2 screen = camera.WorldToScreen(entry.Position);
            float distance = Vector2.Distance(screen, screenPx);
            if (distance <= best)
            {
                best = distance;
                entityId = entry.EntityId;
                found = true;
            }
        }

        return found;
    }

    private void DrawFooter(TextBatch text, Vector2 viewport, string? selectedName)
    {
        string mode = _mode == DisplayMode.LabelsAndMarkers ? "labels" : "markers";
        text.Text(new Vector2(16f, viewport.Y - 40f), 12f, LabelColor,
            "DEBUG GROUND TRUTH [" + mode + "]  (host-only, hidden from the track picture)   G: declutter   RIGHT-CLICK: move any entity");

        if (selectedName is not null)
        {
            text.Text(new Vector2(16f, viewport.Y - 58f), 12f, SelectColor,
                "SELECTED (truth): " + selectedName + " - right-click the map to reposition it");
        }
    }

    private static void DrawSquare(PrimitiveBatch prims, Vector2 center, float half, Rgba color)
    {
        Vector2 tl = center + new Vector2(-half, -half);
        Vector2 tr = center + new Vector2(half, -half);
        Vector2 br = center + new Vector2(half, half);
        Vector2 bl = center + new Vector2(-half, half);
        prims.Line(tl, tr, color);
        prims.Line(tr, br, color);
        prims.Line(br, bl, color);
        prims.Line(bl, tl, color);
    }

    private static string LabelFor(GroundTruthEntry entry)
    {
        string pos = string.Create(CultureInfo.InvariantCulture,
            $" ({entry.Position.X:0}, {entry.Position.Y:0})");
        return entry.TypeName is null ? entry.Name + pos : entry.Name + " [" + entry.TypeName + "]" + pos;
    }

    private static Rgba ColorFor(FactionType faction) => faction switch
    {
        FactionType.Hostile => HostileColor,
        FactionType.Friendly => FriendlyColor,
        _ => NeutralColor,
    };
}
