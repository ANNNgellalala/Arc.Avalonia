using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Services;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Gallery;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ArcThemeManager.Apply(this, ArcColorScheme.Light, ArcAccent.Neutral);

        // M2-G3b: app-level services. ArcWindow registers defaults for its own overlay-backed services;
        // the file picker and theme facade are registered here so every window shares one implementation.
        ArcServiceProvider.Register<IFilePickerService>(StorageProviderFilePickerService.Instance);
        ArcServiceProvider.Register<IArcThemeService>(new ArcThemeService(this));

        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();

        // M2 mobile wave: the same shell runs inside a single-view lifetime (Android/iOS). The platform
        // supplies one view, so the shell is forced into the compact presentation from the first layout pass.
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new GalleryShell(initialSlug: null, compact: true);
            base.OnFrameworkInitializationCompleted();
            return;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (args.Contains("--shot"))
            {
                // Offscreen rendering can occasionally leave the Win32 loop alive after the last page, so a
                // watchdog guarantees scripted runs terminate (default 300 s, override with --timeout).
                var timeoutSeconds = 300;
                var timeoutIndex = Array.IndexOf(args, "--timeout");
                if (timeoutIndex >= 0 && timeoutIndex + 1 < args.Length && int.TryParse(args[timeoutIndex + 1], out var parsed) && parsed > 0)
                {
                    timeoutSeconds = parsed;
                }

                _ = new Timer(_ => Environment.Exit(0), null, TimeSpan.FromSeconds(timeoutSeconds), Timeout.InfiniteTimeSpan);
                _ = RunShotsAsync(desktop, args);
                base.OnFrameworkInitializationCompleted();
                return;
            }

            if (args.Contains("--audit"))
            {
                var exitCode = CatalogAudit.Run();
                Console.Out.Flush();
                Environment.Exit(exitCode);
            }

            if (args.Contains("--theme-report"))
            {
                var exitCode = ThemeReport.Run();
                Console.Out.Flush();
                Environment.Exit(exitCode);
            }

            if (args.Contains("--list"))
            {
                foreach (var page in ArcPageRegistry.Discover())
                {
                    Console.WriteLine($"{page.Order:000} {page.Group} / {page.Title} ({page.Slug})");
                }

                Console.Out.Flush();
                Environment.Exit(0);
            }

            if (args.Contains("--smoke"))
            {
                // Navigation regression harness: drive the real shell through the sidebar and page routes,
                // then exit with 0 only when nothing threw. See Framework/NavigationSmoke.cs.
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                _ = new Timer(_ => { Console.WriteLine("SMOKE TIMEOUT"); Environment.Exit(2); },
                    null, TimeSpan.FromSeconds(300), Timeout.InfiniteTimeSpan);
                Dispatcher.UIThread.Post(() =>
                {
                    var exitCode = 1;
                    try
                    {
                        exitCode = NavigationSmoke.Run(args.Contains("--mobile"));
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine("SMOKE CRASH " + exception);
                    }
                    finally
                    {
                        Console.Out.Flush();
                        Environment.Exit(exitCode);
                    }
                }, DispatcherPriority.Background);

                base.OnFrameworkInitializationCompleted();
                return;
            }

            var pageIndex = Array.IndexOf(args, "--page");
            var initialSlug = pageIndex >= 0 && pageIndex + 1 < args.Length ? args[pageIndex + 1] : null;
            // M2 mobile wave: --mobile opens the phone shell (412x915 DIP, compact single-column layout);
            // any narrow window width also switches to compact at runtime.
            var mobile = args.Contains("--mobile");
            desktop.MainWindow = new GalleryWindow(initialSlug, mobile);
            desktop.MainWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task RunShotsAsync(IClassicDesktopStyleApplicationLifetime desktop, string[] args)
    {
        try
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            await ShotRunner.RunAsync(args);
        }
        catch (Exception exception)
        {
            Console.WriteLine("SHOT FAIL " + exception);
        }
        finally
        {
            desktop.Shutdown();
            // The offscreen window can keep the Win32 loop alive in some environments; exit explicitly so
            // scripted screenshot runs always terminate.
            await Task.Delay(200);
            Environment.Exit(0);
        }
    }
}



