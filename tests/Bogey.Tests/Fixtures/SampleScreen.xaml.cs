using Bogey.Renderer.Ui.Controls;
using Bogey.Renderer.Ui.Xaml;

namespace Bogey.Tests.Fixtures;

[GenerateTypedNameReferences]
public sealed partial class SampleScreen : Control
{
    public SampleScreen()
    {
        BogeyXaml.Load(this);
        OkButton.OnPressed += () => Clicked = true;
    }

    public bool Clicked { get; private set; }

    public Control BarPanel => Bar;

    public Button Ok => OkButton;

    public Label CaptionLabel => Caption;
}
