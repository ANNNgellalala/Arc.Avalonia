using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Gallery;

/// <summary>
/// Regression harness for gallery navigation. It drives the real shell through the same paths the pointer
/// handlers use (sidebar buttons, category expanders, item buttons, tier filters, theme switches) and then
/// shows every discovered page repeatedly so cached views have to move between content hosts. Exits non-zero
/// if any navigation throws, which catches reparenting regressions such as
/// "the control StackPanel already has a visual parent StackPanel". Run with `--smoke`.
/// </summary>
public static class NavigationSmoke
{
    private const int CacheResetInterval = 32;

    public static int Run()
    {
        var failures = new List<string>();
        var clicks = 0;
        var pageShows = 0;
        var itemShows = 0;

        var window = new GalleryWindow { Width = 1180, Height = 900, ShowInTaskbar = false };
        var root = window.Content;

        try
        {
            // 1. The real click path: page buttons, category expanders, tier filters, theme buttons and,
            //    once the categories are expanded, every catalog item button.
            for (var pass = 0; pass < 3; pass++)
            {
                clicks += ClickEveryButton(window, pass, failures);
            }

            // 2. Show every page, switch to another page and come back. The switch-away/switch-back cycle
            //    is what used to throw: cached content was added to a second wrapper panel.
            var pages = ArcPageRegistry.Discover().ToList();
            for (var index = 0; index < pages.Count; index++)
            {
                var page = pages[index];
                var other = pages[(index + 1) % pages.Count];
                Run(failures, $"page {page.Title}", () =>
                {
                    Invoke(window, "ShowPage", page);
                    Invoke(window, "ShowPage", other);
                    Invoke(window, "ShowPage", page);
                });
                pageShows += 3;

                if ((index + 1) % CacheResetInterval == 0)
                {
                    Invoke(window, "InvalidateBuiltContent");
                    Run(failures, "rebuild after page sweep", () => Invoke(window, "RebuildAll"));
                }
            }

            // 3. Every catalog item twice, exercising the item page / alias / shared section routes.
            foreach (var item in ArcCatalog.AllItems)
            {
                Run(failures, $"item {item.Slug}", () =>
                {
                    Invoke(window, "ShowItem", item);
                    Invoke(window, "ShowItem", item);
                });
                itemShows += 2;

                if (itemShows % CacheResetInterval == 0)
                {
                    Invoke(window, "InvalidateBuiltContent");
                }
            }

            // 4. Theme and accent switches rebuild the sidebar and the current page in place.
            foreach (var scheme in new[] { ArcColorScheme.Dark, ArcColorScheme.Light })
            {
                Run(failures, $"theme {scheme}", () =>
                {
                    ArcThemeManager.SetScheme(Application.Current!, scheme);
                    Invoke(window, "RebuildAll");
                    Invoke(window, "ShowPage", pages[0]);
                    Invoke(window, "ShowPage", pages[1]);
                });
            }

            foreach (var accent in Enum.GetValues<ArcAccent>())
            {
                Run(failures, $"accent {accent}", () =>
                {
                    ArcThemeManager.SetAccent(Application.Current!, accent);
                    Invoke(window, "RebuildAll");
                    Invoke(window, "ShowPage", pages[0]);
                });
            }

            // 5. Search filters the sidebar; navigating from a result must work like any other item.
            var searchClicks = 0;
            Run(failures, "search filter", () =>
            {
                var search = window.GetLogicalDescendants().OfType<TextBox>().FirstOrDefault()
                             ?? throw new InvalidOperationException("sidebar search box not found");
                search.Text = "button";
                searchClicks = ClickEveryButton(window, 99, failures);
                search.Text = string.Empty;
            });
            clicks += searchClicks;

            if (!ReferenceEquals(window.Content, root))
            {
                failures.Add("the window content root was replaced during navigation");
            }
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

        Console.WriteLine($"GALLERY SMOKE: clicks={clicks} pageShows={pageShows} itemShows={itemShows} failures={failures.Count}");
        return failures.Count == 0 ? 0 : 1;
    }

    private static int ClickEveryButton(GalleryWindow window, int pass, List<string> failures)
    {
        var buttons = window.GetLogicalDescendants().OfType<Button>().ToList();
        var clicked = 0;
        foreach (var button in buttons)
        {
            var label = button.Content?.ToString() ?? button.GetType().Name;
            Run(failures, $"click pass {pass}: {label}", () =>
            {
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                clicked++;
            });
        }

        return clicked;
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

    private static void Invoke(GalleryWindow window, string method, params object?[] arguments)
    {
        var info = typeof(GalleryWindow).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                   ?? throw new MissingMethodException(nameof(GalleryWindow), method);
        try
        {
            info.Invoke(window, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
