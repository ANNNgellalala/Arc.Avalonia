using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Sample.Login;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Tokens + accent palette; ArcTheme already brought in the Fluent baseline and component themes.
        ArcThemeManager.Apply(this, ArcColorScheme.Light, ArcAccent.Neutral);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}