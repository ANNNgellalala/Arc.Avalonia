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

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();

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
                        exitCode = NavigationSmoke.Run();
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

            desktop.MainWindow = new GalleryWindow();
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



