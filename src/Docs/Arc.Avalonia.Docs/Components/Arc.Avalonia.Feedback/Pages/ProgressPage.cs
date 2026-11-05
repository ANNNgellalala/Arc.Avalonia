using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;

namespace Arc.Avalonia.Feedback.Pages;

/// <summary>
/// Progress's completeness preview: all three bar thicknesses, the three status tones, a determinate bar,
/// the indeterminate sweep and the completed form with its check. Declared keys match Docs/progress.json.
/// </summary>
public sealed class ProgressPage : ArcItemPage
{
    public override string ItemSlug => "progress";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "size-sm", "size-md", "size-lg",
        "state-determinate", "state-indeterminate", "state-complete",
        "tone-success", "tone-warning", "tone-danger"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 24, HorizontalAlignment = HorizontalAlignment.Left, Width = 620 };

        panel.Children.Add(Column("尺寸",
            Bar(ArcProgressSize.Sm, "Small · 6 px", 64),
            Bar(ArcProgressSize.Md, "Medium · 8 px", 64),
            Bar(ArcProgressSize.Lg, "Large · 12 px", 64)));

        panel.Children.Add(Column("色调",
            Bar(ArcProgressSize.Md, "Import finished", 100, ArcProgressTone.Success),
            Bar(ArcProgressSize.Md, "Storage migration", 86, ArcProgressTone.Warning),
            Bar(ArcProgressSize.Md, "Sync backlog", 97, ArcProgressTone.Danger)));

        panel.Children.Add(Column("状态",
            Bar(ArcProgressSize.Md, "Uploading report.pdf", 71),
            new ArcProgress { Label = "Waiting for the server", Indeterminate = true },
            Bar(ArcProgressSize.Md, "Import finished", 100)));

        return panel;
    }

    private static ArcProgress Bar(ArcProgressSize size, string label, double value, ArcProgressTone? tone = null) => new()
    {
        Label = label,
        Value = value,
        ShowValue = true,
        Size = size,
        Tone = tone ?? ArcProgressTone.Accent
    };

    private static Control Column(string label, params Control[] children)
    {
        var column = new StackPanel { Spacing = 14 };
        column.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        foreach (var child in children)
        {
            column.Children.Add(child);
        }

        return column;
    }
}
