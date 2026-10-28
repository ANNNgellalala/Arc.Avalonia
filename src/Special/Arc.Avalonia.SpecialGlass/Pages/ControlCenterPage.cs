using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialGlass.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialGlass.Pages;

/// <summary>The Control center catalog page: quick settings, the focus dial and rubber-banded meters.</summary>
public sealed class ControlCenterPage : ArcItemPage
{
    public override string ItemSlug => "control-center";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A quick settings panel with a handful of toggles and levels for a web workspace.",
        "Focus session controls with a duration dial and the people who can still reach you."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Enter or Space toggles a tile or opens its detail; the Focus tile with no session starts the first mode.",
        "Arrows move a meter by one step and the dial by five minutes; Page keys move four steps and fifteen minutes; Home and End jump to the limits.",
        "Escape closes the open detail and returns focus to its chevron; rows are named and show their on or off state."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "A tile grows into its detail over a scrim while the grid behind it dims.",
        "Meters follow the pointer one to one from where they were grabbed and resist past their ends; the dial knob springs to snapped values.",
        "Reduced motion removes travel: details fade in place and values jump to their new position."
    };

    public override string? ImplementationNotes =>
        "The tile-to-detail morph is a fade and scale inside one fixed height panel rather than a shared layout animation, and the session dial paints its ring with arcs and FormattedText. Glass is layered translucency. No Pro source was used.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var modes = new List<ArcFocusMode>
        {
            new() { Id = "deep", Label = "Deep work", Icon = "briefcase", Description = "Silences everything but mentions", DefaultMinutes = 50 },
            new() { Id = "rest", Label = "Rest", Icon = "moon", Description = "Silences all alerts", DefaultMinutes = 30 }
        };
        var channels = new List<ArcChannelOption>
        {
            new() { Id = "mentions", Label = "Mentions", Description = "Direct mentions and replies", DefaultOn = true },
            new() { Id = "deploys", Label = "Deploys", Description = "Production releases only" }
        };
        var people = new List<string> { "Ana Ruiz", "Sam Lee", "Noah Park" };

        var tray = new ArcControlCenter
        {
            FocusModes = modes,
            Channels = channels,
            People = people,
            DefaultVolume = 62,
            DefaultTextSize = 105
        };

        var focus = new ArcControlCenter
        {
            FocusModes = modes,
            Channels = channels,
            People = people,
            DefaultVolume = 62,
            DefaultTextSize = 105
        };

        tray.HorizontalAlignment = HorizontalAlignment.Center;
        focus.HorizontalAlignment = HorizontalAlignment.Center;

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 36,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { tray, focus }
        };

        var labels = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), Width = 756 };
        labels.Children.Add(GlassDemo.Caption("Quick settings"));
        var focusLabel = GlassDemo.Caption("Focus detail");
        labels.Children.Add(focusLabel);
        focusLabel.Foreground = ArcGlass.Solid(ArcGlass.PhotoMuted);
        if (labels.Children[0] is TextBlock quickLabel)
        {
            quickLabel.Foreground = ArcGlass.Solid(ArcGlass.PhotoMuted);
        }
        Grid.SetColumn(labels.Children[1], 1);

        var body = new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { labels, row }
        };

        var stage = GlassDemo.Stage(body, ArcGlassScene.Studio, 520, 12, seed: 6);
        Dispatcher.UIThread.Post(() => focus.Open("focus"), DispatcherPriority.Loaded);

        return new StackPanel
        {
            Children =
            {
                stage,
                GlassDemo.Hint("Open a tile's detail from its chevron; drag the dial or a meter, and press Escape to close the detail.")
            }
        };
    }
}


