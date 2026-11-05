using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Segmented control's completeness preview: two and three segment groups with a selection, the accessory
/// count slot and the no-value state without a highlight pill. Declared keys match Docs/segmented-control.json.
/// </summary>
public sealed class SegmentedControlPage : ArcItemPage
{
    private static readonly ArcSegmentOption[] Ranges =
    {
        new("day", "Day"),
        new("week", "Week"),
        new("month", "Month")
    };

    private static readonly ArcSegmentOption[] Densities =
    {
        new("compact", "Compact"),
        new("standard", "Standard")
    };

    private static readonly ArcSegmentOption[] Inboxes =
    {
        new("open", "Open", "12"),
        new("snoozed", "Snoozed", "3"),
        new("done", "Done")
    };

    public override string ItemSlug => "segmented-control";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-selected", "variant-two", "variant-three", "variant-accessory", "state-unselected"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("两段与三段",
            new ArcSegmentedControl { Label = "Density", Width = 300, Value = "standard", Options = Densities },
            new ArcSegmentedControl { Label = "Range", Width = 300, Value = "week", Options = Ranges }));

        panel.Children.Add(Row("段尾计数与无选中",
            new ArcSegmentedControl { Label = "Inbox", Width = 320, Value = "open", Options = Inboxes },
            new ArcSegmentedControl { Label = "Range（无默认值）", Width = 300, Options = Ranges }));

        return panel;
    }

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var rowPanel = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 24, 12);
            rowPanel.Children.Add(child);
        }

        row.Children.Add(rowPanel);
        return row;
    }
}
