using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Avalonia.Android;
using Arc.Avalonia.Gallery;

namespace Arc.Avalonia.Gallery.Android;

/// <summary>
/// Phone host for the Gallery: Avalonia's single-view lifetime renders <c>GalleryShell(compact: true)</c>
/// (see <see cref="App.OnFrameworkInitializationCompleted"/>), so the same pages, docs and touch behaviour
/// as the desktop app run on the device.
/// </summary>
[Activity(
    Label = "Arc Gallery",
    Theme = "@android:style/Theme.Material.Light.NoActionBar",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode
        | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.Density)]
public class MainActivity : AvaloniaMainActivity
{
}

/// <summary>Wires the Avalonia app type into the Android application object.</summary>
[Application]
public class GalleryAndroidApplication : AvaloniaAndroidApplication<App>
{
    protected GalleryAndroidApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }
}
