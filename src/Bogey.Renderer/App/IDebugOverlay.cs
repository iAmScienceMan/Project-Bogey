using System.Collections.Generic;
using System.Numerics;
using Bogey.Renderer.Camera;
using Bogey.Renderer.Gl;
using Bogey.Renderer.Text;

namespace Bogey.Renderer.App;

public readonly record struct TeleportRequest(int EntityId, Vector2 Position);

public interface IDebugOverlay
{
    void Draw(PrimitiveBatch prims, TextBatch text, Camera2D camera, Vector2 viewport);

    string CycleDisplay();

    string ToggleSeekers();

    IReadOnlyList<string> DescribeMunitions();

    bool Teleport(int entityId, Vector2 worldPosition);

    TeleportRequest? PickOrPlace(Vector2 screenPx, Camera2D camera);
}
