using Avalonia;
using Avalonia.Markup.Xaml;

namespace SwipeInteractionProbe;

public partial class ProbeApp : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
}
