using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Avalonia;
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
    Theme = "@style/ArcGalleryTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode
        | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.Density)]
public class MainActivity : AvaloniaMainActivity
{
    /// <summary>Slug passed with `--es arc.slug &lt;slug&gt;`; lets device tests open one page directly.</summary>
    public static string? LaunchSlug { get; private set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        LaunchSlug = Intent?.GetStringExtra("arc.slug");
        base.OnCreate(savedInstanceState);
    }
}

/// <summary>Wires the Avalonia app type into the Android application object.</summary>
[Application]
public class GalleryAndroidApplication : AvaloniaAndroidApplication<MobileApp>
{
    protected GalleryAndroidApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder);
}




