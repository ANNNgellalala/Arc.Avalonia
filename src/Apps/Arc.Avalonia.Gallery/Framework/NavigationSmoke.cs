using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Gallery;

/// <summary>
/// Regression harness for the documentation shell. It drives the real window through every catalog entry,
/// every page, theme and accent switches and a search jump, so a cached document that is re-parented or a
/// page that throws is caught before screenshots. Run with `--smoke`; exits non-zero on any failure.
/// </summary>
public static class NavigationSmoke
{
    public static int Run()
    {
        var failures = new List<string>();
        var itemShows = 0;
        var pageShows = 0;

        var window = new GalleryWindow { Width = 1280, Height = 900, ShowInTaskbar = false };

        try
        {
            foreach (var item in ArcCatalog.AllItems)
            {
                Run(failures, $"item {item.Slug}", () => window.ShowSlug(item.Slug));
                itemShows++;
            }

            foreach (var page in ArcPageRegistry.Discover())
            {
                Run(failures, $"page {page.Title}", () => window.ShowPage(page));
                pageShows++;
            }

            Run(failures, "home", window.ShowHome);

            foreach (var scheme in new[] { ArcColorScheme.Dark, ArcColorScheme.Light })
            {
                Run(failures, $"theme {scheme}", () =>
                {
                    ArcThemeManager.SetScheme(Application.Current!, scheme);
                    window.RebuildAll();
                    window.ShowSlug("button");
                });
            }

            foreach (var accent in new[] { ArcAccent.Violet, ArcAccent.Neutral })
            {
                Run(failures, $"accent {accent}", () =>
                {
                    ArcThemeManager.SetAccent(Application.Current!, accent);
                    window.RebuildAll();
                });
            }

            Run(failures, "search", () =>
            {
                window.Search("按钮");
                window.ShowSlug("button");
                window.Search(string.Empty);
            });
        }
        catch (Exception exception)
        {
            failures.Add("unhandled " + exception);
        }
        finally
        {
            window.Close();
        }

        foreach (var failure in failures.Take(30))
        {
            Console.WriteLine("SMOKE FAIL " + failure);
        }

        Console.WriteLine($"GALLERY SMOKE: itemShows={itemShows} pageShows={pageShows} failures={failures.Count}");
        return failures.Count == 0 ? 0 : 1;
    }

    private static void Run(List<string> failures, string what, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            failures.Add($"{what}: {exception.GetType().Name}: {exception.Message}");
        }
    }
}
