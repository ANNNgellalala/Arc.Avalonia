using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.BlocksTeam.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.BlocksTeam.Pages;

/// <summary>
/// Cohort retention: a cohort triangle that switches to retention curves without changing data, with segment
/// filters and a keyboard-navigable cell cursor. Cohort sizes and rates are deterministic sample data; the KPI
/// row derives from the visible cells rather than from separate numbers.
/// </summary>
public sealed class CohortRetentionPage : ArcItemPage
{
    public override string ItemSlug => "cohort-retention";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Growth or customer success dashboards comparing how signup cohorts retain month over month.",
        "Views that need both the cohort triangle and retention curves from the same data."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use journey-flow for paths between funnel steps, or activity-heatmap for activity by day."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Arrow keys move the cell cursor, Home and End jump within a cohort, and Escape clears it.",
        "The chart is a labelled group with instructions, and a polite live region reads the focused cohort, month and rate."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Cells wash to the new segment tint with a short stagger; switching to curves crossfades the geometry in place.",
        "Reduced motion jumps values and geometry instantly and keeps the cursor following the pointer."
    };

    public override string? ImplementationNotes =>
        "Cohorts are deterministic sample data for three segments; the triangle is eight cohorts by eight months. " +
        "The reference morphs individual cells into curves with layout animation; this port crossfades the two " +
        "rendering modes. Hover readout is by keyboard or pointer cursor. No Pro source was consulted.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var chart = new ArcCohortChart
        {
            Segment = "all",
            Mode = "grid",
            Height = 316
        };
        AutomationProperties.SetName(chart, "Cohort retention, eight cohorts by eight months");
        AutomationProperties.SetHelpText(chart, "Use arrow keys to inspect a cohort and month; the escape key clears the cursor.");

        var readout = TeamKit.Caption("Use arrow keys or click a cell to inspect a cohort.");
        AutomationProperties.SetLiveSetting(readout, AutomationLiveSetting.Polite);

        var kpiM3 = new TextBlock { Text = "—", Classes = { "arc-heading" } };
        var kpiBest = new TextBlock { Text = "—", Classes = { "arc-heading" } };
        var kpiM6 = new TextBlock { Text = "—", Classes = { "arc-heading" } };
        var mix = TeamKit.Caption("All signups");

        void UpdateKpis()
        {
            kpiM3.Text = $"{chart.AverageAt(3):0.0}%";
            kpiBest.Text = $"{chart.BestAt(6):0.0}%";
            kpiM6.Text = $"{chart.AverageAt(6):0.0}%";
            mix.Text = chart.Segment switch
            {
                "pro" => "Paying workspaces",
                "team" => "Team plan",
                _ => "All signups"
            };
        }

        void SetReadout(string text) => readout.Text = text;

        chart.CursorChanged += (_, args) =>
        {
            SetReadout($"{args.Cohort} · M{args.Column} · {args.Rate:0.0}% retained");
        };

        UpdateKpis();

        var segment = TeamKit.Chips(new[]
        {
            ("all", "All signups"),
            ("pro", "Pro"),
            ("team", "Team")
        }, "all", value =>
        {
            chart.Segment = value;
            UpdateKpis();
        }, "Segment");

        var mode = TeamKit.Segmented(new[]
        {
            ("grid", "Triangle"),
            ("curves", "Curves")
        }, "grid", value =>
        {
            chart.Mode = value;
        }, "View");

        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heading.Children.Add(new StackPanel
        {
            Spacing = 3,
            Children =
            {
                new TextBlock { Text = "Cohort retention", Classes = { "arc-heading" } },
                TeamKit.Caption("Monthly signup cohorts · Nov 2025 – Jun 2026 · rates are month over month")
            }
        });
        Grid.SetColumn(mode, 1);
        heading.Children.Add(mode);

        var kpiRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*") };
        kpiRow.Children.Add(Kpi(kpiM3, "Month 3 retention", "Average across visible cohorts", mix));
        var best = Kpi(kpiBest, "Best at month 6", "Highest cohort in view", TeamKit.Caption("Pro plan leads at 70%"));
        Grid.SetColumn(best, 1);
        kpiRow.Children.Add(best);
        var m6 = Kpi(kpiM6, "Month 6 average", "Now over 8 cohorts", TeamKit.Caption("Trending up 2.4 pts"));
        Grid.SetColumn(m6, 2);
        kpiRow.Children.Add(m6);

        var chartPanel = new Border
        {
            Background = TeamKit.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(10, 4, 10, 8),
            Child = chart
        };

        var legend = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };
        legend.Children.Add(TeamKit.Caption("Less active"));
        for (var index = 0; index < 4; index++)
        {
            legend.Children.Add(new Border
            {
                Width = 16,
                Height = 9,
                CornerRadius = new CornerRadius(3),
                Background = TeamKit.Tint(ArcTokens.Accent, 0.06 + index * 0.09)
            });
        }

        legend.Children.Add(TeamKit.Caption("More active"));

        var hintRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        hintRow.Children.Add(readout);
        Grid.SetColumn(legend, 1);
        hintRow.Children.Add(legend);

        var demo = TeamKit.Stack(12,
            heading,
            TeamKit.Row(12, segment, TeamKit.Caption("Cell colour is the share of the cohort still active.")),
            kpiRow,
            TeamKit.Divider(),
            chartPanel,
            hintRow);

        return TeamKit.Root(new Border
        {
            Background = TeamKit.Brush(ArcTokens.Surface),
            BorderBrush = TeamKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(26),
            ClipToBounds = true,
            Padding = new Thickness(20),
            Child = demo
        });
    }

    private static Control Kpi(TextBlock value, string label, string note, Control footer)
    {
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(value);
        stack.Children.Add(TeamKit.Caption(label));
        stack.Children.Add(footer);
        stack.Children.Add(TeamKit.Caption(note));
        return stack;
    }
}



