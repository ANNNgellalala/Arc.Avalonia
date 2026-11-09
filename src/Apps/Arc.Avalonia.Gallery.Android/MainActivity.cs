using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using AndroidX.Activity;
using Avalonia.Android;
using Arc.Avalonia.Gallery;

namespace Arc.Avalonia.Gallery.Android;

[Activity(
    Label = "Arc Gallery",
    Theme = "@style/ArcGalleryTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode
        | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.Density)]
public class MainActivity : AvaloniaMainActivity
{
    public static string? LaunchSlug { get; private set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        LaunchSlug = Intent?.GetStringExtra("arc.slug");
        base.OnCreate(savedInstanceState);
    }

    protected override void OnPostCreate(Bundle? savedInstanceState)
    {
        base.OnPostCreate(savedInstanceState);
        if (OnBackPressedDispatcher is { } dispatcher && this is AndroidX.Lifecycle.ILifecycleOwner owner)
        {
            dispatcher.AddCallback(owner, new ArcBackCallback(this));
        }
    }
}

[Application]
public class GalleryAndroidApplication : AvaloniaAndroidApplication<MobileApp>
{
    public static string? DataDirectory { get; private set; }

    protected GalleryAndroidApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    public override void OnCreate()
    {
        DataDirectory = FilesDir?.AbsolutePath;
        base.OnCreate();
    }
}

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
