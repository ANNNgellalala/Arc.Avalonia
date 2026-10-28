using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Gallery;

/// <summary>
/// Offscreen screenshot harness. Pages render in light and dark at their natural height, capped to the
/// screen work area (keep each page under about 1350 px and split into another page if needed). Usage:
/// dotnet src/Arc.Avalonia.Gallery/bin/Debug/net10.0/Arc.Avalonia.Gallery.dll --shot shots --theme both [--page button]
/// </summary>
public static class ShotRunner
{
    private const int MaxHeight = 1400;

    public static async Task RunAsync(string[] args)
    {
        var app = Application.Current ?? throw new InvalidOperationException("No application");
        ArcThemeManager.ReducedMotion = true;

        var output = GetArg(args, "--shot") ?? "shots";
        var filter = GetArg(args, "--page");
        var themeArg = GetArg(args, "--theme") ?? "both";
        var accentArg = GetArg(args, "--accent");
        var accent = accentArg is not null && Enum.TryParse<ArcAccent>(accentArg, true, out var parsedAccent) ? parsedAccent : ArcAccent.Neutral;
        var width = int.TryParse(GetArg(args, "--width"), out var parsedWidth) ? parsedWidth : 1180;

        Directory.CreateDirectory(output);

        if (args.Contains("--shell"))
        {
            await RenderShellAsync(app, output, width);
            return;
        }

        var pages = ArcPageRegistry.Discover()
            .Where(page => filter is null || page.Slug.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (pages.Count == 0)
        {
            Console.WriteLine("SHOT: no pages matched");
            return;
        }

        var themes = themeArg.Equals("both", StringComparison.OrdinalIgnoreCase)
            ? new[] { ArcColorScheme.Light, ArcColorScheme.Dark }
            : new[] { themeArg.Equals("dark", StringComparison.OrdinalIgnoreCase) ? ArcColorScheme.Dark : ArcColorScheme.Light };

        var host = new Border
        {
            Width = width,
            Background = ArcResources.Brush(ArcTokens.Background),
            Padding = new Thickness(56, 48, 56, 40)
        };

        var window = new Window
        {
            Width = width,
            Height = 900,
            Position = new PixelPoint(-6000, -6000),
            ShowInTaskbar = false,
            Content = host
        };
        window.Show();
        await Task.Delay(300);

        foreach (var theme in themes)
        {
            ArcThemeManager.Apply(app, theme, accent);
            // The palette is swapped at runtime, so brushes resolved from it are snapshots: re-resolve them per theme.
            host.Background = ArcResources.Brush(ArcTokens.Background);
            window.Background = ArcResources.Brush(ArcTokens.Background);
            var themeDirectory = Path.Combine(output, theme.ToString().ToLowerInvariant());
            Directory.CreateDirectory(themeDirectory);

            foreach (var page in pages)
            {
                try
                {
                    await RenderPageAsync(host, window, page, theme, themeDirectory, width);
                    Console.WriteLine($"SHOT {theme.ToString().ToLowerInvariant()}/{page.Slug}.png");
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"SHOT FAILED {page.Slug}: {exception.Message}");
                }
            }
        }
    }

    private static async Task RenderPageAsync(Border host, Window window, ArcPage page, ArcColorScheme theme, string directory, int width)
    {
        host.Child = BuildPage(page, theme);

        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        await Task.Delay(60);

        host.Measure(new Size(width, double.PositiveInfinity));
        var naturalHeight = Math.Max(240, host.DesiredSize.Height);
        var height = (int)Math.Ceiling(Math.Min(naturalHeight, MaxHeight));
        window.Height = height;
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        await Task.Delay(120);

        var pixelWidth = Math.Max(1, (int)Math.Round(host.Bounds.Width));
        var pixelHeight = Math.Max(1, (int)Math.Round(host.Bounds.Height));
        if (naturalHeight > pixelHeight + 4)
        {
            Console.WriteLine($"SHOT WARNING {page.Slug}: page is {naturalHeight:0}px tall and was clipped to {pixelHeight}px; split it into two pages");
        }

        using var bitmap = new RenderTargetBitmap(new PixelSize(pixelWidth, pixelHeight), new Vector(96, 96));
        bitmap.Render(host);
        bitmap.Save(Path.Combine(directory, $"{page.Order:000}-{page.Slug}.png"));
    }

    private static Control BuildPage(ArcPage page, ArcColorScheme theme)
    {
        Control content;
        try
        {
            content = page.Build(new ArcDemoContext(theme, ArcThemeManager.Accent));
        }
        catch (Exception exception)
        {
            content = new TextBlock { Text = $"Page failed to build: {exception.Message}" };
        }

        // Catalog item pages render their own documentation header (title, tier, docs link, description);
        // only the older grouped pages need the gallery header above them.
        if (page is ArcItemPage)
        {
            return content;
        }

        var header = new StackPanel { Spacing = 10, Margin = new Thickness(0, 0, 0, 36) };
        header.Children.Add(new TextBlock { Text = page.Title, Classes = { "arc-title-sm" } });
        if (!string.IsNullOrWhiteSpace(page.Description))
        {
            header.Children.Add(new TextBlock
            {
                Text = page.Description,
                Classes = { "arc-body", "arc-secondary" },
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 760
            });
        }

        return new StackPanel { Children = { header, content } };
    }

    private static async Task RenderShellAsync(Application app, string directory, int width)
    {
        foreach (var theme in new[] { ArcColorScheme.Light, ArcColorScheme.Dark })
        {
            ArcThemeManager.Apply(app, theme, ArcAccent.Neutral);
            await Task.Delay(60);
            var window = new GalleryWindow { Width = width, Height = 900 };
            window.Show();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            await Task.Delay(400);

            var root = (Control)window.Content!;
            // The root panel is transparent (the window paints the background), so rendering it on its own
            // produced screenshots with transparent pixels and unreadable dark-theme text. Paint the window
            // background onto the root before capturing.
            if (root is Panel panel)
            {
                panel.Background = window.Background;
            }

            var pixelWidth = Math.Max(1, (int)Math.Round(root.Bounds.Width));
            var pixelHeight = Math.Max(1, (int)Math.Round(root.Bounds.Height));
            using var bitmap = new RenderTargetBitmap(new PixelSize(pixelWidth, pixelHeight), new Vector(96, 96));
            bitmap.Render(root);
            bitmap.Save(Path.Combine(directory, $"shell-{theme.ToString().ToLowerInvariant()}.png"));
        }
    }

    private static string? GetArg(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}




