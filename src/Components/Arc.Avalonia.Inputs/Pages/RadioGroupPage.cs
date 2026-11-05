using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;

namespace Arc.Avalonia.Inputs.Pages;

/// <summary>
/// Radio group's completeness preview: the label-only rows, the rows with descriptions, a selected group
/// and a group with no default selection. Declared keys match Docs/radio-group.json.
/// </summary>
public sealed class RadioGroupPage : ArcItemPage
{
    private static readonly ArcRadioOption[] Plans =
    {
        new("solo", "Solo", "One seat and one workspace."),
        new("team", "Team", "Up to 20 seats with shared labels."),
        new("scale", "Scale", "Unlimited seats and SSO.")
    };

    private static readonly ArcRadioOption[] Densities =
    {
        new("compact", "Compact"),
        new("standard", "Standard"),
        new("comfortable", "Comfortable")
    };

    public override string ItemSlug => "radio-group";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-plain", "variant-description", "state-selected", "state-unselected"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Row("带说明、有选中",
            new ArcRadioGroup { Label = "Plan", Value = "team", Width = 300, Options = Plans },
            new ArcRadioGroup { Label = "Plan（无默认值）", Width = 300, Options = Plans }));

        panel.Children.Add(Row("只有标签",
            new ArcRadioGroup { Label = "Density", Value = "standard", Width = 300, Options = Densities },
            new ArcRadioGroup { Label = "Density（无默认值）", Width = 300, Options = Densities }));

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
