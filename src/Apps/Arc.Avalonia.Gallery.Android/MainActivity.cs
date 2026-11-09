using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using AndroidX.Activity;
using Avalonia.Android;
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

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        LaunchSlug = Intent?.GetStringExtra("arc.slug");
        base.OnCreate(savedInstanceState);
    }

    protected override void OnPostCreate(Bundle? savedInstanceState)
    {
        base.OnPostCreate(savedInstanceState);

        // AppCompatActivity routes the system back gesture through OnBackPressedDispatcher, so overriding
        // OnBackPressed() alone is never called. Register a callback instead: close the compact drawer
        // first, otherwise finish the activity. Registered after the first create pass and null-guarded:
        // during OnCreate the dispatcher may not be wired yet.
        if (OnBackPressedDispatcher is { } dispatcher && this is AndroidX.Lifecycle.ILifecycleOwner owner)
        {
            dispatcher.AddCallback(owner, new ArcBackCallback(this));
        }
    }
}

/// <summary>Hardware/gesture back: close the mobile drawer when it is open, else let the app close.</summary>
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

