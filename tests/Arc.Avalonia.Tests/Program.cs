using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Arc.Avalonia.DataDisplay.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feature.DataGrid;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Services;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Verify;

/// <summary>
/// Headless verification for the bits that must not depend on a desktop: the OKLCH colour engine,
/// the motion tokens, and the public catalog inventory. Kept as a plain console app because the
/// sandbox this repository is developed in cannot start the vstest host process.
/// </summary>
public static class Program
{
    private static readonly List<string> Failures = new();
    private static int _checks;

    public static int Main()
    {
        ColourChecks();
        MotionChecks();
        CatalogChecks();
        ThemeChecks();
        ListChecks();

        Console.WriteLine($"VERIFY: checks={_checks} failures={Failures.Count}");
        foreach (var failure in Failures)
        {
            Console.WriteLine("  FAIL: " + failure);
        }

        return Failures.Count == 0 ? 0 : 1;
    }

    private static void ColourChecks()
    {
        var hex = ArcColor.Hex("#1e90ff");
        Check("hex channels", hex.R == 0x1e && hex.G == 0x90 && hex.B == 0xff && hex.A == 255);

        var alpha = ArcColor.Hex("#80112233");
        Check("hex alpha prefix", alpha.A is >= 127 and <= 129 && alpha.R == 0x11 && alpha.G == 0x22 && alpha.B == 0x33);

        var colour = ArcColor.Oklch(0.6, 0.08, 240, 0.75);
        var (l, c, _) = ArcColor.ToOklch(colour);
        Check("oklch round trip", Math.Abs(l - 0.6) < 0.01 && Math.Abs(c - 0.08) < 0.01 && colour.A is >= 191 and <= 192);

        var black = Colors.Black;
        var white = Colors.White;
        var mixZero = ArcColor.Mix(black, white, 0);
        var mixOne = ArcColor.Mix(black, white, 1);
        var mixHalf = ArcColor.Mix(black, white, 0.5);
        Check("mix keeps endpoints",
            Math.Abs(mixZero.R - black.R) <= 1 && Math.Abs(mixZero.G - black.G) <= 1 && Math.Abs(mixZero.B - black.B) <= 1 &&
            Math.Abs(mixOne.R - white.R) <= 1 && Math.Abs(mixOne.G - white.G) <= 1 && Math.Abs(mixOne.B - white.B) <= 1 &&
            mixHalf.R is >= 90 and <= 210);

        var translucent = ArcColor.WithAlpha(hex, 0.4);
        Check("with alpha keeps channels",
            translucent.R == hex.R && translucent.G == hex.G && translucent.B == hex.B &&
            translucent.A is >= 101 and <= 103);

        var tokenFields = typeof(ArcTokens).GetFields().Count(f => f.IsLiteral && f.FieldType == typeof(string));
        // 82 declared constants must all materialise in ArcPalette.Build for every scheme/accent combo
        // (ControlThumbShadow was the one that used to be computed but never registered), and the
        // injectable IArcResourceHost must feed ArcResources without a live Application.
        Check("token count, palette coverage and resource host",
            tokenFields == 82 && PaletteCoversAllTokens() && ResourceHostChecks());
        Check("token keys",
            ArcTokens.Background == "ArcBackground" &&
            ArcTokens.Surface == "ArcSurface" &&
            ArcTokens.Border == "ArcBorder" &&
            ArcTokens.Accent == "ArcAccent" &&
            ArcTokens.Foreground == "ArcForeground" &&
            ArcTokens.RadiusSurface == "ArcRadiusSurface");
    }

    private static void MotionChecks()
    {
        var easings = new (string Name, Easing Easing)[]
        {
            ("Enter", ArcMotion.EaseEnter),
            ("Standard", ArcMotion.EaseStandard),
            ("Exit", ArcMotion.EaseExit),
            ("InOut", ArcMotion.EaseInOut),
            ("SpringSmooth", ArcMotion.SpringSmooth),
            ("SpringSnappy", ArcMotion.SpringSnappy),
            ("SpringMorph", ArcMotion.SpringMorph),
        };

        foreach (var (name, easing) in easings)
        {
            Check($"{name} pinned at 0", Math.Abs(easing.Ease(0)) < 1e-6);
            Check($"{name} pinned at 1", Math.Abs(easing.Ease(1) - 1) < 1e-6);
        }

        var previous = 0.0;
        var monotonic = true;
        for (var i = 0; i <= 100; i++)
        {
            var value = ArcMotion.SpringSmooth.Ease(i / 100.0);
            monotonic &= value >= 0 && value <= 1 && value >= previous - 1e-9;
            previous = value;
        }

        Check("spring smooth never overshoots", monotonic);
        Check("duration order",
            ArcMotion.Instant < ArcMotion.Fast &&
            ArcMotion.Fast < ArcMotion.Standard &&
            ArcMotion.Standard < ArcMotion.Considered);
        Check("spring durations",
            ArcMotion.SpringSmoothDuration == TimeSpan.FromSeconds(0.40) &&
            ArcMotion.SpringSnappyDuration == TimeSpan.FromSeconds(0.26) &&
            ArcMotion.SpringMorphDuration == TimeSpan.FromSeconds(0.42));
    }

    private static void CatalogChecks()
    {
        var items = ArcCatalog.AllItems;
        Check("catalog total", items.Count == 294);
        Check("catalog components", ArcCatalog.ComponentGroups.Sum(g => g.Items.Count) == 128);
        Check("catalog blocks", ArcCatalog.BlockGroups.Sum(g => g.Items.Count) == 111);
        Check("catalog specials", ArcCatalog.SpecialGroups.Sum(g => g.Items.Count) == 55);
        Check("catalog component categories", ArcCatalog.ComponentGroups.Count == 23);
        Check("catalog special groups", ArcCatalog.SpecialGroups.Count == 5 && ArcCatalog.SpecialGroups.All(g => g.Items.Count == 11));
        Check("catalog block groups",
            ArcCatalog.BlockGroups.Select(g => g.Items.Count).SequenceEqual(new[] { 18, 18, 21, 16, 20, 18 }));
        Check("catalog slugs unique", items.Select(i => i.Slug).Distinct(StringComparer.OrdinalIgnoreCase).Count() == items.Count);
        Check("catalog descriptions", items.All(i => !string.IsNullOrWhiteSpace(i.Description)));

        var button = ArcCatalog.Get("button");
        var glass = ArcCatalog.Get("glass-menu");
        var siteHeader = ArcCatalog.Get("site-header");
        Check("catalog resolves known items",
            button is { Category: "Buttons" } &&
            glass is { Kind: "special" } &&
            siteHeader is { Kind: "block" });
    }

    /// <summary>
    /// M1-T3: tokens live in ThemeDictionaries (Light/Dark) and the 47 Fluent keys are covered per variant,
    /// so both the token surface and the bare-Fluent reskin follow the active theme and accent.
    /// </summary>
    private static void ThemeChecks()
    {
        var tokenKeys = TokenKeys();
        var neutral = ArcPalette.BuildThemeDictionaries(ArcAccent.Neutral);

        Check("token dictionaries have Light and Dark",
            neutral.ThemeDictionaries.ContainsKey(ThemeVariant.Light) &&
            neutral.ThemeDictionaries.ContainsKey(ThemeVariant.Dark));
        Check("token dictionaries cover every token (both variants)",
            CoversAllKeys(neutral, tokenKeys));

        var neutralLight = Variant(neutral, ThemeVariant.Light);
        var neutralDark = Variant(neutral, ThemeVariant.Dark);
        Check("token dictionaries differ per variant",
            BrushColor(neutralLight, ArcTokens.Background) != BrushColor(neutralDark, ArcTokens.Background) &&
            BrushColor(neutralLight, ArcTokens.Background) is not null);

        // At least two accents must visibly swap the accent token in both schemes (M1-T3 DoD).
        foreach (var accent in new[] { ArcAccent.Violet, ArcAccent.Rose })
        {
            var themed = ArcPalette.BuildThemeDictionaries(accent);
            Check($"accent {accent} swaps the accent token (both variants)",
                BrushColor(Variant(themed, ThemeVariant.Light), ArcTokens.Accent) != BrushColor(neutralLight, ArcTokens.Accent) &&
                BrushColor(Variant(themed, ThemeVariant.Dark), ArcTokens.Accent) != BrushColor(neutralDark, ArcTokens.Accent));
        }

        Check("Fluent key table has 47 entries", ArcFluentKeys.All.Count == 47);

        var overrides = ArcFluentOverrides.Build(ArcAccent.Neutral);
        Check("Fluent overrides have Light and Dark",
            overrides.ThemeDictionaries.ContainsKey(ThemeVariant.Light) &&
            overrides.ThemeDictionaries.ContainsKey(ThemeVariant.Dark));
        Check("Fluent overrides cover every declared key (both variants)",
            CoversAllKeys(overrides, ArcFluentKeys.All));

        var lightOverrides = Variant(overrides, ThemeVariant.Light);
        var darkOverrides = Variant(overrides, ThemeVariant.Dark);
        Check("Fluent overrides differ per variant",
            BrushColor(lightOverrides, ArcFluentKeys.TextControlBackground) !=
            BrushColor(darkOverrides, ArcFluentKeys.TextControlBackground));

        var expectedRadius = ArcPalette.Build(ArcColorScheme.Light, ArcAccent.Neutral)[ArcTokens.RadiusControl];
        Check("ControlCornerRadius follows the Arc radius token",
            lightOverrides[ArcFluentKeys.ControlCornerRadius] is CornerRadius corner &&
            expectedRadius is CornerRadius expected &&
            corner == expected);

        var violetOverrides = Variant(ArcFluentOverrides.Build(ArcAccent.Violet), ThemeVariant.Light);
        Check("accent reaches the Fluent overrides",
            ColorOf(lightOverrides[ArcFluentKeys.TextControlSelectionHighlightColor]) !=
            ColorOf(violetOverrides[ArcFluentKeys.TextControlSelectionHighlightColor]));

        Check("accent colours differ between schemes",
            ArcPalette.AccentColor(ArcColorScheme.Light, ArcAccent.Neutral) !=
            ArcPalette.AccentColor(ArcColorScheme.Dark, ArcAccent.Neutral));
    }

    private static IReadOnlyList<string> TokenKeys() => typeof(ArcTokens)
        .GetFields()
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToArray();

    private static ResourceDictionary Variant(ResourceDictionary themeAware, ThemeVariant variant) =>
        (ResourceDictionary)themeAware.ThemeDictionaries[variant];

    private static bool CoversAllKeys(ResourceDictionary themeAware, IReadOnlyList<string> keys)
    {
        foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var dictionary = Variant(themeAware, variant);
            foreach (var key in keys)
            {
                if (!dictionary.ContainsKey(key))
                {
                    Console.WriteLine($"  MISSING KEY: {key} [{variant}]");
                    return false;
                }
            }
        }

        return true;
    }

    private static Color? BrushColor(ResourceDictionary dictionary, string key) =>
        dictionary.TryGetValue(key, out var value) && value is ISolidColorBrush brush ? brush.Color : null;

    private static Color? ColorOf(object? value) =>
        value is Color color ? color : value is ISolidColorBrush brush ? brush.Color : null;

    /// <summary>Every ArcTokens constant must be registered for Light/Dark x every accent (16 palettes).</summary>
    private static bool PaletteCoversAllTokens()
    {
        var keys = typeof(ArcTokens)
            .GetFields()
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();

        if (keys.Length < 82)
        {
            Console.WriteLine($"  MISSING TOKENS: only {keys.Length} declared, expected at least 82");
            return false;
        }

        foreach (var scheme in new[] { ArcColorScheme.Light, ArcColorScheme.Dark })
        {
            foreach (var accent in Enum.GetValues<ArcAccent>())
            {
                var palette = ArcPalette.Build(scheme, accent);
                foreach (var key in keys)
                {
                    if (!palette.ContainsKey(key))
                    {
                        Console.WriteLine($"  MISSING TOKEN: {key} in ArcPalette.Build({scheme}, {accent})");
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>ArcResources must consult the injected host (default host = Application.Current).</summary>
    private static bool ResourceHostChecks()
    {
        var host = new FakeResourceHost();
        host.Values[ArcTokens.Background] = Brushes.Red;
        host.Values[ArcTokens.TextBase] = 42.0;
        host.Values[ArcTokens.RadiusControl] = new CornerRadius(7);
        host.Values[ArcTokens.DurationFast] = TimeSpan.FromSeconds(1.5);
        host.Values[ArcTokens.ShadowFloating] = new BoxShadows(new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 2,
            Blur = 4,
            Color = Colors.Black
        });
        host.DarkOnly[ArcTokens.Series1] = Brushes.Blue;

        ArcResources.UseHost(host);
        try
        {
            var ok = true;

            void Expect(string label, bool passed, object? actual = null)
            {
                if (!passed)
                {
                    ok = false;
                    Console.WriteLine($"  RESOURCE HOST: {label} failed (actual={actual})");
                }
            }

            Expect("Brush", ReferenceEquals(ArcResources.Brush(ArcTokens.Background), Brushes.Red),
                ArcResources.Brush(ArcTokens.Background));
            Expect("Color", ArcResources.Color(ArcTokens.Background) == Colors.Red,
                ArcResources.Color(ArcTokens.Background));
            Expect("Double", ArcResources.Double(ArcTokens.TextBase) == 42.0,
                ArcResources.Double(ArcTokens.TextBase));
            Expect("Radius", ArcResources.Radius(ArcTokens.RadiusControl) == new CornerRadius(7),
                ArcResources.Radius(ArcTokens.RadiusControl));
            Expect("Duration", ArcResources.Duration(ArcTokens.DurationFast) == TimeSpan.FromSeconds(1.5),
                ArcResources.Duration(ArcTokens.DurationFast));
            Expect("Shadows", ArcResources.Shadows(ArcTokens.ShadowFloating).Count == 1,
                ArcResources.Shadows(ArcTokens.ShadowFloating).Count);
            // Variant-aware lookups reach ThemeDictionaries-style entries (ADR 0003 §4.2). The untargeted
            // overload must follow the host's ActualThemeVariant (M1-T3 fix) instead of silently missing
            // every theme-dictionary token.
            Expect("untargeted lookup follows the active variant",
                ReferenceEquals(ArcResources.Brush(ArcTokens.Series1), Brushes.Blue),
                ArcResources.Brush(ArcTokens.Series1));
            Expect("explicit light lookup skips dark-only entries",
                ReferenceEquals(ArcResources.Brush(ArcTokens.Series1, ThemeVariant.Light), Brushes.Transparent),
                ArcResources.Brush(ArcTokens.Series1, ThemeVariant.Light));
            Expect("variant lookup uses theme-dictionary entries",
                ReferenceEquals(ArcResources.Brush(ArcTokens.Series1, ThemeVariant.Dark), Brushes.Blue),
                ArcResources.Brush(ArcTokens.Series1, ThemeVariant.Dark));

            return ok;
        }
        finally
        {
            ArcResources.UseHost(null);
        }
    }

    private sealed class FakeResourceHost : IArcResourceHost
    {
        public Dictionary<string, object?> Values { get; } = new();
        public Dictionary<string, object?> DarkOnly { get; } = new();

        public ThemeVariant? ActualThemeVariant => ThemeVariant.Dark;

        public bool TryFindResource(string key, out object? value) => Values.TryGetValue(key, out value);

        public bool TryFindResource(string key, ThemeVariant variant, out object? value)
        {
            if (variant.Equals(ThemeVariant.Dark) && DarkOnly.TryGetValue(key, out value))
            {
                return true;
            }

            return Values.TryGetValue(key, out value);
        }
    }

    /// <summary>
    /// M2-G3c: the tree table's window arithmetic, the bounded row-reuse cache and the Feature DataGrid
    /// host's paging contract. All three are pure logic, so they run in this no-UI console harness.
    /// </summary>
    private static void ListChecks()
    {
        // Tree table window: 10,000 rows, 44 px rows, 600 px viewport, 6 rows overscan.
        const int total = 10_000;
        const double rowHeight = 44;
        const double viewport = 600;
        const int overscan = ArcTreeTableWindow.DefaultOverscan;
        var viewportRows = (int)Math.Ceiling(viewport / rowHeight);

        var (first, last) = ArcTreeTableWindow.Compute(total, rowHeight, viewport, 0, overscan);
        var realized = last - first + 1;
        Check("tree window starts at the top", first == 0 && last >= viewportRows - 1);
        Check("tree window is viewport-sized, not 10k", realized <= viewportRows + 2 * overscan + 1);
        Check("tree window within 2x viewport budget", realized <= viewportRows * 2);

        var (midFirst, midLast) = ArcTreeTableWindow.Compute(total, rowHeight, viewport, 220_000, overscan);
        Check("tree window follows the scroll offset", midFirst > 4_900 && midLast < 5_100);
        Check("tree window at mid is viewport-sized", midLast - midFirst + 1 <= viewportRows + 2 * overscan + 1);

        var (endFirst, endLast) = ArcTreeTableWindow.Compute(total, rowHeight, viewport, total * rowHeight - viewport, overscan);
        Check("tree window clamps to the last row", endLast == total - 1 && endFirst < endLast);

        var (emptyFirst, emptyLast) = ArcTreeTableWindow.Compute(0, rowHeight, viewport, 0, overscan);
        Check("tree window empty data", emptyFirst == 0 && emptyLast == -1);
        var (tinyFirst, tinyLast) = ArcTreeTableWindow.Compute(3, rowHeight, viewport, 0, overscan);
        Check("tree window tiny data", tinyFirst == 0 && tinyLast == 2);

        // Row reuse cache: re-entry returns the same instance, capacity evicts the oldest entry.
        var cache = new ArcRowWindowCache(2);
        var firstControl = new Border();
        var secondControl = new Border();
        var thirdControl = new Border();
        cache.Return(1, firstControl);
        cache.Return(2, secondControl);
        _ = secondControl;
        cache.Return(3, thirdControl);
        Check("row cache evicts the oldest entry", cache.Count == 2 && !cache.TryTake(1, out _));
        Check("row cache reuses the same control instance",
            cache.TryTake(3, out var reused) && ReferenceEquals(reused, thirdControl));

        // Feature DataGrid host: defaults, page count and slicing are public contract.
        var host = new ArcDataGridHost();
        Check("feature host defaults", host.PageSize == 100 && host.PageIndex == 0 && host.TotalItems == -1 && host.AutoPage);
        host.ItemsSource = Enumerable.Range(0, 250).Cast<object?>().ToArray();
        Check("feature host page count", host.PageCount == 3);
        host.NextPage();
        Check("feature host slices the middle page", host.CurrentPageItems.Count == 100 && host.PageIndex == 1);
        host.LastPage();
        Check("feature host last page is short", host.CurrentPageItems.Count == 50 && host.PageCount == 3);
    }

    /// <summary>
    /// M2-G3b: the service layer's headless contract. A fake presenter, sink and file picker exercise the
    /// generic dialog result plumbing, the dialog queue, notification de-duplication and file-picker injection
    /// without a platform window.
    /// </summary>
    private static void ServiceChecks()
    {
        ArcServiceProvider.Reset();

        // Registry round trip + fake file picker injection (what the file controls resolve at click time).
        var picker = new FakeFilePickerService();
        ArcServiceProvider.Register<IFilePickerService>(picker);
        var found = ArcServiceProvider.TryGet<IFilePickerService>(out var resolved) && ReferenceEquals(resolved, picker);
        var files = resolved!.OpenFileAsync(new FilePickerOpenOptions { AllowMultiple = true }).GetAwaiter().GetResult();
        Check("service provider registration round trip", found);
        Check("file picker fake is called through the service", picker.Calls == 1 && files.Count == 0);

        // Dialog result plumbing + one-at-a-time queue.
        var presenter = new FakeDialogPresenter();
        var dialogs = new ArcDialogService(presenter);
        var confirmTask = dialogs.ConfirmAsync(new ArcConfirmRequest { Title = "Delete?", Message = "Sure?" });
        var renameTask = dialogs.ShowAsync(new ArcDialogRequest<string>
        {
            Title = "Rename",
            PrimaryResult = "renamed"
        });
        var confirmed = confirmTask.GetAwaiter().GetResult();
        var renamed = renameTask.GetAwaiter().GetResult();
        Check("dialog confirm returns the confirm value", confirmed);
        Check("dialog generic result round trip", renamed == "renamed" && presenter.Requests == 2);
        Check("dialog service serialises one dialog at a time", presenter.MaxConcurrent == 1);

        // Notification de-duplication + lifecycle.
        var sink = new FakeNotificationSink();
        var notifications = new ArcNotificationService(sink);
        var dismissed = 0;
        notifications.Dismissed += (_, _) => dismissed++;
        var first = notifications.Show(new ArcNotification { Title = "Saved", Key = "save" });
        var second = notifications.Show(new ArcNotification { Title = "Saved again", Key = "save" });
        var dismissedByKey = notifications.Dismiss("save");
        Check("notification dedup by key", first && !second && sink.Presented == 1);
        Check("notification lifecycle", dismissedByKey && notifications.Active.Count == 0 && dismissed == 1);

        // Navigation stack over the minimal frame.
        var frame = new ContentControl();
        var navigation = new ArcNavigationService();
        navigation.Attach(frame);
        navigation.Navigate("a");
        navigation.Navigate("b");
        var wentBack = navigation.GoBack();
        navigation.Replace("c");
        Check("navigation stack pushes, pops and replaces",
            wentBack && navigation.Stack.Count == 1 && (string?)navigation.Current == "c" &&
            ReferenceEquals(frame.Content, "c"));

        ArcServiceProvider.Reset();
    }

    private sealed class FakeFilePickerService : IFilePickerService
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<IStorageFile>> OpenFileAsync(
            FilePickerOpenOptions options,
            TopLevel? owner = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<IStorageFile>>(Array.Empty<IStorageFile>());
        }

        public Task<IStorageFile?> SaveFileAsync(
            FilePickerSaveOptions options,
            TopLevel? owner = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IStorageFile?>(null);
        }

        public Task<IReadOnlyList<IStorageFolder>> PickFolderAsync(
            FolderPickerOpenOptions options,
            TopLevel? owner = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<IStorageFolder>>(Array.Empty<IStorageFolder>());
        }
    }

    private sealed class FakeDialogPresenter : IArcDialogPresenter
    {
        private int _active;
        private int _requests;

        public int Requests => _requests;

        public int MaxConcurrent { get; private set; }

        public async Task<TResult> ShowAsync<TResult>(ArcDialogRequest<TResult> request, CancellationToken cancellationToken)
        {
            _requests++;
            _active++;
            MaxConcurrent = Math.Max(MaxConcurrent, _active);
            try
            {
                await Task.Delay(5, cancellationToken);
                return request.PrimaryResult;
            }
            finally
            {
                _active--;
            }
        }
    }

    private sealed class FakeNotificationSink : IArcNotificationSink
    {
        public int Presented { get; private set; }

        public void Present(ArcNotification notification, Action dismiss) => Presented++;

        public void Clear()
        {
        }
    }

    private static void Check(string name, bool passed)
    {
        _checks++;
        if (!passed)
        {
            Failures.Add(name);
        }
    }
}
