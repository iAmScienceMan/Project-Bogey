using Bogey.Shared.Commands;
using Bogey.Shared.Tracks;

namespace Bogey.Renderer.RealTime;

public interface ISimSession
{

    TrackPictureSnapshot? Previous { get; }


    TrackPictureSnapshot? Current { get; }


    float Alpha { get; }

    int Speed { get; }


    int Tick { get; }

    void Advance(double realDeltaSeconds);

    void SetSpeed(int speed);

    
    void Enqueue(SimCommand command);
}
