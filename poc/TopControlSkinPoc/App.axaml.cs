using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Arc.Avalonia.Theme;

namespace TopControlSkinPoc;

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
            Console.WriteLine(exception);
            Environment.Exit(2);
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ArcThemeManager.Apply(this, ArcColorScheme.Light, ArcAccent.Neutral);

            // Fluent 12 renders a resting ScrollBar as a ~2 px sliver by scaling the thumb with these
            // resource-driven transforms. Neutralise them for the screenshot so the *reskinned* thickness is
            // visible in both panels; the shrink itself is a behaviour difference Arc has to opt out of
            // (documented in ADR 0004, it is not a colour/size key).
            Resources["VerticalSmallScrollThumbScaleTransform"] = Avalonia.Media.Transformation.TransformOperations.Parse("scale(1)");
            Resources["HorizontalSmallScrollThumbScaleTransform"] = Avalonia.Media.Transformation.TransformOperations.Parse("scale(1)");

            var window = new MainWindow
            {
                ShowInTaskbar = false,
                Position = new PixelPoint(-6000, -6000)
            };
            desktop.MainWindow = window;

            var outDir = RepoPaths.DocsImagesPoc;
            Directory.CreateDirectory(outDir);
            var probeOnly = Environment.GetCommandLineArgs().Contains("--probe");

            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    if (probeOnly)
                    {
                        await SkinProbe.DumpAsync(this, window);
                        Console.WriteLine("POC-B RESULT: PROBE ONLY");
                        Environment.Exit(0);
                    }

                    var failures = await SkinRunner.RunAsync(this, window, outDir);
                    Console.WriteLine(failures == 0
                        ? "POC-B RESULT: KEY-ONLY RESKIN COVERS THE SAMPLED CONTROLS"
                        : $"POC-B RESULT: {failures} assertion(s) failed");
                    Environment.Exit(failures == 0 ? 0 : 1);
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    Environment.Exit(3);
                }
            }, DispatcherPriority.Background);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
