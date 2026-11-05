using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Arc.Avalonia.Theme;
using ArcThemePoc;

namespace ArcThemePoc;

public partial class App : Application
{
    public override void Initialize()
    {
        try
        {
            AvaloniaXamlLoader.Load(this);
        }
        catch (Exception exception)
        {
            // POC-A evidence capture: if nesting <FluentTheme/> inside a custom Styles is rejected by
            // the XAML compiler/loader, the consumer app dies here. Record the verbatim exception.
            WriteFailure("App.axaml load failed (the single-line <poc:ArcTheme/> entry point)", exception);
            Console.WriteLine("POC-A RESULT: SINGLE-LINE XAML ENTRY POINT NOT VIABLE");
            Console.WriteLine(exception);
            Environment.Exit(2);
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ArcThemeManager.Apply(this, ArcColorScheme.Light, ArcAccent.Neutral);
            var window = new MainWindow
            {
                ShowInTaskbar = false,
                Position = new PixelPoint(-6000, -6000)
            };
            desktop.MainWindow = window;

            var outDir = RepoPaths.DocsImagesPoc;
            Directory.CreateDirectory(outDir);
            var probeOnly = Environment.GetCommandLineArgs().Contains("--probe");

            // Run after the window has been shown and laid out by the lifetime.
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    if (probeOnly)
                    {
                        ThemeShotRunner.Probe(this);
                        Console.WriteLine("POC-A RESULT: PROBE ONLY (no screenshots)");
                        Environment.Exit(0);
                    }

                    var failures = await ThemeShotRunner.RunAsync(this, window, outDir);
                    Console.WriteLine(failures == 0
                        ? "POC-A RESULT: SINGLE-LINE XAML ENTRY POINT VIABLE"
                        : $"POC-A RESULT: FAILED ({failures} assertion(s))");
                    Environment.Exit(failures == 0 ? 0 : 1);
                }
                catch (Exception exception)
                {
                    WriteFailure("Render harness failed", exception);
                    Console.WriteLine(exception);
                    Environment.Exit(3);
                }
            }, DispatcherPriority.Background);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void WriteFailure(string stage, Exception exception)
    {
        try
        {
            var directory = RepoPaths.Poc;
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "ArcThemePoc-failure.log"),
                $"stage: {stage}\n{DateTimeOffset.Now:O}\n\n{exception}");
            Console.WriteLine($"POC-A failure log: {Path.Combine(directory, "ArcThemePoc-failure.log")}");
        }
        catch
        {
            // Diagnostics only; never mask the original failure.
        }
    }
}
