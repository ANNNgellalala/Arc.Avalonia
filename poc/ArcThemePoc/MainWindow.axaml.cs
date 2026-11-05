using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ArcThemePoc;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void SetThemeLine(string text)
    {
        this.FindControl<TextBlock>("ThemeLine")!.Text = text;
    }
}
