using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Avalonia.Media;
using Arc.Avalonia.Gallery;

namespace Arc.Avalonia.Gallery.Android;

/// <summary>
/// Phone host for the Gallery: Avalonia's single-view lifetime renders <c>GalleryShell(compact: true)</c>
/// (see <see cref="App.OnFrameworkInitializationCompleted"/>), so the same pages, docs and touch behaviour
/// as the desktop app run on the device.
/// </summary>
[Activity(
    Label = "Arc Gallery",
    Theme = "@style/ArcGalleryTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode
        | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.Density)]
public class MainActivity : AvaloniaMainActivity
{
}

/// <summary>
/// Wires the Avalonia app type into the Android application object. Inter is registered so the Arc font
/// tokens resolve, and Android's system sans-serif is installed as the CJK fallback (Inter has no CJK
/// glyphs, so Chinese text would otherwise render as tofu boxes on the device).
/// </summary>
[Application]
public class GalleryAndroidApplication : AvaloniaAndroidApplication<MobileApp>
{
    protected GalleryAndroidApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .With(new FontManagerOptions
            {
                FontFallbacks = new[]
                {
                    new FontFallback { FontFamily = new FontFamily("sans-serif") },
                },
            });
}


