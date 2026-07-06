using Lattice.Renderer.Ui.Controls;
using Lattice.Renderer.Ui.Xaml;

namespace Content.Tests.Fixtures;

[GenerateTypedNameReferences]
public sealed partial class SampleScreen : Control
{
    public SampleScreen()
    {
        LatticeXaml.Load(this);
        OkButton.OnPressed += () => Clicked = true;
    }

    public bool Clicked { get; private set; }

    public Control BarPanel => Bar;

    public Button Ok => OkButton;

    public Label CaptionLabel => Caption;
}
