using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Arc.Avalonia.Gallery;
using Arc.Avalonia.Services;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Gallery.Android;

/// <summary>Minimal phone application: Arc theme + one compact GalleryShell, nothing from the desktop harness.</summary>
public class MobileApp : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        ArcThemeManager.Apply(this, ArcColorScheme.Light, ArcAccent.Neutral);
        ArcServiceProvider.Register<IFilePickerService>(StorageProviderFilePickerService.Instance);
        ArcServiceProvider.Register<IArcThemeService>(new ArcThemeService(this));

        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new GalleryShell(initialSlug: null, compact: true);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
