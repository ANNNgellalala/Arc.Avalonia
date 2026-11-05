using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>
/// Usage meter's completeness preview: a single segment below the threshold, a multi-segment bar, the
/// Almost full warning and the Over limit state. Declared keys match Docs/usage-meter.json.
/// </summary>
public sealed class UsageMeterPage : ArcItemPage
{
    public override string ItemSlug => "usage-meter";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-single", "variant-multi", "state-normal", "state-warning", "state-over"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 24, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("单段与多段",
            Meter(new ArcUsageMeter
            {
                Label = "Requests",
                Unit = "requests",
                Limit = 10000,
                Decimals = 1,
                Segments = new[]
                {
                    new ArcUsageSegment { Id = "api", Label = "API", Value = 8400 }
                }
            }),
            Meter(new ArcUsageMeter
            {
                Label = "Workspace storage",
                Unit = "GB",
                Limit = 50,
                Decimals = 1,
                Segments = new[]
                {
                    new ArcUsageSegment { Id = "files", Label = "Files", Value = 21.4 },
                    new ArcUsageSegment { Id = "media", Label = "Media", Value = 14.2 },
                    new ArcUsageSegment { Id = "backups", Label = "Backups", Value = 6.8 }
                }
            })));

        panel.Children.Add(Row("阈值状态",
            Meter(new ArcUsageMeter
            {
                Label = "Team seats",
                Unit = "seats",
                Limit = 100,
                Decimals = 0,
                Segments = new[]
                {
                    new ArcUsageSegment { Id = "members", Label = "Members", Value = 95 }
                }
            }),
            Meter(new ArcUsageMeter
            {
                Label = "API calls",
                Unit = "calls",
                Limit = 100,
                Decimals = 0,
                Segments = new[]
                {
                    new ArcUsageSegment { Id = "sync", Label = "Sync", Value = 96 },
                    new ArcUsageSegment { Id = "webhooks", Label = "Webhooks", Value = 16 }
                }
            })));

        return panel;
    }

    private static ArcUsageMeter Meter(ArcUsageMeter meter)
    {
        meter.Width = 340;
        return meter;
    }

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var rowPanel = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 20, 12);
            rowPanel.Children.Add(child);
        }

        row.Children.Add(rowPanel);
        return row;
    }
}
