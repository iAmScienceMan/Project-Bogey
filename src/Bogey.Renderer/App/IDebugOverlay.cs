using System.Numerics;
using Bogey.Renderer.Camera;
using Bogey.Renderer.Gl;
using Bogey.Renderer.Text;

namespace Bogey.Renderer.App;

public interface IDebugOverlay
{
    void Draw(PrimitiveBatch prims, TextBatch text, Camera2D camera, Vector2 viewport);

    void CycleDisplay();

    bool HandleClick(Vector2 screenPx, Camera2D camera);
}
