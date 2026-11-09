using System;
using System.IO;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Arc.Avalonia.Gallery;
using Arc.Avalonia.Services;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Gallery.Android;

/// <summary>Minimal phone application: Arc theme + one compact GalleryShell, nothing from the desktop harness.</summary>
public class MobileApp : Application
{
    /// <summary>The live shell, so the Android host can route the hardware back button to the drawer.</summary>
    public static GalleryShell? CurrentShell { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        ArcThemeManager.Apply(this, ArcColorScheme.Light, ArcAccent.Neutral);
        ArcServiceProvider.Register<IFilePickerService>(StorageProviderFilePickerService.Instance);
        ArcServiceProvider.Register<IArcThemeService>(new ArcThemeService(this));

        // Android ships assemblies inside the APK bundle (no loose .dll files on disk), so reflection-based
        // page/doc discovery cannot find them by path: load the documentation assembly explicitly first.
        _ = Assembly.Load(new AssemblyName("Arc.Avalonia.Docs"));

        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            var shell = new GalleryShell(ResolveLaunchSlug(), compact: true);
            CurrentShell = shell;
            singleView.MainView = shell;
            shell.AttachedToVisualTree += (_, _) => ApplySafeArea(shell);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Device-test hook: <c>run-as &lt;pkg&gt; sh -c 'echo &lt;slug&gt; &gt; files/slug.txt'</c> opens one page
    /// directly. (Avalonia creates the app before the activity intent is available, so an intent extra
    /// cannot be read here.)
    /// </summary>
    private static string? ResolveLaunchSlug()
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "slug.txt");
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Edge-to-edge Android windows overlay the status bar; give the compact header the platform safe-area
    /// top inset (28 DIP fallback for common densities) so the app bar never sits under the clock.
    /// </summary>
    private static void ApplySafeArea(Control shell)
    {
        var top = TopLevel.GetTopLevel(shell);
        var padding = top?.InsetsManager?.SafeAreaPadding ?? default;
        var topGap = padding.Top > 0 ? padding.Top : 28;
        var bottomGap = padding.Bottom > 0 ? padding.Bottom : 12;
        shell.Margin = new Thickness(0, topGap, 0, bottomGap);
    }
}
