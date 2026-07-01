using System;
using System.Globalization;
using System.Numerics;
using System.Text;
using Bogey.Shared.Tracks;
using Bogey.View.Presentation;

namespace Bogey.View;

public sealed class ScopeRenderer
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private const int Rows = 25;
    private const int Cols = 51;
    private const int CenterRow = Rows / 2;
    private const int CenterCol = Cols / 2;

    private const float MinHalfKm = 1f;

    private const char Empty = ' ';

    public string Render(TrackPictureSnapshot snapshot)
    {
        Vector2 center = snapshot.OwnUnits.Count > 0 ? snapshot.OwnUnits[0].Position : Vector2.Zero;
        string centerName = snapshot.OwnUnits.Count > 0 ? snapshot.OwnUnits[0].Name : "origin";

        float halfKm = HalfExtentKm(snapshot, center);
        float kmPerCell = halfKm / CenterRow;

        char[,] grid = new char[Rows, Cols];
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                grid[r, c] = Empty;
            }
        }

        foreach (Track track in snapshot.Tracks)
        {
            Plot(grid, center, kmPerCell, track.EstimatedPosition, TrackPresentation.ScopeGlyph(track), overwrite: false);
        }

        for (int i = 0; i < snapshot.OwnUnits.Count; i++)
        {
            OwnUnitView unit = snapshot.OwnUnits[i];
            char glyph = i == 0 ? '@' : '+';
            Plot(grid, center, kmPerCell, unit.Position, glyph, overwrite: true);
        }

        return Compose(grid, snapshot.Tick, centerName, kmPerCell);
    }

    private static float HalfExtentKm(TrackPictureSnapshot snapshot, Vector2 center)
    {
        float half = MinHalfKm;

        foreach (Track track in snapshot.Tracks)
        {
            half = Math.Max(half, Chebyshev(track.EstimatedPosition - center));
        }

        foreach (OwnUnitView unit in snapshot.OwnUnits)
        {
            half = Math.Max(half, Chebyshev(unit.Position - center));
        }

        return half;
    }

    private static float Chebyshev(Vector2 delta) => Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y));

    private static void Plot(char[,] grid, Vector2 center, float kmPerCell, Vector2 world, char glyph, bool overwrite)
    {
        Vector2 delta = world - center;
        int col = CenterCol + (int)MathF.Round(delta.X / kmPerCell);
        int row = CenterRow - (int)MathF.Round(delta.Y / kmPerCell); 

        if (row < 0 || row >= Rows || col < 0 || col >= Cols)
        {
            return;
        }

        if (overwrite || grid[row, col] == Empty)
        {
            grid[row, col] = glyph;
        }
    }

    private static string Compose(char[,] grid, int tick, string centerName, float kmPerCell)
    {
        StringBuilder sb = new();

        sb.Append("== TICK ").Append(tick.ToString(Invariant))
          .Append("  |  scope centred on ").Append(centerName)
          .Append("  |  N up, ").Append(Round(kmPerCell)).AppendLine("km/cell ==");

        string border = "+" + new string('-', Cols) + "+";
        sb.AppendLine(border);
        for (int r = 0; r < Rows; r++)
        {
            sb.Append('|');
            for (int c = 0; c < Cols; c++)
            {
                sb.Append(grid[r, c]);
            }

            sb.Append('|').AppendLine();
        }

        sb.AppendLine(border);
        sb.AppendLine("  @ flagship  + own unit   ? unknown  a/s/u classifying  A/S/U identified  ~ stale");

        return sb.ToString();
    }

    private static string Round(float value) => ((int)MathF.Round(value)).ToString(Invariant);
}
