using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;
using Arc.Avalonia.BlocksMarketing.Controls;
using Mk = Arc.Avalonia.BlocksMarketing.MarketingKit;

namespace Arc.Avalonia.BlocksMarketing.Pages;

/// <summary>
/// Stats band: headline numbers that count up when the block appears, each with a tiny visual that backs the
/// claim and a context line on hover, laid out plain or in a hairline grid. Numbers roll with a short ease-out
/// and always land on the final figure; the first paint shows the value, so screenshots and reduced motion
/// agree.
/// </summary>
public sealed class StatsBandPage : ArcItemPage
{
    public override string ItemSlug => "stats-band";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A proof band under the hero, where four numbers tell the story.",
        "Investor or launch pages that need scale without a chart.",
        "Closing a page on outcomes rather than features."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Each figure is a group read as label, value, delta, in that order.",
        "The context line sits in the same group, so hovering is never the only way to read it.",
        "Colour is never the only carrier: every delta also carries a signed value."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Numbers count up once when the band enters the viewport and never overshoot.",
        "The context line fades in on hover; reduced motion shows it without travel and jumps the count."
    };

    public override string? ImplementationNotes =>
        "The count-up is a local number control with a 460 ms ease-out that lands on the exact final value; under reduced motion, and in this screenshot harness, the first paint shows the final number. The sparkline is the shared chart control over the same sample series the figures describe. Plain and hairline are the two documented treatments.";

    private sealed record Stat(string Label, double Value, string Prefix, string Suffix, int Decimals, string Delta, bool Down, string Context, IReadOnlyList<double> Series);

    private static readonly Stat[] Stats =
    {
        new("Revenue growth", 4.28, "$", "M", 2, "+24.6%", false, "Expansion revenue overtook new business in March.", new double[] { 12, 14, 13, 16, 18, 21, 20, 24, 26, 29, 32, 36 }),
        new("Teams on Arc", 12480, string.Empty, string.Empty, 0, "+1,820", false, "Most new workspaces start with a single project.", new double[] { 20, 24, 27, 31, 34, 33, 38, 42, 47, 51, 56, 62 }),
        new("Uptime", 99.98, string.Empty, "%", 2, "90 days", false, "No incident longer than four minutes this quarter.", new double[] { 60, 60, 59, 60, 60, 60, 59, 60, 60, 60, 60, 60 }),
        new("Time to value", 4.2, string.Empty, " days", 1, "-2.1 days", true, "Median time from signup to the first shipped release.", new double[] { 40, 39, 37, 36, 33, 31, 30, 28, 26, 24, 22, 21 })
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var gridHost = new ContentControl();
        var hairline = true;

        void Rebuild()
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*") };
            for (var index = 0; index < Stats.Length; index++)
            {
                var cell = Cell(Stats[index], hairline, index > 0);
                Grid.SetColumn(cell, index);
                grid.Children.Add(cell);
            }

            gridHost.Content = grid;
        }

        Rebuild();

        var toggle = new ArcSwitch { Label = "Hairline grid", Checked = true };
        toggle.CheckedChanged += (_, _) =>
        {
            hairline = toggle.Checked;
            Rebuild();
        };

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        head.Children.Add(Mk.Caption("Figures are sample data for the last 12 months."));
        toggle.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(toggle, 1);
        head.Children.Add(toggle);

        var band = new Border { Padding = new Thickness(24, 18, 24, 16), Child = Mk.Stack(14, head, gridHost) };
        return Mk.Surface(band);
    }

    private static Control Cell(Stat stat, bool hairline, bool divider)
    {
        var number = new ArcStatNumber
        {
            Value = stat.Value,
            Prefix = stat.Prefix,
            Suffix = stat.Suffix,
            Decimals = stat.Decimals,
            FontSize = 34,
            FontWeight = FontWeight.Medium
        };

        var delta = Mk.Row(5,
            new ArcIcon
            {
                Icon = stat.Down ? "arrow-down" : "arrow-up",
                Size = 12,
                Foreground = Mk.Brush(ArcTokens.Success)
            },
            Mk.Text(stat.Delta, "arc-caption"));

        var context = Mk.Caption(stat.Context);
        context.Opacity = 0;
        context.Height = 18;

        var content = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            Children =
            {
                Mk.Caption(stat.Label),
                number,
                delta,
                Mk.Spark(stat.Series, 40),
                context
            }
        };

        var padded = new Border
        {
            Padding = new Thickness(divider ? 22 : 0, 0, 22, 0),
            BorderBrush = Mk.Brush(ArcTokens.BorderSubtle),
            BorderThickness = new Thickness(divider && hairline ? 1 : 0, 0, 0, 0),
            Child = content
        };

        padded.PointerEntered += (_, _) => context.Opacity = 1;
        padded.PointerExited += (_, _) => context.Opacity = 0;

        if (!ArcThemeManager.ReducedMotion)
        {
            context.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = Visual.OpacityProperty,
                    Duration = ArcMotion.Fast,
                    Easing = ArcMotion.EaseStandard
                }
            };
        }

        return padded;
    }
}

