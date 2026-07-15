using Lattice.Renderer.Gl;
using Lattice.Renderer.Text;
using Lattice.Renderer.Ui;
using Lattice.Renderer.Ui.Controls;

namespace Content.Renderer.Ui.Screens;

public sealed class MenuBackdrop : Control
{
    public override void Draw(PrimitiveBatch prims, TextBatch text)
    {
        if (!Visible)
        {
            return;
        }

        MenuBackground.Draw(prims, Bounds);
    }
}
