using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Bogey.Renderer.Camera;
using Bogey.Renderer.Gl;
using Bogey.Renderer.RealTime;
using Bogey.Renderer.Text;
using Bogey.Shared.Tracks;
using Bogey.View.Presentation;

namespace Bogey.Renderer.Map;

public sealed class TacticalMapRenderer
{
    private const float FadeInSeconds = 0.25f;
    private const float FadeOutSeconds = 0.6f;
    private const float MarkerRadiusKm = 2.25f;
    private const float OwnMarkerKm = 2.25f;
    private const float SpriteSideKm = 7.5f;
    private const float MinMarkerPx = 4f;
    private const float MinSpritePx = 14f;

    private static readonly Rgba OwnColor = new(0.30f, 0.85f, 1.0f);
    private static readonly Rgba SelectColor = new(1.0f, 0.95f, 0.35f);
    private static readonly Rgba OrderColor = new(0.40f, 1.0f, 0.55f);
    private static readonly Rgba HudColor = new(0.80f, 0.86f, 0.92f);
    private static readonly Rgba GlyphColor = new(0.97f, 0.97f, 0.97f);

    private readonly Dictionary<int, TrackVisual> _visuals = new();
    private readonly Dictionary<int, Track> _prevScratch = new();

    public void Draw(
        ISimSession session,
        Camera2D camera,
        PrimitiveBatch prims,
        SpriteBatch sprites,
        EntitySprites entitySprites,
        TextBatch text,
        float frameDt,
        string? selectedUnit,
        IReadOnlyDictionary<string, Vector2> pendingOrders)
    {
        Vector2 viewport = camera.Viewport;

        TrackPictureSnapshot? current = session.Current;
        if (current is null)
        {
            text.Text(new Vector2(20f, 20f), 16f, HudColor, "WAITING FOR FIRST SNAPSHOT...");
            return;
        }

        UpdateTrackVisuals(current, session.Previous, session.Alpha, frameDt);

        
        foreach (int id in SortedVisualIds())
        {
            DrawTrack(prims, sprites, entitySprites, text, camera, _visuals[id]);
        }

        foreach ((OwnUnitView unit, Vector2 worldPos) in Interp.OwnUnits(session))
        {
            bool selected = string.Equals(unit.Name, selectedUnit, StringComparison.Ordinal);
            DrawOwnUnit(prims, sprites, entitySprites, text, camera, unit, worldPos, selected);

            if (pendingOrders.TryGetValue(unit.Name, out Vector2 destination))
            {
                DrawOrder(prims, camera, worldPos, destination);
            }
        }

        DrawHud(text, session, selectedUnit, viewport);
    }

    private void UpdateTrackVisuals(TrackPictureSnapshot current, TrackPictureSnapshot? previous, float alpha, float dt)
    {
        foreach (TrackVisual visual in _visuals.Values)
        {
            visual.SeenThisFrame = false;
        }

        _prevScratch.Clear();
        if (previous is not null)
        {
            foreach (Track track in previous.Tracks)
            {
                _prevScratch[track.TrackId] = track;
            }
        }

        foreach (Track track in current.Tracks)
        {
            Vector2 position = _prevScratch.TryGetValue(track.TrackId, out Track? prior)
                ? Vector2.Lerp(prior.EstimatedPosition, track.EstimatedPosition, alpha)
                : track.EstimatedPosition;

            if (!_visuals.TryGetValue(track.TrackId, out TrackVisual? visual))
            {
                visual = new TrackVisual { Fade = 0f };
                _visuals[track.TrackId] = visual;
            }

            visual.Position = position;
            visual.Latest = track;
            visual.SeenThisFrame = true;
            visual.Fade = MathF.Min(1f, visual.Fade + (dt / FadeInSeconds));
        }

        
        List<int>? expired = null;
        foreach ((int id, TrackVisual visual) in _visuals)
        {
            if (visual.SeenThisFrame)
            {
                continue;
            }

            visual.Fade -= dt / FadeOutSeconds;
            if (visual.Fade <= 0f)
            {
                (expired ??= new List<int>()).Add(id);
            }
        }

        if (expired is not null)
        {
            foreach (int id in expired)
            {
                _visuals.Remove(id);
            }
        }
    }

    private List<int> SortedVisualIds()
    {
        List<int> ids = new(_visuals.Keys);
        ids.Sort();
        return ids;
    }

    private static void DrawTrack(PrimitiveBatch prims, SpriteBatch sprites, EntitySprites entitySprites, TextBatch text, Camera2D camera, TrackVisual visual)
    {
        Track track = visual.Latest!;
        MarkerStyle style = TrackPresentation.StyleFor(track);
        Rgba baseColor = ColorFor(style);
        float confidence = Math.Clamp(track.Confidence, 0f, 1f);
        float opacity = (0.35f + (0.65f * confidence)) * visual.Fade;

        Vector2 screen = camera.WorldToScreen(visual.Position);
        float markerPx = MathF.Max(MinMarkerPx, camera.KmToPixels(MarkerRadiusKm));

        float errorPx = MathF.Max(markerPx * 0.7f, camera.KmToPixels(track.PositionalErrorKm));
        prims.Ring(screen, errorPx, baseColor.WithAlpha(0.22f * visual.Fade), 40);

        Texture? sprite = entitySprites.ForTrack(track);
        float topOffsetPx;
        if (sprite is not null)
        {
            float sidePx = MathF.Max(MinSpritePx, camera.KmToPixels(SpriteSideKm));
            float styleAlpha = style switch
            {
                MarkerStyle.Stale => 0.7f,
                MarkerStyle.Dropped => 0.5f,
                _ => 1f,
            };
            sprites.Draw(sprite, screen, sidePx, new Rgba(1f, 1f, 1f, opacity * styleAlpha));
            topOffsetPx = sidePx * 0.5f;
        }
        else
        {
            DrawMarkerShape(prims, screen, markerPx, style, baseColor, opacity);

            char glyph = TrackPresentation.ScopeGlyph(track);
            float glyphSize = MathF.Max(10f, markerPx * 1.3f);
            text.Text(screen - new Vector2(glyphSize * 0.5f, glyphSize * 0.5f), glyphSize,
                GlyphColor.WithAlpha(MathF.Min(1f, opacity + 0.2f)), glyph.ToString());
            topOffsetPx = markerPx;
        }

        DrawConfidenceBar(prims, screen - new Vector2(0f, topOffsetPx + 9f), confidence, baseColor, visual.Fade);
    }

    private static void DrawMarkerShape(PrimitiveBatch prims, Vector2 screen, float radiusPx, MarkerStyle style, Rgba baseColor, float opacity)
    {
        switch (style)
        {
            case MarkerStyle.Identified:
                prims.FilledCircle(screen, radiusPx, baseColor.WithAlpha(opacity));
                break;
            case MarkerStyle.Classifying:
                prims.Ring(screen, radiusPx, baseColor.WithAlpha(opacity));
                break;
            case MarkerStyle.Stale:
                prims.DashedRing(screen, radiusPx, baseColor.WithAlpha(opacity * 0.7f));
                break;
            case MarkerStyle.Dropped:
                prims.DashedRing(screen, radiusPx, baseColor.WithAlpha(opacity * 0.5f));
                break;
            default:
                prims.DashedRing(screen, radiusPx, baseColor.WithAlpha(opacity));
                break;
        }
    }

    private static void DrawConfidenceBar(PrimitiveBatch prims, Vector2 center, float confidence, Rgba color, float fade)
    {
        const float width = 22f;
        const float height = 3f;
        Vector2 min = center - new Vector2(width * 0.5f, height * 0.5f);
        Vector2 max = center + new Vector2(width * 0.5f, height * 0.5f);
        prims.FilledQuad(min, max, new Rgba(0.15f, 0.15f, 0.18f, 0.6f * fade));

        Vector2 fillMax = new(min.X + (width * Math.Clamp(confidence, 0f, 1f)), max.Y);
        prims.FilledQuad(min, fillMax, color.WithAlpha(0.85f * fade));
    }

    private static void DrawOwnUnit(PrimitiveBatch prims, SpriteBatch sprites, EntitySprites entitySprites, TextBatch text, Camera2D camera, OwnUnitView unit, Vector2 worldPos, bool selected)
    {
        Vector2 screen = camera.WorldToScreen(worldPos);

        if (unit.SensorRangeKm > 0f)
        {
            prims.Ring(screen, camera.KmToPixels(unit.SensorRangeKm), OwnColor.WithAlpha(0.13f), 72);
        }

        Texture? sprite = entitySprites.OwnUnit;
        float halfExtentPx;
        if (sprite is not null)
        {
            float sidePx = MathF.Max(MinSpritePx, camera.KmToPixels(SpriteSideKm));
            sprites.Draw(sprite, screen, sidePx, new Rgba(1f, 1f, 1f, 1f));
            halfExtentPx = sidePx * 0.5f;
        }
        else
        {
            float s = MathF.Max(MinMarkerPx, camera.KmToPixels(OwnMarkerKm));
            Vector2 top = screen + new Vector2(0f, -s);
            Vector2 right = screen + new Vector2(s, 0f);
            Vector2 bottom = screen + new Vector2(0f, s);
            Vector2 left = screen + new Vector2(-s, 0f);
            prims.FilledTriangle(top, right, bottom, OwnColor);
            prims.FilledTriangle(top, bottom, left, OwnColor);
            halfExtentPx = s;
        }

        if (selected)
        {
            prims.Ring(screen, halfExtentPx + 6f, SelectColor, 40);
        }

        text.Text(screen + new Vector2(halfExtentPx + 4f, -halfExtentPx), 12f, OwnColor, unit.Name);
    }

    private static void DrawOrder(PrimitiveBatch prims, Camera2D camera, Vector2 fromWorld, Vector2 destWorld)
    {
        Vector2 from = camera.WorldToScreen(fromWorld);
        Vector2 dest = camera.WorldToScreen(destWorld);
        prims.Line(from, dest, OrderColor.WithAlpha(0.7f));
        prims.Ring(dest, 6f, OrderColor, 20);
        prims.Line(dest - new Vector2(7f, 0f), dest + new Vector2(7f, 0f), OrderColor);
        prims.Line(dest - new Vector2(0f, 7f), dest + new Vector2(0f, 7f), OrderColor);
    }

    private static void DrawHud(TextBatch text, ISimSession session, string? selectedUnit, Vector2 viewport)
    {
        string speed = session.Speed switch
        {
            SimSpeed.Paused => "PAUSED",
            SimSpeed.Normal => "1x",
            SimSpeed.Fast => "FAST",
            _ => "?",
        };

        text.Text(new Vector2(16f, 14f), 15f, HudColor,
            "TICK " + session.Tick.ToString(CultureInfo.InvariantCulture));
        text.Text(new Vector2(16f, 34f), 15f, HudColor, "SPEED " + speed);
        text.Text(new Vector2(16f, 54f), 15f, HudColor, "SELECTED " + (selectedUnit ?? "--"));

        text.Text(new Vector2(16f, viewport.Y - 22f), 11f, HudColor.WithAlpha(0.75f),
            "L-CLICK unit:select  L-CLICK map:move  DRAG:pan  SCROLL:zoom  SPACE:pause  1/2:speed  ESC:quit");
    }

    private static Rgba ColorFor(MarkerStyle style) => style switch
    {
        MarkerStyle.Unknown => new Rgba(0.85f, 0.75f, 0.20f),
        MarkerStyle.Classifying => new Rgba(0.95f, 0.60f, 0.12f),
        MarkerStyle.Identified => new Rgba(0.95f, 0.27f, 0.22f),
        MarkerStyle.Stale => new Rgba(0.55f, 0.55f, 0.62f),
        MarkerStyle.Dropped => new Rgba(0.45f, 0.28f, 0.28f),
        _ => new Rgba(0.85f, 0.75f, 0.20f),
    };
}
