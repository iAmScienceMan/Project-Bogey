using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Content.Renderer.App;
using Lattice.Renderer.Camera;
using Lattice.Renderer.Gl;
using Lattice.Renderer.Text;
using Content.Shared.Components;
using Content.Sim;

namespace Content.Host;

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
    private bool _showSeekers;

    public GroundTruthOverlay(SimRuntime sim) => _sim = sim;

    public string CycleDisplay()
    {
        _mode = _mode switch
        {
            DisplayMode.LabelsAndMarkers => DisplayMode.MarkersOnly,
            DisplayMode.MarkersOnly => DisplayMode.Hidden,
            _ => DisplayMode.LabelsAndMarkers,
        };

        return ModeName(_mode);
    }

    public string ToggleSeekers()
    {
        _showSeekers = !_showSeekers;
        return _showSeekers ? "on" : "off";
    }

    public IReadOnlyList<string> DescribeMunitions()
    {
        List<string> lines = new();
        foreach (MunitionDebug munition in _sim.DumpMunitions())
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"#{munition.Id} {munition.Faction} {munition.Seeker} {PhaseOf(munition)} " +
                $"fov {munition.FovDegrees:0}° acq {munition.AcquisitionRangeKm:0}km " +
                $"datum ({munition.Datum.X:0}, {munition.Datum.Y:0})"));
        }

        if (lines.Count == 0)
        {
            lines.Add("No munitions in flight.");
        }

        return lines;
    }

    public bool Teleport(int entityId, Vector2 worldPosition) =>
        _sim.DebugSetPosition(entityId, worldPosition);

    public TeleportRequest? PickOrPlace(Vector2 screenPx, Camera2D camera)
    {
        if (_mode == DisplayMode.Hidden)
        {
            return null;
        }

        if (TryPick(screenPx, camera, out int entityId))
        {
            _selectedEntity = entityId;
            return null;
        }

        if (_selectedEntity is { } selected)
        {
            _selectedEntity = null;
            return new TeleportRequest(selected, camera.ScreenToWorld(screenPx));
        }

        return null;
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

        if (_showSeekers)
        {
            DrawSeekers(prims, text, camera);
        }

        DrawFooter(text, viewport, selectedName);
    }

    private void DrawSeekers(PrimitiveBatch prims, TextBatch text, Camera2D camera)
    {
        foreach (MunitionDebug munition in _sim.DumpMunitions())
        {
            Rgba color = ColorFor(munition.Faction);
            Vector2 screen = camera.WorldToScreen(munition.Position);

            if (munition.AcquisitionRangeKm > 0f)
            {
                prims.Ring(screen, camera.KmToPixels(munition.AcquisitionRangeKm), color.WithAlpha(0.22f), 48);
            }

            if (munition.FovDegrees > 0f && munition.FovDegrees < 360f && munition.AcquisitionRangeKm > 0f)
            {
                DrawSeekerCone(prims, camera, munition, color);
            }

            if (!munition.DatumPassed)
            {
                prims.Line(screen, camera.WorldToScreen(munition.Datum), color.WithAlpha(0.3f));
            }

            if (munition.Locked && munition.TargetPosition is { } targetPosition)
            {
                prims.Line(screen, camera.WorldToScreen(targetPosition), color);
            }

            text.Text(screen + new Vector2(7f, -14f), 10f, LabelColor,
                munition.Seeker + " " + PhaseOf(munition));
        }
    }

    private static void DrawSeekerCone(PrimitiveBatch prims, Camera2D camera, MunitionDebug munition, Rgba color)
    {
        float half = munition.FovDegrees * 0.5f * (MathF.PI / 180f);
        float acq = munition.AcquisitionRangeKm;
        Rgba edge = color.WithAlpha(0.5f);

        Vector2 Edge(float angle)
            => camera.WorldToScreen(munition.Position + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * acq));

        Vector2 apex = camera.WorldToScreen(munition.Position);
        float start = munition.HeadingRadians - half;
        Vector2 previous = Edge(start);
        prims.Line(apex, previous, edge);

        const int segments = 16;
        for (int i = 1; i <= segments; i++)
        {
            Vector2 point = Edge(start + ((2f * half) * (i / (float)segments)));
            prims.Line(previous, point, edge);
            previous = point;
        }

        prims.Line(apex, previous, edge);
    }

    private static string PhaseOf(MunitionDebug munition)
    {
        if (munition.Locked)
        {
            return "LOCKED";
        }

        return munition.Seeker == SeekerType.Gps ? "GPS" : "SEEK";
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
        string seekers = _showSeekers ? "on" : "off";
        string footer =
            "DEBUG GROUND TRUTH [" + mode + "]   seekers: [" + seekers + "]";
        text.Text(new Vector2(viewport.X - TextBatch.Measure(footer, 12f) - 16f, viewport.Y - 40f), 12f, LabelColor, footer);

        if (selectedName is not null)
        {
            string selection = "SELECTED (truth): " + selectedName + " - right-click the map to reposition it";
            text.Text(new Vector2(viewport.X - TextBatch.Measure(selection, 12f) - 16f, viewport.Y - 58f), 12f, SelectColor, selection);
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
        string id = string.Create(CultureInfo.InvariantCulture, $" #{entry.EntityId}");
        return entry.TypeName is null
            ? entry.Name + id + pos
            : entry.Name + id + " [" + entry.TypeName + "]" + pos;
    }

    private static string ModeName(DisplayMode mode) => mode switch
    {
        DisplayMode.LabelsAndMarkers => "labels and markers",
        DisplayMode.MarkersOnly => "markers only",
        _ => "hidden",
    };

    private static Rgba ColorFor(FactionType faction) => faction switch
    {
        FactionType.Hostile => HostileColor,
        FactionType.Friendly => FriendlyColor,
        _ => NeutralColor,
    };
}
