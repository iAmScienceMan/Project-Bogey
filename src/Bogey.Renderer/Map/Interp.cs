using System.Collections.Generic;
using System.Numerics;
using Bogey.Renderer.RealTime;
using Bogey.Shared.Tracks;

namespace Bogey.Renderer.Map;

internal static class Interp
{
    
    public static List<(OwnUnitView Unit, Vector2 Position)> OwnUnits(ISimSession session)
    {
        List<(OwnUnitView, Vector2)> result = new();
        TrackPictureSnapshot? current = session.Current;
        if (current is null)
        {
            return result;
        }

        foreach (OwnUnitView unit in current.OwnUnits)
        {
            result.Add((unit, OwnUnitPosition(session, unit.Name, unit.Position)));
        }

        return result;
    }

    public static Vector2 OwnUnitPosition(ISimSession session, string name, Vector2 currentPosition)
    {
        TrackPictureSnapshot? previous = session.Previous;
        if (previous is null)
        {
            return currentPosition;
        }

        foreach (OwnUnitView prior in previous.OwnUnits)
        {
            if (prior.Name == name)
            {
                return Vector2.Lerp(prior.Position, currentPosition, session.Alpha);
            }
        }

        return currentPosition;
    }

    public static Vector2 MunitionPosition(ISimSession session, int id, Vector2 currentPosition)
    {
        TrackPictureSnapshot? previous = session.Previous;
        if (previous is null)
        {
            return currentPosition;
        }

        foreach (MunitionView prior in previous.Munitions)
        {
            if (prior.Id == id)
            {
                return Vector2.Lerp(prior.Position, currentPosition, session.Alpha);
            }
        }

        return currentPosition;
    }
}
