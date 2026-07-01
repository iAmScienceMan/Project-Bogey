using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Bogey.Shared.Tracks;
using Bogey.View.Presentation;

namespace Bogey.View;

public sealed class TrackPictureRenderer
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public string Render(TrackPictureSnapshot snapshot)
    {
        StringBuilder sb = new();

        Vector2 reference = snapshot.OwnUnits.Count > 0 ? snapshot.OwnUnits[0].Position : Vector2.Zero;
        string referenceName = snapshot.OwnUnits.Count > 0 ? snapshot.OwnUnits[0].Name : "origin";

        sb.Append("== TICK ").Append(snapshot.Tick.ToString(Invariant))
          .Append("  |  picture relative to ").Append(referenceName)
          .AppendLine(" ==");

        AppendOwnUnits(sb, snapshot.OwnUnits);

        IReadOnlyList<Track> tracks = snapshot.Tracks
            .OrderBy(static t => t.TrackId)
            .ToList();

        if (tracks.Count == 0)
        {
            sb.AppendLine("  (no contacts)");
            return sb.ToString();
        }

        foreach (Track track in tracks)
        {
            AppendTrack(sb, track, reference);
        }

        return sb.ToString();
    }

    private static void AppendOwnUnits(StringBuilder sb, IReadOnlyList<OwnUnitView> ownUnits)
    {
        foreach (OwnUnitView unit in ownUnits)
        {
            sb.Append("  OWN  ").Append(unit.Name);
            if (unit.SensorRangeKm > 0f)
            {
                sb.Append("  (sensor ").Append(Round(unit.SensorRangeKm)).Append("km)");
            }

            sb.AppendLine();
        }
    }

    private static void AppendTrack(StringBuilder sb, Track track, Vector2 reference)
    {
        Vector2 delta = track.EstimatedPosition - reference;
        float range = delta.Length();
        int bearing = BearingDegrees(delta);

        sb.Append("  [T").Append(track.TrackId.ToString("D2", Invariant)).Append("]  ")
          .Append("BRG ").Append(bearing.ToString("D3", Invariant)).Append("deg  ")
          .Append("RNG ").Append(Pad(Round(range) + "km", 6)).Append("  ")
          .Append("+/-").Append(Pad(Round(track.PositionalErrorKm) + "km", 5)).Append("  ")
          .Append("conf ").Append(Pad(Percent(track.Confidence), 4)).Append("  ")
          .Append(Pad(TrackPresentation.DescribeGuess(track), 22)).Append("  ")
          .Append(TrackPresentation.StateLabel(track.State))
          .AppendLine();
    }

    
    private static int BearingDegrees(Vector2 delta)
    {
        if (delta.LengthSquared() < 1e-6f)
        {
            return 0;
        }

        double degrees = Math.Atan2(delta.X, delta.Y) * 180.0 / Math.PI;
        if (degrees < 0)
        {
            degrees += 360.0;
        }

        return (int)Math.Round(degrees) % 360;
    }

    private static string Percent(float value) => Round(value * 100f) + "%";

    private static string Round(float value) => ((int)Math.Round(value)).ToString(Invariant);

    private static string Pad(string text, int width) => text.PadRight(width);
}
