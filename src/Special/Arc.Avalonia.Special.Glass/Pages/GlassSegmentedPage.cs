using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Special.Glass.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Glass.Pages;

/// <summary>The Glass segmented catalog page: a liquid indicator you can drag and fling.</summary>
public sealed class GlassSegmentedPage : ArcItemPage
{
    public override string ItemSlug => "glass-segmented";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Switching the view of a photo, map, or media surface.",
        "A compact filter over imagery, such as day, week and month on a hero chart."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Plain forms on neutral surfaces. Use the standard segmented control.",
        "More than five options or long labels. Use tabs or a select."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Tab focuses the selected segment; the whole group is a single tab stop.",
        "Left and Right (and Up and Down) select the previous or next segment and wrap at the ends; Home and End jump.",
        "Selection is shown by the indicator, the label colour and weight, never by colour alone; the indicator itself is decorative.",
        "The group is labelled and each segment is a named radio item; keyboard focus draws Arc's two pixel focus outline."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Each edge of the indicator springs separately: the edge facing the travel is quicker and the other lags, so the drop stretches and thins like liquid.",
        "Dragging the selected segment follows the pointer with the same lag; releasing projects velocity to pick where it lands.",
        "With reduced motion the indicator jumps and dragging tracks directly."
    };

    public override string? ImplementationNotes =>
        "The glass track is a translucent surface with a rim gradient derived from the surface token, since Avalonia has no backdrop filter. The liquid indicator is a rounded Border positioned per frame: the two edges are interpolated on the shared morph spring rather than sampled from two live motion springs. Dragging starts on the selected segment only, as documented, and a mouse drag stands in for a touch drag on desktop. No Pro source was used.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var primary = new ArcGlassSegmented
        {
            Label = "Map mode",
            Value = "explore",
            Options = new List<ArcGlassSegmentedOption>
            {
                new() { Value = "explore", Label = "Explore", Icon = "compass" },
                new() { Value = "drive", Label = "Drive", Icon = "navigation" },
                new() { Value = "transit", Label = "Transit", Icon = "layers", Badge = "3" }
            }
        };

        var compact = new ArcGlassSegmented
        {
            Label = "Range",
            Size = ArcGlassSegmentedSize.Sm,
            Value = "week",
            Options = new List<ArcGlassSegmentedOption>
            {
                new() { Value = "day", Label = "Day" },
                new() { Value = "week", Label = "Week" },
                new() { Value = "month", Label = "Month" }
            }
        };

        var full = new ArcGlassSegmented
        {
            Label = "View",
            FullWidth = true,
            Width = 440,
            Value = "week",
            Options = new List<ArcGlassSegmentedOption>
            {
                new() { Value = "day", Label = "Day" },
                new() { Value = "week", Label = "Week" },
                new() { Value = "month", Label = "Month" }
            }
        };

        var panelContent = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new TextBlock
                {
                    Text = "Map mode",
                    Classes = { "arc-label" },
                    Margin = new Thickness(2, 0, 0, 0)
                },
                primary,
                compact,
                full
            }
        };

        var panel = ArcGlass.Panel(26, new Thickness(22), floating: true, fillAlpha: 0.58);
        panel.Width = 492;
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;
        panel.Child = panelContent;

        var stage = GlassDemo.Stage(panel, ArcGlassScene.Garden, 300, 24, seed: 2);
        return new StackPanel
        {
            Children =
            {
                stage,
                GlassDemo.Hint("Drag the selected segment and release to fling it onto the next one; arrow keys, Home and End also change the selection.")
            }
        };
    }
}
