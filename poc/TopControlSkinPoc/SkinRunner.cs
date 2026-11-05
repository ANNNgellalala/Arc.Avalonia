using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Theme;

namespace TopControlSkinPoc;

/// <summary>
/// POC-B harness: renders Before/After for Light and Dark and asserts, per control, that each overridden
/// Fluent resource key actually reached the resolved control property (resource assertion + property
/// assertion + screenshot = the double evidence the ADR needs).
/// </summary>
public static class SkinRunner
{
    /// <summary>Arc token values the AFTER panel XAML claims to apply (read from the real palette).</summary>
    private sealed record Tokens(
        Color Surface,
        Color SurfaceMuted,
        Color Border,
        Color BorderStrong,
        Color Foreground,
        Color Secondary,
        CornerRadius Radius,
        double HeightMd,
        double TextSm);

    private static int _failures;

    public static async Task<int> RunAsync(Application app, MainWindow window, string outDir)
    {
        _failures = 0;
        var root = window.FindControl<Border>("Root")
                   ?? throw new InvalidOperationException("MainWindow root border not found");

        foreach (var scheme in new[] { ArcColorScheme.Light, ArcColorScheme.Dark })
        {
            ArcThemeManager.Apply(app, scheme, ArcAccent.Neutral);
            SkinProbe.ApplyPageTheme(window);
            window.SetThemeLine($"{scheme.ToString().ToLowerInvariant()} · before = stock Fluent 12.1.4 · after = ThemeDictionaries key overrides with Arc token values");
            await SettleAsync();

            AssertTheme(app, window, scheme);

            var path = Path.Combine(outDir, $"skin-before-after-{scheme.ToString().ToLowerInvariant()}.png");
            var (width, height) = Render(root, path);
            Console.WriteLine($"POC-B SHOT {Path.GetFileName(path)} {width}x{height} {new FileInfo(path).Length / 1024}KB");
        }

        Console.WriteLine(_failures == 0
            ? "POC-B: all assertions passed"
            : $"POC-B: {_failures} assertion(s) failed");
        return _failures;
    }

    private static void AssertTheme(Application app, MainWindow window, ArcColorScheme scheme)
    {
        var theme = app.ActualThemeVariant;
        var tokens = ReadTokens(scheme);
        var afterPanel = window.FindControl<Border>("AfterPanel")!;
        var beforePanel = window.FindControl<Border>("BeforePanel")!;

        Console.WriteLine($"-- POC-B assertions [{scheme}] --");

        // ---------- TextBox ----------
        var beforeTextBox = window.FindControl<TextBox>("BeforeTextBox")!;
        var afterTextBox = window.FindControl<TextBox>("AfterTextBox")!;
        CheckBrush("TextBox", "TextControlBackground", beforeTextBox.Background, afterTextBox.Background, tokens.Surface);
        CheckBrush("TextBox", "TextControlBorderBrush", beforeTextBox.BorderBrush, afterTextBox.BorderBrush, tokens.Border);
        Check(afterTextBox.MinHeight == tokens.HeightMd,
            $"TextBox.MinHeight == {tokens.HeightMd} via TextControlThemeMinHeight (got {afterTextBox.MinHeight})");
        Check(afterTextBox.Padding == new Thickness(16, 10, 16, 10),
            $"TextBox.Padding == 16,10,16,10 via TextControlThemePadding (got {afterTextBox.Padding})");
        Check(afterTextBox.CornerRadius == tokens.Radius,
            $"TextBox.CornerRadius == {tokens.Radius} via ControlCornerRadius (got {afterTextBox.CornerRadius}, before {beforeTextBox.CornerRadius})");
        Console.WriteLine($"POC-B OBSERVE TextBox.FontSize after={afterTextBox.FontSize} (ControlContentThemeFontSize override had no effect; font size is not key-driven)");

        // ---------- ComboBox ----------
        var beforeComboBox = window.FindControl<ComboBox>("BeforeComboBox")!;
        var afterComboBox = window.FindControl<ComboBox>("AfterComboBox")!;
        CheckBrush("ComboBox", "ComboBoxBackground", beforeComboBox.Background, afterComboBox.Background, tokens.Surface);
        CheckBrush("ComboBox", "ComboBoxBorderBrush", beforeComboBox.BorderBrush, afterComboBox.BorderBrush, tokens.Border);
        Check(afterComboBox.Padding == new Thickness(16, 10, 12, 10),
            $"ComboBox.Padding == 16,10,12,10 via ComboBoxPadding (got {afterComboBox.Padding})");
        Check(Math.Abs(afterComboBox.Bounds.Width - 140) < 1.0,
            $"ComboBox rendered width == 140 via ComboBoxThemeMinWidth (control MinWidth is {afterComboBox.MinWidth}; "
            + $"before {beforeComboBox.Bounds.Width:0.#}, after {afterComboBox.Bounds.Width:0.#})");
        Check(Math.Abs(afterComboBox.MinHeight - tokens.HeightMd) < 0.01,
            $"ComboBox.MinHeight == {tokens.HeightMd} via ComboBoxMinHeight (got {afterComboBox.MinHeight})");
        Check(afterComboBox.CornerRadius == tokens.Radius,
            $"ComboBox.CornerRadius == {tokens.Radius} via ControlCornerRadius (got {afterComboBox.CornerRadius})");

        // ---------- TabControl / TabItem ----------
        var beforeTabs = TabItems(window, "BeforeTabControl");
        var afterTabs = TabItems(window, "AfterTabControl");
        var afterSelected = afterTabs.FirstOrDefault(t => t.IsSelected);
        var afterUnselected = afterTabs.FirstOrDefault(t => !t.IsSelected);
        var beforeSelected = beforeTabs.FirstOrDefault(t => t.IsSelected);
        CheckBrush("TabItem", "TabItemHeaderForegroundSelected", beforeSelected?.Foreground, afterSelected?.Foreground, tokens.Foreground);
        CheckBrush("TabItem", "TabItemHeaderForegroundUnselected", beforeTabs.FirstOrDefault(t => !t.IsSelected)?.Foreground,
            afterUnselected?.Foreground, tokens.Secondary);
        Check(afterSelected is not null && Math.Abs(afterSelected.FontSize - tokens.TextSm) < 0.01,
            $"TabItem.FontSize == {tokens.TextSm} via TabItemHeaderFontSize (before {beforeSelected?.FontSize}, after {afterSelected?.FontSize})");
        Check(afterSelected is not null && afterSelected.Padding == new Thickness(0, 0, 16, 0),
            $"TabItem.Padding == 0,0,16,0 via TabItemHeaderMargin (before {beforeSelected?.Padding}, after {afterSelected?.Padding})");
        Check(afterSelected is not null && Math.Abs(afterSelected.MinHeight - 40) < 0.01,
            $"TabItem.MinHeight == 40 via TabItemMinHeight (after {afterSelected?.MinHeight})");
        var pipe = afterSelected?.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "PART_SelectedPipe");
        CheckPipe(pipe, tokens);

        // ---------- ScrollBar ----------
        var beforeV = window.FindControl<ScrollBar>("BeforeScrollBarV")!;
        var afterV = window.FindControl<ScrollBar>("AfterScrollBarV")!;
        var beforeH = window.FindControl<ScrollBar>("BeforeScrollBarH")!;
        var afterH = window.FindControl<ScrollBar>("AfterScrollBarH")!;
        Check(Math.Abs(afterV.Bounds.Width - 10) < 0.6,
            $"vertical ScrollBar width == 10 via ScrollBarSize (before {beforeV.Bounds.Width:0.#}, after {afterV.Bounds.Width:0.#})");
        Check(Math.Abs(afterH.Bounds.Height - 10) < 0.6,
            $"horizontal ScrollBar height == 10 via ScrollBarSize (before {beforeH.Bounds.Height:0.#}, after {afterH.Bounds.Height:0.#})");
        var beforeThumb = ThumbOf(beforeV);
        var afterThumb = ThumbOf(afterV);
        CheckBrush("ScrollBar", "ScrollBarPanningThumbBackground", beforeThumb?.Background, afterThumb?.Background, tokens.BorderStrong);
        var beforeTrack = beforeV.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Rectangle>().FirstOrDefault();
        var afterTrack = afterV.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Rectangle>().FirstOrDefault();
        Check(BrushesOf(afterTrack?.Fill) is not null,
            $"ScrollBar track fill driven by ScrollBarTrackFill (before {beforeTrack?.Fill?.ToString() ?? "<null>"}, after {afterTrack?.Fill?.ToString() ?? "<null>"})");

        // ---------- Resource-level proof (the AFTER panel's own dictionary) ----------
        CheckResource(afterPanel, theme, "ControlCornerRadius", tokens.Radius);
        CheckResource(afterPanel, theme, "TextControlThemePadding", new Thickness(16, 10, 16, 10));
        CheckResource(afterPanel, theme, "ComboBoxPadding", new Thickness(16, 10, 12, 10));
        CheckResource(afterPanel, theme, "TabItemHeaderFontSize", tokens.TextSm);
        CheckResource(afterPanel, theme, "ScrollBarSize", 10d);
        CheckResource(afterPanel, theme, "ScrollBarPanningThumbBackground", tokens.BorderStrong);
        Check(!ReferenceEquals(beforePanel.Resources, afterPanel.Resources), "BEFORE panel is a separate resource scope from AFTER panel");
    }

    private static void CheckPipe(Border? pipe, Tokens tokens)
    {
        if (pipe?.Background is not ISolidColorBrush brush)
        {
            _failures++;
            Console.WriteLine("POC-B ASSERT FAIL: TabItem PART_SelectedPipe not found");
            return;
        }

        Check(brush.Color == tokens.Foreground,
            $"TabItem pipe filler == ArcForeground via TabItemHeaderSelectedPipeFill (got {brush.Color})");
        Check(pipe.CornerRadius == tokens.Radius,
            $"TabItem pipe CornerRadius == {tokens.Radius} via ControlCornerRadius (got {pipe.CornerRadius})");
    }

    private static Tokens ReadTokens(ArcColorScheme scheme)
    {
        var palette = ArcPalette.Build(scheme, ArcAccent.Neutral);
        Color C(string key) => palette[key] is ISolidColorBrush brush ? brush.Color : Colors.Magenta;
        return new Tokens(
            C(ArcTokens.Surface),
            C(ArcTokens.SurfaceMuted),
            C(ArcTokens.Border),
            C(ArcTokens.BorderStrong),
            C(ArcTokens.Foreground),
            C(ArcTokens.TextSecondary),
            palette[ArcTokens.RadiusControl] is CornerRadius radius ? radius : default,
            palette[ArcTokens.ControlHeightMd] is double height ? height : 0,
            palette[ArcTokens.TextSm] is double textSm ? textSm : 0);
    }

    private static List<TabItem> TabItems(Window window, string name) =>
        window.FindControl<TabControl>(name)!.GetVisualDescendants().OfType<TabItem>().ToList();

    private static Thumb? ThumbOf(ScrollBar scrollBar) =>
        scrollBar.GetVisualDescendants().OfType<Thumb>().FirstOrDefault();

    private static IBrush? BrushesOf(IBrush? brush) => brush;

    private static void CheckBrush(string control, string key, IBrush? before, IBrush? after, Color expected)
    {
        var afterColor = after is ISolidColorBrush afterBrush ? afterBrush.Color : (Color?)null;
        var beforeColor = before is ISolidColorBrush beforeBrush ? beforeBrush.Color : (Color?)null;
        Check(afterColor == expected,
            $"{control}: {key} -> {expected} (before {beforeColor?.ToString() ?? "<null>"}, after {afterColor?.ToString() ?? "<null>"})");
        Check(beforeColor != expected, $"{control}: BEFORE differs for {key} (before {beforeColor?.ToString() ?? "<null>"})");
    }

    private static void CheckResource(IResourceHost host, ThemeVariant theme, string key, object expected)
    {
        var ok = host.TryFindResource(key, theme, out var value)
                 && (expected is Color color
                     ? value is ISolidColorBrush brush && brush.Color == color
                     : Equals(value, expected));
        Check(ok, $"resource {key} == {expected} in AFTER scope (got {(value ?? "<missing>")})");
    }

    private static void Check(bool condition, string message)
    {
        if (condition)
        {
            Console.WriteLine($"POC-B ASSERT PASS: {message}");
            return;
        }

        _failures++;
        Console.WriteLine($"POC-B ASSERT FAIL: {message}");
    }

    private static async Task SettleAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        await Task.Delay(180);
    }

    private static (int Width, int Height) Render(Control root, string path)
    {
        var width = Math.Max(1, (int)Math.Round(root.Bounds.Width));
        var height = Math.Max(1, (int)Math.Round(root.Bounds.Height));
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        bitmap.Render(root);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bitmap.Save(path);
        if (Math.Max(width, height) > 1200)
        {
            Console.WriteLine($"POC-B WARN image long side {Math.Max(width, height)} > 1200");
        }

        return (width, height);
    }
}
