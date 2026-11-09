using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
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
}
