using System;
using System.Collections.Generic;
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
    private const float MunitionSideKm = 4.0f;
    private const float MinMarkerPx = 4f;
    private const float MinSpritePx = 14f;
    private const float MinMunitionPx = 10f;

    private static readonly Rgba OwnColor = new(0.30f, 0.85f, 1.0f);
    private static readonly Rgba LockColor = new(1.0f, 0.40f, 0.30f);
    private static readonly Rgba HullBackColor = new(0.15f, 0.15f, 0.18f, 0.6f);
    private static readonly Rgba MunitionColor = new(0.55f, 0.90f, 1.0f);
    private static readonly Rgba MunitionLockedColor = new(1.0f, 0.80f, 0.40f);
    private static readonly Rgba SelectColor = new(1.0f, 0.95f, 0.35f);
    private static readonly Rgba OrderColor = new(0.40f, 1.0f, 0.55f);
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
        int? selectedTarget,
        IReadOnlyDictionary<string, Vector2> pendingOrders)
    {
        TrackPictureSnapshot? current = session.Current;
        if (current is null)
        {
            return;
        }

        UpdateTrackVisuals(current, session.Previous, session.Alpha, frameDt);


        foreach (int id in SortedVisualIds())
        {
            DrawTrack(prims, sprites, entitySprites, text, camera, _visuals[id]);
        }

        if (selectedTarget is { } targetId && _visuals.TryGetValue(targetId, out TrackVisual? targetVisual))
        {
            DrawTargetReticle(prims, camera, targetVisual.Position);
        }

        foreach ((OwnUnitView unit, Vector2 worldPos) in Interp.OwnUnits(session))
        {
            bool selected = string.Equals(unit.Name, selectedUnit, StringComparison.Ordinal);
            DrawOwnUnit(prims, sprites, entitySprites, text, camera, unit, worldPos, selected);

            if (selected && unit.LockedTrackId is { } lockedId && _visuals.TryGetValue(lockedId, out TrackVisual? lockedVisual))
            {
                DrawLockReticle(prims, text, camera, lockedVisual.Position);
            }

            if (pendingOrders.TryGetValue(unit.Name, out Vector2 destination))
            {
                DrawOrder(prims, camera, worldPos, destination);
            }
        }

        foreach (MunitionView munition in current.Munitions)
        {
            Vector2 world = Interp.MunitionPosition(session, munition.Id, munition.Position);
            DrawMunition(prims, sprites, entitySprites, camera, munition, world);
        }
    }

    private static void DrawMunition(PrimitiveBatch prims, SpriteBatch sprites, EntitySprites entitySprites, Camera2D camera, MunitionView munition, Vector2 world)
    {
        Vector2 screen = camera.WorldToScreen(world);
        Rgba color = munition.Locked ? MunitionLockedColor : MunitionColor;
        float rotation = -munition.HeadingRadians;

        Texture? sprite = entitySprites.Munition;
        if (sprite is not null)
        {
            float sidePx = MathF.Max(MinMunitionPx, camera.KmToPixels(MunitionSideKm));
            sprites.Draw(sprite, screen, new Vector2(sidePx, sidePx), color, rotation);
        }
        else
        {
            float length = MathF.Max(MinMunitionPx, camera.KmToPixels(MunitionSideKm));
            Vector2 dir = new(MathF.Cos(rotation), MathF.Sin(rotation));
            prims.Line(screen - (dir * length * 0.5f), screen + (dir * length * 0.5f), color);
        }
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

        if (unit.HullMax > 0f)
        {
            DrawHullBar(prims, screen + new Vector2(0f, halfExtentPx + 8f), unit.HullCurrent / unit.HullMax);
        }

        text.Text(screen + new Vector2(halfExtentPx + 4f, -halfExtentPx), 12f, OwnColor, unit.Name);
    }

    private static void DrawHullBar(PrimitiveBatch prims, Vector2 center, float fraction)
    {
        const float width = 22f;
        const float height = 3f;
        fraction = Math.Clamp(fraction, 0f, 1f);

        Vector2 min = center - new Vector2(width * 0.5f, height * 0.5f);
        Vector2 max = center + new Vector2(width * 0.5f, height * 0.5f);
        prims.FilledQuad(min, max, HullBackColor);

        Rgba fill = new(1f - fraction, fraction * 0.9f, 0.15f, 0.9f);
        prims.FilledQuad(min, new Vector2(min.X + (width * fraction), max.Y), fill);
    }

    private static void DrawLockReticle(PrimitiveBatch prims, TextBatch text, Camera2D camera, Vector2 world)
    {
        Vector2 screen = camera.WorldToScreen(world);
        float r = 19f;
        prims.Ring(screen, r, LockColor, 28);
        text.Text(screen + new Vector2(r + 4f, -6f), 11f, LockColor, "LOCK");
    }

    private static void DrawTargetReticle(PrimitiveBatch prims, Camera2D camera, Vector2 world)
    {
        Vector2 screen = camera.WorldToScreen(world);
        float r = 14f;
        prims.Ring(screen, r, SelectColor, 28);
        prims.Line(screen + new Vector2(-r - 4f, 0f), screen + new Vector2(-r + 4f, 0f), SelectColor);
        prims.Line(screen + new Vector2(r - 4f, 0f), screen + new Vector2(r + 4f, 0f), SelectColor);
        prims.Line(screen + new Vector2(0f, -r - 4f), screen + new Vector2(0f, -r + 4f), SelectColor);
        prims.Line(screen + new Vector2(0f, r - 4f), screen + new Vector2(0f, r + 4f), SelectColor);
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
