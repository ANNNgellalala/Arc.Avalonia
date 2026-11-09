using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Avalonia;
using AndroidX.Activity;
using Avalonia.Android;
using Avalonia.Media;
using Arc.Avalonia.Gallery;

namespace Arc.Avalonia.Gallery.Android;

/// <summary>
/// Phone host for the Gallery. The Activity theme must descend from Theme.AppCompat (see
/// Resources/values/styles.xml): Avalonia.Android hosts its view through an AppCompatActivity.
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

    /// <summary>
    /// AppCompatActivity routes the system back gesture through OnBackPressedDispatcher (overriding
    /// OnBackPressed() is never called). The drawer closes first; otherwise the default back runs.
    /// </summary>
    protected override void OnPostCreate(Bundle? savedInstanceState)
    {
        base.OnPostCreate(savedInstanceState);
        if (OnBackPressedDispatcher is { } dispatcher && this is AndroidX.Lifecycle.ILifecycleOwner owner)
        {
            dispatcher.AddCallback(owner, new ArcBackCallback(this));
        }
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        LaunchSlug = Intent?.GetStringExtra("arc.slug");
        base.OnCreate(savedInstanceState);
    }
}

/// <summary>
/// Avalonia's Android application object, which performs the AppBuilder setup. Without this
/// [Application] class the default Android Application is used, Avalonia never initialises and the
/// activity dies with "AvaloniaView initialization has failed".
/// </summary>
[Application]
public class GalleryAndroidApplication : AvaloniaAndroidApplication<MobileApp>
{
    protected GalleryAndroidApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    /// <summary>
    /// System font by default (no brand font pinning), but Android's default family does not always cover
    /// CJK glyphs for body runs: fall back through the platform families so Chinese never renders as tofu.
    /// </summary>
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).With(new FontManagerOptions
        {
            FontFallbacks = new FontFallback[]
            {
                new() { FontFamily = new FontFamily("sans-serif") },
                new() { FontFamily = new FontFamily("sans-serif-condensed") },
            },
        });

    public override void OnCreate()
    {
        try
        {
            base.OnCreate();
        }
        catch (System.Exception exception)
        {
            global::Android.Util.Log.Error("ArcAppInit", "application create failed: " + exception);
            throw;
        }
    }
}
/// <summary>Hardware/gesture back: close the compact drawer when open, else let the app close.</summary>
internal sealed class ArcBackCallback : OnBackPressedCallback
{
    private readonly MainActivity _activity;

    public ArcBackCallback(MainActivity activity)
        : base(true)
    {
        _activity = activity;
    }

    public override void HandleOnBackPressed()
    {
        if (MobileApp.CurrentShell is { IsNavigationOpen: true } shell)
        {
            shell.CloseNavigation();
            return;
        }

        Remove();
        _activity.OnBackPressedDispatcher.OnBackPressed();
    }
}
