using Bogey.Shared.Commands;
using Bogey.Shared.Tracks;

namespace Bogey.Renderer.RealTime;

public enum SimSpeed
{
    Paused,
    Normal,
    Fast,
}

public interface ISimSession
{
    
    TrackPictureSnapshot? Previous { get; }

    
    TrackPictureSnapshot? Current { get; }

    
    float Alpha { get; }

    SimSpeed Speed { get; }

    
    int Tick { get; }

    void Advance(double realDeltaSeconds);

    void SetSpeed(SimSpeed speed);

    
    void Enqueue(SimCommand command);
}
