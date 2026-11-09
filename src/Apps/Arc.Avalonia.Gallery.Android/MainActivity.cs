using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;

namespace Arc.Avalonia.Gallery.Android;

/// <summary>
/// Phone host for the Gallery: Avalonia's single-view lifetime renders <c>GalleryShell(compact: true)</c>
/// (see Arc.Avalonia.Gallery.App.OnFrameworkInitializationCompleted), so the same pages, docs and touch
/// behaviour as the desktop app run on the device.
/// </summary>
[Activity(
    Label = "Arc Gallery",
    Theme = "@android:style/Theme.Material.Light.NoActionBar",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode | ConfigChanges.KeyboardHidden)]
public class MainActivity : AvaloniaMainActivity<Arc.Avalonia.Gallery.App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).UseAndroid();
}
